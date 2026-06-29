using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UWE;
using UnityEngine;

namespace SubnauticaSpeedrunningMod.Runtime.Seeds
{
    internal static class ModBetterRngRuntimeHost
    {
        private const string HarmonyId = "subnautica.speedrunning.mod.betterrng";

        private static readonly BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private static readonly List<ModBetterRngCandidate> Candidates = new List<ModBetterRngCandidate>(128);

        private static bool _installAttempted;
        private static bool _installed;
        private static Harmony _harmony;
        private static MethodInfo _randomStartIsStartPointValidMethod;
        private static FieldInfo _randomStartTimeOfLastStartField;
        private static FieldInfo _lootDistributionSrcField;
        private static FieldInfo _lootDistributionDstField;
        private static FieldInfo _csvSpawnerLootDistributionField;
        private static Dictionary<BiomeType, float> _biomeOverrides;
        private static Dictionary<TechType, ModBetterRngResolvedEntityOverride> _entityOverrides;
        private static HashSet<BiomeType> _blockedCreatureBiomes;
        private static HashSet<BiomeType> _kelpForestBiomes;

        public static bool EnsureInstalled()
        {
            if (_installAttempted)
            {
                return _installed;
            }

            _installAttempted = true;

            try
            {
                if (!ResolveRuntimeMetadata())
                {
                    ModLog.Warn("BetterRNG runtime hooks were not installed because one or more game reflection targets could not be resolved.");
                    return false;
                }

                _harmony = new Harmony(HarmonyId);
                _harmony.Patch(
                    typeof(RandomStart).GetMethod("GetRandomStartPoint", InstanceFlags, null, Type.EmptyTypes, null),
                    prefix: new HarmonyMethod(typeof(ModBetterRngRuntimeHost).GetMethod(nameof(RandomStartPrefix), BindingFlags.Static | BindingFlags.NonPublic)));
                _harmony.Patch(
                    typeof(LootDistributionData).GetMethod("Initialize", InstanceFlags, null, new[] { typeof(Dictionary<string, LootDistributionData.SrcData>) }, null),
                    prefix: new HarmonyMethod(typeof(ModBetterRngRuntimeHost).GetMethod(nameof(LootDistributionInitializePrefix), BindingFlags.Static | BindingFlags.NonPublic)));
                _harmony.Patch(
                    typeof(EntitySlotData).GetMethod("IsTypeAllowed", InstanceFlags, null, new[] { typeof(EntitySlot.Type) }, null),
                    prefix: new HarmonyMethod(typeof(ModBetterRngRuntimeHost).GetMethod(nameof(EntitySlotIsTypeAllowedPrefix), BindingFlags.Static | BindingFlags.NonPublic)));
                _harmony.Patch(
                    typeof(CSVEntitySpawner).GetMethod("GetPrefabForSlot", InstanceFlags, null, new[] { typeof(IEntitySlot), typeof(bool) }, null),
                    prefix: new HarmonyMethod(typeof(ModBetterRngRuntimeHost).GetMethod(nameof(CsvEntitySpawnerGetPrefabForSlotPrefix), BindingFlags.Static | BindingFlags.NonPublic)));

                _installed = true;
                ModLog.Info("Installed BetterRNG runtime hooks.");
                return true;
            }
            catch (Exception ex)
            {
                ModLog.Warn("Failed to install BetterRNG runtime hooks: " + ex.Message);
                return false;
            }
        }

