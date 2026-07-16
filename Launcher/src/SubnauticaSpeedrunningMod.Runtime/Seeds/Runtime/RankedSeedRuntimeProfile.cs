using System;
using System.Collections.Generic;
using UnityEngine;

namespace SubnauticaSpeedrunningMod.Runtime.Seeds
{
    internal sealed class ModSeedRuntimeProfile
    {
        private sealed class ModResolvedManualCreatureSpawn
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
        private readonly Dictionary<TechType, ModResolvedManualCreatureSpawn> _manualCreatureSpawns;
        private readonly HashSet<BiomeType> _kelpForestBiomes;
        private readonly HashSet<BiomeType> _prisonAquariumBiomes;
        private readonly bool _isBetterRng;

        private ModSeedRuntimeProfile(
            ModSeedDefinition definition,
            Dictionary<TechType, float> fragmentMultipliers,
            Dictionary<TechType, float> resourceMultipliers,
            Dictionary<TechType, float> creatureMultipliers,
            Dictionary<TechType, float> alwaysMultipliers,
            Dictionary<BiomeType, float> seedBiomeMultipliers,
            Dictionary<BiomeType, float> alwaysBiomeMultipliers,
            Dictionary<BiomeType, Dictionary<TechType, float>> alwaysBiomeTechMultipliers,
            Dictionary<TechType, ModResolvedManualCreatureSpawn> manualCreatureSpawns,
            HashSet<BiomeType> kelpForestBiomes,
            HashSet<BiomeType> prisonAquariumBiomes,
            bool isBetterRng)
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
            _isBetterRng = isBetterRng;
        }

        public ModSeedDefinition Definition { get; private set; }

        public bool IsBetterRng
        {
            get { return _isBetterRng; }
        }

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

            ModResolvedManualCreatureSpawn value;
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

        public static ModSeedRuntimeProfile Create(ModSeedDefinition definition)
        {
            if (definition == null)
            {
                definition = ModSeedDefinition.CreateDefaultCreativeRangeSeed();
            }

            definition.Normalize();
            ModSeedRollContext rollContext = new ModSeedRollContext(definition);

            return new ModSeedRuntimeProfile(
                definition,
                BuildTechTypeMap(definition.Survival.Fragments, "Fragments", rollContext),
                BuildTechTypeMap(definition.Survival.Resources, "Resources", rollContext),
                BuildTechTypeMap(definition.Survival.Creatures, "Creatures", rollContext),
                BuildTechTypeMap(definition.Survival.Always, "Always", rollContext),
                BuildBiomeMap(definition.Survival.Biomes, "Biomes", rollContext),
                BuildBiomeMap(definition.Survival.AlwaysBiomeMultipliers, "AlwaysBiomes", null),
                BuildBiomeTechMap(definition.Survival.AlwaysBiomeTechMultipliers),
                BuildManualCreatureSpawnMap(definition.Survival.ManualCreatureSpawns, rollContext),
                BuildBiomeSet(ModSeedReferenceCatalog.KelpForestBiomes),
                BuildBiomeSet(ModSeedReferenceCatalog.PrisonAquariumBiomes),
                false);
        }

        public static ModSeedRuntimeProfile CreateBetterRngFixedProfile()
        {
            ModSeedDefinition definition = new ModSeedDefinition
            {
                SeedId = "BetterRngPreset",
                SeedValue = "betterrng-fixed",
                Description = "Fixed BetterRNG preset profile.",
                Creative = ModCreativeSeedDefinition.CreateDefault(),
                Survival = ModSurvivalSeedDefinition.CreateTemplate()
            };

            definition.Normalize();

            if (definition.Survival.Defaults != null)
            {
                definition.Survival.Defaults.DisableFishSchools = true;
                definition.Survival.Defaults.StalkerToothDropProbability = 100f;
                definition.Survival.Defaults.FixBadMetal = true;
                definition.Survival.Defaults.StalkerBitesDropTeeth = true;
                definition.Survival.Defaults.ForceSecondGold = true;
            }

            definition.Survival.Fragments = new List<ModSpawnMultiplierEntry>();
            definition.Survival.Resources = new List<ModSpawnMultiplierEntry>();
            definition.Survival.Creatures = new List<ModSpawnMultiplierEntry>();
            definition.Survival.Biomes = new List<ModBiomeMultiplierEntry>();
            definition.Survival.ManualCreatureSpawns = new List<ModManualCreatureSpawnEntry>();

            definition.Normalize();

            return new ModSeedRuntimeProfile(
                definition,
                new Dictionary<TechType, float>(),
                new Dictionary<TechType, float>(),
                new Dictionary<TechType, float>(),
                BuildTechTypeMap(definition.Survival.Always, "Always", null),
                new Dictionary<BiomeType, float>(),
                BuildBiomeMap(definition.Survival.AlwaysBiomeMultipliers, "AlwaysBiomes", null),
                BuildBiomeTechMap(definition.Survival.AlwaysBiomeTechMultipliers),
                new Dictionary<TechType, ModResolvedManualCreatureSpawn>(),
                BuildBiomeSet(ModSeedReferenceCatalog.KelpForestBiomes),
                BuildBiomeSet(ModSeedReferenceCatalog.PrisonAquariumBiomes),
                true);
        }

