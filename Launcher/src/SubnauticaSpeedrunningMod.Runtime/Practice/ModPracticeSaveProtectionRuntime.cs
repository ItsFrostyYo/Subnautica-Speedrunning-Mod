using System;
using System.Collections;
using System.Reflection;
using HarmonyLib;
using SubnauticaSpeedrunningMod.Runtime.Seeds;
using UnityEngine;
using UnityEngine.UI;

namespace SubnauticaSpeedrunningMod.Runtime.Practice
{
    internal static class ModPracticeSaveProtectionRuntime
    {
        private const string HarmonyId = "subnautica.speedrunning.mod.practice.protection";
        private const string PracticeSaveBlockedMessage = "Saving is disabled for packaged practice saves.";
        private const string RankedSaveBlockedMessage = "Saving is disabled for race runs.";
        private const string SaveDisabledLabel = "Save Disabled";
        private const string ForfeitLabel = "Forfeit";
        private const string ForfeitConfirmationLabel = "Forfeit this race?";
        private const string ReturnToRoomLabel = "Leave";
        private const string ReturnToRoomConfirmationLabel = "Return to the private race room?";

        private static bool _installAttempted;
        private static bool _installed;
        private static bool _available = true;
        private static Harmony _harmony;
        private static float _lastSaveBlockedMessageAt = -1000f;

        public static bool EnsureInstalled()
        {
            if (_installAttempted)
            {
                return _installed && _available;
            }

            _installAttempted = true;

            try
            {
                MethodInfo update = typeof(IngameMenu).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic);
                MethodInfo onSelect = typeof(IngameMenu).GetMethod("OnSelect", BindingFlags.Instance | BindingFlags.Public, null, new[] { typeof(bool) }, null);
                MethodInfo saveGame = typeof(IngameMenu).GetMethod("SaveGame", BindingFlags.Instance | BindingFlags.Public);
                MethodInfo saveGameAsync = typeof(IngameMenu).GetMethod("SaveGameAsync", BindingFlags.Instance | BindingFlags.NonPublic);
                MethodInfo quitSubscreen = typeof(IngameMenu).GetMethod("QuitSubscreen", BindingFlags.Instance | BindingFlags.Public);
                MethodInfo updatePostfix = typeof(ModPracticeSaveProtectionRuntime).GetMethod(nameof(UpdatePostfix), BindingFlags.Static | BindingFlags.NonPublic);
                MethodInfo onSelectPostfix = typeof(ModPracticeSaveProtectionRuntime).GetMethod(nameof(OnSelectPostfix), BindingFlags.Static | BindingFlags.NonPublic);
                MethodInfo saveGamePrefix = typeof(ModPracticeSaveProtectionRuntime).GetMethod(nameof(SaveGamePrefix), BindingFlags.Static | BindingFlags.NonPublic);
                MethodInfo saveGameAsyncPrefix = typeof(ModPracticeSaveProtectionRuntime).GetMethod(nameof(SaveGameAsyncPrefix), BindingFlags.Static | BindingFlags.NonPublic);
                MethodInfo quitSubscreenPrefix = typeof(ModPracticeSaveProtectionRuntime).GetMethod(nameof(QuitSubscreenPrefix), BindingFlags.Static | BindingFlags.NonPublic);
                if (update == null || onSelect == null || saveGame == null || saveGameAsync == null || quitSubscreen == null ||
                    updatePostfix == null || onSelectPostfix == null || saveGamePrefix == null || saveGameAsyncPrefix == null || quitSubscreenPrefix == null)
                {
                    _available = false;
                    ModLog.Warn("Practice save protection hooks unavailable; required ingame menu methods could not be resolved.");
                    return false;
                }

                _harmony = new Harmony(HarmonyId);
                _harmony.Patch(update, postfix: new HarmonyMethod(updatePostfix));
                _harmony.Patch(onSelect, postfix: new HarmonyMethod(onSelectPostfix));
                _harmony.Patch(saveGame, prefix: new HarmonyMethod(saveGamePrefix));
                _harmony.Patch(saveGameAsync, prefix: new HarmonyMethod(saveGameAsyncPrefix));
                _harmony.Patch(quitSubscreen, prefix: new HarmonyMethod(quitSubscreenPrefix));

                _installed = true;
                ModLog.Info("Installed practice save read-only protection hooks.");
                return true;
            }
            catch (Exception ex)
            {
                _available = false;
                ModLog.Warn("Failed to install practice save read-only protection hooks: " + ex.Message);
                return false;
            }
        }

        private static void UpdatePostfix(IngameMenu __instance)
        {
            ApplyReadOnlyMenuState(__instance);
        }

        private static void OnSelectPostfix(IngameMenu __instance)
        {
            ApplyReadOnlyMenuState(__instance);
        }

        private static bool SaveGamePrefix()
        {
            if (!IsProtectedSaveSessionActive())
            {
                return true;
            }

            ShowSaveBlockedMessage();
            return false;
        }

        private static bool SaveGameAsyncPrefix(ref IEnumerator __result)
        {
            if (!IsProtectedSaveSessionActive())
            {
                return true;
            }

            __result = EmptyCoroutine();
            return false;
        }

