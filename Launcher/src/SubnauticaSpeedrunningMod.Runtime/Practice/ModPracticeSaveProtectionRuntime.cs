using System;
using System.Collections;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace SubnauticaSpeedrunningMod.Runtime.Practice
{
    internal static class ModPracticeSaveProtectionRuntime
    {
        private const string HarmonyId = "subnautica.speedrunning.mod.practice.protection";
        private const string SaveBlockedMessage = "Saving is disabled for packaged practice saves.";
        private const string SaveDisabledLabel = "Save Disabled";

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
                MethodInfo updatePostfix = typeof(ModPracticeSaveProtectionRuntime).GetMethod(nameof(UpdatePostfix), BindingFlags.Static | BindingFlags.NonPublic);
                MethodInfo onSelectPostfix = typeof(ModPracticeSaveProtectionRuntime).GetMethod(nameof(OnSelectPostfix), BindingFlags.Static | BindingFlags.NonPublic);
                MethodInfo saveGamePrefix = typeof(ModPracticeSaveProtectionRuntime).GetMethod(nameof(SaveGamePrefix), BindingFlags.Static | BindingFlags.NonPublic);
                MethodInfo saveGameAsyncPrefix = typeof(ModPracticeSaveProtectionRuntime).GetMethod(nameof(SaveGameAsyncPrefix), BindingFlags.Static | BindingFlags.NonPublic);
                if (update == null || onSelect == null || saveGame == null || saveGameAsync == null ||
                    updatePostfix == null || onSelectPostfix == null || saveGamePrefix == null || saveGameAsyncPrefix == null)
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
            if (!IsPracticeSaveSessionActive())
            {
                return true;
            }

            ShowSaveBlockedMessage();
            return false;
        }

        private static bool SaveGameAsyncPrefix(ref IEnumerator __result)
        {
            if (!IsPracticeSaveSessionActive())
            {
                return true;
            }

            __result = EmptyCoroutine();
            return false;
        }

        private static IEnumerator EmptyCoroutine()
        {
            yield break;
        }

        private static void ApplyReadOnlyMenuState(IngameMenu menu)
        {
            if (menu == null || !IsPracticeSaveSessionActive())
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
            }
            catch (Exception ex)
            {
                ModLog.Warn("Failed to apply practice save menu protection state: " + ex.Message);
            }
        }

        private static bool IsPracticeSaveSessionActive()
        {
            return ModClientSessionMode.IsPracticeSaveSelected;
        }

        private static void ShowSaveBlockedMessage()
        {
            if (Time.unscaledTime - _lastSaveBlockedMessageAt < 1f)
            {
                return;
            }

            _lastSaveBlockedMessageAt = Time.unscaledTime;
            ErrorMessage.AddMessage(SaveBlockedMessage);
            ModLog.Info("Blocked manual save for packaged practice save '" + ModClientSessionMode.PracticeSaveId + "'.");
        }
    }
}
