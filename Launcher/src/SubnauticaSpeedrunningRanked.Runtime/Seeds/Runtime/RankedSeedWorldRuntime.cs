using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UWE;
using UnityEngine;

namespace SubnauticaSpeedrunningRanked.Runtime.Seeds
{
    internal static class RankedSeedWorldRuntime
    {
        private static readonly HashSet<LootDistributionData> PatchedLootDistributions = new HashSet<LootDistributionData>();
        private static FieldInfo _csvEntitySpawnerLootDistributionField;
        private static float _nextFishSweepAt;
        private static float _nextCreatureSweepAt;
        private static float _nextStalkerObserverSweepAt;
        private static float _nextManualCreatureSweepAt;
        private static float _nextLootSweepAt;
        private static int _schoolObjectsSuppressed;
        private static bool _manualCreatureSpawnInProgress;
        private static bool _manualCreatureSpawnComplete;

        public static void Update(bool worldActive, bool inGame)
        {
            if (!worldActive)
            {
                return;
            }

            RankedSeedRuntimeProfile profile = RankedSeedRuntimeHost.GetProfile();
            if (profile == null || !RankedSeedRuntimeHost.IsSupportedGameplayMode())
            {
                return;
            }

            if (inGame)
            {
                RankedSeedRuntimeHost.EnsureAlwaysActiveHooksInstalled();
            }

            bool includeSurvivalSeedGroups = RankedSeedRuntimeHost.IsSurvivalLikeMode();

            if (Time.unscaledTime >= _nextLootSweepAt)
            {
                _nextLootSweepAt = Time.unscaledTime + 1f;
                ApplyAlwaysOnLootDistributionRules(profile, includeSurvivalSeedGroups);
            }

            if (inGame && profile.DisableFishSchools && Time.unscaledTime >= _nextFishSweepAt)
            {
                _nextFishSweepAt = Time.unscaledTime + 1f;
                SuppressFishSchools();
            }

            if (inGame &&
                !RankedSeedRuntimeHost.IsStalkerToothHookInstalled() &&
                (profile.StalkerToothDropProbability > 0f || profile.RestrictStalkersToKelpForest) &&
                Time.unscaledTime >= _nextStalkerObserverSweepAt)
            {
                _nextStalkerObserverSweepAt = Time.unscaledTime + 2f;
                EnsureStalkerObservers();
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
                _nextCreatureSweepAt = Time.unscaledTime + 2f;
                CullBlockedCreatures(profile);
            }
        }

        public static void Reset()
        {
            PatchedLootDistributions.Clear();
            _nextFishSweepAt = 0f;
            _nextCreatureSweepAt = 0f;
            _nextStalkerObserverSweepAt = 0f;
            _nextManualCreatureSweepAt = 0f;
            _nextLootSweepAt = 0f;
            _schoolObjectsSuppressed = 0;
            _manualCreatureSpawnInProgress = false;
            _manualCreatureSpawnComplete = false;
        }

        private static void ApplyAlwaysOnLootDistributionRules(RankedSeedRuntimeProfile profile, bool includeSurvivalSeedGroups)
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
                RankedLog.Info(
                    "Applied always-on loot distribution rules to " +
                    patchedThisSweep +
                    " live spawner tables touching " +
                    touchedBiomeEntries +
                    " biome probability entries.");
            }
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

