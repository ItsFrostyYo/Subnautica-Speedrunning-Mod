using System;
using System.Collections.Generic;
using System.Reflection;
using UWE;
using UnityEngine;

namespace SubnauticaSpeedrunningMod.Runtime.Seeds.Ruleset8
{
    // Called from IL inserted directly into the build-247 Assembly-CSharp methods.
    public static class DirectHooks
    {
        private const string OriginalPrefix = "__SubnauticaSetSeedOriginal_";
        private static readonly Dictionary<string, MethodInfo> OriginalMethods = new Dictionary<string, MethodInfo>(StringComparer.Ordinal);
        private static readonly FieldInfo EcoTargetsField = typeof(EcoRegion).GetField("ecoTargets", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo LootDistributionField = typeof(CSVEntitySpawner).GetField("lootDistribution", BindingFlags.Instance | BindingFlags.NonPublic);

        public static bool IsDirectPatchInstalled()
        {
            return typeof(RandomStart).GetMethod(OriginalPrefix + "GetRandomStartPoint", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) != null &&
                   typeof(CSVEntitySpawner).GetMethod(OriginalPrefix + "GetPrefabForSlot", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) != null &&
                   typeof(TechFragment).GetMethod(OriginalPrefix + "GetRandomTech", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) != null;
        }

        public static Vector3 GetRandomStartPoint(RandomStart instance)
        {
            if (!SeedRuntime.Active)
            {
                return (Vector3)InvokeOriginal(instance, "GetRandomStartPoint", new object[0]);
            }

            ulong state = SeedRuntime.Derive("lifepod-start");
            Texture2D texture = instance.validStartPointTexture;
            for (int i = 0; i < 1000; i++)
            {
                float x = -2048f + DeterministicHash.NextUnitFloat(ref state) * 4096f;
                float z = -2048f + DeterministicHash.NextUnitFloat(ref state) * 4096f;
                float u = Mathf.Clamp01((x + 2048f) / 4096f);
                float v = Mathf.Clamp01((z + 2048f) / 4096f);
                int pixelX = Mathf.Min((int)(u * texture.width), texture.width - 1);
                int pixelY = Mathf.Min((int)(v * texture.height), texture.height - 1);
                if (texture.GetPixel(pixelX, pixelY).g > 0.5f)
                {
                    Vector3 result = new Vector3(x, 0f, z);
                    SeedRuntime.LogLifepodPosition(result);
                    return result;
                }
            }

            SeedRuntime.LogLifepodPosition(Vector3.zero);
            return Vector3.zero;
        }

        public static EntitySlot.Filler GetPrefabForSlot(CSVEntitySpawner instance, IEntitySlot slot, bool filterKnown)
        {
            if (!SeedRuntime.Active)
            {
                return (EntitySlot.Filler)InvokeOriginal(instance, "GetPrefabForSlot", new object[] { slot, filterKnown });
            }

            EntitySlot.Filler result = new EntitySlot.Filler { classId = null, count = 0 };
            LootDistributionData distribution = LootDistributionField == null ? null : LootDistributionField.GetValue(instance) as LootDistributionData;
            LootDistributionData.DstData biomeData;
            if (distribution == null || !distribution.GetBiomeLoot(slot.GetBiomeType(), out biomeData))
            {
                return result;
            }

            List<LootDistributionData.PrefabData> candidates = new List<LootDistributionData.PrefabData>();
            List<float> probabilities = new List<float>();
            float total = 0f;
            float skippedFragmentWeight = 0f, retainedFragmentWeight = 0f;
            List<bool> fragments = new List<bool>();
            for (int i = 0; i < biomeData.prefabs.Count; i++)
            {
                LootDistributionData.PrefabData candidate = biomeData.prefabs[i];
                LootDistributionData.SrcData unused;
                WorldEntityInfo info;
                if (string.Equals(candidate.classId, "None") ||
                    !distribution.srcDistribution.TryGetValue(candidate.classId, out unused) ||
                    !WorldEntityDatabase.TryGetInfo(candidate.classId, out info) ||
                    !slot.IsTypeAllowed(info.slotType))
                {
                    continue;
                }
                float probability = GenerationRules.SpawnWeight(candidate.classId, candidate.probability,
                    SeedRuntime.RulesetVersion) / slot.GetDensity();
                if (probability <= 0f)
                {
                    continue;
                }
                bool fragment = filterKnown && PDAScanner.IsFragment(info.techType);
                if (fragment && PDAScanner.ContainsCompleteEntry(info.techType))
                {
                    skippedFragmentWeight += probability;
                    continue;
                }
                candidates.Add(candidate);
                fragments.Add(fragment);
                if (fragment) retainedFragmentWeight += probability;
                probabilities.Add(probability);
                total += probability;
            }

            // Match native compensation: removed fragment weight goes only to remaining fragments.
            if (skippedFragmentWeight > 0f && retainedFragmentWeight > 0f)
            {
                float scale = (skippedFragmentWeight + retainedFragmentWeight) / retainedFragmentWeight;
                for (int i = 0; i < probabilities.Count; i++) if (fragments[i]) probabilities[i] *= scale;
            }
            ApplyMetalSalvageRules(slot, candidates, probabilities, ref total);
            // Preserve the legacy sum/rounding exactly when nothing has been removed.
            if (skippedFragmentWeight > 0f)
            {
                total = 0f;
                for (int i = 0; i < probabilities.Count; i++) total += probabilities[i];
            }

            if (total <= 0f)
            {
                return result;
            }
            ulong state = SeedRuntime.DeriveSlot(slot);
            float roll = DeterministicHash.NextUnitFloat(ref state);
            if (total > 1f)
            {
                roll *= total;
            }
            float cumulative = 0f;
            for (int i = 0; i < candidates.Count; i++)
            {
                cumulative += probabilities[i];
                if (cumulative >= roll)
                {
                    result.classId = candidates[i].classId;
                    result.count = candidates[i].count;
                    return result;
                }
            }
            return result;
        }

        public static void SpawnPlaceholder(EntitySlotsPlaceholder instance)
        {
            if (!SeedRuntime.Active || instance.slotsData == null)
            {
                InvokeOriginal(instance, "Spawn", new object[0]);
                return;
            }
            instance.virtualEntityPrefab.SetActive(false);
            for (int i = 0; i < instance.slotsData.Length; i++)
            {
                EntitySlotData slot = instance.slotsData[i];
                SlotContextData context = SlotContext.Push(instance, slot, i);
                try
                {
                    EntitySlot.Filler filler = LargeWorld.main.streamer.cellManager.GetPrefabForSlot(slot);
                    DeterministicEntitySpawn.Spawn(instance.virtualEntityPrefab, instance.transform,
                        slot.localPosition, slot.localRotation, filler, context.Key, false);
                }
                finally
                {
                    SlotContext.Clear();
                }
            }
        }

        public static void SpawnLiveSlot(EntitySlot instance)
        {
            if (!SeedRuntime.Active || LargeWorldStreamer.main == null || LargeWorldStreamer.main.cellManager == null)
            {
                InvokeOriginal(instance, "SpawnVirtualEntities", new object[0]);
                return;
            }
            GameObject virtualPrefab = VirtualEntitiesManager.GetVirtualEntityPrefab();
            virtualPrefab.SetActive(false);
            ulong key = SeedRuntime.DeriveSlot(instance);
            EntitySlot.Filler filler = LargeWorldStreamer.main.cellManager.GetPrefabForSlot(instance);
            DeterministicEntitySpawn.Spawn(virtualPrefab, instance.transform.parent,
                instance.transform.localPosition, instance.transform.localRotation, filler, key, true);
        }

        public static bool SubmitConsole(DevConsole instance, string value)
        {
            return (bool)InvokeOriginal(instance, "Submit", new object[] { value });
        }

        public static TechType GetFragmentTech(TechFragment instance)
        {
            if (!SeedRuntime.Active)
            {
                return (TechType)InvokeOriginal(instance, "GetRandomTech", new object[0]);
            }

            ulong state = SeedRuntime.DeriveComponent(instance, "tech-fragment");
            for (int i = 0; i < instance.techList.Count; i++)
            {
                TechFragment.RandomTech choice = instance.techList[i];
                if (DeterministicHash.NextUnitFloat(ref state) <= choice.chance)
                {
                    return choice.techType;
                }
            }
            return instance.defaultTechType;
        }

        public static void StartSpawnRandom(SpawnRandom instance)
        {
            if (!SeedRuntime.Active || instance.prefabs == null || instance.prefabs.Length == 0)
            {
                InvokeOriginal(instance, "Start", new object[0]);
                return;
            }

            int index = SeedRuntime.DeriveIndex(instance, "spawn-random", instance.prefabs.Length);
            GameObject spawned = UnityEngine.Object.Instantiate(instance.prefabs[index], instance.transform.localPosition, instance.transform.localRotation) as GameObject;
            spawned.transform.SetParent(instance.transform.parent, false);
            UnityEngine.Object.Destroy(instance.gameObject);
        }

        public static void StartSpawnRandomChild(SpawnRandomChild instance)
        {
            if (!SeedRuntime.Active || instance.gridTestSpawn || instance.prefabs == null || instance.prefabs.Length == 0)
            {
                InvokeOriginal(instance, "Start", new object[0]);
                return;
            }

            int index = SeedRuntime.DeriveIndex(instance, "spawn-random-child", instance.prefabs.Length);
            Utils.SpawnZeroedAt(instance.prefabs[index], instance.transform, instance.keepScale);
        }

        public static void StartRandomModel(RandomModel instance)
        {
            if (!SeedRuntime.Active || instance.modelVariants == null || instance.modelVariants.Length == 0)
            {
                InvokeOriginal(instance, "Start", new object[0]);
                return;
            }

            int index = SeedRuntime.DeriveIndex(instance, "random-model", instance.modelVariants.Length);
            Utils.SpawnZeroedAt(instance.modelVariants[index], instance.transform);
        }

        public static void SpawnStoredLoot(SpawnStoredLoot instance)
        {
            InvokeSeededOriginal(instance, "SpawnRandomStoredItems", new object[0], "stored-loot", false);
        }

        public static void RunDelayedSpawn(DelayedSpawn instance)
        {
            InvokeSeededOriginal(instance, "Spawn", new object[0], "delayed-spawn", true);
        }

        public static void StartCreature(Creature instance)
        {
            SeedRuntime.RegisterCreatureOrigin(instance);
            InvokeSeededOriginal(instance, "Start", new object[0], "creature-initialization", false);
        }

        public static float NextCreatureAIValue(Component instance)
        {
            return SeedRuntime.NextCreatureAIValue(instance);
        }

        public static Vector3 NextCreatureAIOnUnitSphere(Component instance)
        {
            Vector3 value = SeedRuntime.InsideUnitSphere(SeedRuntime.NextCreatureAISeed(instance));
            return value.sqrMagnitude > 0f ? value.normalized : Vector3.up;
        }

        public static Vector3 NextCreatureAIInsideUnitSphere(Component instance)
        {
            return SeedRuntime.InsideUnitSphere(SeedRuntime.NextCreatureAISeed(instance));
        }

        public static Vector2 NextCreatureAIInsideUnitCircle(Component instance)
        {
            return SeedRuntime.InsideUnitCircle(SeedRuntime.NextCreatureAISeed(instance));
        }

        public static float NextCreatureAIRangeFloat(float minimum, float maximum, Component instance)
        {
            return Mathf.Lerp(minimum, maximum, SeedRuntime.NextCreatureAIValue(instance));
        }

        public static int NextCreatureAIRangeInt(int minimum, int maximum, Component instance)
        {
            if (maximum <= minimum)
            {
                return minimum;
            }
            int value = minimum + (int)(SeedRuntime.NextCreatureAIValue(instance) * (maximum - minimum));
            return value < maximum ? value : maximum - 1;
        }

        public static void FindNearestEcoTarget(EcoRegion instance, EcoTargetType type, Vector3 worldPosition,
            EcoRegion.TargetFilter filter, ref float bestDistance, ref IEcoTarget best)
        {
            instance.timeStamp = Time.time;
            if (EcoTargetsField == null)
            {
                throw new MissingFieldException(typeof(EcoRegion).FullName, "ecoTargets");
            }

            Dictionary<int, HashSet<IEcoTarget>> targets =
                (Dictionary<int, HashSet<IEcoTarget>>)EcoTargetsField.GetValue(instance);
            HashSet<IEcoTarget> candidates;
            if (!targets.TryGetValue((int)type, out candidates))
            {
                return;
            }

            float closestSquared = float.MaxValue;
            ulong closestTie = 0UL;
            bool hasClosestTie = false;
            foreach (IEcoTarget candidate in candidates)
            {
                if (candidate == null || candidate.Equals(null) || (filter != null && !filter(candidate)))
                {
                    continue;
                }

                float squared = (worldPosition - candidate.GetPosition()).sqrMagnitude;
                if (squared < closestSquared)
                {
                    best = candidate;
                    closestSquared = squared;
                    hasClosestTie = false;
                }
                else if (squared == closestSquared)
                {
                    if (!hasClosestTie)
                    {
                        closestTie = SeedRuntime.DeriveEcoTargetTie(best);
                        hasClosestTie = true;
                    }
                    ulong tie = SeedRuntime.DeriveEcoTargetTie(candidate);
                    if (tie < closestTie)
                    {
                        best = candidate;
                        closestTie = tie;
                    }
                }
            }
            if (best != null)
            {
                bestDistance = Mathf.Sqrt(closestSquared);
            }
        }

        public static void InitializeReefbackLife(ReefbackLife instance)
        {
            InvokeSeededOriginal(instance, "Initialize", new object[0], "reefback-ecosystem", false);
        }

        public static void BreakResource(BreakableResource instance)
        {
            InvokeSeededOriginal(instance, "BreakIntoResources", new object[0], "breakable-resource", true);
        }

        public static void DrillResource(Drillable instance, Vector3 position, Exosuit exosuit, out GameObject hitObject)
        {
            object[] arguments = { position, exosuit, null };
            InvokeSeededOriginal(instance, "OnDrill", arguments, "drillable-resource", true);
            hitObject = arguments[2] as GameObject;
        }

        public static void CheckStalkerTooth(Stalker instance, GameObject target)
        {
            if (!SeedRuntime.Active)
            {
                InvokeOriginal(instance, "CheckLoseTooth", new object[] { target });
                return;
            }

            // Build 247 normally checks hardness and then an additional 50% roll. Metal
            // salvage has hardness 0.5, producing an overall 25% chance. Ruleset 8 uses
            // one deterministic hardness roll, producing the requested 50% salvage rate.
            float hardness = HardnessMixin.GetHardness(target);
            ulong state = SeedRuntime.DeriveComponentEvent(instance, "stalker-tooth-r7");
            if (DeterministicHash.NextUnitFloat(ref state) < hardness)
            {
                MethodInfo loseTooth = typeof(Stalker).GetMethod("LoseTooth", BindingFlags.Instance | BindingFlags.NonPublic);
                if (loseTooth == null)
                {
                    throw new MissingMethodException(typeof(Stalker).FullName, "LoseTooth");
                }
                loseTooth.Invoke(instance, new object[0]);
            }
        }

        public static void SetAuroraExplosionTime(CrashedShipExploder instance)
        {
            InvokeSeededOriginal(instance, "SetExplodeTime", new object[0], "aurora-explosion-time", false);
        }

        public static void StartScaleVariance(ScaleVariance instance)
        {
            InvokeSeededOriginal(instance, "Start", new object[0], "scale-variance", false);
        }

        public static void BeforeInitializeNewGame()
        {
            SeedRuntime.BindCurrentSave();
        }

        public static void AfterSetSavegameDir()
        {
            if (!Utils.GetContinueMode())
            {
                SeedRuntime.ActivatePendingForNewGame();
            }
        }

        public static Vector3 ForceLifepodPosition(Vector3 requested)
        {
            if (!SeedRuntime.Active)
            {
                return requested;
            }

            // StartAtPosition is patched in-place, so this is an independent last line of
            // defense if an internal caller ever bypasses the RandomStart wrapper.
            if (!SeedRuntime.HasLifepodPosition && RandomStart.main != null)
            {
                return GetRandomStartPoint(RandomStart.main);
            }

            return SeedRuntime.HasLifepodPosition ? SeedRuntime.LifepodPosition : requested;
        }

        public static string AppendWreckToBiome(string biome)
        {
            return biome ?? string.Empty;
        }

        public static void DrawSlotInspectorF1()
        {
        }

        private static object InvokeOriginal(object instance, string methodName, object[] arguments)
        {
            Type instanceType = instance.GetType();
            string originalName = OriginalPrefix + methodName;
            string cacheKey = instanceType.AssemblyQualifiedName + "|" + originalName;
            MethodInfo method;
            if (!OriginalMethods.TryGetValue(cacheKey, out method))
            {
                method = instanceType.GetMethod(originalName,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                OriginalMethods[cacheKey] = method;
            }
            if (method == null)
            {
                throw new MissingMethodException(instanceType.FullName, originalName);
            }
            return method.Invoke(instance, arguments);
        }

        private static void ApplyMetalSalvageRules(IEntitySlot slot, List<LootDistributionData.PrefabData> candidates,
            List<float> probabilities, ref float total)
        {
            const string badMetal = "b2d10d9b-878e-4ff8-b71f-cd578e0d2038";
            string[] goodMetals =
            {
                "947f2823-c42a-45ef-94e4-52a9f1d3459c",
                "7e507655-9fbf-42e0-8422-163ddd668747",
                "7cd86cbf-0708-41dc-84d7-58c648e25b06"
            };
            int badIndex = -1;
            List<int> goodIndexes = new List<int>();
            for (int i = 0; i < candidates.Count; i++)
            {
                if (string.Equals(candidates[i].classId, badMetal, StringComparison.Ordinal)) badIndex = i;
                for (int j = 0; j < goodMetals.Length; j++)
                {
                    if (string.Equals(candidates[i].classId, goodMetals[j], StringComparison.Ordinal))
                    {
                        goodIndexes.Add(i);
                        break;
                    }
                }
            }
            if (badIndex < 0 || goodIndexes.Count == 0) return;

            string biomeName = slot.GetBiomeType().ToString();
            bool kelpForest = biomeName.StartsWith("Kelp_", StringComparison.OrdinalIgnoreCase) ||
                biomeName.StartsWith("KelpForest", StringComparison.OrdinalIgnoreCase);
            float factor = kelpForest ? 0f : 0.1f;
            float original = probabilities[badIndex];
            float transfer = original * (1f - factor) / goodIndexes.Count;
            probabilities[badIndex] = original * factor;
            for (int i = 0; i < goodIndexes.Count; i++) probabilities[goodIndexes[i]] += transfer;
            if (factor == 0f)
            {
                candidates.RemoveAt(badIndex);
                probabilities.RemoveAt(badIndex);
            }
            // Total probability remains unchanged: only the salvage variant weights move.
        }

        private static object InvokeSeededOriginal(Component instance, string methodName, object[] arguments, string domain, bool eventScope)
        {
            if (!SeedRuntime.Active)
            {
                return InvokeOriginal(instance, methodName, arguments);
            }

            ulong key = eventScope
                ? SeedRuntime.DeriveComponentEvent(instance, domain)
                : SeedRuntime.DeriveComponent(instance, domain);
            UnityRandomScopeState scope = UnityRandomScope.Begin(key);
            try
            {
                return InvokeOriginal(instance, methodName, arguments);
            }
            finally
            {
                UnityRandomScope.End(scope);
            }
        }
    }
}

