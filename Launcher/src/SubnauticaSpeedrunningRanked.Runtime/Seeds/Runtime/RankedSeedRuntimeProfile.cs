using System;
using System.Collections.Generic;
using UnityEngine;

namespace SubnauticaSpeedrunningRanked.Runtime.Seeds
{
    internal sealed class RankedSeedRuntimeProfile
    {
        private sealed class RankedResolvedManualCreatureSpawn
        {
            public int Amount;
            public List<Vector3> SpawnPoints;
        }

        private readonly Dictionary<TechType, float> _fragmentMultipliers;
        private readonly Dictionary<TechType, float> _resourceMultipliers;
        private readonly Dictionary<TechType, float> _creatureMultipliers;
        private readonly Dictionary<TechType, float> _alwaysMultipliers;
        private readonly Dictionary<BiomeType, float> _seedBiomeMultipliers;
        private readonly Dictionary<BiomeType, float> _alwaysBiomeMultipliers;
        private readonly Dictionary<BiomeType, Dictionary<TechType, float>> _alwaysBiomeTechMultipliers;
        private readonly Dictionary<TechType, RankedResolvedManualCreatureSpawn> _manualCreatureSpawns;
        private readonly HashSet<BiomeType> _kelpForestBiomes;
        private readonly HashSet<BiomeType> _prisonAquariumBiomes;

        private RankedSeedRuntimeProfile(
            RankedSeedDefinition definition,
            Dictionary<TechType, float> fragmentMultipliers,
            Dictionary<TechType, float> resourceMultipliers,
            Dictionary<TechType, float> creatureMultipliers,
            Dictionary<TechType, float> alwaysMultipliers,
            Dictionary<BiomeType, float> seedBiomeMultipliers,
            Dictionary<BiomeType, float> alwaysBiomeMultipliers,
            Dictionary<BiomeType, Dictionary<TechType, float>> alwaysBiomeTechMultipliers,
            Dictionary<TechType, RankedResolvedManualCreatureSpawn> manualCreatureSpawns,
            HashSet<BiomeType> kelpForestBiomes,
            HashSet<BiomeType> prisonAquariumBiomes)
        {
            Definition = definition;
            _fragmentMultipliers = fragmentMultipliers;
            _resourceMultipliers = resourceMultipliers;
            _creatureMultipliers = creatureMultipliers;
            _alwaysMultipliers = alwaysMultipliers;
            _seedBiomeMultipliers = seedBiomeMultipliers;
            _alwaysBiomeMultipliers = alwaysBiomeMultipliers;
            _alwaysBiomeTechMultipliers = alwaysBiomeTechMultipliers;
            _manualCreatureSpawns = manualCreatureSpawns;
            _kelpForestBiomes = kelpForestBiomes;
            _prisonAquariumBiomes = prisonAquariumBiomes;
        }

        public RankedSeedDefinition Definition { get; private set; }

        public bool DisableFishSchools
        {
            get { return Definition != null && Definition.Survival != null && Definition.Survival.Defaults != null && Definition.Survival.Defaults.DisableFishSchools; }
        }

        public bool BlockCreaturesInPrisonAquarium
        {
            get { return Definition != null && Definition.Survival != null && Definition.Survival.Defaults != null && Definition.Survival.Defaults.BlockCreaturesInPrisonAquarium; }
        }

        public bool RestrictStalkersToKelpForest
        {
            get { return Definition != null && Definition.Survival != null && Definition.Survival.Defaults != null && Definition.Survival.Defaults.RestrictStalkersToKelpForest; }
        }

        public float StalkerToothDropProbability
        {
            get
            {
                if (Definition == null || Definition.Survival == null || Definition.Survival.Defaults == null)
                {
                    return 100f;
                }

                return Mathf.Clamp(Definition.Survival.Defaults.StalkerToothDropProbability, 0f, 100f);
            }
        }

        public bool StalkerBitesDropTeeth
        {
            get { return Definition != null && Definition.Survival != null && Definition.Survival.Defaults != null && Definition.Survival.Defaults.StalkerBitesDropTeeth; }
        }

        public bool FixBadMetal
        {
            get { return Definition != null && Definition.Survival != null && Definition.Survival.Defaults != null && Definition.Survival.Defaults.FixBadMetal; }
        }