        private static int ApplyProfileToLootDistribution(LootDistributionData lootDistribution, RankedSeedRuntimeProfile profile, bool includeSurvivalSeedGroups)
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
                RankedLog.Warn("Failed to apply live loot distribution rules: " + ex.Message);
            }

            return touchedEntries;
        }

        private static void SuppressFishSchools()
        {
            VFXSchoolFishManager[] managers = UnityEngine.Object.FindObjectsOfType<VFXSchoolFishManager>();
            for (int i = 0; i < managers.Length; i++)
            {
                VFXSchoolFishManager manager = managers[i];
                if (manager == null)
                {
                    continue;
                }

                manager.enableAI = false;
                manager.enableRepulsor = false;
                manager.enablePlayerRepulse = false;
                manager.enabled = false;
            }

            VFXSchoolFish[] schools = UnityEngine.Object.FindObjectsOfType<VFXSchoolFish>();
            for (int i = 0; i < schools.Length; i++)
            {
                VFXSchoolFish school = schools[i];
                if (school == null)
                {
                    continue;
                }

                if (school.meshRenderer != null)
                {
                    school.meshRenderer.enabled = false;
                }

                school.enabled = false;
                if (school.gameObject.activeSelf)
                {
                    school.gameObject.SetActive(false);
                    _schoolObjectsSuppressed++;
                }
            }

            School[] simpleSchools = UnityEngine.Object.FindObjectsOfType<School>();
            for (int i = 0; i < simpleSchools.Length; i++)
            {
                School school = simpleSchools[i];
                if (school == null)
                {
                    continue;
                }

                Renderer renderer = school.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.enabled = false;
                }

                school.enabled = false;
                if (school.gameObject.activeSelf)
                {
                    school.gameObject.SetActive(false);
                    _schoolObjectsSuppressed++;
                }
            }
        }

        private static void EnsureStalkerObservers()
        {
            Stalker[] stalkers = UnityEngine.Object.FindObjectsOfType<Stalker>();
            for (int i = 0; i < stalkers.Length; i++)
            {
                Stalker stalker = stalkers[i];
                if (stalker == null)
                {
                    continue;
                }

                RankedStalkerRuntimeObserver observer = stalker.GetComponent<RankedStalkerRuntimeObserver>();
                if (observer == null)
                {
                    stalker.gameObject.AddComponent<RankedStalkerRuntimeObserver>();
                }
            }
        }

        private static void EnsureManualCreatureSpawns(RankedSeedRuntimeProfile profile)
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
                RankedLog.Warn("Failed to request prefab for manual creature spawns '" + techType + "': " + ex.Message);
                _manualCreatureSpawnInProgress = false;
                yield break;
            }

            yield return prefabRequest;

            GameObject prefab = prefabRequest != null ? prefabRequest.GetResult() : null;
            if (prefab == null)
            {
                RankedLog.Warn("Manual creature spawn prefab request returned null for '" + techType + "'.");
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
                RankedLog.Warn("Manual creature spawn sweep failed part-way through for '" + techType + "'. A later world sweep will retry.");
                yield break;
            }

            _manualCreatureSpawnComplete = true;
            RankedLog.Info(
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

        private static void CullBlockedCreatures(RankedSeedRuntimeProfile profile)
        {
            bool usesManualStalkerSpawns = RankedSeedRuntimeHost.IsSurvivalLikeMode() && profile.UsesManualCreatureSpawn(TechType.Stalker);
            Creature[] creatures = UnityEngine.Object.FindObjectsOfType<Creature>();
            for (int i = 0; i < creatures.Length; i++)
            {
                Creature creature = creatures[i];
                if (creature == null || IsProtectedCreature(creature))
                {
                    continue;
                }

                BiomeType biome;
                if (!RankedSeedRuntimeHost.TryResolveBiomeType(creature.transform.position, out biome))
                {
                    continue;
                }

                if (profile.BlockCreaturesInPrisonAquarium && profile.IsPrisonAquariumBiome(biome))
                {
                    RankedLog.Info("Removed creature '" + creature.name + "' from prison aquarium biome '" + biome + "'.");
                    UnityEngine.Object.Destroy(creature.gameObject);
                    continue;
                }

                if (!usesManualStalkerSpawns && profile.RestrictStalkersToKelpForest && creature is Stalker && !profile.IsKelpForestBiome(biome))
                {
                    RankedLog.Info("Removed stalker outside kelp forest from biome '" + biome + "'.");
                    UnityEngine.Object.Destroy(creature.gameObject);
                }
            }
        }

        private static bool IsProtectedCreature(Creature creature)
        {
            string typeName = creature.GetType().Name;
            return typeName.IndexOf("SeaEmperor", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private sealed class RankedStalkerRuntimeObserver : MonoBehaviour
        {
            private static MethodInfo _loseToothMethod;
            private static FieldInfo _mouthField;

            private Stalker _stalker;
            private Coroutine _pendingDropRoutine;

            private void Awake()
            {
                _stalker = GetComponent<Stalker>();
            }

            public void OnShinyPickedUp(GameObject target)
            {
                QueueGuaranteedDrop(target, false);
            }

            public void OnMeleeAttack(GameObject target)
            {
                QueueGuaranteedDrop(target, true);
            }

            private void QueueGuaranteedDrop(GameObject target, bool fromMeleeAttack)
            {
                RankedSeedRuntimeProfile profile = RankedSeedRuntimeHost.GetProfile();
                if (profile == null || _stalker == null || !RankedSeedRuntimeHost.IsSupportedGameplayMode())
                {
                    return;
                }

                bool salvageTarget = target != null && CraftData.GetTechType(target) == TechType.ScrapMetal;
                if (!salvageTarget && (!fromMeleeAttack || !profile.StalkerBitesDropTeeth))
                {
                    return;
                }

                if (salvageTarget && !profile.FixBadMetal && ShouldSkipLegacyBadMetal(target))
                {
                    return;
                }

                if (_pendingDropRoutine != null)
                {
                    StopCoroutine(_pendingDropRoutine);
                }

                _pendingDropRoutine = StartCoroutine(GuaranteeDropNextFrame(profile));
            }

            private IEnumerator GuaranteeDropNextFrame(RankedSeedRuntimeProfile profile)
            {
                yield return null;

                _pendingDropRoutine = null;
                if (_stalker == null || profile == null)
                {
                    yield break;
                }

                float chancePercent = Mathf.Max(0f, profile.StalkerToothDropProbability);
                if (chancePercent <= 0f)
                {
                    yield break;
                }

                Vector3 mouthPosition = GetMouthPosition();
                if (HasNearbyStalkerTooth(mouthPosition, 3f))
                {
                    yield break;
                }

                MethodInfo loseToothMethod = GetLoseToothMethod(_stalker.GetType());
                if (loseToothMethod == null)
                {
                    yield break;
                }

                int guaranteedDrops = (int)(chancePercent / 100f);
                float bonusChance = chancePercent - guaranteedDrops * 100f;

                for (int i = 0; i < guaranteedDrops; i++)
                {
                    loseToothMethod.Invoke(_stalker, null);
                }

                if (bonusChance > 0f && (bonusChance >= 100f || UnityEngine.Random.value <= bonusChance / 100f))
                {
                    loseToothMethod.Invoke(_stalker, null);
                }
            }

            private Vector3 GetMouthPosition()
            {
                FieldInfo mouthField = GetMouthField(_stalker.GetType());
                GameObject mouth = mouthField != null ? mouthField.GetValue(_stalker) as GameObject : null;
                return mouth != null ? mouth.transform.position : _stalker.transform.position;
            }

            private static bool HasNearbyStalkerTooth(Vector3 center, float radius)
            {
                Pickupable[] pickupables = UnityEngine.Object.FindObjectsOfType<Pickupable>();
                float radiusSquared = radius * radius;
                for (int i = 0; i < pickupables.Length; i++)
                {
                    Pickupable pickupable = pickupables[i];
                    if (pickupable == null)
                    {
                        continue;
                    }

                    if (CraftData.GetTechType(pickupable.gameObject) != TechType.StalkerTooth)
                    {
                        continue;
                    }

                    if ((pickupable.transform.position - center).sqrMagnitude <= radiusSquared)
                    {
                        return true;
                    }
                }

                return false;
            }

            private static MethodInfo GetLoseToothMethod(Type stalkerType)
            {
                if (_loseToothMethod == null)
                {
                    _loseToothMethod = stalkerType.GetMethod("LoseTooth", BindingFlags.Instance | BindingFlags.NonPublic);
                }

                return _loseToothMethod;
            }

            private static FieldInfo GetMouthField(Type stalkerType)
            {
                if (_mouthField == null)
                {
                    _mouthField = stalkerType.GetField("mouth", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                }

                return _mouthField;
            }

            private static bool ShouldSkipLegacyBadMetal(GameObject target)
            {
                if (target == null)
                {
                    return false;
                }

                float hardness = HardnessMixin.GetHardness(target);
                if (hardness <= 0f)
                {
                    return true;
                }

                string token = NormalizeToken(target.name);
                return token.Contains("lshape") ||
                       token.Contains("lshaped") ||
                       token.Contains("shape_l") ||
                       token.EndsWith("_l", StringComparison.Ordinal) ||
                       token.Contains("scrapmetal_l");
            }

            private static string NormalizeToken(string value)
            {
                if (string.IsNullOrEmpty(value))
                {
                    return string.Empty;
                }

                System.Text.StringBuilder builder = new System.Text.StringBuilder(value.Length);
                for (int i = 0; i < value.Length; i++)
                {
                    char character = value[i];
                    if (char.IsLetterOrDigit(character))
                    {
                        builder.Append(char.ToLowerInvariant(character));
                    }
                    else if (character == '_' || character == '-')
                    {
                        builder.Append(character);
                    }
                }

                return builder.ToString();
            }
        }
    }
}
