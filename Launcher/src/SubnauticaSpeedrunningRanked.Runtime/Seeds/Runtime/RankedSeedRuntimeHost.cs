using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace SubnauticaSpeedrunningRanked.Runtime.Seeds
{
    internal static class RankedSeedRuntimeHost
    {
        private static bool _installed;
        private static bool _hooksInstallAttempted;
        private static Harmony _harmony;
        private static bool _fishSchoolHooksInstalled;
        private static bool _stalkerToothHookInstalled;
        private static bool _lootDistributionHookInstalled;
        private static bool _forceSecondGoldHookInstalled;

        public static void Install(RuntimeContext context)
        {
            if (_installed)
            {
                return;
            }

            _installed = true;
            RankedLog.Info("Ranked seed runtime armed. Always-active hooks will install once the main menu runtime is active, before any save begins loading. Deterministic survival seed multipliers and manual creature spawn rules will resolve from the active seed definition.");
        }

        public static RankedSeedRuntimeProfile GetProfile()
        {
            return RankedSeedStore.GetActiveProfile();
        }

        public static bool IsSupportedGameplayMode()
        {
            GameMode mode = Utils.GetLegacyGameMode();
            return mode == GameMode.Creative || mode == GameMode.Survival || mode == GameMode.Hardcore;
        }

        public static bool IsCreativeMode()
        {
            return Utils.GetLegacyGameMode() == GameMode.Creative;
        }

        public static bool IsSurvivalMode()
        {
            return Utils.GetLegacyGameMode() == GameMode.Survival;
        }

        public static bool IsHardcoreMode()
        {
            return Utils.GetLegacyGameMode() == GameMode.Hardcore;
        }

        public static bool IsSurvivalLikeMode()
        {
            GameMode mode = Utils.GetLegacyGameMode();
            return mode == GameMode.Survival || mode == GameMode.Hardcore;
        }

        public static bool TryResolveBiomeType(Vector3 position, out BiomeType biome)
        {
            biome = 0;
            if (LargeWorld.main == null)
            {
                return false;
            }

            try
            {
                string biomeName = LargeWorld.main.GetBiome(position);
                if (string.IsNullOrEmpty(biomeName))
                {
                    return false;
                }

                biome = (BiomeType)Enum.Parse(typeof(BiomeType), biomeName, true);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static bool IsStalkerToothHookInstalled()
        {
            return _stalkerToothHookInstalled;
        }

        public static bool IsLootDistributionHookInstalled()
        {
            return _lootDistributionHookInstalled;
        }

        public static void EnsureAlwaysActiveHooksInstalled()
        {
            if (_hooksInstallAttempted)
            {
                return;
            }

            _hooksInstallAttempted = true;
            InstallAlwaysActiveHooks();
        }

        public static void UpdateSharedRuleState(string saveSlot, bool inMainMenu, bool continueMode)
        {
            if (inMainMenu)
            {
                RankedForceSecondGoldRuntime.Reset();
                return;
            }

            bool eligibleFreshRunState = IsSupportedGameplayMode() && !continueMode;
            RankedForceSecondGoldRuntime.UpdateSessionState(saveSlot, eligibleFreshRunState);
        }

        private static void InstallAlwaysActiveHooks()
        {
            try
            {
                _harmony = new Harmony("subnauticaspeedrunningranked.runtime.shareddefaults");
                _fishSchoolHooksInstalled = false;
                _stalkerToothHookInstalled = false;
                _lootDistributionHookInstalled = false;
                _forceSecondGoldHookInstalled = RankedForceSecondGoldRuntime.Install(_harmony);
                RankedLog.Info(
                    "Installed always-active seed runtime at safe main-menu phase. Live-data systems remain primary, with targeted Harmony only where deterministic runtime correction is required. FishSchools=" +
                    _fishSchoolHooksInstalled +
                    ", StalkerTooth=" +
                    _stalkerToothHookInstalled +
                    ", LootDistribution=" +
                    _lootDistributionHookInstalled +
                    ", ForceSecondGold=" +
                    _forceSecondGoldHookInstalled +
                    ".");
            }
            catch (Exception ex)
            {
                RankedLog.Error("Failed to install always-active seed hooks: " + ex);
            }
        }

        private static bool InstallFishSchoolHooks(Harmony harmony)
        {
            bool installedAny = false;
            MethodInfo blockSchoolPrefix = typeof(RankedSeedHarmonyPatches).GetMethod(
                "BlockFishSchoolPrefix",
                BindingFlags.Static | BindingFlags.NonPublic);
            MethodInfo blockSchoolManagerAddPrefix = typeof(RankedSeedHarmonyPatches).GetMethod(
                "BlockFishSchoolManagerAddPrefix",
                BindingFlags.Static | BindingFlags.NonPublic);
            MethodInfo blockSimpleSchoolPrefix = typeof(RankedSeedHarmonyPatches).GetMethod(
                "BlockSimpleSchoolPrefix",
                BindingFlags.Static | BindingFlags.NonPublic);
            installedAny |= PatchPrefix(harmony, typeof(VFXSchoolFish), "OnEnable", 0, blockSchoolPrefix);
            installedAny |= PatchPrefix(harmony, typeof(VFXSchoolFishManager), "AddSchool", 1, blockSchoolManagerAddPrefix);
            installedAny |= PatchPrefix(harmony, typeof(School), "Start", 0, blockSimpleSchoolPrefix);
            return installedAny;
        }

        private static bool InstallStalkerToothHook(Harmony harmony)
        {
            MethodInfo prefix = typeof(RankedSeedHarmonyPatches).GetMethod(
                "CheckLoseToothPrefix",
                BindingFlags.Static | BindingFlags.NonPublic);
            return PatchPrefix(harmony, typeof(Stalker), "CheckLoseTooth", 1, prefix);
        }

        private static bool InstallLootDistributionHook(Harmony harmony)
        {
            MethodInfo prefix = typeof(RankedSeedHarmonyPatches).GetMethod(
                "LootDistributionInitializePrefix",
                BindingFlags.Static | BindingFlags.NonPublic);
            return PatchPrefix(harmony, typeof(LootDistributionData), "Initialize", 1, prefix);
        }

        private static bool PatchPrefix(Harmony harmony, Type targetType, string methodName, int parameterCount, MethodInfo prefix)
        {
            if (harmony == null || targetType == null || prefix == null || string.IsNullOrEmpty(methodName))
            {
                return false;
            }

            MethodInfo target = FindMethod(targetType, methodName, parameterCount);
            if (target == null)
            {
                RankedLog.Warn("Unable to find hook target '" + targetType.FullName + "." + methodName + "'.");
                return false;
            }

            harmony.Patch(target, prefix: new HarmonyMethod(prefix));
            RankedLog.Info("Installed seed hook: " + targetType.Name + "." + methodName);
            return true;
        }

        private static MethodInfo FindMethod(Type type, string methodName, int parameterCount)
        {
            while (type != null)
            {
                MethodInfo[] methods = type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                for (int i = 0; i < methods.Length; i++)
                {
                    MethodInfo method = methods[i];
                    if (!string.Equals(method.Name, methodName, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    ParameterInfo[] parameters = method.GetParameters();
                    if (parameters != null && parameters.Length == parameterCount)
                    {
                        return method;
                    }
                }

                type = type.BaseType;
            }

            return null;
        }
    }
}