        private static bool ResolveRuntimeMetadata()
        {
            _randomStartTimeOfLastStartField = typeof(RandomStart).GetField("timeOfLastStart", InstanceFlags);
            _randomStartIsStartPointValidMethod = typeof(RandomStart).GetMethod("IsStartPointValid", InstanceFlags, null, new[] { typeof(Vector3), typeof(bool) }, null);
            _lootDistributionSrcField = typeof(LootDistributionData).GetField("<srcDistribution>k__BackingField", InstanceFlags);
            _lootDistributionDstField = typeof(LootDistributionData).GetField("<dstDistribution>k__BackingField", InstanceFlags);
            _csvSpawnerLootDistributionField = typeof(CSVEntitySpawner).GetField("lootDistribution", InstanceFlags);
            _biomeOverrides = ResolveBiomeOverrideMap(ModBetterRngPresetCatalog.BiomeDistributionOverrides);
            _entityOverrides = ResolveEntityOverrideMap(ModBetterRngPresetCatalog.EntityDistributionOverrides);
            _blockedCreatureBiomes = ResolveBiomeSet(ModBetterRngPresetCatalog.BlockedCreatureBiomeNames);
            _kelpForestBiomes = ResolveBiomeSet(ModSeedReferenceCatalog.KelpForestBiomes);

            return _randomStartTimeOfLastStartField != null &&
                _randomStartIsStartPointValidMethod != null &&
                _lootDistributionSrcField != null &&
                _lootDistributionDstField != null &&
                _csvSpawnerLootDistributionField != null &&
                _biomeOverrides != null &&
                _entityOverrides != null &&
                _blockedCreatureBiomes != null &&
                _kelpForestBiomes != null;
        }

