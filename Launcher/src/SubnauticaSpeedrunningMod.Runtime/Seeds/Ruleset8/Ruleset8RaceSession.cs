using System;
using System.Globalization;
using System.Reflection;
using HarmonyLib;

namespace SubnauticaSpeedrunningMod.Runtime.Seeds.Ruleset8
{
    internal static class Ruleset8RaceSession
    {
        private const string HarmonyId = "subnautica.speedrunning.ruleset8.session";
        private static bool _installAttempted;
        private static bool _installed;
        private static string _pendingSeed = string.Empty;

        internal static bool EnsureInstalled()
        {
            if (_installAttempted)
            {
                return _installed;
            }

            _installAttempted = true;
            try
            {
                MethodInfo target = typeof(uGUI_MainMenu).GetMethod("StartNewGame", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                MethodInfo prefix = typeof(Ruleset8RaceSession).GetMethod("StartNewGamePrefix", BindingFlags.Static | BindingFlags.NonPublic);
                if (target == null || prefix == null)
                {
                    throw new MissingMethodException("Could not resolve uGUI_MainMenu.StartNewGame for Ruleset 8.");
                }

                new Harmony(HarmonyId).Patch(target, new HarmonyMethod(prefix));
                _installed = true;
                ModLog.Info("Ruleset 8 race session hook installed.");
            }
            catch (Exception ex)
            {
                ModLog.Warn("Ruleset 8 race session hook failed: " + ex);
            }

            return _installed;
        }

        internal static bool Prepare(string seed)
        {
            long numeric;
            if (string.IsNullOrEmpty(seed) || !long.TryParse(seed.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out numeric))
            {
                ModLog.Warn("Rejected non-numeric Ruleset 8 race seed '" + (seed ?? string.Empty) + "'.");
                return false;
            }

            _pendingSeed = numeric.ToString(CultureInfo.InvariantCulture);
            SeedRuntime.SetPendingSeed(_pendingSeed);
            return true;
        }

        internal static bool PrepareSingleplayerFromPool()
        {
            try
            {
                string seed = Ruleset8SeedPool.Choose();
                if (!Prepare(seed))
                {
                    return false;
                }

                ModLog.Info("Selected Ruleset 8 seed '" + _pendingSeed + "' for Singleplayer Race Survival.");
                return true;
            }
            catch (Exception ex)
            {
                ModLog.Error("Could not select a Ruleset 8 Singleplayer Race seed.", ex);
                return false;
            }
        }

        internal static bool ShouldActivateForNewGame()
        {
            return !string.IsNullOrEmpty(_pendingSeed) &&
                (ModClientSessionMode.IsRankedSingleplayerPracticeSelected || ModClientSessionMode.IsRankedMultiplayerSelected) &&
                !ModClientSessionMode.IsBetterRngSingleplayerSelected;
        }

        internal static void Reset()
        {
            if (!string.IsNullOrEmpty(_pendingSeed) || SeedRuntime.Active)
            {
                ModLog.Info("Cleared Ruleset 8 race seed session.");
            }
            _pendingSeed = string.Empty;
            SeedRuntime.Deactivate();
        }

        private static void StartNewGamePrefix(GameMode gameMode)
        {
            if (gameMode != GameMode.Survival)
            {
                if (!ModClientSessionMode.IsRankedMultiplayerSelected)
                {
                    Reset();
                }
                return;
            }

            if (ModClientSessionMode.IsRankedSingleplayerPracticeSelected)
            {
                if (string.IsNullOrEmpty(_pendingSeed) && !PrepareSingleplayerFromPool())
                {
                    return;
                }
            }

            if (ShouldActivateForNewGame())
            {
                SeedRuntime.ActivatePendingForNewGame();
                ModLog.Info("Starting Survival race with Ruleset 8 seed '" + _pendingSeed + "'.");
            }
        }
    }
}
