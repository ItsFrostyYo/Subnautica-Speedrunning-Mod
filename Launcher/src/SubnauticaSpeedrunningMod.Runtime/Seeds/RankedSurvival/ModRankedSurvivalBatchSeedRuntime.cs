using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using HarmonyLib;
using UWE;
using UnityEngine;

namespace SubnauticaSpeedrunningMod.Runtime.Seeds
{
    internal static class ModRankedSurvivalBatchSeedRuntime
    {
        private const string HarmonyId = "subnautica.speedrunning.ranked.batchsurvival";

        private static readonly BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private static readonly FieldInfo IsStartingNewGameField = typeof(uGUI_MainMenu).GetField("isStartingNewGame", InstanceFlags);

        private static bool _installAttempted;
        private static bool _installed;
        private static bool _available = true;
        private static Harmony _harmony;
        private static string _activeSaveSlot = string.Empty;
        private static GameMode _activeMode = GameMode.None;
        private static string _activeSeedName = string.Empty;
        private static string _activeSeedDirectoryPath = string.Empty;
        private static bool _spawnResolved;
        private static float _spawnX;
        private static float _spawnZ;
        private static string _spawnDescription = string.Empty;

        public static bool EnsureInstalled()
        {
            if (_installAttempted)
            {
                return _installed && _available;
            }

            _installAttempted = true;

            try
            {
                MethodInfo startNewGame = typeof(uGUI_MainMenu).GetMethod("StartNewGame", InstanceFlags);
                MethodInfo prefix = typeof(ModRankedSurvivalBatchSeedRuntime).GetMethod(nameof(StartNewGamePrefix), BindingFlags.Static | BindingFlags.NonPublic);
                if (startNewGame == null || prefix == null)
                {
                    _available = false;
                    ModLog.Warn("Ranked survival batch seed runtime unavailable; StartNewGame hook could not be resolved.");
                    return false;
                }

                _harmony = new Harmony(HarmonyId);
                _harmony.Patch(startNewGame, prefix: new HarmonyMethod(prefix));
                _installed = true;
                ModLog.Info("Installed ranked survival batch seed runtime hook.");
                return true;
            }
            catch (Exception ex)
            {
                _available = false;
                ModLog.Warn("Failed to install ranked survival batch seed runtime hook: " + ex.Message);
                return false;
            }
        }

        public static bool IsSeedContextActive(string saveSlot, GameMode mode)
        {
            if (string.IsNullOrEmpty(saveSlot))
            {
                return false;
            }

            return string.Equals(_activeSaveSlot, saveSlot, StringComparison.OrdinalIgnoreCase) &&
                   _activeMode == mode &&
                   mode == GameMode.Survival;
        }

        public static bool IsCurrentSessionActive()
        {
            return ModClientSessionMode.IsRankedSingleplayerPracticeSelected &&
                   _activeMode == GameMode.Survival &&
                   !string.IsNullOrEmpty(_activeSaveSlot) &&
                   !string.IsNullOrEmpty(_activeSeedDirectoryPath);
        }

        public static ModSeedRuntimeProfile GetActiveProfile()
        {
            return IsCurrentSessionActive() ? ModRankedSurvivalBatchSeedCatalog.Profile : null;
        }

        public static bool TryGetFreshRunStartPoint(out Vector3 startPoint, out string description)
        {
            if (!_spawnResolved)
            {
                startPoint = Vector3.zero;
                description = string.Empty;
                return false;
            }

            startPoint = new Vector3(_spawnX, 0f, _spawnZ);
            description = _spawnDescription ?? string.Empty;
            return true;
        }

        public static void ResetSession()
        {
            _activeSaveSlot = string.Empty;
            _activeMode = GameMode.None;
            _activeSeedName = string.Empty;
            _activeSeedDirectoryPath = string.Empty;
            _spawnResolved = false;
            _spawnX = 0f;
            _spawnZ = 0f;
            _spawnDescription = string.Empty;
        }

        private static bool StartNewGamePrefix(uGUI_MainMenu __instance, GameMode gameMode, ref IEnumerator __result)
        {
            if (!ShouldOverrideStartNewGame(gameMode))
            {
                return true;
            }

            __result = StartRankedSurvivalBatchSeedNewGame(__instance, gameMode);
            return false;
        }

