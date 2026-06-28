using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UWE;
using UnityEngine;

namespace SubnauticaSpeedrunningMod.Runtime.Seeds
{
    internal static class ModSeedWorldRuntime
    {
        private static readonly HashSet<LootDistributionData> PatchedLootDistributions = new HashSet<LootDistributionData>();
        private static FieldInfo _csvEntitySpawnerLootDistributionField;
        private static float _nextCreatureSweepAt;
        private static float _nextManualCreatureSweepAt;
        private static float _nextLootSweepAt;
        private static int _lootIdleSweeps;
        private static int _creatureCullIdleSweeps;
        private static bool _manualCreatureSpawnInProgress;
        private static bool _manualCreatureSpawnComplete;

        public static void Update(bool worldActive, bool inGame)
        {
            if (!worldActive)
            {
                return;
            }

            ModSeedRuntimeProfile profile = ModSeedRuntimeHost.GetProfile();
            if (profile == null || !ModSeedRuntimeHost.IsSupportedGameplayMode())
            {
                return;
            }

            if (inGame)
            {
                ModSeedRuntimeHost.EnsureAlwaysActiveHooksInstalled();
            }

            bool includeSurvivalSeedGroups = ModSeedRuntimeHost.IsSurvivalLikeMode();

            if (Time.unscaledTime >= _nextLootSweepAt)
            {
                bool patchedLiveDistributions = ApplyAlwaysOnLootDistributionRules(profile, includeSurvivalSeedGroups);
                _lootIdleSweeps = patchedLiveDistributions ? 0 : _lootIdleSweeps + 1;
                _nextLootSweepAt = Time.unscaledTime + (_lootIdleSweeps >= 3 ? 10f : 1f);
            }

            if (inGame && includeSurvivalSeedGroups && Time.unscaledTime >= _nextManualCreatureSweepAt)
            {
                _nextManualCreatureSweepAt = Time.unscaledTime + 1f;
                EnsureManualCreatureSpawns(profile);
            }

            if (inGame &&
                (profile.BlockCreaturesInPrisonAquarium || profile.RestrictStalkersToKelpForest) &&
                Time.unscaledTime >= _nextCreatureSweepAt)
            {
                bool removedCreature = CullBlockedCreatures(profile);
                _creatureCullIdleSweeps = removedCreature ? 0 : _creatureCullIdleSweeps + 1;
                _nextCreatureSweepAt = Time.unscaledTime + (_creatureCullIdleSweeps >= 2 ? 10f : 2f);
            }
        }

        public static void Reset()
        {
            PatchedLootDistributions.Clear();
            _nextCreatureSweepAt = 0f;
            _nextManualCreatureSweepAt = 0f;
            _nextLootSweepAt = 0f;
            _lootIdleSweeps = 0;
            _creatureCullIdleSweeps = 0;
            _manualCreatureSpawnInProgress = false;
            _manualCreatureSpawnComplete = false;
        }

        private static bool ApplyAlwaysOnLootDistributionRules(ModSeedRuntimeProfile profile, bool includeSurvivalSeedGroups)
        {
            CSVEntitySpawner[] spawners = UnityEngine.Object.FindObjectsOfType<CSVEntitySpawner>();
            int patchedThisSweep = 0;
            int touchedBiomeEntries = 0;

            for (int i = 0; i < spawners.Length; i++)
            {
                CSVEntitySpawner spawner = spawners[i];
                LootDistributionData lootDistribution = GetLootDistribution(spawner);
                if (lootDistribution == null || lootDistribution.srcDistribution == null || PatchedLootDistributions.Contains(lootDistribution))
                {
                    continue;
                }

                int touchedForDistribution = ApplyProfileToLootDistribution(lootDistribution, profile, includeSurvivalSeedGroups);
                PatchedLootDistributions.Add(lootDistribution);
                patchedThisSweep++;
                touchedBiomeEntries += touchedForDistribution;
            }

            if (patchedThisSweep > 0)
            {
                ModLog.Info(
                    "Applied always-on loot distribution rules to " +
                    patchedThisSweep +
                    " live spawner tables touching " +
                    touchedBiomeEntries +
                    " biome probability entries.");
            }

            return patchedThisSweep > 0;
        }

