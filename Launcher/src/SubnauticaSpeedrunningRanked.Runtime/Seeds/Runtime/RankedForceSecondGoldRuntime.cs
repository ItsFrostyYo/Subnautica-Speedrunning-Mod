using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace SubnauticaSpeedrunningRanked.Runtime.Seeds
{
    internal static class RankedForceSecondGoldRuntime
    {
        private struct SandstoneProfile
        {
            public GameObject GoldPrefab;
            public float SilverChance;
        }

        private const int SandstoneWindowSize = 6;
        private const int RequiredGolds = 2;
        private const float DefaultSilverChance = 0.5f;

        private static readonly BindingFlags StaticFlags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        private static readonly BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        private static readonly Type BreakableResourceType = ResolveType("BreakableResource, Assembly-CSharp");
        private static readonly Type PlayerType = ResolveType("Player, Assembly-CSharp");
        private static readonly Type PlayerEntropyType = ResolveType("PlayerEntropy, Assembly-CSharp");
        private static readonly MethodInfo ChooseRandomResourceMethod = BreakableResourceType == null
            ? null
            : BreakableResourceType.GetMethod("ChooseRandomResource", InstanceFlags, null, Type.EmptyTypes, null);
        private static readonly FieldInfo DefaultPrefabField = FindField(BreakableResourceType, "defaultPrefab");
        private static readonly FieldInfo PrefabListField = FindField(BreakableResourceType, "prefabList");
        private static readonly FieldInfo RandomPrefabPrefabField = FindField(ResolveType("BreakableResource+RandomPrefab, Assembly-CSharp"), "prefab");
        private static readonly FieldInfo RandomPrefabChanceField = FindField(ResolveType("BreakableResource+RandomPrefab, Assembly-CSharp"), "chance");
        private static readonly MethodInfo CraftDataGetTechTypeMethod = FindCraftDataGetTechTypeMethod(ResolveType("CraftData, Assembly-CSharp"));
        private static readonly MethodInfo GameObjectGetComponentByTypeMethod = typeof(GameObject).GetMethod("GetComponent", new[] { typeof(Type) });
        private static readonly FieldInfo PlayerMainField = FindField(PlayerType, "main");
        private static readonly PropertyInfo PlayerMainProperty = FindProperty(PlayerType, "main");
        private static readonly FieldInfo PlayerEntropyRandomizersField = FindField(PlayerEntropyType, "randomizers");
        private static readonly FieldInfo TechEntropyTechTypeField = FindField(ResolveType("PlayerEntropy+TechEntropy, Assembly-CSharp"), "techType");
        private static readonly FieldInfo TechEntropyEntropyField = FindField(ResolveType("PlayerEntropy+TechEntropy, Assembly-CSharp"), "entropy");
        private static readonly FieldInfo FairRandomizerEntropyField = FindField(ResolveType("FairRandomizer, Assembly-CSharp"), "entropy");

        private static bool _initialized;
        private static bool _available = true;
        private static bool _installed;
        private static string _activeSlotPath = string.Empty;
        private static bool _activeSlotEligible;
        private static int _sandstoneBrokenThisRun;
        private static int _goldSeenThisRun;

        public static bool Install(Harmony harmony)
        {
            if (_initialized)
            {
                return _available && _installed;
            }

            _initialized = true;
            if (harmony == null)
            {
                _available = false;
                return false;
            }

            if (ChooseRandomResourceMethod == null ||
                DefaultPrefabField == null ||
                PrefabListField == null ||
                RandomPrefabPrefabField == null ||
                RandomPrefabChanceField == null ||
                CraftDataGetTechTypeMethod == null ||
                GameObjectGetComponentByTypeMethod == null ||
                PlayerEntropyRandomizersField == null ||
                TechEntropyTechTypeField == null ||
                TechEntropyEntropyField == null ||
                FairRandomizerEntropyField == null)
            {
                _available = false;
                RankedLog.Warn("Force 2nd Gold patch unavailable; required sandstone reflection hooks were not found.");
                return false;
            }

            try
            {
                MethodInfo postfix = typeof(RankedForceSecondGoldRuntime).GetMethod("ChooseRandomResourcePostfix", StaticFlags);
                if (postfix == null)
                {
                    _available = false;
                    return false;
                }

                harmony.Patch(ChooseRandomResourceMethod, postfix: new HarmonyMethod(postfix));
                _installed = true;
                RankedLog.Info("Installed sandstone Force 2nd Gold patch.");
                return true;
            }
            catch (Exception ex)
            {
                _available = false;
                RankedLog.Warn("Failed to install Force 2nd Gold patch: " + ex.Message);
                return false;
            }
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

            if (!_activeSlotEligible && isEligible)
            {
                ResetCounters(normalizedSlotPath);
            }

            _activeSlotEligible = isEligible;
        }

        public static void Reset()
        {
            ResetRunState(string.Empty);
        }

        private static void ChooseRandomResourcePostfix(object __instance, ref GameObject __result)
        {
            try
            {
                if (!ShouldApply() || __instance == null)
                {
                    return;
                }

                if (_sandstoneBrokenThisRun >= SandstoneWindowSize || _goldSeenThisRun >= RequiredGolds)
                {
                    return;
                }

                SandstoneProfile profile;
                if (!TryGetSandstoneProfile(__instance, out profile) || profile.GoldPrefab == null)
                {
                    return;
                }

                int sandstoneBreakNumber = _sandstoneBrokenThisRun + 1;
                bool resultIsGold = IsPrefabTechType(__result, "Gold");
                bool originalWasSilver = IsPrefabTechType(__result, "Silver");
                bool mustForceGold = !resultIsGold &&
                    _goldSeenThisRun + (SandstoneWindowSize - sandstoneBreakNumber) < RequiredGolds;

                if (mustForceGold && TryApplyForcedGoldEntropy(originalWasSilver, profile.SilverChance))
                {
                    __result = profile.GoldPrefab;
                    resultIsGold = true;
                    RankedLog.Info(
                        "Force 2nd Gold applied on sandstone break " +
                        sandstoneBreakNumber +
                        " of " +
                        SandstoneWindowSize +
                        "; priorGolds=" +
                        _goldSeenThisRun +
                        "; slot='" +
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
                RankedLog.Warn("Force 2nd Gold patch failed: " + ex.Message);
            }
        }

        private static bool ShouldApply()
        {
            RankedSeedRuntimeProfile profile = RankedSeedRuntimeHost.GetProfile();
            return _available &&
                _installed &&
                _activeSlotEligible &&
                profile != null &&
                profile.ForceSecondGold &&
                RankedSeedRuntimeHost.IsSurvivalLikeMode();
        }

        private static bool TryGetSandstoneProfile(object resource, out SandstoneProfile profile)
        {
            profile = default(SandstoneProfile);

            GameObject defaultPrefab = DefaultPrefabField.GetValue(resource) as GameObject;
            if (!IsPrefabTechType(defaultPrefab, "Lead"))
            {
                return false;
            }

            IList prefabs = PrefabListField.GetValue(resource) as IList;
            if (prefabs == null || prefabs.Count == 0)
            {
                return false;
            }

            bool foundGold = false;
            bool foundSilver = false;
            for (int i = 0; i < prefabs.Count; i++)
            {
                object randomPrefab = prefabs[i];
                if (randomPrefab == null)
                {
                    continue;
                }

                GameObject prefab = RandomPrefabPrefabField.GetValue(randomPrefab) as GameObject;
                float chance = ReadSingle(RandomPrefabChanceField.GetValue(randomPrefab), 0f);
                if (prefab == null)
                {
                    continue;
                }

                if (IsPrefabTechType(prefab, "Gold"))
                {
                    profile.GoldPrefab = prefab;
                    foundGold = true;
                }
                else if (IsPrefabTechType(prefab, "Silver"))
                {
                    profile.SilverChance = chance;
                    foundSilver = true;
                }
            }

            if (!foundGold || !foundSilver)
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
            object player = ReadStaticMember(PlayerMainField, PlayerMainProperty);
            object playerEntropy;
            if (player == null || !TryGetPlayerEntropyComponent(player, out playerEntropy))
            {
                return false;
            }

            if (!TryAdjustEntropy(playerEntropy, "Gold", -1f))
            {
                return false;
            }

            if (originalWasSilver)
            {
                return TryAdjustEntropy(playerEntropy, "Silver", 1f - silverChance);
            }

            return TryAdjustEntropy(playerEntropy, "Silver", -silverChance);
        }

        private static bool TryGetPlayerEntropyComponent(object player, out object playerEntropy)
        {
            playerEntropy = null;
            if (PlayerEntropyType == null || player == null || GameObjectGetComponentByTypeMethod == null)
            {
                return false;
            }

            try
            {
                GameObject gameObject = (player as Component)?.gameObject;
                if (gameObject == null)
                {
                    return false;
                }

                playerEntropy = GameObjectGetComponentByTypeMethod.Invoke(gameObject, new object[] { PlayerEntropyType });
                return playerEntropy != null;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryAdjustEntropy(object playerEntropy, string techTypeName, float delta)
        {
            if (playerEntropy == null || IsBlank(techTypeName))
            {
                return false;
            }

            IList randomizers = PlayerEntropyRandomizersField.GetValue(playerEntropy) as IList;
            if (randomizers == null)
            {
                return false;
            }

            for (int i = 0; i < randomizers.Count; i++)
            {
                object techEntropy = randomizers[i];
                if (techEntropy == null)
                {
                    continue;
                }

                object techType = TechEntropyTechTypeField.GetValue(techEntropy);
                if (!string.Equals(techType == null ? string.Empty : techType.ToString(), techTypeName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                object fairRandomizer = TechEntropyEntropyField.GetValue(techEntropy);
                if (fairRandomizer == null)
                {
                    return false;
                }

                float currentEntropy = ReadSingle(FairRandomizerEntropyField.GetValue(fairRandomizer), 0f);
                FairRandomizerEntropyField.SetValue(fairRandomizer, currentEntropy + delta);
                return true;
            }

            return false;
        }

        private static bool IsPrefabTechType(GameObject prefab, string techTypeName)
        {
            if (prefab == null || IsBlank(techTypeName) || CraftDataGetTechTypeMethod == null)
            {
                return false;
            }

            try
            {
                object techType = CraftDataGetTechTypeMethod.Invoke(null, new object[] { prefab });
                return string.Equals(techType == null ? string.Empty : techType.ToString(), techTypeName, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private static void ResetRunState(string slotPath)
        {
            _activeSlotPath = NormalizeSlot(slotPath);
            _activeSlotEligible = false;
            _sandstoneBrokenThisRun = 0;
            _goldSeenThisRun = 0;
        }

        private static void ResetCounters(string slotPath)
        {
            _activeSlotPath = NormalizeSlot(slotPath);
            _sandstoneBrokenThisRun = 0;
            _goldSeenThisRun = 0;
        }

        private static string NormalizeSlot(string saveSlot)
        {
            if (IsBlank(saveSlot))
            {
                return string.Empty;
            }

            try
            {
                return Path.GetFullPath(saveSlot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }
            catch
            {
                return saveSlot.Trim();
            }
        }

        private static object ReadStaticMember(FieldInfo field, PropertyInfo property)
        {
            try
            {
                if (field != null)
                {
                    return field.GetValue(null);
                }

                if (property != null)
                {
                    return property.GetValue(null, null);
                }
            }
            catch
            {
            }

            return null;
        }

        private static float ReadSingle(object value, float fallback)
        {
            try
            {
                if (value == null)
                {
                    return fallback;
                }

                return Convert.ToSingle(value, CultureInfo.InvariantCulture);
            }
            catch
            {
                return fallback;
            }
        }

        private static Type ResolveType(string qualifiedName)
        {
            try
            {
                Type resolved = Type.GetType(qualifiedName, false);
                if (resolved != null)
                {
                    return resolved;
                }

                int commaIndex = qualifiedName.IndexOf(',');
                string typeName = commaIndex > 0 ? qualifiedName.Substring(0, commaIndex).Trim() : qualifiedName.Trim();
                Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
                for (int i = 0; i < assemblies.Length; i++)
                {
                    try
                    {
                        resolved = assemblies[i].GetType(typeName, false);
                        if (resolved != null)
                        {
                            return resolved;
                        }
                    }
                    catch
                    {
                    }
                }
            }
            catch
            {
            }

            return null;
        }

        private static FieldInfo FindField(Type declaringType, string name)
        {
            if (declaringType == null || IsBlank(name))
            {
                return null;
            }

            return declaringType.GetField(name, StaticFlags | InstanceFlags);
        }

        private static PropertyInfo FindProperty(Type declaringType, string name)
        {
            if (declaringType == null || IsBlank(name))
            {
                return null;
            }

            return declaringType.GetProperty(name, StaticFlags | InstanceFlags);
        }

        private static MethodInfo FindCraftDataGetTechTypeMethod(Type craftDataType)
        {
            if (craftDataType == null)
            {
                return null;
            }

            MethodInfo[] methods = craftDataType.GetMethods(StaticFlags);
            for (int i = 0; i < methods.Length; i++)
            {
                MethodInfo method = methods[i];
                if (!string.Equals(method.Name, "GetTechType", StringComparison.Ordinal))
                {
                    continue;
                }

                ParameterInfo[] parameters = method.GetParameters();
                if (parameters != null && parameters.Length == 1 && parameters[0].ParameterType == typeof(GameObject))
                {
                    return method;
                }
            }

            return null;
        }

        private static bool IsBlank(string value)
        {
            return value == null || value.Trim().Length == 0;
        }
    }
}