        private static bool ShouldOverrideStartNewGame(GameMode gameMode)
        {
            return _installed &&
                   _available &&
                   gameMode == GameMode.Survival &&
                   ModClientSessionMode.IsRankedSingleplayerPracticeSelected &&
                   !ModClientSessionMode.IsBetterRngSingleplayerSelected &&
                   !ModClientSessionMode.IsRankedMultiplayerSelected &&
                   SaveLoadManager.main != null;
        }

        private static IEnumerator StartRankedSurvivalBatchSeedNewGame(uGUI_MainMenu menu, GameMode gameMode)
        {
            if (ReadIsStartingNewGame(menu))
            {
                yield break;
            }

            WriteIsStartingNewGame(menu, true);
            Utils.SetContinueMode(mode: false);
            Utils.SetLegacyGameMode(gameMode);

            CoroutineTask<SaveLoadManager.CreateResult> createSlotTask = SaveLoadManager.main.CreateSlotAsync();
            yield return createSlotTask;

            SaveLoadManager.CreateResult createSlotResult = createSlotTask.GetResult();
            if (!createSlotResult.success)
            {
                WriteIsStartingNewGame(menu, false);
                yield break;
            }

            Utils.SetSavegameDir(createSlotResult.slotName);

            string preparationError;
            if (!PrepareFreshRun(createSlotResult.slotName, gameMode, out preparationError))
            {
                FailStartNewGame(menu, preparationError);
                yield break;
            }

            VRLoadingOverlay.Show();

            UserStorage.AsyncOperation clearSlotTask = SaveLoadManager.main.ClearSlotAsync(createSlotResult.slotName);
            yield return clearSlotTask;
            if (!clearSlotTask.GetSuccessful())
            {
                UWE.Utils.LogReport("Clearing save data failed. But we ignore it.");
            }

            string stageError;
            if (!StageSelectedSeedIntoTemporarySave(out stageError))
            {
                FailStartNewGame(menu, stageError);
                yield break;
            }

            if (GamepadInputModule.current != null)
            {
                GamepadInputModule.current.SetCurrentGrid(null);
            }

            uGUI.main.loading.BeginAsyncSceneLoad("Main");
        }

        private static bool PrepareFreshRun(string saveSlot, GameMode mode, out string error)
        {
            error = string.Empty;

            if (string.IsNullOrEmpty(saveSlot))
            {
                error = "Ranked survival batch seed could not resolve the save slot.";
                return false;
            }

            string seedDirectoryPath;
            string seedName;
            if (!ModRankedSurvivalBatchSeedCatalog.TryChooseSeed(out seedDirectoryPath, out seedName))
            {
                error =
                    "Ranked survival batch seed files are missing. Expected folders like 'BS 1' or 'BS 2' inside '" +
                    ModRankedSurvivalBatchSeedCatalog.GetInstalledRootPath() +
                    "'.";
                return false;
            }

            if (!Directory.Exists(seedDirectoryPath))
            {
                error = "Ranked survival batch seed folder is missing: " + seedDirectoryPath;
                return false;
            }

            _activeSaveSlot = saveSlot;
            _activeMode = mode;
            _activeSeedName = seedName ?? string.Empty;
            _activeSeedDirectoryPath = seedDirectoryPath;
            ModRankedSurvivalBatchSeedCatalog.ResolveClipCSpawn(out _spawnX, out _spawnZ, out _spawnDescription, _activeSeedName);
            _spawnResolved = true;

            ModLog.Info(
                "Prepared ranked survival batch seed '" +
                _activeSeedName +
                "' for slot '" +
                _activeSaveSlot +
                "' with spawn " +
                _spawnDescription +
                ".");
            return true;
        }