        public bool ForceSecondGold
        {
            get { return Definition != null && Definition.Survival != null && Definition.Survival.Defaults != null && Definition.Survival.Defaults.ForceSecondGold; }
        }

        public float GetEntityProbabilityMultiplier(TechType techType, BiomeType biome, bool isCreatureSlot, bool includeSurvivalSeedGroups)
        {
            float multiplier = 1f;
            bool usesManualStalkerSpawns = includeSurvivalSeedGroups && UsesManualCreatureSpawn(TechType.Stalker);

            if (isCreatureSlot && BlockCreaturesInPrisonAquarium && IsPrisonAquariumBiome(biome))
            {
                return 0f;
            }

            if (usesManualStalkerSpawns && techType == TechType.Stalker)
            {
                return 0f;
            }

            if (!usesManualStalkerSpawns && techType == TechType.Stalker && RestrictStalkersToKelpForest && !IsKelpForestBiome(biome))
            {
                return 0f;
            }

            multiplier *= GetDictionaryMultiplier(_alwaysMultipliers, techType);
            multiplier *= GetDictionaryMultiplier(_alwaysBiomeMultipliers, biome);
            multiplier *= GetBiomeTechMultiplier(biome, techType);

            if (!includeSurvivalSeedGroups)
            {
                return Mathf.Max(0f, multiplier);
            }

            multiplier *= GetDictionaryMultiplier(_seedBiomeMultipliers, biome);

            if (PDAScanner.IsFragment(techType))
            {
                multiplier *= GetDictionaryMultiplier(_fragmentMultipliers, techType);
            }
            else if (isCreatureSlot || techType == TechType.Stalker)
            {
                multiplier *= GetDictionaryMultiplier(_creatureMultipliers, techType);
            }
            else
            {
                multiplier *= GetDictionaryMultiplier(_resourceMultipliers, techType);
            }

            return Mathf.Max(0f, multiplier);
        }

        public bool UsesManualCreatureSpawn(TechType techType)
        {
            return _manualCreatureSpawns != null && _manualCreatureSpawns.ContainsKey(techType);
        }

        public bool TryGetManualCreatureSpawnData(TechType techType, out int amount, out List<Vector3> spawnPoints)
        {
            amount = 0;
            spawnPoints = null;

            RankedResolvedManualCreatureSpawn value;
            if (_manualCreatureSpawns == null || !_manualCreatureSpawns.TryGetValue(techType, out value) || value == null)
            {
                return false;
            }

            amount = value.Amount;
            spawnPoints = value.SpawnPoints;
            return amount > 0 && spawnPoints != null && spawnPoints.Count > 0;
        }

        public bool IsKelpForestBiome(BiomeType biome)
        {
            return _kelpForestBiomes.Contains(biome);
        }

        public bool IsPrisonAquariumBiome(BiomeType biome)
        {
            return _prisonAquariumBiomes.Contains(biome);
        }

        private float GetBiomeTechMultiplier(BiomeType biome, TechType techType)
        {
            Dictionary<TechType, float> biomeValues;
            if (_alwaysBiomeTechMultipliers != null &&
                _alwaysBiomeTechMultipliers.TryGetValue(biome, out biomeValues) &&
                biomeValues != null)
            {
                float multiplier;
                if (biomeValues.TryGetValue(techType, out multiplier))
                {
                    return multiplier;
                }
            }

            return 1f;
        }

        private static float GetDictionaryMultiplier(Dictionary<TechType, float> values, TechType key)
        {
            float multiplier;
            if (values != null && values.TryGetValue(key, out multiplier))
            {
                return multiplier;
            }

            return 1f;
        }

        private static float GetDictionaryMultiplier(Dictionary<BiomeType, float> values, BiomeType key)
        {
            float multiplier;
            if (values != null && values.TryGetValue(key, out multiplier))
            {
                return multiplier;
            }

            return 1f;
        }

