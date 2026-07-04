using System;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace SubnauticaSpeedrunningMod.Runtime.Seeds
{
    [Serializable]
    [XmlRoot("ModSeedDefinition")]
    public sealed class ModSeedDefinition
    {
        public const string DefaultActiveSeedId = "Survival-Singleplayer";
        public const string DefaultActiveSeedValue = "survival-singleplayer-default";
        public const string DefaultActiveSeedDescription = "Default singleplayer Survival seed definition for ranked route testing and deterministic spawn rolls.";
        public const string LegacyDefaultActiveSeedId = "creative-range-test";
        public const string LegacyDefaultActiveSeedValue = "creative-range-test-default";

        [XmlElement("SeedId")]
        public string SeedId { get; set; }

        [XmlElement("SeedValue")]
        public string SeedValue { get; set; }

        [XmlElement("Description")]
        public string Description { get; set; }

        [XmlElement("Creative")]
        public ModCreativeSeedDefinition Creative { get; set; }

        [XmlElement("Survival")]
        public ModSurvivalSeedDefinition Survival { get; set; }

        public void Normalize()
        {
            if (string.IsNullOrEmpty(SeedId))
            {
                SeedId = "ranked-seed";
            }

            if (string.IsNullOrEmpty(SeedValue))
            {
                SeedValue = SeedId + "-default";
            }

            if (Creative == null)
            {
                Creative = ModCreativeSeedDefinition.CreateDefault();
            }

            if (Survival == null)
            {
                Survival = ModSurvivalSeedDefinition.CreateTemplate();
            }

            Creative.Normalize();
            Survival.Normalize();
        }

        public static ModSeedDefinition CreateDefaultActiveSeed()
        {
            return new ModSeedDefinition
            {
                SeedId = DefaultActiveSeedId,
                SeedValue = DefaultActiveSeedValue,
                Description = DefaultActiveSeedDescription,
                Creative = ModCreativeSeedDefinition.CreateDefault(),
                Survival = ModSurvivalSeedDefinition.CreateTemplate()
            };
        }

        public static ModSeedDefinition CreateDefaultCreativeRangeSeed()
        {
            return CreateDefaultActiveSeed();
        }

        public static void NormalizeDeterministicAlias(ref string seedId, ref string seedValue)
        {
            if (string.Equals(seedId, DefaultActiveSeedId, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(seedValue, DefaultActiveSeedValue, StringComparison.OrdinalIgnoreCase))
            {
                seedId = LegacyDefaultActiveSeedId;
                seedValue = LegacyDefaultActiveSeedValue;
            }
        }
    }

    [Serializable]
    public sealed class ModCreativeSeedDefinition
    {
        [XmlElement("SpawnMode")]
        public ModCreativeSpawnMode SpawnMode { get; set; }

        [XmlElement("FixedX")]
        public float FixedX { get; set; }

        [XmlElement("FixedZ")]
        public float FixedZ { get; set; }

        [XmlElement("MinX")]
        public float MinX { get; set; }

        [XmlElement("MaxX")]
        public float MaxX { get; set; }

        [XmlElement("MinZ")]
        public float MinZ { get; set; }

        [XmlElement("MaxZ")]
        public float MaxZ { get; set; }

        [XmlArray("Ranges")]
        [XmlArrayItem("Range")]
        public List<ModCreativeSpawnRangeDefinition> Ranges { get; set; }

        public void Normalize()
        {
            if (Ranges == null)
            {
                Ranges = new List<ModCreativeSpawnRangeDefinition>();
            }

            if (MinX > MaxX)
            {
                float value = MinX;
                MinX = MaxX;
                MaxX = value;
            }

            if (MinZ > MaxZ)
            {
                float value2 = MinZ;
                MinZ = MaxZ;
                MaxZ = value2;
            }

            for (int i = 0; i < Ranges.Count; i++)
            {
                if (Ranges[i] != null)
                {
                    Ranges[i].Normalize();
                }
            }
        }

        public static ModCreativeSeedDefinition CreateDefault()
        {
            return new ModCreativeSeedDefinition
            {
                SpawnMode = ModCreativeSpawnMode.RandomRange,
                MinX = -100f,
                MaxX = -30f,
                MinZ = -430f,
                MaxZ = -330f
            };
        }
    }

    [Serializable]
    public sealed class ModCreativeSpawnRangeDefinition
    {
        [XmlElement("Name")]
        public string Name { get; set; }

        [XmlElement("Weight")]
        public float Weight { get; set; }

        [XmlElement("MinX")]
        public float MinX { get; set; }

        [XmlElement("MaxX")]
        public float MaxX { get; set; }

        [XmlElement("MinZ")]
        public float MinZ { get; set; }

        [XmlElement("MaxZ")]
        public float MaxZ { get; set; }

        public void Normalize()
        {
            if (MinX > MaxX)
            {
                float value = MinX;
                MinX = MaxX;
                MaxX = value;
            }

            if (MinZ > MaxZ)
            {
                float value2 = MinZ;
                MinZ = MaxZ;
                MaxZ = value2;
            }

            if (Weight < 0f)
            {
                Weight = 0f;
            }
        }
    }

    public enum ModCreativeSpawnMode
    {
        FixedPoint,
        RandomRange,
        WeightedRanges
    }

    [Serializable]
    public sealed class ModSurvivalSeedDefinition
    {
        public const int CurrentTemplateVersion = 6;

        [XmlElement("TemplateVersion")]
        public int TemplateVersion { get; set; }

        [XmlElement("Enabled")]
        public bool Enabled { get; set; }

        [XmlElement("Notes")]
        public string Notes { get; set; }

        [XmlElement("Spawn")]
        public ModSurvivalSpawnDefinition Spawn { get; set; }

        [XmlElement("Defaults")]
        public ModSurvivalDefaultsDefinition Defaults { get; set; }

        [XmlArray("Fragments")]
        [XmlArrayItem("Entry")]
        public List<ModSpawnMultiplierEntry> Fragments { get; set; }

        [XmlArray("Resources")]
        [XmlArrayItem("Entry")]
        public List<ModSpawnMultiplierEntry> Resources { get; set; }

        [XmlArray("Creatures")]
        [XmlArrayItem("Entry")]
        public List<ModSpawnMultiplierEntry> Creatures { get; set; }

        [XmlArray("Biomes")]
        [XmlArrayItem("Entry")]
        public List<ModBiomeMultiplierEntry> Biomes { get; set; }

        [XmlArray("Always")]
        [XmlArrayItem("Entry")]
        public List<ModSpawnMultiplierEntry> Always { get; set; }

        [XmlArray("AlwaysBiomeMultipliers")]
        [XmlArrayItem("Entry")]
        public List<ModBiomeMultiplierEntry> AlwaysBiomeMultipliers { get; set; }

        [XmlArray("AlwaysBiomeTechMultipliers")]
        [XmlArrayItem("Entry")]
        public List<ModBiomeTechMultiplierEntry> AlwaysBiomeTechMultipliers { get; set; }

        [XmlArray("ManualCreatureSpawns")]
        [XmlArrayItem("Entry")]
        public List<ModManualCreatureSpawnEntry> ManualCreatureSpawns { get; set; }

        public void Normalize()
        {
            if (Spawn == null)
            {
                Spawn = ModSurvivalSpawnDefinition.CreateDefault();
            }

            if (Defaults == null)
            {
                Defaults = ModSurvivalDefaultsDefinition.CreateDefault();
            }

            if (Fragments == null)
            {
                Fragments = new List<ModSpawnMultiplierEntry>();
            }

            if (Resources == null)
            {
                Resources = new List<ModSpawnMultiplierEntry>();
            }

            if (Creatures == null)
            {
                Creatures = new List<ModSpawnMultiplierEntry>();
            }

            if (Biomes == null)
            {
                Biomes = new List<ModBiomeMultiplierEntry>();
            }

            if (Always == null)
            {
                Always = new List<ModSpawnMultiplierEntry>();
            }

            if (AlwaysBiomeMultipliers == null)
            {
                AlwaysBiomeMultipliers = new List<ModBiomeMultiplierEntry>();
            }

            if (AlwaysBiomeTechMultipliers == null)
            {
                AlwaysBiomeTechMultipliers = new List<ModBiomeTechMultiplierEntry>();
            }

            if (ManualCreatureSpawns == null)
            {
                ManualCreatureSpawns = new List<ModManualCreatureSpawnEntry>();
            }

            ModSeedReferenceCatalog.EnsureDefaultFragmentEntries(Fragments);
            ModSeedReferenceCatalog.EnsureDefaultResourceEntries(Resources);
            ModSeedReferenceCatalog.EnsureDefaultCreatureEntries(Creatures);
            ModSeedReferenceCatalog.EnsureDefaultBiomeEntries(Biomes);
            ModSeedReferenceCatalog.EnsureDefaultAlwaysEntries(Always);
            ModSeedReferenceCatalog.EnsureDefaultAlwaysBiomeEntries(AlwaysBiomeMultipliers);
            ModSeedReferenceCatalog.EnsureDefaultAlwaysBiomeTechEntries(AlwaysBiomeTechMultipliers);
            ModSeedReferenceCatalog.EnsureDefaultManualCreatureSpawnEntries(ManualCreatureSpawns);

            NormalizeSpawnEntries(Fragments);
            NormalizeSpawnEntries(Resources);
            NormalizeSpawnEntries(Creatures);
            NormalizeSpawnEntries(Always);
            NormalizeBiomeEntries(Biomes);
            NormalizeBiomeEntries(AlwaysBiomeMultipliers);
            NormalizeBiomeTechEntries(AlwaysBiomeTechMultipliers);
            NormalizeManualCreatureSpawnEntries(ManualCreatureSpawns);
        }

        private static void NormalizeSpawnEntries(List<ModSpawnMultiplierEntry> entries)
        {
            if (entries == null)
            {
                return;
            }

            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i] != null)
                {
                    entries[i].Normalize();
                }
            }
        }

        private static void NormalizeBiomeEntries(List<ModBiomeMultiplierEntry> entries)
        {
            if (entries == null)
            {
                return;
            }

            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i] != null)
                {
                    entries[i].Normalize();
                }
            }
        }

        private static void NormalizeBiomeTechEntries(List<ModBiomeTechMultiplierEntry> entries)
        {
            if (entries == null)
            {
                return;
            }

            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i] != null)
                {
                    entries[i].Normalize();
                }
            }
        }

        private static void NormalizeManualCreatureSpawnEntries(List<ModManualCreatureSpawnEntry> entries)
        {
            if (entries == null)
            {
                return;
            }

            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i] != null)
                {
                    entries[i].Normalize();
                }
            }
        }

        public static ModSurvivalSeedDefinition CreateTemplate()
        {
            return new ModSurvivalSeedDefinition
            {
                TemplateVersion = CurrentTemplateVersion,
                Enabled = true,
                Notes = "Basic ranked Survival seed template. Edit the chance multipliers after confirming exact routes and placements.",
                Spawn = ModSurvivalSpawnDefinition.CreateDefault(),
                Defaults = ModSurvivalDefaultsDefinition.CreateDefault(),
                Fragments = ModSeedReferenceCatalog.CreateDefaultFragmentEntries(),
                Resources = ModSeedReferenceCatalog.CreateDefaultResourceEntries(),
                Creatures = ModSeedReferenceCatalog.CreateDefaultCreatureEntries(),
                Biomes = ModSeedReferenceCatalog.CreateDefaultBiomeEntries(),
                Always = ModSeedReferenceCatalog.CreateDefaultAlwaysEntries(),
                AlwaysBiomeMultipliers = ModSeedReferenceCatalog.CreateDefaultAlwaysBiomeEntries(),
                AlwaysBiomeTechMultipliers = ModSeedReferenceCatalog.CreateDefaultAlwaysBiomeTechEntries(),
                ManualCreatureSpawns = ModSeedReferenceCatalog.CreateDefaultManualCreatureSpawns()
            };
        }
    }

    [Serializable]
    public sealed class ModSurvivalSpawnDefinition
    {
        [XmlElement("SpawnMode")]
        public ModCreativeSpawnMode SpawnMode { get; set; }

        [XmlArray("Ranges")]
        [XmlArrayItem("Range")]
        public List<ModCreativeSpawnRangeDefinition> Ranges { get; set; }

        public void Normalize()
        {
            if (Ranges == null)
            {
                Ranges = new List<ModCreativeSpawnRangeDefinition>();
            }

            for (int i = 0; i < Ranges.Count; i++)
            {
                if (Ranges[i] != null)
                {
                    Ranges[i].Normalize();
                }
            }
        }

        public static ModSurvivalSpawnDefinition CreateDefault()
        {
            return new ModSurvivalSpawnDefinition
            {
                SpawnMode = ModCreativeSpawnMode.WeightedRanges,
                Ranges = new List<ModCreativeSpawnRangeDefinition>
                {
                    new ModCreativeSpawnRangeDefinition
                    {
                        Name = "Clip C",
                        Weight = 1f,
                        MinX = -175f,
                        MaxX = -75f,
                        MinZ = 50f,
                        MaxZ = 100f
                    }
                }
            };
        }
    }

    [Serializable]
    public sealed class ModSurvivalDefaultsDefinition
    {
        [XmlElement("DisableFishSchools")]
        public bool DisableFishSchools { get; set; }

        [XmlElement("BlockCreaturesInPrisonAquarium")]
        public bool BlockCreaturesInPrisonAquarium { get; set; }

        [XmlElement("RestrictStalkersToKelpForest")]
        public bool RestrictStalkersToKelpForest { get; set; }

        [XmlElement("StalkerToothDropProbability")]
        public float StalkerToothDropProbability { get; set; }

        [XmlElement("FixBadMetal")]
        public bool FixBadMetal { get; set; }

        [XmlElement("StalkerBitesDropTeeth")]
        public bool StalkerBitesDropTeeth { get; set; }

        [XmlElement("ForceSecondGold")]
        public bool ForceSecondGold { get; set; }

        public static ModSurvivalDefaultsDefinition CreateDefault()
        {
            return new ModSurvivalDefaultsDefinition
            {
                DisableFishSchools = true,
                BlockCreaturesInPrisonAquarium = true,
                RestrictStalkersToKelpForest = true,
                StalkerToothDropProbability = 100f,
                FixBadMetal = true,
                StalkerBitesDropTeeth = false,
                ForceSecondGold = true
            };
        }
    }

    [Serializable]
    public sealed class ModSpawnMultiplierEntry
    {
        [XmlElement("Name")]
        public string Name { get; set; }

        [XmlElement("ChanceMultiplier")]
        public float ChanceMultiplier { get; set; }

        [XmlElement("UseSeedRange")]
        public bool UseSeedRange { get; set; }

        [XmlElement("MinChanceMultiplier")]
        public float MinChanceMultiplier { get; set; }

        [XmlElement("MaxChanceMultiplier")]
        public float MaxChanceMultiplier { get; set; }

        [XmlElement("ResolutionStep")]
        public float ResolutionStep { get; set; }

        public void Normalize()
        {
            if (ChanceMultiplier < 0f)
            {
                ChanceMultiplier = 0f;
            }

            if (MinChanceMultiplier < 0f)
            {
                MinChanceMultiplier = 0f;
            }

            if (MaxChanceMultiplier < 0f)
            {
                MaxChanceMultiplier = 0f;
            }

            if (MinChanceMultiplier > MaxChanceMultiplier)
            {
                float value = MinChanceMultiplier;
                MinChanceMultiplier = MaxChanceMultiplier;
                MaxChanceMultiplier = value;
            }

            if (ResolutionStep <= 0f)
            {
                ResolutionStep = 0.001f;
            }
        }
    }

    [Serializable]
    public sealed class ModBiomeMultiplierEntry
    {
        [XmlElement("Name")]
        public string Name { get; set; }

        [XmlElement("ChanceMultiplier")]
        public float ChanceMultiplier { get; set; }

        [XmlElement("UseSeedRange")]
        public bool UseSeedRange { get; set; }

        [XmlElement("MinChanceMultiplier")]
        public float MinChanceMultiplier { get; set; }

        [XmlElement("MaxChanceMultiplier")]
        public float MaxChanceMultiplier { get; set; }

        [XmlElement("ResolutionStep")]
        public float ResolutionStep { get; set; }

        public void Normalize()
        {
            if (ChanceMultiplier < 0f)
            {
                ChanceMultiplier = 0f;
            }

            if (MinChanceMultiplier < 0f)
            {
                MinChanceMultiplier = 0f;
            }

            if (MaxChanceMultiplier < 0f)
            {
                MaxChanceMultiplier = 0f;
            }

            if (MinChanceMultiplier > MaxChanceMultiplier)
            {
                float value = MinChanceMultiplier;
                MinChanceMultiplier = MaxChanceMultiplier;
                MaxChanceMultiplier = value;
            }

            if (ResolutionStep <= 0f)
            {
                ResolutionStep = 0.001f;
            }
        }
    }

    [Serializable]
    public sealed class ModBiomeTechMultiplierEntry
    {
        [XmlElement("TechTypeName")]
        public string TechTypeName { get; set; }

        [XmlElement("BiomeName")]
        public string BiomeName { get; set; }

        [XmlElement("ChanceMultiplier")]
        public float ChanceMultiplier { get; set; }

        public void Normalize()
        {
            if (ChanceMultiplier < 0f)
            {
                ChanceMultiplier = 0f;
            }
        }
    }

    [Serializable]
    public sealed class ModManualCreatureSpawnEntry
    {
        [XmlElement("TechTypeName")]
        public string TechTypeName { get; set; }

        [XmlElement("UseSeedRange")]
        public bool UseSeedRange { get; set; }

        [XmlElement("Amount")]
        public int Amount { get; set; }

        [XmlElement("MinAmount")]
        public int MinAmount { get; set; }

        [XmlElement("MaxAmount")]
        public int MaxAmount { get; set; }

        [XmlElement("AmountStep")]
        public int AmountStep { get; set; }

        [XmlArray("SpawnPoints")]
        [XmlArrayItem("SpawnPoint")]
        public List<ModSpawnPointDefinition> SpawnPoints { get; set; }

        public void Normalize()
        {
            if (Amount < 0)
            {
                Amount = 0;
            }

            if (MinAmount < 0)
            {
                MinAmount = 0;
            }

            if (MaxAmount < 0)
            {
                MaxAmount = 0;
            }

            if (MinAmount > MaxAmount)
            {
                int value = MinAmount;
                MinAmount = MaxAmount;
                MaxAmount = value;
            }

            if (AmountStep <= 0)
            {
                AmountStep = 1;
            }

            if (SpawnPoints == null)
            {
                SpawnPoints = new List<ModSpawnPointDefinition>();
            }
        }
    }

    [Serializable]
    public sealed class ModSpawnPointDefinition
    {
        [XmlElement("X")]
        public float X { get; set; }

        [XmlElement("Y")]
        public float Y { get; set; }

        [XmlElement("Z")]
        public float Z { get; set; }
    }
}
