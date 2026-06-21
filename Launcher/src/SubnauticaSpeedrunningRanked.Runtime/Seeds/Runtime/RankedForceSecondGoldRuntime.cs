using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace SubnauticaSpeedrunningRanked.Runtime.Seeds
{
    internal static class RankedForceSecondGoldRuntime
    {
        private sealed class SandstoneProfile
        {
            public GameObject GoldPrefab;
            public float SilverChance;
        }

        private const int SandstoneWindowSize = 6;
        private const int RequiredGolds = 2;
        private const float DefaultSilverChance = 0.5f;

        private static readonly BindingFlags StaticFlags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        private static readonly BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        private static readonly FieldInfo PlayerMainField = typeof(Player).GetField("main", StaticFlags);
        private static readonly FieldInfo FairRandomizerEntropyField = typeof(FairRandomizer).GetField("entropy", InstanceFlags);

        private static bool _installed;
        private static string _activeSlotPath = string.Empty;
        private static bool _activeSlotEligible;
        private static int _sandstoneBrokenThisRun;
        private static int _goldSeenThisRun;

        public static bool Install(Harmony harmony)
        {
            if (_installed)
            {
                return true;
            }

            if (harmony == null)
            {
                return false;
            }

            MethodInfo target = typeof(BreakableResource).GetMethod("ChooseRandomResource", InstanceFlags, null, Type.EmptyTypes, null);
            MethodInfo postfix = typeof(RankedForceSecondGoldRuntime).GetMethod("ChooseRandomResourcePostfix", StaticFlags);
            if (target == null || postfix == null)
            {
                RankedLog.Warn("Force 2nd Gold patch unavailable because sandstone hook targets could not be resolved.");
                return false;
            }

            harmony.Patch(target, postfix: new HarmonyMethod(postfix));
            _installed = true;
            RankedLog.Info("Installed Force 2nd Gold sandstone patch.");
            return true;
        }

        public static void UpdateSessionState(string saveSlot, bool isEligible)
        {
            string normalizedSlotPath = NormalizeSlot(saveSlot);
            if (string.IsNullOrEmpty(normalizedSlotPath))
            {
                ResetRunState(string.Empty);
                return;
            }

            if (!string.Equals(normalizedSlotPath, _activeSlotPath, StringComparison.OrdinalIgnoreCase))
            {
                ResetRunState(normalizedSlotPath);
            }

            _activeSlotEligible = isEligible;
        }

        public static void Reset()
        {
            ResetRunState(string.Empty);
        }

        private static void ChooseRandomResourcePostfix(BreakableResource __instance, ref GameObject __result)
        {
            try
            {
                if (!ShouldApply() || __instance == null || _sandstoneBrokenThisRun >= SandstoneWindowSize || _goldSeenThisRun >= RequiredGolds)
                {
                    return;
                }

                SandstoneProfile profile;
                if (!TryGetSandstoneProfile(__instance, out profile) || profile == null || profile.GoldPrefab == null)
                {
                    return;
                }

                int sandstoneBreakNumber = _sandstoneBrokenThisRun + 1;
                bool resultIsGold = IsPrefabTechType(__result, TechType.Gold);
                bool originalWasSilver = IsPrefabTechType(__result, TechType.Silver);

                if (!resultIsGold &&
                    _goldSeenThisRun + (SandstoneWindowSize - sandstoneBreakNumber) < RequiredGolds &&
                    TryApplyForcedGoldEntropy(originalWasSilver, profile.SilverChance))
                {
                    __result = profile.GoldPrefab;
                    resultIsGold = true;
                    RankedLog.Info(
                        "Force 2nd Gold applied on sandstone break " +
                        sandstoneBreakNumber +
                        " of " +
                        SandstoneWindowSize +
                        " for slot '" +
                        _activeSlotPath +
                        "'.");
                }

                _sandstoneBrokenThisRun = sandstoneBreakNumber;
                if (resultIsGold)
                {
                    _goldSeenThisRun++;
                }
            }
            catch (Exception ex)
            {
                RankedLog.Warn("Force 2nd Gold sandstone patch fell back to vanilla result: " + ex.Message);
            }
        }

        private static bool ShouldApply()
        {
            RankedSeedRuntimeProfile profile = RankedSeedRuntimeHost.GetProfile();
            return _installed &&
                   _activeSlotEligible &&
                   profile != null &&
                   profile.ForceSecondGold &&
                   RankedSeedRuntimeHost.IsSupportedGameplayMode();
        }

        private static bool TryGetSandstoneProfile(BreakableResource resource, out SandstoneProfile profile)
        {
            profile = null;
            if (resource == null || !IsPrefabTechType(resource.defaultPrefab, TechType.Lead) || resource.prefabList == null || resource.prefabList.Count == 0)
            {
                return false;
            }

            profile = new SandstoneProfile();
            for (int i = 0; i < resource.prefabList.Count; i++)
            {
                BreakableResource.RandomPrefab entry = resource.prefabList[i];
                if (entry == null || entry.prefab == null)
                {
                    continue;
                }

                if (IsPrefabTechType(entry.prefab, TechType.Gold))
                {
                    profile.GoldPrefab = entry.prefab;
                    continue;
                }

                if (IsPrefabTechType(entry.prefab, TechType.Silver))
                {
                    profile.SilverChance = entry.chance;
                }
            }

            if (profile.GoldPrefab == null)
            {
                return false;
            }

            if (profile.SilverChance <= 0f)
            {
                profile.SilverChance = DefaultSilverChance;
            }

            return true;
        }

        private static bool TryApplyForcedGoldEntropy(bool originalWasSilver, float silverChance)
        {
            PlayerEntropy playerEntropy = GetPlayerEntropy();
            if (playerEntropy == null)
            {
                return false;
            }

            if (!TryAdjustEntropy(playerEntropy, TechType.Gold, -1f))
            {
                return false;
            }

            if (originalWasSilver)
            {
                return TryAdjustEntropy(playerEntropy, TechType.Silver, 1f - silverChance);
            }

            return TryAdjustEntropy(playerEntropy, TechType.Silver, 0f - silverChance);
        }

        private static PlayerEntropy GetPlayerEntropy()
        {
            try
            {
                Player player = PlayerMainField != null ? PlayerMainField.GetValue(null) as Player : Player.main;
                return player != null ? player.GetComponent<PlayerEntropy>() : null;
            }
            catch
            {
                return null;
            }
        }

        private static bool TryAdjustEntropy(PlayerEntropy playerEntropy, TechType techType, float delta)
        {
            if (playerEntropy == null || playerEntropy.randomizers == null)
            {
                return false;
            }

            for (int i = 0; i < playerEntropy.randomizers.Count; i++)
            {
                PlayerEntropy.TechEntropy entry = playerEntropy.randomizers[i];
                if (entry == null || entry.techType != techType || entry.entropy == null)
                {
                    continue;
                }

                float currentEntropy = entry.entropy.entropy;
                if (FairRandomizerEntropyField != null)
                {
                    try
                    {
                        FairRandomizerEntropyField.SetValue(entry.entropy, currentEntropy + delta);
                        return true;
                    }
                    catch
                    {
                    }
                }

                entry.entropy.entropy = currentEntropy + delta;
                return true;
            }

            return false;
        }

        private static bool IsPrefabTechType(GameObject prefab, TechType expectedTechType)
        {
            if (prefab == null)
            {
                return false;
            }

            try
            {
                return CraftData.GetTechType(prefab) == expectedTechType;
            }
            catch
            {
                return false;
            }
        }

        private static void ResetRunState(string slotPath)
        {
            _activeSlotPath = slotPath ?? string.Empty;
            _activeSlotEligible = false;
            _sandstoneBrokenThisRun = 0;
            _goldSeenThisRun = 0;
        }

        private static string NormalizeSlot(string saveSlot)
        {
            if (string.IsNullOrEmpty(saveSlot))
            {
                return string.Empty;
            }

            return saveSlot.Trim();
        }
    }
}
