using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Threading;
using UWE;
using UnityEngine;

namespace SubnauticaSpeedrunningMod.Runtime.Practice
{
    internal static class ModPracticeSaveRuntimeHost
    {
        private const string GameInfoFileName = "gameinfo.json";
        private const string PracticeSlotPrefix = "practice_";
        private static readonly MethodInfo ClearTemporarySaveMethod = typeof(SaveLoadManager).GetMethod("ClearTemporarySave", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly MethodInfo CreateTemporarySaveMethod = typeof(SaveLoadManager).GetMethod("CreateTemporarySave", BindingFlags.Static | BindingFlags.NonPublic);
        private static bool _launchInProgress;

        public static void StartPracticeSave(ModPracticeSaveDefinition definition)
        {
            if (_launchInProgress)
            {
                ModLog.Warn("Ignored practice save launch because another launch is already in progress.");
                return;
            }

            CoroutineHost.StartCoroutine(StartPracticeSaveCoroutine(definition));
        }

        private static IEnumerator StartPracticeSaveCoroutine(ModPracticeSaveDefinition definition)
        {
            _launchInProgress = true;
            bool loadingScreenVisible = false;

            try
            {
                if (SaveLoadManager.main == null)
                {
                    FailLaunch("Practice save runtime is not ready yet.", ref loadingScreenVisible);
                    yield break;
                }

                string templatePath = ModPracticeSaveCatalog.GetInstalledSavePath(definition);
                if (!Directory.Exists(templatePath))
                {
                    FailLaunch("Practice save files are missing for " + definition.DisplayName + ".", ref loadingScreenVisible);
                    yield break;
                }

                PracticeTemplateGameInfo templateGameInfo;
                string gameInfoError;
                if (!TryLoadTemplateGameInfo(templatePath, out templateGameInfo, out gameInfoError))
                {
                    FailLaunch(gameInfoError, ref loadingScreenVisible);
                    yield break;
                }

                if (uGUI.main != null && uGUI.main.loading != null)
                {
                    uGUI.main.loading.ShowLoadingScreen();
                    loadingScreenVisible = true;
                }

                if (uGUI_MainMenu.main != null)
                {
                    uGUI_MainMenu.main.ShowPrimaryOptions(false);
                }

                FPSInputModule.SelectGroup(null);

                string temporarySavePath;
                string tempPathError;
                if (!TryPrepareTemporarySavePath(out temporarySavePath, out tempPathError))
                {
                    FailLaunch(tempPathError, ref loadingScreenVisible);
                    yield break;
                }

                BackgroundDirectoryCopyOperation copyOperation = StartBackgroundDirectoryCopy(templatePath, temporarySavePath);
                while (!copyOperation.IsCompleted)
                {
                    yield return null;
                }

                if (!copyOperation.Succeeded)
                {
                    string copyErrorMessage = copyOperation.Error != null ? copyOperation.Error.Message : "Unknown copy failure.";
                    FailLaunch("Failed to prepare the practice save workspace: " + copyErrorMessage, ref loadingScreenVisible);
                    yield break;
                }

                Utils.SetContinueMode(mode: true);
                Utils.SetLegacyGameMode(templateGameInfo.GameMode);
                Utils.SetSavegameDir(GetSyntheticSlotName(definition));
                VRLoadingOverlay.Show();

                FPSInputModule.SelectGroup(null);
                if (uGUI.main != null && uGUI.main.loading != null)
                {
                    uGUI.main.loading.BeginAsyncSceneLoad("Main");
                    loadingScreenVisible = false;
                }

                ModLog.Info(
                    "Started practice save '" +
                    definition.DisplayName +
                    "' from template '" +
                    templatePath +
                    "' using temporary workspace '" +
                    temporarySavePath +
                    "' and synthetic slot '" +
                    Utils.GetSavegameDir() +
                    "'.");
            }
            finally
            {
                _launchInProgress = false;
            }
        }

        private static void FailLaunch(string message, ref bool loadingScreenVisible)
        {
            ModClientSessionMode.SelectVanilla();

            if (loadingScreenVisible && uGUI.main != null && uGUI.main.loading != null)
            {
                uGUI.main.loading.End(fade: false);
                loadingScreenVisible = false;
            }

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

        private static bool TryLoadTemplateGameInfo(string templatePath, out PracticeTemplateGameInfo templateGameInfo, out string error)
        {
            templateGameInfo = default(PracticeTemplateGameInfo);
            error = string.Empty;

            string gameInfoPath = Path.Combine(templatePath, GameInfoFileName);
            if (!File.Exists(gameInfoPath))
            {
                error = "Practice save is missing " + GameInfoFileName + ".";
                return false;
            }

            try
            {
                string json = File.ReadAllText(gameInfoPath);
                PracticeTemplateGameInfoFile data = JsonUtility.FromJson<PracticeTemplateGameInfoFile>(json);
                if (data == null)
                {
                    error = "Practice save " + GameInfoFileName + " could not be parsed.";
                    return false;
                }

                templateGameInfo = new PracticeTemplateGameInfo(
                    data.changeSet,
                    ConvertGameMode(data.gameMode));
                return true;
            }
            catch (Exception ex)
            {
                error = "Practice save " + GameInfoFileName + " could not be read: " + ex.Message;
                return false;
            }
        }

        private static GameMode ConvertGameMode(int rawGameMode)
        {
            switch (rawGameMode)
            {
                case 0:
                    return GameMode.Survival;
                case 1:
                    return GameMode.Freedom;
                case 2:
                    return GameMode.Hardcore;
                case 3:
                    return GameMode.Creative;
                default:
                    ModLog.Warn("Unknown practice save game mode value '" + rawGameMode + "'. Falling back to Survival.");
                    return GameMode.Survival;
            }
        }

        private static bool TryPrepareTemporarySavePath(out string temporarySavePath, out string error)
        {
            temporarySavePath = string.Empty;
            error = string.Empty;

            if (SaveLoadManager.main == null)
            {
                error = "Practice save runtime is not available.";
                return false;
            }

            if (ClearTemporarySaveMethod == null || CreateTemporarySaveMethod == null)
            {
                error = "Practice save runtime could not resolve the game's temporary save workspace.";
                return false;
            }

            try
            {
                ClearTemporarySaveMethod.Invoke(SaveLoadManager.main, null);
                CreateTemporarySaveMethod.Invoke(null, null);
                temporarySavePath = SaveLoadManager.GetTemporarySavePath();
                if (!string.IsNullOrEmpty(temporarySavePath) && Directory.Exists(temporarySavePath))
                {
                    return true;
                }

                error = "Practice save workspace was created, but the temporary path could not be resolved.";
                return false;
            }
            catch (TargetInvocationException ex)
            {
                Exception inner = ex.InnerException ?? ex;
                error = "Failed to prepare practice save workspace: " + inner.Message;
                return false;
            }
        }

        private static string GetSyntheticSlotName(ModPracticeSaveDefinition definition)
        {
            string saveId = definition.SaveId ?? "practice";
            saveId = saveId.Replace(' ', '_');
            return PracticeSlotPrefix + saveId;
        }

        private static void CopyDirectory(string sourcePath, string destinationPath)
        {
            if (!Directory.Exists(sourcePath))
            {
                throw new DirectoryNotFoundException(sourcePath);
            }

            if (Directory.Exists(destinationPath))
            {
                Directory.Delete(destinationPath, recursive: true);
            }

            Directory.CreateDirectory(destinationPath);

            string[] directories = Directory.GetDirectories(sourcePath, "*", SearchOption.AllDirectories);
            for (int i = 0; i < directories.Length; i++)
            {
                string relativeDirectory = directories[i].Substring(sourcePath.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                Directory.CreateDirectory(Path.Combine(destinationPath, relativeDirectory));
            }

            string[] files = Directory.GetFiles(sourcePath, "*", SearchOption.AllDirectories);
            for (int i = 0; i < files.Length; i++)
            {
                string relativeFile = files[i].Substring(sourcePath.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string targetPath = Path.Combine(destinationPath, relativeFile);
                string targetDirectory = Path.GetDirectoryName(targetPath);
                if (!string.IsNullOrEmpty(targetDirectory) && !Directory.Exists(targetDirectory))
                {
                    Directory.CreateDirectory(targetDirectory);
                }

                File.Copy(files[i], targetPath, overwrite: true);
            }
        }

        private static BackgroundDirectoryCopyOperation StartBackgroundDirectoryCopy(string sourcePath, string destinationPath)
        {
            BackgroundDirectoryCopyOperation operation = new BackgroundDirectoryCopyOperation(sourcePath, destinationPath);
            ThreadPool.QueueUserWorkItem(ExecuteBackgroundDirectoryCopy, operation);
            return operation;
        }

        private static void ExecuteBackgroundDirectoryCopy(object state)
        {
            BackgroundDirectoryCopyOperation operation = state as BackgroundDirectoryCopyOperation;
            if (operation == null)
            {
                return;
            }

            try
            {
                CopyDirectory(operation.SourcePath, operation.DestinationPath);
                operation.MarkCompleted(null);
            }
            catch (Exception ex)
            {
                operation.MarkCompleted(ex);
            }
        }

        private struct PracticeTemplateGameInfo
        {
            public PracticeTemplateGameInfo(int changeSet, GameMode gameMode)
            {
                ChangeSet = changeSet;
                GameMode = gameMode;
            }

            public int ChangeSet { get; private set; }

            public GameMode GameMode { get; private set; }
        }

        [Serializable]
        private sealed class PracticeTemplateGameInfoFile
        {
            public int changeSet;
            public int gameMode;
        }

        private sealed class BackgroundDirectoryCopyOperation
        {
            public BackgroundDirectoryCopyOperation(string sourcePath, string destinationPath)
            {
                SourcePath = sourcePath ?? string.Empty;
                DestinationPath = destinationPath ?? string.Empty;
            }

            public string SourcePath { get; private set; }

            public string DestinationPath { get; private set; }

            public bool IsCompleted { get; private set; }

            public Exception Error { get; private set; }

            public bool Succeeded
            {
                get { return IsCompleted && Error == null; }
            }

            public void MarkCompleted(Exception error)
            {
                Error = error;
                IsCompleted = true;
            }
        }
    }
}