        public static ModSeedRuntimeProfile CreateRankedBatchSurvivalProfile()
        {
            ModSeedDefinition definition = new ModSeedDefinition
            {
                SeedId = "RankedSurvivalBatch",
                SeedValue = "ranked-survival-batch",
                Description = "Ranked survival batch-set runtime profile.",
                Creative = ModCreativeSeedDefinition.CreateDefault(),
                Survival = new ModSurvivalSeedDefinition
                {
                    Spawn = ModSurvivalSpawnDefinition.CreateDefault(),
                    Defaults = new ModSurvivalDefaultsDefinition
                    {
                        DisableFishSchools = true,
                        BlockCreaturesInPrisonAquarium = true,
                        RestrictStalkersToKelpForest = true,
                        StalkerToothDropProbability = 100f,
                        FixBadMetal = false,
                        StalkerBitesDropTeeth = false,
                        ForceSecondGold = true
                    },
                    Fragments = new List<ModSpawnMultiplierEntry>(),
                    Resources = new List<ModSpawnMultiplierEntry>(),
                    Creatures = new List<ModSpawnMultiplierEntry>(),
                    Biomes = new List<ModBiomeMultiplierEntry>(),
                    Always = new List<ModSpawnMultiplierEntry>(),
                    AlwaysBiomeMultipliers = new List<ModBiomeMultiplierEntry>(),
                    AlwaysBiomeTechMultipliers = new List<ModBiomeTechMultiplierEntry>(),
                    ManualCreatureSpawns = ModSeedReferenceCatalog.CreateDefaultManualCreatureSpawns()
                }
            };

            return new ModSeedRuntimeProfile(
                definition,
                new Dictionary<TechType, float>(),
                new Dictionary<TechType, float>(),
                new Dictionary<TechType, float>(),
                new Dictionary<TechType, float>(),
                new Dictionary<BiomeType, float>(),
                new Dictionary<BiomeType, float>(),
                new Dictionary<BiomeType, Dictionary<TechType, float>>(),
                BuildManualCreatureSpawnMap(definition.Survival.ManualCreatureSpawns, null),
                BuildBiomeSet(ModSeedReferenceCatalog.KelpForestBiomes),
                BuildBiomeSet(ModSeedReferenceCatalog.PrisonAquariumBiomes),
                false);
        }

        private static Dictionary<TechType, float> BuildTechTypeMap(List<ModSpawnMultiplierEntry> entries, string groupName, ModSeedRollContext rollContext)
        {
            Dictionary<TechType, float> values = new Dictionary<TechType, float>();
            if (entries == null)
            {
                return values;
            }

            for (int i = 0; i < entries.Count; i++)
            {
                ModSpawnMultiplierEntry entry = entries[i];
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
                    ModLog.Warn("Seed group '" + groupName + "' contains unknown TechType '" + entry.Name + "': " + ex.Message);
                }
            }

            return values;
        }

        private static Dictionary<BiomeType, float> BuildBiomeMap(List<ModBiomeMultiplierEntry> entries, string groupName, ModSeedRollContext rollContext)
        {
            Dictionary<BiomeType, float> values = new Dictionary<BiomeType, float>();
            if (entries == null)
            {
                return values;
            }

            for (int i = 0; i < entries.Count; i++)
            {
                ModBiomeMultiplierEntry entry = entries[i];
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
                    ModLog.Warn("Seed biome list contains unknown BiomeType '" + entry.Name + "': " + ex.Message);
                }
            }

            return values;
        }

        private static Dictionary<BiomeType, Dictionary<TechType, float>> BuildBiomeTechMap(List<ModBiomeTechMultiplierEntry> entries)
        {
            Dictionary<BiomeType, Dictionary<TechType, float>> values = new Dictionary<BiomeType, Dictionary<TechType, float>>();
            if (entries == null)
            {
                return values;
            }

            for (int i = 0; i < entries.Count; i++)
            {
                ModBiomeTechMultiplierEntry entry = entries[i];
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
                    ModLog.Warn("Always biome-tech rule contains unknown value '" + entry.TechTypeName + "' / '" + entry.BiomeName + "': " + ex.Message);
                }
            }

            return values;
        }

        private static Dictionary<TechType, ModResolvedManualCreatureSpawn> BuildManualCreatureSpawnMap(List<ModManualCreatureSpawnEntry> entries, ModSeedRollContext rollContext)
        {
            Dictionary<TechType, ModResolvedManualCreatureSpawn> values = new Dictionary<TechType, ModResolvedManualCreatureSpawn>();
            if (entries == null)
            {
                return values;
            }

            for (int i = 0; i < entries.Count; i++)
            {
                ModManualCreatureSpawnEntry entry = entries[i];
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
                            ModSpawnPointDefinition point = entry.SpawnPoints[j];
                            if (point == null)
                            {
                                continue;
                            }

                            spawnPoints.Add(new Vector3(point.X, point.Y, point.Z));
                        }
                    }

                    values[techType] = new ModResolvedManualCreatureSpawn
                    {
                        Amount = amount,
                        SpawnPoints = spawnPoints
                    };
                }
                catch (Exception ex)
                {
                    ModLog.Warn("Manual creature spawn list contains unknown TechType '" + entry.TechTypeName + "': " + ex.Message);
                }
            }

            return values;
        }

        private static int ResolveSpawnAmount(ModManualCreatureSpawnEntry entry, ModSeedRollContext rollContext)
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
                    ModLog.Warn("Failed to resolve reference biome '" + names[i] + "': " + ex.Message);
                }
            }

            return values;
        }

        private static float ResolveChanceMultiplier(string groupName, string entryName, ModSpawnMultiplierEntry entry, ModSeedRollContext rollContext)
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

        private static float ResolveChanceMultiplier(string groupName, string entryName, ModBiomeMultiplierEntry entry, ModSeedRollContext rollContext)
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