        public static RankedSeedRuntimeProfile Create(RankedSeedDefinition definition)
        {
            if (definition == null)
            {
                definition = RankedSeedDefinition.CreateDefaultCreativeRangeSeed();
            }

            definition.Normalize();
            RankedSeedRollContext rollContext = new RankedSeedRollContext(definition);

            return new RankedSeedRuntimeProfile(
                definition,
                BuildTechTypeMap(definition.Survival.Fragments, "Fragments", rollContext),
                BuildTechTypeMap(definition.Survival.Resources, "Resources", rollContext),
                BuildTechTypeMap(definition.Survival.Creatures, "Creatures", rollContext),
                BuildTechTypeMap(definition.Survival.Always, "Always", rollContext),
                BuildBiomeMap(definition.Survival.Biomes, "Biomes", rollContext),
                BuildBiomeMap(definition.Survival.AlwaysBiomeMultipliers, "AlwaysBiomes", null),
                BuildBiomeTechMap(definition.Survival.AlwaysBiomeTechMultipliers),
                BuildManualCreatureSpawnMap(definition.Survival.ManualCreatureSpawns, rollContext),
                BuildBiomeSet(RankedSeedReferenceCatalog.KelpForestBiomes),
                BuildBiomeSet(RankedSeedReferenceCatalog.PrisonAquariumBiomes));
        }

        private static Dictionary<TechType, float> BuildTechTypeMap(List<RankedSpawnMultiplierEntry> entries, string groupName, RankedSeedRollContext rollContext)
        {
            Dictionary<TechType, float> values = new Dictionary<TechType, float>();
            if (entries == null)
            {
                return values;
            }

            for (int i = 0; i < entries.Count; i++)
            {
                RankedSpawnMultiplierEntry entry = entries[i];
                if (entry == null || string.IsNullOrEmpty(entry.Name))
                {
                    continue;
                }

                try
                {
                    TechType techType = (TechType)Enum.Parse(typeof(TechType), entry.Name, true);
                    values[techType] = ResolveChanceMultiplier(groupName, entry.Name, entry, rollContext);
                }
                catch (Exception ex)
                {
                    RankedLog.Warn("Seed group '" + groupName + "' contains unknown TechType '" + entry.Name + "': " + ex.Message);
                }
            }

            return values;
        }

        private static Dictionary<BiomeType, float> BuildBiomeMap(List<RankedBiomeMultiplierEntry> entries, string groupName, RankedSeedRollContext rollContext)
        {
            Dictionary<BiomeType, float> values = new Dictionary<BiomeType, float>();
            if (entries == null)
            {
                return values;
            }

            for (int i = 0; i < entries.Count; i++)
            {
                RankedBiomeMultiplierEntry entry = entries[i];
                if (entry == null || string.IsNullOrEmpty(entry.Name))
                {
                    continue;
                }

                try
                {
                    BiomeType biome = (BiomeType)Enum.Parse(typeof(BiomeType), entry.Name, true);
                    values[biome] = ResolveChanceMultiplier(groupName, entry.Name, entry, rollContext);
                }
                catch (Exception ex)
                {
                    RankedLog.Warn("Seed biome list contains unknown BiomeType '" + entry.Name + "': " + ex.Message);
                }
            }

            return values;
        }

        private static Dictionary<BiomeType, Dictionary<TechType, float>> BuildBiomeTechMap(List<RankedBiomeTechMultiplierEntry> entries)
        {
            Dictionary<BiomeType, Dictionary<TechType, float>> values = new Dictionary<BiomeType, Dictionary<TechType, float>>();
            if (entries == null)
            {
                return values;
            }

            for (int i = 0; i < entries.Count; i++)
            {
                RankedBiomeTechMultiplierEntry entry = entries[i];
                if (entry == null || string.IsNullOrEmpty(entry.BiomeName) || string.IsNullOrEmpty(entry.TechTypeName))
                {
                    continue;
                }

                try
                {
                    BiomeType biome = (BiomeType)Enum.Parse(typeof(BiomeType), entry.BiomeName, true);
                    TechType techType = (TechType)Enum.Parse(typeof(TechType), entry.TechTypeName, true);

                    Dictionary<TechType, float> biomeValues;
                    if (!values.TryGetValue(biome, out biomeValues) || biomeValues == null)
                    {
                        biomeValues = new Dictionary<TechType, float>();
                        values[biome] = biomeValues;
                    }

                    float existing;
                    if (biomeValues.TryGetValue(techType, out existing))
                    {
                        biomeValues[techType] = Mathf.Max(0f, existing * Mathf.Max(0f, entry.ChanceMultiplier));
                    }
                    else
                    {
                        biomeValues[techType] = Mathf.Max(0f, entry.ChanceMultiplier);
                    }
                }
                catch (Exception ex)
                {
                    RankedLog.Warn("Always biome-tech rule contains unknown value '" + entry.TechTypeName + "' / '" + entry.BiomeName + "': " + ex.Message);
                }
            }

            return values;
        }

