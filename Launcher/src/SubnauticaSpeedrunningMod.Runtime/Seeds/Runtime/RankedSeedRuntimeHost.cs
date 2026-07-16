using System;
using UnityEngine;

namespace SubnauticaSpeedrunningMod.Runtime.Seeds
{
    internal static class ModSeedRuntimeHost
    {
        private static readonly ModSeedRuntimeProfile BetterRngFixedProfile = ModSeedRuntimeProfile.CreateBetterRngFixedProfile();
        private static bool _installed;
        private static bool _startupHooksInstallAttempted;
        private static bool _hooksInstallAttempted;
        private static bool _fishSchoolHookInstalled;
        private static bool _stalkerToothHookInstalled;
        private static bool _lootDistributionHookInstalled;
        private static bool _betterRngHookInstalled;
        private static bool _consoleCommandHookInstalled;

        public static void Install(RuntimeContext context)
        {
            if (_installed)
            {
                return;
            }

            _installed = true;
            ModLog.Info("Ranked seed runtime armed. Startup Harmony hooks and live world runtime sweeps will resolve from the active seed definition.");
        }

        public static ModSeedRuntimeProfile GetProfile()
        {
            if (ShouldApplyBetterRngRules())
            {
                return BetterRngFixedProfile;
            }

            ModSeedRuntimeProfile rankedSurvivalBatchProfile = ModRankedSurvivalBatchSeedRuntime.GetActiveProfile();
            if (rankedSurvivalBatchProfile != null)
            {
                return rankedSurvivalBatchProfile;
            }

            return ModSeedStore.GetActiveProfile();
        }

        public static bool HasActiveSeedAssignment()
        {
            return ModSeedStore.HasActiveSeedAssignment();
        }

        public static bool IsBetterRngSeedActive()
        {
            return ShouldApplyBetterRngRules();
        }

        public static bool IsRankedSingleplayerSeedActive()
        {
            if (ModRankedSurvivalBatchSeedRuntime.IsCurrentSessionActive())
            {
                return true;
            }

            return ModClientSessionMode.IsRankedSingleplayerPracticeSelected &&
                   HasActiveSeedAssignment() &&
                   !IsBetterRngSeedActive();
        }

        public static bool IsRankedMultiplayerSeedActive()
        {
            if (ModRankedSurvivalBatchSeedRuntime.IsCurrentSessionActive() && ModClientSessionMode.IsRankedMultiplayerSelected)
            {
                return true;
            }

            return ModClientSessionMode.IsRankedMultiplayerSelected &&
                   HasActiveSeedAssignment() &&
                   !IsBetterRngSeedActive();
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
            if (_startupHooksInstallAttempted)
            {
                return;
            }

            _startupHooksInstallAttempted = true;
            _betterRngHookInstalled = ModBetterRngRuntimeHost.EnsureInstalled();
            bool forceSecondGoldInstalled = ModForceSecondGoldRuntime.EnsureInstalled();
            bool rankedSurvivalBatchInstalled = ModRankedSurvivalBatchSeedRuntime.EnsureInstalled();
            _consoleCommandHookInstalled = ModConsoleCommandsRuntime.EnsureInstalled();
            _fishSchoolHookInstalled = ModFishSchoolHookRuntime.EnsureInstalled();
            _stalkerToothHookInstalled = ModStalkerToothHookRuntime.EnsureInstalled();
            ModLog.Info(
                "Initialized legacy BepInEx Harmony seed support. " +
                "BetterRngHooksInstalled=" +
                _betterRngHookInstalled +
                ", " +
                "RankedSurvivalBatchInstalled=" +
                rankedSurvivalBatchInstalled +
                ", " +
                "ForceSecondGoldInstalled=" +
                forceSecondGoldInstalled +
                ", " +
                "ConsoleCommandsInstalled=" +
                _consoleCommandHookInstalled +
                ", FishSchoolHooksInstalled=" +
                _fishSchoolHookInstalled +
                ", StalkerToothHooksInstalled=" +
                _stalkerToothHookInstalled +
                ".");
        }

