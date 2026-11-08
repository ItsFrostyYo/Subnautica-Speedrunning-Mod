using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SubnauticaSpeedrunningMod.Runtime.Optimizations
{
    internal static class ModPreloadBaseRuntime
    {
        private const string HarmonyId = "subnautica.speedrunning.preloadbase";
        private static bool installed;
        private static bool scheduled;
        private static bool enabled = true;
        internal static bool Enabled { get { return enabled; } }

        internal static void Install()
        {
            if (scheduled) return;
            // Bootstrap runs before Unity's native math bindings are ready. Patch
            // settings now, but defer Base's static initializer until the menu.
            new Harmony(HarmonyId).CreateClassProcessor(typeof(PreloadBaseSettingsPatch)).Patch();
            SceneManager.sceneLoaded += OnSceneLoaded;
            scheduled = true;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != "XMenu" || installed) return;
            var harmony = new Harmony(HarmonyId);
            try
            {
                foreach (Type type in new[] { typeof(BasePiecePreload), typeof(BasePieceCachedRequests),
                    typeof(KeepBaseBundleDuringCleanup), typeof(CachedBaseLoadingLabel), typeof(GeneralOptimizationOptionsPatch) })
                    harmony.CreateClassProcessor(type).Patch();
                installed = true;
                ModLog.Info("Improve Preload Base option installed; current value=" + (Enabled ? "On" : "Off") + ".");
            }
            catch (Exception ex)
            {
                try { harmony.UnpatchSelf(); } catch (Exception cleanup) { ModLog.Warn("Preload hook rollback: " + cleanup.Message); }
                ModLog.Error("Could not install Improve Preload Base; native loading remains in use.", ex);
            }
        }

        internal static void SetSelectedIndex(int index)
        {
            enabled = index != 0;
            if (!enabled) BaseSessionCache.Disable();
            ModLog.Info("Improve Preload Base=" + (enabled ? "On" : "Off") + ". Applies to subsequent world loads.");
        }

        internal static void Serialize(GameSettings.ISerializer serializer)
        {
            enabled = serializer.Serialize("SpeedrunningMod/Optimizations/ImprovePreloadBase", serializer.IsReading() ? true : enabled);
        }

        // Use native insertion methods so appearance, arrows, selection sounds and
        // keyboard/controller navigation match the game's other choice settings.
        internal static void AddBeforeDisplay(uGUI_OptionsPanel panel, int tabIndex, string label)
        {
            panel.AddHeading(tabIndex, "Optimizations");
            panel.AddChoiceOption(tabIndex, "Improve Preload Base", new[] { "Off", "On" }, Enabled ? 1 : 0, SetSelectedIndex);
            panel.AddHeading(tabIndex, label);
        }
    }

    [HarmonyPatch(typeof(GameSettings), "SerializeSettings")]
    internal static class PreloadBaseSettingsPatch
    {
        private static void Postfix(GameSettings.ISerializer serializer) { ModPreloadBaseRuntime.Serialize(serializer); }
    }

    [HarmonyPatch(typeof(uGUI_OptionsPanel), "AddGeneralTab")]
    internal static class GeneralOptimizationOptionsPatch
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var code = new List<CodeInstruction>(instructions);
            MethodInfo heading = AccessTools.Method(typeof(uGUI_TabbedControlsPanel), "AddHeading");
            int replaced = 0;
            for (int i = 0; i + 1 < code.Count; i++)
            {
                if (code[i].opcode == OpCodes.Ldstr && Equals(code[i].operand, "Display") && code[i + 1].Calls(heading))
                {
                    code[i + 1].opcode = OpCodes.Call;
                    code[i + 1].operand = AccessTools.Method(typeof(ModPreloadBaseRuntime), "AddBeforeDisplay");
                    replaced++;
                }
            }
            if (replaced != 1) throw new InvalidOperationException("Could not locate the General tab's native Display heading.");
            return code;
        }
    }
}
