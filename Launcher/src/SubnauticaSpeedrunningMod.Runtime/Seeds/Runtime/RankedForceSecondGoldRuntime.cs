using System;
using System.Collections;
using System.IO;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace SubnauticaSpeedrunningMod.Runtime.Seeds
{
    internal static class ModForceSecondGoldRuntime
    {
        private const string HarmonyId = "subnautica.speedrunning.ranked.force.second.gold";
        private const int SandstoneWindowSize = 6;
        private const int RequiredGolds = 2;
        private const float SandstoneSilverChance = 0.5f;

        private static readonly BindingFlags StaticFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
        private static readonly BindingFlags InstanceFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        private static bool _installAttempted;
        private static bool _installed;
        private static bool _available = true;
        private static Harmony _harmony;
        private static MethodInfo _chooseRandomResourceMethod;
        private static MethodInfo _craftDataGetTechTypeMethod;
        private static FieldInfo _defaultPrefabField;
        private static FieldInfo _prefabListField;
        private static FieldInfo _randomPrefabPrefabField;
        private static FieldInfo _randomPrefabChanceField;
        private static FieldInfo _playerMainField;
        private static PropertyInfo _playerMainProperty;
        private static MethodInfo _gameObjectGetComponentByTypeMethod;
        private static Type _playerEntropyType;
        private static FieldInfo _playerEntropyRandomizersField;
        private static FieldInfo _techEntropyTechTypeField;
        private static FieldInfo _techEntropyEntropyField;
        private static FieldInfo _fairRandomizerEntropyField;

        private static string _activeSlotPath = string.Empty;
        private static bool _activeSlotEligible;
        private static int _sandstoneBrokenThisRun;
        private static int _goldSeenThisRun;

        public static bool EnsureInstalled()
        {
            if (_installAttempted)
            {
                return _installed && _available;
            }

            _installAttempted = true;

            if (!ResolveHooks())
            {
                _available = false;
                ModLog.Warn("Force 2nd Gold patch unavailable; required sandstone reflection hooks were not found.");
                return false;
            }

            try
            {
                MethodInfo postfix = typeof(ModForceSecondGoldRuntime).GetMethod(nameof(ChooseRandomResourcePostfix), StaticFlags);
                if (postfix == null)
                {
                    _available = false;
                    ModLog.Warn("Force 2nd Gold patch unavailable; postfix method could not be resolved.");
                    return false;
                }

                _harmony = new Harmony(HarmonyId);
                _harmony.Patch(_chooseRandomResourceMethod, postfix: new HarmonyMethod(postfix));
                _installed = true;
                ModLog.Info("Installed sandstone Force 2nd Gold patch.");
                return true;
            }
            catch (Exception ex)
            {
                _available = false;
                ModLog.Warn("Failed to install Force 2nd Gold patch: " + ex.Message);
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
                ModLog.Info("Force 2nd Gold armed for fresh run slot '" + normalizedSlotPath + "'.");
            }

            _activeSlotEligible = isEligible;
        }

        public static void Reset()
        {
            ResetRunState(string.Empty);
        }

        private static void ChooseRandomResourcePostfix(object __instance, ref GameObject __result)
        {
            HandleChooseRandomResource(__instance, ref __result);
        }

        private static void HandleChooseRandomResource(object instance, ref GameObject result)
        {
            try
            {
                if (!ShouldApplyRuntime() || instance == null)
                {
                    return;
                }

                if (_sandstoneBrokenThisRun >= SandstoneWindowSize || _goldSeenThisRun >= RequiredGolds)
                {
                    return;
                }

                SandstoneProfile profile;
                if (!TryGetSandstoneProfile(instance, out profile))
                {
                    return;
                }

                int currentBreakIndex = _sandstoneBrokenThisRun + 1;
                bool originalWasGold = IsPrefabTechType(result, "Gold");
                bool originalWasSilver = IsPrefabTechType(result, "Silver");
                bool mustForceGold =
                    !originalWasGold &&
                    _goldSeenThisRun + (SandstoneWindowSize - currentBreakIndex) < RequiredGolds;

                if (mustForceGold && profile.GoldPrefab != null)
                {
                    if (TryApplyForcedGoldEntropy(originalWasSilver, profile.SilverChance))
                    {
                        result = profile.GoldPrefab;
                        originalWasGold = true;
                        ModLog.Info(
                            "Force 2nd Gold applied on sandstone break " +
                            currentBreakIndex +
                            " of " +
                            SandstoneWindowSize +
                            "; priorGolds=" +
                            _goldSeenThisRun +
                            ".");
                    }
                }

                _sandstoneBrokenThisRun = currentBreakIndex;
                if (originalWasGold)
                {
                    _goldSeenThisRun++;
                }
            }
            catch (Exception ex)
            {
                ModLog.Warn("Force 2nd Gold patch failed: " + ex.Message);
            }
        }

        private static bool ResolveHooks()
        {
            Type breakableResourceType = ResolveType("BreakableResource, Assembly-CSharp");
            Type randomPrefabType = ResolveType("BreakableResource+RandomPrefab, Assembly-CSharp");
            Type playerType = ResolveType("Player, Assembly-CSharp");
            _playerEntropyType = ResolveType("PlayerEntropy, Assembly-CSharp");
            Type craftDataType = ResolveType("CraftData, Assembly-CSharp");
            Type techEntropyType = ResolveType("PlayerEntropy+TechEntropy, Assembly-CSharp");
            Type fairRandomizerType = ResolveType("FairRandomizer, Assembly-CSharp");

            _chooseRandomResourceMethod = breakableResourceType == null
                ? null
                : breakableResourceType.GetMethod("ChooseRandomResource", InstanceFlags, null, Type.EmptyTypes, null);
            _defaultPrefabField = FindField(breakableResourceType, "defaultPrefab");
            _prefabListField = FindField(breakableResourceType, "prefabList");
            _randomPrefabPrefabField = FindField(randomPrefabType, "prefab");
            _randomPrefabChanceField = FindField(randomPrefabType, "chance");
            _craftDataGetTechTypeMethod = FindCraftDataGetTechTypeMethod(craftDataType);
            _gameObjectGetComponentByTypeMethod = typeof(GameObject).GetMethod("GetComponent", new[] { typeof(Type) });
            _playerMainField = FindField(playerType, "main");
            _playerMainProperty = FindProperty(playerType, "main");
            _playerEntropyRandomizersField = FindField(_playerEntropyType, "randomizers");
            _techEntropyTechTypeField = FindField(techEntropyType, "techType");
            _techEntropyEntropyField = FindField(techEntropyType, "entropy");
            _fairRandomizerEntropyField = FindField(fairRandomizerType, "entropy");

            return _chooseRandomResourceMethod != null &&
                   _defaultPrefabField != null &&
                   _prefabListField != null &&
                   _randomPrefabPrefabField != null &&
                   _randomPrefabChanceField != null &&
                   _craftDataGetTechTypeMethod != null &&
                   _gameObjectGetComponentByTypeMethod != null &&
                   _playerEntropyType != null &&
                   _playerEntropyRandomizersField != null &&
                   _techEntropyTechTypeField != null &&
                   _techEntropyEntropyField != null &&
                   _fairRandomizerEntropyField != null;
        }

        private static bool TryGetSandstoneProfile(object instance, out SandstoneProfile profile)
        {
            profile = default(SandstoneProfile);

            GameObject defaultPrefab = _defaultPrefabField.GetValue(instance) as GameObject;
            if (!IsPrefabTechType(defaultPrefab, "Lead"))
            {
                return false;
            }

            IList prefabs = _prefabListField.GetValue(instance) as IList;
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

                GameObject prefab = _randomPrefabPrefabField.GetValue(randomPrefab) as GameObject;
                float chance = ReadSingle(_randomPrefabChanceField.GetValue(randomPrefab), 0f);
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
                profile.SilverChance = SandstoneSilverChance;
            }

            return true;
        }

        private static bool TryApplyForcedGoldEntropy(bool originalWasSilver, float silverChance)
        {
            object player = ReadStaticMember(_playerMainField, _playerMainProperty);
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
            if (_playerEntropyType == null || player == null)
            {
                return false;
            }

            try
            {
                Component component = player as Component;
                if (component == null || component.gameObject == null)
                {
                    return false;
                }

                playerEntropy = _gameObjectGetComponentByTypeMethod.Invoke(component.gameObject, new object[] { _playerEntropyType });
                return playerEntropy != null;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryAdjustEntropy(object playerEntropy, string techTypeName, float delta)
        {
            if (playerEntropy == null || string.IsNullOrEmpty(techTypeName))
            {
                return false;
            }

            IList randomizers = _playerEntropyRandomizersField.GetValue(playerEntropy) as IList;
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

                object techType = _techEntropyTechTypeField.GetValue(techEntropy);
                if (!string.Equals(techType == null ? string.Empty : techType.ToString(), techTypeName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                object fairRandomizer = _techEntropyEntropyField.GetValue(techEntropy);
                if (fairRandomizer == null)
                {
                    return false;
                }

                float currentEntropy = ReadSingle(_fairRandomizerEntropyField.GetValue(fairRandomizer), 0f);
                _fairRandomizerEntropyField.SetValue(fairRandomizer, currentEntropy + delta);
                return true;
            }

            return false;
        }

        private static bool IsPrefabTechType(GameObject prefab, string techTypeName)
        {
            if (prefab == null || string.IsNullOrEmpty(techTypeName) || _craftDataGetTechTypeMethod == null)
            {
                return false;
            }

            try
            {
                object techType = _craftDataGetTechTypeMethod.Invoke(null, new object[] { prefab });
                return string.Equals(techType == null ? string.Empty : techType.ToString(), techTypeName, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private static bool ShouldApplyRuntime()
        {
            ModSeedRuntimeProfile profile = ModSeedRuntimeHost.GetProfile();
            return _installed &&
                   _activeSlotEligible &&
                   _available &&
                   profile != null &&
                   profile.ForceSecondGold &&
                   ModSeedRuntimeHost.IsSurvivalLikeMode() &&
                   Player.main != null;
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
            if (string.IsNullOrEmpty(saveSlot))
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

                return Convert.ToSingle(value, System.Globalization.CultureInfo.InvariantCulture);
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
            if (declaringType == null || string.IsNullOrEmpty(name))
            {
                return null;
            }

            return declaringType.GetField(name, StaticFlags | InstanceFlags);
        }

        private static PropertyInfo FindProperty(Type declaringType, string name)
        {
            if (declaringType == null || string.IsNullOrEmpty(name))
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

        private struct SandstoneProfile
        {
            public GameObject GoldPrefab;
            public float SilverChance;
        }
    }
}