        private static bool StageSelectedSeedIntoTemporarySave(out string error)
        {
            error = string.Empty;

            if (string.IsNullOrEmpty(_activeSeedDirectoryPath) || !Directory.Exists(_activeSeedDirectoryPath))
            {
                error = "Ranked survival batch seed folder is no longer available.";
                return false;
            }

            string temporarySavePath = SaveLoadManager.GetTemporarySavePath();
            if (string.IsNullOrEmpty(temporarySavePath))
            {
                error = "The game did not expose a temporary save workspace for the ranked survival batch seed.";
                return false;
            }

            if (!Directory.Exists(temporarySavePath))
            {
                error = "The temporary save workspace does not exist: " + temporarySavePath;
                return false;
            }

            try
            {
                ClearExistingBatchSeedFiles(temporarySavePath);
                CopyDirectoryContents(_activeSeedDirectoryPath, temporarySavePath);
                ModLog.Info(
                    "Staged ranked survival batch seed '" +
                    _activeSeedName +
                    "' into temporary save workspace '" +
                    temporarySavePath +
                    "'.");
                return true;
            }
            catch (Exception ex)
            {
                error = "Failed to stage ranked survival batch seed '" + _activeSeedName + "': " + ex.Message;
                return false;
            }
        }

        private static void ClearExistingBatchSeedFiles(string temporarySavePath)
        {
            string[] existingBatchFiles = Directory.GetFiles(temporarySavePath, "batch-objects-*.txt", SearchOption.TopDirectoryOnly);
            for (int i = 0; i < existingBatchFiles.Length; i++)
            {
                File.Delete(existingBatchFiles[i]);
            }

            string cellsCachePath = Path.Combine(temporarySavePath, "CellsCache");
            if (!Directory.Exists(cellsCachePath))
            {
                return;
            }

            string[] existingCellFiles = Directory.GetFiles(cellsCachePath, "baked-batch-cells-*.bin", SearchOption.TopDirectoryOnly);
            for (int i = 0; i < existingCellFiles.Length; i++)
            {
                File.Delete(existingCellFiles[i]);
            }
        }

        private static void CopyDirectoryContents(string sourceRoot, string destinationRoot)
        {
            string[] directories = Directory.GetDirectories(sourceRoot, "*", SearchOption.AllDirectories);
            for (int i = 0; i < directories.Length; i++)
            {
                string relativeDirectory = directories[i]
                    .Substring(sourceRoot.Length)
                    .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                Directory.CreateDirectory(Path.Combine(destinationRoot, relativeDirectory));
            }

            string[] files = Directory.GetFiles(sourceRoot, "*", SearchOption.AllDirectories);
            for (int i = 0; i < files.Length; i++)
            {
                string relativeFile = files[i]
                    .Substring(sourceRoot.Length)
                    .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string destinationPath = Path.Combine(destinationRoot, relativeFile);
                string destinationDirectory = Path.GetDirectoryName(destinationPath);
                if (!string.IsNullOrEmpty(destinationDirectory) && !Directory.Exists(destinationDirectory))
                {
                    Directory.CreateDirectory(destinationDirectory);
                }

                File.Copy(files[i], destinationPath, overwrite: true);
            }
        }

        private static void FailStartNewGame(uGUI_MainMenu menu, string message)
        {
            WriteIsStartingNewGame(menu, false);
            ResetSession();
            ModClientSessionMode.SelectVanilla();
            ModLog.Error(message);

            if (uGUI.main != null && uGUI.main.confirmation != null)
            {
                uGUI.main.confirmation.Show(message, delegate(bool confirmed)
                {
                    if (uGUI_MainMenu.main != null)
                    {
                        uGUI_MainMenu.main.Select();
                    }
                });
            }
        }

        private static bool ReadIsStartingNewGame(uGUI_MainMenu menu)
        {
            if (menu == null || IsStartingNewGameField == null)
            {
                return false;
            }

            try
            {
                object value = IsStartingNewGameField.GetValue(menu);
                return value is bool && (bool)value;
            }
            catch
            {
                return false;
            }
        }

        private static void WriteIsStartingNewGame(uGUI_MainMenu menu, bool value)
        {
            if (menu == null || IsStartingNewGameField == null)
            {
                return;
            }

            try
            {
                IsStartingNewGameField.SetValue(menu, value);
            }
            catch
            {
            }
        }
    }
}