        private static bool QuitSubscreenPrefix(IngameMenu __instance)
        {
            if (!IsRankedSessionActive() || __instance == null)
            {
                return true;
            }

            if (ShouldUseForfeitMenuState())
            {
                __instance.ChangeSubscreen("QuitConfirmation");
                ApplyForfeitMenuState(__instance);
                return false;
            }

            if (ShouldUseReturnToRoomMenuState())
            {
                __instance.ChangeSubscreen("QuitConfirmation");
                ApplyReturnToRoomMenuState(__instance);
                return false;
            }

            return true;
        }

        private static IEnumerator EmptyCoroutine()
        {
            yield break;
        }

        private static void ApplyReadOnlyMenuState(IngameMenu menu)
        {
            if (menu == null || !IsProtectedSaveSessionActive())
            {
                return;
            }

            try
            {
                if (menu.saveButton != null)
                {
                    menu.saveButton.interactable = false;
                    menu.saveButton.gameObject.SetActive(!GameModeUtils.IsPermadeath());
                    Text label = menu.saveButton.GetComponentInChildren<Text>(includeInactive: true);
                    if (label != null)
                    {
                        label.text = SaveDisabledLabel;
                    }
                }

                if (menu.quitToMainMenuButton != null)
                {
                    menu.quitToMainMenuButton.interactable = true;
                }

                if (IsRankedSessionActive())
                {
                    if (ShouldUseForfeitMenuState())
                    {
                        ApplyForfeitMenuState(menu);
                    }
                    else if (ShouldUseReturnToRoomMenuState())
                    {
                        ApplyReturnToRoomMenuState(menu);
                    }
                }
            }
            catch (Exception ex)
            {
                ModLog.Warn("Failed to apply practice save menu protection state: " + ex.Message);
            }
        }

        private static void ApplyForfeitMenuState(IngameMenu menu)
        {
            if (menu == null)
            {
                return;
            }

            if (menu.quitToMainMenuText != null)
            {
                menu.quitToMainMenuText.text = ForfeitLabel;
            }

            if (menu.quitLastSaveText != null)
            {
                menu.quitLastSaveText.text = ForfeitConfirmationLabel;
            }

            ApplyConfirmationText(menu.transform.Find("QuitConfirmation"), ForfeitConfirmationLabel);
            ApplyConfirmationText(menu.transform.Find("QuitConfirmationWithSaveWarning"), ForfeitConfirmationLabel);
        }

        private static void ApplyReturnToRoomMenuState(IngameMenu menu)
        {
            if (menu == null)
            {
                return;
            }

            if (menu.quitToMainMenuText != null)
            {
                menu.quitToMainMenuText.text = ReturnToRoomLabel;
            }

            if (menu.quitLastSaveText != null)
            {
                menu.quitLastSaveText.text = ReturnToRoomConfirmationLabel;
            }

            ApplyConfirmationText(menu.transform.Find("QuitConfirmation"), ReturnToRoomConfirmationLabel);
            ApplyConfirmationText(menu.transform.Find("QuitConfirmationWithSaveWarning"), ReturnToRoomConfirmationLabel);
        }

        private static void ApplyConfirmationText(Transform screenTransform, string replacementText)
        {
            if (screenTransform == null)
            {
                return;
            }

            Text[] texts = screenTransform.GetComponentsInChildren<Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                Text text = texts[i];
                if (text == null)
                {
                    continue;
                }

                string current = text.text ?? string.Empty;
                if (string.Equals(current, "Yes", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(current, "No", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (current.IndexOf("quit", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    current.IndexOf("save", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    current.IndexOf("sure", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    current.IndexOf("main menu", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    text.text = replacementText;
                    text.resizeTextForBestFit = false;
                    text.fontSize = Math.Min(text.fontSize > 0 ? text.fontSize : 28, 24);
                }
            }
        }

        private static bool IsProtectedSaveSessionActive()
        {
            return IsPracticeSaveSessionActive() || IsRankedSessionActive();
        }

        private static bool IsPracticeSaveSessionActive()
        {
            return ModClientSessionMode.IsPracticeSaveSelected;
        }

        private static bool IsRankedSessionActive()
        {
            return ModSeedRuntimeHost.IsRankedSingleplayerSeedActive() ||
                   ModSeedRuntimeHost.IsRankedMultiplayerSeedActive();
        }

        private static bool ShouldUseForfeitMenuState()
        {
            if (ModSeedRuntimeHost.IsRankedSingleplayerSeedActive())
            {
                return true;
            }

            return ModSeedRuntimeHost.IsRankedMultiplayerSeedActive() &&
                   ModPrivateRaceCountdownRuntimeHost.ShouldUseForfeitMenu;
        }

        private static bool ShouldUseReturnToRoomMenuState()
        {
            return ModSeedRuntimeHost.IsRankedMultiplayerSeedActive() &&
                   ModPrivateRaceCountdownRuntimeHost.ShouldUseReturnToRoomMenu;
        }

        private static void ShowSaveBlockedMessage()
        {
            if (Time.unscaledTime - _lastSaveBlockedMessageAt < 1f)
            {
                return;
            }

            _lastSaveBlockedMessageAt = Time.unscaledTime;
            if (IsRankedSessionActive())
            {
                ErrorMessage.AddMessage(RankedSaveBlockedMessage);
                ModLog.Info("Blocked manual save while race session was active.");
                return;
            }

            ErrorMessage.AddMessage(PracticeSaveBlockedMessage);
            ModLog.Info("Blocked manual save for packaged practice save '" + ModClientSessionMode.PracticeSaveId + "'.");
        }
    }
}