        public static void UpdateSharedRuleState(string saveSlot, bool inMainMenu, bool continueMode)
        {
            string normalizedSaveSlot = saveSlot ?? string.Empty;
            if (inMainMenu ||
                !normalizedSaveSlot.StartsWith("slot", StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name, "Main", StringComparison.OrdinalIgnoreCase) ||
                !ShouldApplyAnyModdedSeedRules())
            {
                ModForceSecondGoldRuntime.Reset();
                return;
            }

            bool eligibleFreshRunState = IsSurvivalLikeMode() && !continueMode;
            ModForceSecondGoldRuntime.UpdateSessionState(normalizedSaveSlot, eligibleFreshRunState);
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

            if (ShouldApplyBetterRngRules())
            {
                return false;
            }

            Vector3 rankedBatchStartPoint;
            if (mode == GameMode.Survival &&
                ModRankedSurvivalBatchSeedRuntime.TryGetFreshRunStartPoint(out rankedBatchStartPoint, out description))
            {
                startPoint = new Vector3(rankedBatchStartPoint.x, 0f, rankedBatchStartPoint.z);
                return true;
            }

            string saveSlot = Utils.GetSavegameDir() ?? string.Empty;
            if (string.IsNullOrEmpty(saveSlot))
            {
                return false;
            }

            ModSeedStore.EnsureSeedForSaveContext(saveSlot, mode, continueMode: false, createIfMissing: true);
            if (!ModSeedStore.IsSeedContextActive(saveSlot, mode))
            {
                return false;
            }

            float x;
            float z;
            bool resolved;
            if (mode == GameMode.Creative)
            {
                resolved = ModSeedStore.TryGetCreativeSpawnCoordinates(out x, out z, out description);
            }
            else
            {
                resolved = ModSeedStore.TryGetSurvivalSpawnCoordinates(out x, out z, out description);
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
            _lootDistributionHookInstalled = false;
            ModLog.Info(
                "Always-active world rules initialized. " +
                "FishSchools=" +
                _fishSchoolHookInstalled +
                ", StalkerTooth=" +
                _stalkerToothHookInstalled +
                ", LootDistribution=" +
                _lootDistributionHookInstalled +
                ", ForceSecondGold=StartupPatch" +
                ".");
        }

        public static bool ShouldApplyAnyModdedSeedRules()
        {
            return ShouldApplyRankedSingleplayerRules() || ShouldApplyRankedMultiplayerRules() || ShouldApplyBetterRngRules();
        }

        public static bool ShouldApplyRankedSingleplayerRules()
        {
            if (!IsSupportedGameplayMode())
            {
                return false;
            }

            if (!ModClientSessionMode.IsRankedSingleplayerPracticeSelected)
            {
                return false;
            }

            if (ModRankedSurvivalBatchSeedRuntime.IsCurrentSessionActive())
            {
                return true;
            }

            if (HasActiveSeedAssignment())
            {
                return IsRankedSingleplayerSeedActive();
            }

            return !Utils.GetContinueMode();
        }

        public static bool ShouldApplyBetterRngRules()
        {
            if (!IsSupportedGameplayMode())
            {
                return false;
            }

            if (!ModClientSessionMode.IsBetterRngSingleplayerSelected)
            {
                return false;
            }

            string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            if (string.Equals(sceneName, "XMenu", StringComparison.Ordinal))
            {
                return false;
            }

            return true;
        }

        public static bool ShouldApplyRankedMultiplayerRules()
        {
            if (!IsSupportedGameplayMode())
            {
                return false;
            }

            if (!ModClientSessionMode.IsRankedMultiplayerSelected)
            {
                return false;
            }

            return true;
        }

        private static bool IsSupportedSeedMode(GameMode mode)
        {
            return mode == GameMode.Creative || mode == GameMode.Survival || mode == GameMode.Hardcore;
        }
    }
}