        private static LootDistributionData GetLootDistribution(CSVEntitySpawner spawner)
        {
            if (spawner == null)
            {
                return null;
            }

            if (_csvEntitySpawnerLootDistributionField == null)
            {
                _csvEntitySpawnerLootDistributionField = typeof(CSVEntitySpawner).GetField("lootDistribution", BindingFlags.Instance | BindingFlags.NonPublic);
            }

            return _csvEntitySpawnerLootDistributionField != null
                ? _csvEntitySpawnerLootDistributionField.GetValue(spawner) as LootDistributionData
                : null;
        }

        private static int ApplyProfileToLootDistribution(LootDistributionData lootDistribution, ModSeedRuntimeProfile profile, bool includeSurvivalSeedGroups)
        {
            int touchedEntries = 0;

            try
            {
                foreach (KeyValuePair<string, LootDistributionData.SrcData> entry in lootDistribution.srcDistribution)
                {
                    LootDistributionData.SrcData sourceData = entry.Value;
                    if (sourceData == null || sourceData.distribution == null || sourceData.distribution.Count == 0)
                    {
                        continue;
                    }

                    WorldEntityInfo info;
                    if (!WorldEntityDatabase.TryGetInfo(entry.Key, out info))
                    {
                        continue;
                    }

                    bool isCreatureSlot = info.slotType == EntitySlot.Type.Creature;
                    for (int i = 0; i < sourceData.distribution.Count; i++)
                    {
                        LootDistributionData.BiomeData biomeData = sourceData.distribution[i];
                        if (biomeData == null || biomeData.probability <= 0f)
                        {
                            continue;
                        }

                        float multiplier = profile.GetEntityProbabilityMultiplier(info.techType, biomeData.biome, isCreatureSlot, includeSurvivalSeedGroups);
                        if (Mathf.Approximately(multiplier, 1f))
                        {
                            continue;
                        }

                        biomeData.probability = Mathf.Max(0f, biomeData.probability * multiplier);
                        touchedEntries++;
                    }
                }

                if (touchedEntries > 0)
                {
                    lootDistribution.Initialize(lootDistribution.srcDistribution);
                }
            }
            catch (Exception ex)
            {
                ModLog.Warn("Failed to apply live loot distribution rules: " + ex.Message);
            }

            return touchedEntries;
        }

        private static void EnsureManualCreatureSpawns(ModSeedRuntimeProfile profile)
        {
            if (_manualCreatureSpawnComplete || _manualCreatureSpawnInProgress || profile == null || Player.main == null || LargeWorld.main == null)
            {
                return;
            }

            int amountPerPoint;
            List<Vector3> spawnPoints;
            if (!profile.TryGetManualCreatureSpawnData(TechType.Stalker, out amountPerPoint, out spawnPoints))
            {
                _manualCreatureSpawnComplete = true;
                return;
            }

            string playerBiome = LargeWorld.main.GetBiome(Player.main.transform.position);
            if (string.IsNullOrEmpty(playerBiome))
            {
                return;
            }

            _manualCreatureSpawnInProgress = true;
            CoroutineHost.StartCoroutine(SpawnManualCreaturesAsync(TechType.Stalker, amountPerPoint, spawnPoints));
        }