        private static Dictionary<TechType, RankedResolvedManualCreatureSpawn> BuildManualCreatureSpawnMap(List<RankedManualCreatureSpawnEntry> entries, RankedSeedRollContext rollContext)
        {
            Dictionary<TechType, RankedResolvedManualCreatureSpawn> values = new Dictionary<TechType, RankedResolvedManualCreatureSpawn>();
            if (entries == null)
            {
                return values;
            }

            for (int i = 0; i < entries.Count; i++)
            {
                RankedManualCreatureSpawnEntry entry = entries[i];
                if (entry == null || string.IsNullOrEmpty(entry.TechTypeName))
                {
                    continue;
                }

                try
                {
                    TechType techType = (TechType)Enum.Parse(typeof(TechType), entry.TechTypeName, true);
                    int amount = ResolveSpawnAmount(entry, rollContext);
                    List<Vector3> spawnPoints = new List<Vector3>();
                    if (entry.SpawnPoints != null)
                    {
                        for (int j = 0; j < entry.SpawnPoints.Count; j++)
                        {
                            RankedSpawnPointDefinition point = entry.SpawnPoints[j];
                            if (point == null)
                            {
                                continue;
                            }

                            spawnPoints.Add(new Vector3(point.X, point.Y, point.Z));
                        }
                    }

                    values[techType] = new RankedResolvedManualCreatureSpawn
                    {
                        Amount = amount,
                        SpawnPoints = spawnPoints
                    };
                }
                catch (Exception ex)
                {
                    RankedLog.Warn("Manual creature spawn list contains unknown TechType '" + entry.TechTypeName + "': " + ex.Message);
                }
            }

            return values;
        }

        private static int ResolveSpawnAmount(RankedManualCreatureSpawnEntry entry, RankedSeedRollContext rollContext)
        {
            if (entry == null)
            {
                return 0;
            }

            if (entry.UseSeedRange && rollContext != null)
            {
                return Math.Max(
                    0,
                    rollContext.NextSteppedInt(
                        "survival|ManualCreatureSpawns|" + entry.TechTypeName + "|Amount",
                        entry.MinAmount,
                        entry.MaxAmount,
                        entry.AmountStep));
            }

            return Math.Max(0, entry.Amount);
        }

        private static HashSet<BiomeType> BuildBiomeSet(string[] names)
        {
            HashSet<BiomeType> values = new HashSet<BiomeType>();
            for (int i = 0; i < names.Length; i++)
            {
                try
                {
                    values.Add((BiomeType)Enum.Parse(typeof(BiomeType), names[i], true));
                }
                catch (Exception ex)
                {
                    RankedLog.Warn("Failed to resolve reference biome '" + names[i] + "': " + ex.Message);
                }
            }

            return values;
        }

        private static float ResolveChanceMultiplier(string groupName, string entryName, RankedSpawnMultiplierEntry entry, RankedSeedRollContext rollContext)
        {
            if (entry == null)
            {
                return 1f;
            }

            if (entry.UseSeedRange && rollContext != null)
            {
                return Mathf.Max(
                    0f,
                    rollContext.NextSteppedFloat(
                        "survival|" + groupName + "|" + entryName,
                        entry.MinChanceMultiplier,
                        entry.MaxChanceMultiplier,
                        entry.ResolutionStep));
            }

            return Mathf.Max(0f, entry.ChanceMultiplier);
        }

        private static float ResolveChanceMultiplier(string groupName, string entryName, RankedBiomeMultiplierEntry entry, RankedSeedRollContext rollContext)
        {
            if (entry == null)
            {
                return 1f;
            }

            if (entry.UseSeedRange && rollContext != null)
            {
                return Mathf.Max(
                    0f,
                    rollContext.NextSteppedFloat(
                        "survival|" + groupName + "|" + entryName,
                        entry.MinChanceMultiplier,
                        entry.MaxChanceMultiplier,
                        entry.ResolutionStep));
            }

            return Mathf.Max(0f, entry.ChanceMultiplier);
        }
    }
}
