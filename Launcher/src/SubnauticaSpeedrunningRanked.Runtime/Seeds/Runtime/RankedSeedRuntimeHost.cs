using System;
using UnityEngine;

namespace SubnauticaSpeedrunningRanked.Runtime.Seeds
{
    internal static class RankedSeedRuntimeHost
    {
        private static bool _installed;
        private static bool _hooksInstallAttempted;
        private static bool _stalkerToothHookInstalled;
        private static bool _lootDistributionHookInstalled;
        private static bool _startupHookDeferredLogged;

        public static void Install(RuntimeContext context)
        {
            if (_installed)
            {
                return;
            }

            _installed = true;
            RankedLog.Info("Ranked seed runtime armed. Shared runtime hooks will install at a safe in-game phase after the supported save has fully entered gameplay. Deterministic survival seed multipliers and manual creature spawn rules will resolve from the active seed definition.");
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

        public static void EnsureStartupHooksInstalled()
        {
            if (!_startupHookDeferredLogged)
            {
                _startupHookDeferredLogged = true;
                RankedLog.Info("Startup Harmony seed hooks are disabled on this loader build because they are currently causing a native mono.dll crash. Seed features will use stable runtime paths instead.");
            }
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

        public static bool TryResolveSeededFreshRunStartPoint(out Vector3 startPoint, out string description)
        {
            startPoint = Vector3.zero;
            description = string.Empty;

            if (Utils.GetContinueMode())
            {
                return false;
            }

            GameMode mode = Utils.GetLegacyGameMode();
            if (mode != GameMode.Creative && mode != GameMode.Survival)
            {
                return false;
            }

            string saveSlot = Utils.GetSavegameDir() ?? string.Empty;
            if (string.IsNullOrEmpty(saveSlot))
            {
                return false;
            }

            RankedSeedStore.EnsureSeedForSaveContext(saveSlot, mode, continueMode: false);
            if (!RankedSeedStore.IsSeedContextActive(saveSlot, mode))
            {
                return false;
            }

            float x;
            float z;
            bool resolved;
            if (mode == GameMode.Creative)
            {
                resolved = RankedSeedStore.TryGetCreativeSpawnCoordinates(out x, out z, out description);
            }
            else
            {
                resolved = RankedSeedStore.TryGetSurvivalSpawnCoordinates(out x, out z, out description);
            }

            if (!resolved)
            {
                return false;
            }

            startPoint = new Vector3(x, 0f, z);
            return true;
        }

        private static void InstallAlwaysActiveHooks()
        {
            _stalkerToothHookInstalled = false;
            _lootDistributionHookInstalled = false;
            RankedLog.Info(
                "Skipped always-active Harmony installation after identifying a Mono native crash during post-load hook setup. " +
                "FishSchools=False, StalkerTooth=" +
                _stalkerToothHookInstalled +
                ", LootDistribution=" +
                _lootDistributionHookInstalled +
                ", ForceSecondGold=False" +
                ".");
        }
    }
}