        private static IEnumerator SpawnManualCreaturesAsync(TechType techType, int amountPerPoint, List<Vector3> spawnPoints)
        {
            int spawned = 0;
            int toppedUpPoints = 0;
            bool failed = false;

            CoroutineTask<GameObject> prefabRequest = null;
            try
            {
                prefabRequest = CraftData.GetPrefabForTechTypeAsync(techType, true);
            }
            catch (Exception ex)
            {
                ModLog.Warn("Failed to request prefab for manual creature spawns '" + techType + "': " + ex.Message);
                _manualCreatureSpawnInProgress = false;
                yield break;
            }

            yield return prefabRequest;

            GameObject prefab = prefabRequest != null ? prefabRequest.GetResult() : null;
            if (prefab == null)
            {
                ModLog.Warn("Manual creature spawn prefab request returned null for '" + techType + "'.");
                _manualCreatureSpawnInProgress = false;
                yield break;
            }

            for (int i = 0; i < spawnPoints.Count; i++)
            {
                Vector3 point = spawnPoints[i];
                int nearbyCount = CountNearbyStalkers(point, 12f);
                int missing = Mathf.Max(0, amountPerPoint - nearbyCount);

                for (int j = 0; j < missing; j++)
                {
                    GameObject spawnedObject = Utils.SpawnFromPrefab(prefab, null);
                    if (spawnedObject == null)
                    {
                        failed = true;
                        break;
                    }

                    spawnedObject.transform.position = point;

                    try
                    {
                        LargeWorldEntity.Register(spawnedObject);
                    }
                    catch
                    {
                    }

                    spawnedObject.SetActive(true);

                    spawned++;
                    yield return null;
                }

                if (failed)
                {
                    break;
                }

                if (missing > 0)
                {
                    toppedUpPoints++;
                }

                yield return null;
            }

            _manualCreatureSpawnInProgress = false;
            if (failed)
            {
                ModLog.Warn("Manual creature spawn sweep failed part-way through for '" + techType + "'. A later world sweep will retry.");
                yield break;
            }

            _manualCreatureSpawnComplete = true;
            ModLog.Info(
                "Resolved manual creature spawns for '" +
                techType +
                "' with " +
                amountPerPoint +
                " per point across " +
                spawnPoints.Count +
                " points. Spawned " +
                spawned +
                " creatures and topped up " +
                toppedUpPoints +
                " points.");
        }

        private static int CountNearbyStalkers(Vector3 point, float radius)
        {
            Stalker[] stalkers = UnityEngine.Object.FindObjectsOfType<Stalker>();
            float radiusSquared = radius * radius;
            int count = 0;

            for (int i = 0; i < stalkers.Length; i++)
            {
                Stalker stalker = stalkers[i];
                if (stalker == null)
                {
                    continue;
                }

                if ((stalker.transform.position - point).sqrMagnitude <= radiusSquared)
                {
                    count++;
                }
            }

            return count;
        }

        private static bool CullBlockedCreatures(ModSeedRuntimeProfile profile)
        {
            bool usesManualStalkerSpawns = ModSeedRuntimeHost.IsSurvivalLikeMode() && profile.UsesManualCreatureSpawn(TechType.Stalker);
            bool removedCreature = false;
            Creature[] creatures = UnityEngine.Object.FindObjectsOfType<Creature>();
            for (int i = 0; i < creatures.Length; i++)
            {
                Creature creature = creatures[i];
                if (creature == null || IsProtectedCreature(creature))
                {
                    continue;
                }

                BiomeType biome;
                if (!ModSeedRuntimeHost.TryResolveBiomeType(creature.transform.position, out biome))
                {
                    continue;
                }

                if (profile.BlockCreaturesInPrisonAquarium && profile.IsPrisonAquariumBiome(biome))
                {
                    ModLog.Info("Removed creature '" + creature.name + "' from prison aquarium biome '" + biome + "'.");
                    UnityEngine.Object.Destroy(creature.gameObject);
                    removedCreature = true;
                    continue;
                }

                if (!usesManualStalkerSpawns && profile.RestrictStalkersToKelpForest && creature is Stalker && !profile.IsKelpForestBiome(biome))
                {
                    ModLog.Info("Removed stalker outside kelp forest from biome '" + biome + "'.");
                    UnityEngine.Object.Destroy(creature.gameObject);
                    removedCreature = true;
                }
            }

            return removedCreature;
        }

        private static bool IsProtectedCreature(Creature creature)
        {
            string typeName = creature.GetType().Name;
            return typeName.IndexOf("SeaEmperor", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