        private static bool RandomStartPrefix(RandomStart __instance, ref Vector3 __result)
        {
            if (!ModSeedRuntimeHost.ShouldApplyBetterRngRules())
            {
                return true;
            }

            _randomStartTimeOfLastStartField.SetValue(__instance, Time.time);

            for (int i = 0; i < 1000; i++)
            {
                Vector3 candidate = new Vector3(
                    UnityEngine.Random.Range(ModBetterRngPresetCatalog.SpawnMinX, ModBetterRngPresetCatalog.SpawnMaxX),
                    0f,
                    UnityEngine.Random.Range(ModBetterRngPresetCatalog.SpawnMinZ, ModBetterRngPresetCatalog.SpawnMaxZ));

                try
                {
                    bool isValid = (bool)_randomStartIsStartPointValidMethod.Invoke(__instance, new object[] { candidate, false });
                    if (isValid)
                    {
                        __result = candidate;
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    ModLog.Warn("BetterRNG start-point validation failed: " + ex.Message);
                    break;
                }
            }

            __result = Vector3.zero;
            return false;
        }

        private static bool LootDistributionInitializePrefix(LootDistributionData __instance, Dictionary<string, LootDistributionData.SrcData> src)
        {
            if (!ModSeedRuntimeHost.ShouldApplyBetterRngRules())
            {
                return true;
            }

            if (__instance == null || src == null)
            {
                return true;
            }

            _lootDistributionSrcField.SetValue(__instance, src);
            Dictionary<BiomeType, LootDistributionData.DstData> dstDistribution = new Dictionary<BiomeType, LootDistributionData.DstData>();
            _lootDistributionDstField.SetValue(__instance, dstDistribution);

            foreach (KeyValuePair<string, LootDistributionData.SrcData> entry in src)
            {
                string classId = entry.Key;
                List<LootDistributionData.BiomeData> distribution = entry.Value == null ? null : entry.Value.distribution;
                if (distribution == null)
                {
                    continue;
                }

                for (int i = 0; i < distribution.Count; i++)
                {
                    LootDistributionData.BiomeData biomeData = distribution[i];
                    if (biomeData == null)
                    {
                        continue;
                    }

                    float probability = biomeData.probability;
                    float overrideProbability;
                    if (_biomeOverrides.TryGetValue(biomeData.biome, out overrideProbability))
                    {
                        probability = overrideProbability;
                    }

                    LootDistributionData.DstData dstData;
                    if (!dstDistribution.TryGetValue(biomeData.biome, out dstData) || dstData == null)
                    {
                        dstData = new LootDistributionData.DstData
                        {
                            prefabs = new List<LootDistributionData.PrefabData>()
                        };
                        dstDistribution[biomeData.biome] = dstData;
                    }

                    dstData.prefabs.Add(new LootDistributionData.PrefabData
                    {
                        classId = classId,
                        count = biomeData.count,
                        probability = probability
                    });
                }
            }

            return false;
        }

        private static bool EntitySlotIsTypeAllowedPrefix(EntitySlotData __instance, EntitySlot.Type slotType, ref bool __result)
        {
            if (!ModSeedRuntimeHost.ShouldApplyBetterRngRules())
            {
                return true;
            }

            if (__instance == null || slotType != EntitySlot.Type.Creature)
            {
                return true;
            }

            if (_blockedCreatureBiomes.Contains(__instance.biomeType))
            {
                __result = false;
                return false;
            }

            return true;
        }

        private static bool CsvEntitySpawnerGetPrefabForSlotPrefix(CSVEntitySpawner __instance, IEntitySlot slot, bool filterKnown, ref EntitySlot.Filler __result)
        {
            if (!ModSeedRuntimeHost.ShouldApplyBetterRngRules())
            {
                return true;
            }

            __result = new EntitySlot.Filler
            {
                classId = null,
                count = 0
            };

            LootDistributionData lootDistribution = GetLootDistribution(__instance);
            if (lootDistribution == null || slot == null)
            {
                return true;
            }

            LootDistributionData.DstData biomeLoot;
            if (!lootDistribution.GetBiomeLoot(slot.GetBiomeType(), out biomeLoot) || biomeLoot == null || biomeLoot.prefabs == null)
            {
                return false;
            }

            Dictionary<string, LootDistributionData.SrcData> srcDistribution = lootDistribution.srcDistribution;
            if (srcDistribution == null)
            {
                return false;
            }

            if (Candidates.Count > 0)
            {
                Candidates.Clear();
            }

            float skippedKnownFragmentProbability = 0f;
            float retainedFragmentProbability = 0f;
            BiomeType slotBiome = slot.GetBiomeType();

            for (int i = 0; i < biomeLoot.prefabs.Count; i++)
            {
                LootDistributionData.PrefabData prefabData = biomeLoot.prefabs[i];
                if (prefabData == null ||
                    string.Equals(prefabData.classId, "None", StringComparison.Ordinal) ||
                    !srcDistribution.ContainsKey(prefabData.classId))
                {
                    continue;
                }

                WorldEntityInfo info;
                if (!WorldEntityDatabase.TryGetInfo(prefabData.classId, out info))
                {
                    continue;
                }

                if (!slot.IsTypeAllowed(info.slotType))
                {
                    continue;
                }

                float probability = prefabData.probability / Mathf.Max(0.0001f, slot.GetDensity());
                if (probability <= 0f)
                {
                    continue;
                }

                if (info.techType == TechType.Stalker && !_kelpForestBiomes.Contains(slotBiome))
                {
                    continue;
                }

                int count = prefabData.count;
                ModBetterRngResolvedEntityOverride entityOverride;
                if (_entityOverrides.TryGetValue(info.techType, out entityOverride))
                {
                    if (entityOverride.HasAllowedBiomes &&
                        (entityOverride.AllowedBiomes == null || !entityOverride.AllowedBiomes.Contains(slotBiome)))
                    {
                        continue;
                    }

                    probability = entityOverride.Chance;
                    if (entityOverride.HasCount)
                    {
                        count = entityOverride.Count;
                    }
                }

                if (probability <= 0f)
                {
                    continue;
                }

                bool isFragment = false;
                if (filterKnown)
                {
                    isFragment = PDAScanner.IsFragment(info.techType);
                    if (isFragment && PDAScanner.ContainsCompleteEntry(info.techType))
                    {
                        skippedKnownFragmentProbability += probability;
                        continue;
                    }
                }

                Candidates.Add(new ModBetterRngCandidate
                {
                    ClassId = prefabData.classId,
                    Count = count,
                    Probability = probability,
                    IsFragment = isFragment
                });

                if (isFragment)
                {
                    retainedFragmentProbability += probability;
                }
            }

            if (skippedKnownFragmentProbability > 0f && retainedFragmentProbability > 0f)
            {
                float compensationScale = (skippedKnownFragmentProbability + retainedFragmentProbability) / retainedFragmentProbability;
                for (int i = 0; i < Candidates.Count; i++)
                {
                    ModBetterRngCandidate candidate = Candidates[i];
                    if (candidate.IsFragment)
                    {
                        candidate.Probability *= compensationScale;
                        Candidates[i] = candidate;
                    }
                }
            }

            float totalProbability = 0f;
            for (int i = 0; i < Candidates.Count; i++)
            {
                totalProbability += Candidates[i].Probability;
            }

            if (totalProbability > 0f)
            {
                float roll = UnityEngine.Random.value * (totalProbability > 1f ? totalProbability : 1f);
                float accumulated = 0f;
                for (int i = 0; i < Candidates.Count; i++)
                {
                    ModBetterRngCandidate candidate = Candidates[i];
                    accumulated += candidate.Probability;
                    if (accumulated >= roll)
                    {
                        __result.classId = candidate.ClassId;
                        __result.count = candidate.Count;
                        break;
                    }
                }
            }

            Candidates.Clear();
            return false;
        }

        private static LootDistributionData GetLootDistribution(CSVEntitySpawner spawner)
        {
            return spawner == null
                ? null
                : _csvSpawnerLootDistributionField.GetValue(spawner) as LootDistributionData;
        }

        private static Dictionary<BiomeType, float> ResolveBiomeOverrideMap(Dictionary<string, float> source)
        {
            Dictionary<BiomeType, float> resolved = new Dictionary<BiomeType, float>();
            foreach (KeyValuePair<string, float> entry in source)
            {
                try
                {
                    BiomeType biome = (BiomeType)Enum.Parse(typeof(BiomeType), entry.Key, true);
                    resolved[biome] = Mathf.Max(0f, entry.Value);
                }
                catch (Exception ex)
                {
                    ModLog.Warn("BetterRNG preset contains unknown biome '" + entry.Key + "': " + ex.Message);
                }
            }

            return resolved;
        }

        private static Dictionary<TechType, ModBetterRngResolvedEntityOverride> ResolveEntityOverrideMap(Dictionary<string, ModBetterRngEntityOverrideDefinition> source)
        {
            Dictionary<TechType, ModBetterRngResolvedEntityOverride> resolved = new Dictionary<TechType, ModBetterRngResolvedEntityOverride>();
            foreach (KeyValuePair<string, ModBetterRngEntityOverrideDefinition> entry in source)
            {
                ModBetterRngEntityOverrideDefinition definition = entry.Value;
                if (definition == null || string.IsNullOrEmpty(definition.TechTypeName))
                {
                    continue;
                }

                try
                {
                    TechType techType = (TechType)Enum.Parse(typeof(TechType), definition.TechTypeName, true);
                    resolved[techType] = new ModBetterRngResolvedEntityOverride
                    {
                        Chance = Mathf.Max(0f, definition.Chance),
                        Count = Mathf.Max(0, definition.Count),
                        HasCount = definition.HasCount,
                        AllowedBiomes = definition.BiomeNames == null || definition.BiomeNames.Length == 0
                            ? null
                            : ResolveBiomeSet(definition.BiomeNames),
                        HasAllowedBiomes = definition.BiomeNames != null && definition.BiomeNames.Length > 0
                    };
                }
                catch (Exception ex)
                {
                    ModLog.Warn("BetterRNG preset contains unknown TechType '" + definition.TechTypeName + "': " + ex.Message);
                }
            }

            return resolved;
        }

        private static HashSet<BiomeType> ResolveBiomeSet(string[] names)
        {
            HashSet<BiomeType> resolved = new HashSet<BiomeType>();
            for (int i = 0; i < names.Length; i++)
            {
                string name = names[i];
                if (string.IsNullOrEmpty(name))
                {
                    continue;
                }

                try
                {
                    resolved.Add((BiomeType)Enum.Parse(typeof(BiomeType), name, true));
                }
                catch (Exception ex)
                {
                    ModLog.Warn("BetterRNG preset contains unknown biome '" + name + "': " + ex.Message);
                }
            }

            return resolved;
        }

        private struct ModBetterRngCandidate
        {
            public string ClassId;
            public int Count;
            public float Probability;
            public bool IsFragment;
        }

        private struct ModBetterRngResolvedEntityOverride
        {
            public float Chance;
            public int Count;
            public bool HasCount;
            public HashSet<BiomeType> AllowedBiomes;
            public bool HasAllowedBiomes;
        }
    }
}
