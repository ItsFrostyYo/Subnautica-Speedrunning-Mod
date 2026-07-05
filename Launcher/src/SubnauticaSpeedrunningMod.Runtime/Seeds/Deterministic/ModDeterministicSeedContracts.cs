using System;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace SubnauticaSpeedrunningMod.Runtime.Seeds
{
    [Serializable]
    [XmlRoot("DeterministicSlotSurveyCatalog")]
    public sealed class ModDeterministicSlotSurveyCatalog
    {
        [XmlAttribute("Version")]
        public int Version { get; set; }

        [XmlElement("SurveyId")]
        public string SurveyId { get; set; }

        [XmlElement("GameBuild")]
        public string GameBuild { get; set; }

        [XmlElement("GeneratedAtUtc")]
        public string GeneratedAtUtc { get; set; }

        [XmlArray("Slots")]
        [XmlArrayItem("Slot")]
        public List<ModDeterministicSlotSurveyEntry> Slots { get; set; }

        public void Normalize()
        {
            if (Version <= 0)
            {
                Version = 1;
            }

            if (string.IsNullOrEmpty(SurveyId))
            {
                SurveyId = "survey";
            }

            if (Slots == null)
            {
                Slots = new List<ModDeterministicSlotSurveyEntry>();
            }

            for (int i = 0; i < Slots.Count; i++)
            {
                if (Slots[i] != null)
                {
                    Slots[i].Normalize();
                }
            }
        }
    }

    [Serializable]
    public sealed class ModDeterministicSlotSurveyEntry
    {
        [XmlElement("StableSlotKey")]
        public string StableSlotKey { get; set; }

        [XmlElement("SourceKind")]
        public string SourceKind { get; set; }

        [XmlElement("BiomeName")]
        public string BiomeName { get; set; }

        [XmlElement("SlotTypeName")]
        public string SlotTypeName { get; set; }

        [XmlElement("IsCreatureSlot")]
        public bool IsCreatureSlot { get; set; }

        [XmlElement("BatchX")]
        public int BatchX { get; set; }

        [XmlElement("BatchY")]
        public int BatchY { get; set; }

        [XmlElement("BatchZ")]
        public int BatchZ { get; set; }

        [XmlElement("CellX")]
        public int CellX { get; set; }

        [XmlElement("CellY")]
        public int CellY { get; set; }

        [XmlElement("CellZ")]
        public int CellZ { get; set; }

        [XmlElement("CellLevel")]
        public int CellLevel { get; set; }

        [XmlElement("PlaceholderSlotIndex")]
        public int PlaceholderSlotIndex { get; set; }

        [XmlElement("WorldX")]
        public float WorldX { get; set; }

        [XmlElement("WorldY")]
        public float WorldY { get; set; }

        [XmlElement("WorldZ")]
        public float WorldZ { get; set; }

        [XmlElement("LocalX")]
        public float LocalX { get; set; }

        [XmlElement("LocalY")]
        public float LocalY { get; set; }

        [XmlElement("LocalZ")]
        public float LocalZ { get; set; }

        [XmlElement("Density")]
        public float Density { get; set; }

        [XmlArray("PoolHints")]
        [XmlArrayItem("Hint")]
        public List<string> PoolHints { get; set; }

        [XmlArray("CandidateTechTypes")]
        [XmlArrayItem("TechType")]
        public List<string> CandidateTechTypes { get; set; }

        public void Normalize()
        {
            if (PoolHints == null)
            {
                PoolHints = new List<string>();
            }

            if (CandidateTechTypes == null)
            {
                CandidateTechTypes = new List<string>();
            }

            if (string.IsNullOrEmpty(SourceKind))
            {
                SourceKind = "Unknown";
            }

            if (string.IsNullOrEmpty(SlotTypeName))
            {
                SlotTypeName = "Unknown";
            }
        }
    }

    [Serializable]
    [XmlRoot("DeterministicPoolRuleSet")]
    public sealed class ModDeterministicPoolRuleSet
    {
        [XmlAttribute("Version")]
        public int Version { get; set; }

        [XmlElement("RuleSetId")]
        public string RuleSetId { get; set; }

        [XmlElement("Description")]
        public string Description { get; set; }

        [XmlElement("Mode")]
        public string Mode { get; set; }

        [XmlArray("Pools")]
        [XmlArrayItem("Pool")]
        public List<ModDeterministicPoolDefinition> Pools { get; set; }

        public void Normalize()
        {
            if (Version <= 0)
            {
                Version = 1;
            }

            if (string.IsNullOrEmpty(RuleSetId))
            {
                RuleSetId = "ranked-rules";
            }

            if (Pools == null)
            {
                Pools = new List<ModDeterministicPoolDefinition>();
            }

            for (int i = 0; i < Pools.Count; i++)
            {
                if (Pools[i] != null)
                {
                    Pools[i].Normalize();
                }
            }
        }
    }

    [Serializable]
    public sealed class ModDeterministicPoolDefinition
    {
        [XmlElement("PoolId")]
        public string PoolId { get; set; }

        [XmlElement("Description")]
        public string Description { get; set; }

        [XmlArray("Selectors")]
        [XmlArrayItem("Selector")]
        public List<ModDeterministicPoolSelector> Selectors { get; set; }

        [XmlArray("Guaranteed")]
        [XmlArrayItem("Entry")]
        public List<ModDeterministicPoolRuleEntry> Guaranteed { get; set; }

        [XmlArray("Caps")]
        [XmlArrayItem("Entry")]
        public List<ModDeterministicPoolRuleEntry> Caps { get; set; }

        [XmlArray("Blocked")]
        [XmlArrayItem("Entry")]
        public List<ModDeterministicPoolRuleEntry> Blocked { get; set; }

        [XmlArray("Filler")]
        [XmlArrayItem("Entry")]
        public List<ModDeterministicPoolRuleEntry> Filler { get; set; }

        public void Normalize()
        {
            if (string.IsNullOrEmpty(PoolId))
            {
                PoolId = "Pool";
            }

            if (Selectors == null)
            {
                Selectors = new List<ModDeterministicPoolSelector>();
            }

            if (Guaranteed == null)
            {
                Guaranteed = new List<ModDeterministicPoolRuleEntry>();
            }

            if (Caps == null)
            {
                Caps = new List<ModDeterministicPoolRuleEntry>();
            }

            if (Blocked == null)
            {
                Blocked = new List<ModDeterministicPoolRuleEntry>();
            }

            if (Filler == null)
            {
                Filler = new List<ModDeterministicPoolRuleEntry>();
            }

            NormalizeSelectors(Selectors);
            NormalizeRules(Guaranteed);
            NormalizeRules(Caps);
            NormalizeRules(Blocked);
            NormalizeRules(Filler);
        }

        private static void NormalizeSelectors(List<ModDeterministicPoolSelector> selectors)
        {
            for (int i = 0; i < selectors.Count; i++)
            {
                if (selectors[i] != null)
                {
                    selectors[i].Normalize();
                }
            }
        }

        private static void NormalizeRules(List<ModDeterministicPoolRuleEntry> rules)
        {
            for (int i = 0; i < rules.Count; i++)
            {
                if (rules[i] != null)
                {
                    rules[i].Normalize();
                }
            }
        }
    }

    [Serializable]
    public sealed class ModDeterministicPoolSelector
    {
        [XmlElement("SelectorId")]
        public string SelectorId { get; set; }

        [XmlArray("BiomeNames")]
        [XmlArrayItem("Biome")]
        public List<string> BiomeNames { get; set; }

        [XmlArray("SlotTypeNames")]
        [XmlArrayItem("SlotType")]
        public List<string> SlotTypeNames { get; set; }

        [XmlArray("PoolHints")]
        [XmlArrayItem("Hint")]
        public List<string> PoolHints { get; set; }

        [XmlElement("MinWorldX")]
        public float MinWorldX { get; set; }

        [XmlElement("MaxWorldX")]
        public float MaxWorldX { get; set; }

        [XmlElement("MinWorldY")]
        public float MinWorldY { get; set; }

        [XmlElement("MaxWorldY")]
        public float MaxWorldY { get; set; }

        [XmlElement("MinWorldZ")]
        public float MinWorldZ { get; set; }

        [XmlElement("MaxWorldZ")]
        public float MaxWorldZ { get; set; }

        public void Normalize()
        {
            if (BiomeNames == null)
            {
                BiomeNames = new List<string>();
            }

            if (SlotTypeNames == null)
            {
                SlotTypeNames = new List<string>();
            }

            if (PoolHints == null)
            {
                PoolHints = new List<string>();
            }

            float minWorldX = MinWorldX;
            float maxWorldX = MaxWorldX;
            NormalizeRange(ref minWorldX, ref maxWorldX);
            MinWorldX = minWorldX;
            MaxWorldX = maxWorldX;

            float minWorldY = MinWorldY;
            float maxWorldY = MaxWorldY;
            NormalizeRange(ref minWorldY, ref maxWorldY);
            MinWorldY = minWorldY;
            MaxWorldY = maxWorldY;

            float minWorldZ = MinWorldZ;
            float maxWorldZ = MaxWorldZ;
            NormalizeRange(ref minWorldZ, ref maxWorldZ);
            MinWorldZ = minWorldZ;
            MaxWorldZ = maxWorldZ;
        }

        private static void NormalizeRange(ref float minValue, ref float maxValue)
        {
            if (minValue > maxValue)
            {
                float swap = minValue;
                minValue = maxValue;
                maxValue = swap;
            }
        }
    }

    [Serializable]
    public sealed class ModDeterministicPoolRuleEntry
    {
        [XmlElement("TechTypeName")]
        public string TechTypeName { get; set; }

        [XmlElement("MinCount")]
        public int MinCount { get; set; }

        [XmlElement("MaxCount")]
        public int MaxCount { get; set; }

        [XmlElement("Weight")]
        public float Weight { get; set; }

        [XmlElement("SpawnCountPerSlot")]
        public int SpawnCountPerSlot { get; set; }

        [XmlElement("Notes")]
        public string Notes { get; set; }

        public void Normalize()
        {
            if (MinCount < 0)
            {
                MinCount = 0;
            }

            if (MaxCount < MinCount)
            {
                MaxCount = MinCount;
            }

            if (Weight < 0f)
            {
                Weight = 0f;
            }

            if (SpawnCountPerSlot <= 0)
            {
                SpawnCountPerSlot = 1;
            }
        }
    }

    [Serializable]
    [XmlRoot("DeterministicSeedManifest")]
    public sealed class ModDeterministicSeedManifest
    {
        [XmlAttribute("Version")]
        public int Version { get; set; }

        [XmlElement("ManifestId")]
        public string ManifestId { get; set; }

        [XmlElement("SeedId")]
        public string SeedId { get; set; }

        [XmlElement("SeedValue")]
        public string SeedValue { get; set; }

        [XmlElement("RuleSetId")]
        public string RuleSetId { get; set; }

        [XmlElement("SurveyId")]
        public string SurveyId { get; set; }

        [XmlArray("Entries")]
        [XmlArrayItem("Entry")]
        public List<ModDeterministicSeedManifestEntry> Entries { get; set; }

        public void Normalize()
        {
            if (Version <= 0)
            {
                Version = 1;
            }

            if (string.IsNullOrEmpty(ManifestId))
            {
                ManifestId = "manifest";
            }

            if (Entries == null)
            {
                Entries = new List<ModDeterministicSeedManifestEntry>();
            }

            for (int i = 0; i < Entries.Count; i++)
            {
                if (Entries[i] != null)
                {
                    Entries[i].Normalize();
                }
            }
        }
    }

    [Serializable]
    public sealed class ModDeterministicSeedManifestEntry
    {
        [XmlElement("StableSlotKey")]
        public string StableSlotKey { get; set; }

        [XmlElement("PoolId")]
        public string PoolId { get; set; }

        [XmlElement("TechTypeName")]
        public string TechTypeName { get; set; }

        [XmlElement("SpawnCount")]
        public int SpawnCount { get; set; }

        [XmlElement("Guaranteed")]
        public bool Guaranteed { get; set; }

        [XmlElement("Notes")]
        public string Notes { get; set; }

        public void Normalize()
        {
            if (SpawnCount <= 0)
            {
                SpawnCount = 1;
            }
        }
    }
}
