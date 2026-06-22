using System;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace SubnauticaSpeedrunningRanked.Runtime.Seeds
{
    [Serializable]
    [XmlRoot("RankedSeedDefinition")]
    public sealed class RankedSeedDefinition
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
        public RankedCreativeSeedDefinition Creative { get; set; }

        [XmlElement("Survival")]
        public RankedSurvivalSeedDefinition Survival { get; set; }

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
                Creative = RankedCreativeSeedDefinition.CreateDefault();
            }

            if (Survival == null)
            {
                Survival = RankedSurvivalSeedDefinition.CreateTemplate();
            }

            Creative.Normalize();
            Survival.Normalize();
        }

        public static RankedSeedDefinition CreateDefaultActiveSeed()
        {
            return new RankedSeedDefinition
            {
                SeedId = DefaultActiveSeedId,
                SeedValue = DefaultActiveSeedValue,
                Description = DefaultActiveSeedDescription,
                Creative = RankedCreativeSeedDefinition.CreateDefault(),
                Survival = RankedSurvivalSeedDefinition.CreateTemplate()
            };
        }

        public static RankedSeedDefinition CreateDefaultCreativeRangeSeed()
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
    public sealed class RankedCreativeSeedDefinition
    {
        [XmlElement("SpawnMode")]
        public RankedCreativeSpawnMode SpawnMode { get; set; }

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
        public List<RankedCreativeSpawnRangeDefinition> Ranges { get; set; }

        public void Normalize()
        {
            if (Ranges == null)
            {
                Ranges = new List<RankedCreativeSpawnRangeDefinition>();
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

        public static RankedCreativeSeedDefinition CreateDefault()
        {
            return new RankedCreativeSeedDefinition
            {
                SpawnMode = RankedCreativeSpawnMode.RandomRange,
                MinX = -100f,
                MaxX = -30f,
                MinZ = -430f,
                MaxZ = -330f
            };
        }
    }

    [Serializable]
    public sealed class RankedCreativeSpawnRangeDefinition
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

    public enum RankedCreativeSpawnMode
    {
        FixedPoint,
        RandomRange,
        WeightedRanges
    }

    [Serializable]
    public sealed class RankedSurvivalSeedDefinition
    {
        public const int CurrentTemplateVersion = 4;

        [XmlElement("TemplateVersion")]
        public int TemplateVersion { get; set; }

        [XmlElement("Enabled")]
        public bool Enabled { get; set; }

        [XmlElement("Notes")]
        public string Notes { get; set; }

        [XmlElement("Spawn")]
        public RankedSurvivalSpawnDefinition Spawn { get; set; }

        [XmlElement("Defaults")]
        public RankedSurvivalDefaultsDefinition Defaults { get; set; }

        [XmlArray("Fragments")]
        [XmlArrayItem("Entry")]
        public List<RankedSpawnMultiplierEntry> Fragments { get; set; }

        [XmlArray("Resources")]
        [XmlArrayItem("Entry")]
        public List<RankedSpawnMultiplierEntry> Resources { get; set; }

        [XmlArray("Creatures")]
        [XmlArrayItem("Entry")]
        public List<RankedSpawnMultiplierEntry> Creatures { get; set; }

        [XmlArray("Biomes")]
        [XmlArrayItem("Entry")]
        public List<RankedBiomeMultiplierEntry> Biomes { get; set; }

        [XmlArray("Always")]
        [XmlArrayItem("Entry")]
        public List<RankedSpawnMultiplierEntry> Always { get; set; }

        [XmlArray("AlwaysBiomeMultipliers")]
        [XmlArrayItem("Entry")]
        public List<RankedBiomeMultiplierEntry> AlwaysBiomeMultipliers { get; set; }

        [XmlArray("AlwaysBiomeTechMultipliers")]
        [XmlArrayItem("Entry")]
        public List<RankedBiomeTechMultiplierEntry> AlwaysBiomeTechMultipliers { get; set; }

        [XmlArray("ManualCreatureSpawns")]
        [XmlArrayItem("Entry")]
        public List<RankedManualCreatureSpawnEntry> ManualCreatureSpawns { get; set; }

        public void Normalize()
        {
            if (Spawn == null)
            {
                Spawn = RankedSurvivalSpawnDefinition.CreateDefault();
            }

            if (Defaults == null)
            {
                Defaults = RankedSurvivalDefaultsDefinition.CreateDefault();
            }

            if (Fragments == null)
            {
                Fragments = new List<RankedSpawnMultiplierEntry>();
            }

            if (Resources == null)
            {
                Resources = new List<RankedSpawnMultiplierEntry>();
            }

            if (Creatures == null)
            {
                Creatures = new List<RankedSpawnMultiplierEntry>();
            }

            if (Biomes == null)
            {
                Biomes = new List<RankedBiomeMultiplierEntry>();
            }

            if (Always == null)
            {
                Always = new List<RankedSpawnMultiplierEntry>();
            }

            if (AlwaysBiomeMultipliers == null)
            {
                AlwaysBiomeMultipliers = new List<RankedBiomeMultiplierEntry>();
            }

            if (AlwaysBiomeTechMultipliers == null)
            {
                AlwaysBiomeTechMultipliers = new List<RankedBiomeTechMultiplierEntry>();
            }

            if (ManualCreatureSpawns == null)
            {
                ManualCreatureSpawns = new List<RankedManualCreatureSpawnEntry>();
            }

            RankedSeedReferenceCatalog.EnsureDefaultFragmentEntries(Fragments);
            RankedSeedReferenceCatalog.EnsureDefaultResourceEntries(Resources);
            RankedSeedReferenceCatalog.EnsureDefaultCreatureEntries(Creatures);
            RankedSeedReferenceCatalog.EnsureDefaultBiomeEntries(Biomes);
            RankedSeedReferenceCatalog.EnsureDefaultAlwaysEntries(Always);
            RankedSeedReferenceCatalog.EnsureDefaultAlwaysBiomeEntries(AlwaysBiomeMultipliers);
            RankedSeedReferenceCatalog.EnsureDefaultAlwaysBiomeTechEntries(AlwaysBiomeTechMultipliers);
            RankedSeedReferenceCatalog.EnsureDefaultManualCreatureSpawnEntries(ManualCreatureSpawns);

            NormalizeSpawnEntries(Fragments);
            NormalizeSpawnEntries(Resources);
            NormalizeSpawnEntries(Creatures);
            NormalizeSpawnEntries(Always);
            NormalizeBiomeEntries(Biomes);
            NormalizeBiomeEntries(AlwaysBiomeMultipliers);
            NormalizeBiomeTechEntries(AlwaysBiomeTechMultipliers);
            NormalizeManualCreatureSpawnEntries(ManualCreatureSpawns);
        }

        private static void NormalizeSpawnEntries(List<RankedSpawnMultiplierEntry> entries)
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

        private static void NormalizeBiomeEntries(List<RankedBiomeMultiplierEntry> entries)
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

        private static void NormalizeBiomeTechEntries(List<RankedBiomeTechMultiplierEntry> entries)
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

        private static void NormalizeManualCreatureSpawnEntries(List<RankedManualCreatureSpawnEntry> entries)
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

        public static RankedSurvivalSeedDefinition CreateTemplate()
        {
            return new RankedSurvivalSeedDefinition
            {
                TemplateVersion = CurrentTemplateVersion,
                Enabled = true,
                Notes = "Basic ranked Survival seed template. Edit the chance multipliers after confirming exact routes and placements.",
                Spawn = RankedSurvivalSpawnDefinition.CreateDefault(),
                Defaults = RankedSurvivalDefaultsDefinition.CreateDefault(),
                Fragments = RankedSeedReferenceCatalog.CreateDefaultFragmentEntries(),
                Resources = RankedSeedReferenceCatalog.CreateDefaultResourceEntries(),
                Creatures = RankedSeedReferenceCatalog.CreateDefaultCreatureEntries(),
                Biomes = RankedSeedReferenceCatalog.CreateDefaultBiomeEntries(),
                Always = RankedSeedReferenceCatalog.CreateDefaultAlwaysEntries(),
                AlwaysBiomeMultipliers = RankedSeedReferenceCatalog.CreateDefaultAlwaysBiomeEntries(),
                AlwaysBiomeTechMultipliers = RankedSeedReferenceCatalog.CreateDefaultAlwaysBiomeTechEntries(),
                ManualCreatureSpawns = RankedSeedReferenceCatalog.CreateDefaultManualCreatureSpawns()
            };
        }
    }

    [Serializable]
    public sealed class RankedSurvivalSpawnDefinition
    {
        [XmlElement("SpawnMode")]
        public RankedCreativeSpawnMode SpawnMode { get; set; }

        [XmlArray("Ranges")]
        [XmlArrayItem("Range")]
        public List<RankedCreativeSpawnRangeDefinition> Ranges { get; set; }

        public void Normalize()
        {
            if (Ranges == null)
            {
                Ranges = new List<RankedCreativeSpawnRangeDefinition>();
            }

            for (int i = 0; i < Ranges.Count; i++)
            {
                if (Ranges[i] != null)
                {
                    Ranges[i].Normalize();
                }
            }
        }

        public static RankedSurvivalSpawnDefinition CreateDefault()
        {
            return new RankedSurvivalSpawnDefinition
            {
                SpawnMode = RankedCreativeSpawnMode.WeightedRanges,
                Ranges = new List<RankedCreativeSpawnRangeDefinition>
                {
                    new RankedCreativeSpawnRangeDefinition
                    {
                        Name = "Clip C",
                        Weight = 0.75f,
                        MinX = -175f,
                        MaxX = -75f,
                        MinZ = 50f,
                        MaxZ = 100f
                    },
                    new RankedCreativeSpawnRangeDefinition
                    {
                        Name = "Clip A",
                        Weight = 0.25f,
                        MinX = -75f,
                        MaxX = 50f,
                        MinZ = 75f,
                        MaxZ = 100f
                    }
                }
            };
        }
    }

    [Serializable]
    public sealed class RankedSurvivalDefaultsDefinition
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

        public static RankedSurvivalDefaultsDefinition CreateDefault()
        {
            return new RankedSurvivalDefaultsDefinition
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
    public sealed class RankedSpawnMultiplierEntry
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
    public sealed class RankedBiomeMultiplierEntry
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
    public sealed class RankedBiomeTechMultiplierEntry
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
    public sealed class RankedManualCreatureSpawnEntry
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
        public List<RankedSpawnPointDefinition> SpawnPoints { get; set; }

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
                SpawnPoints = new List<RankedSpawnPointDefinition>();
            }
        }
    }

    [Serializable]
    public sealed class RankedSpawnPointDefinition
    {
        [XmlElement("X")]
        public float X { get; set; }

        [XmlElement("Y")]
        public float Y { get; set; }

        [XmlElement("Z")]
        public float Z { get; set; }
    }
}
