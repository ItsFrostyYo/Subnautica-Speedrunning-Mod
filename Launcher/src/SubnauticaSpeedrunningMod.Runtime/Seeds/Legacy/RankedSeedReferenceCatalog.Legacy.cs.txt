using System.Collections.Generic;

namespace SubnauticaSpeedrunningMod.Runtime.Seeds
{
    internal static class ModSeedReferenceCatalog
    {
        public static readonly string[] FragmentTechTypeNames =
        {
            "SeaglideFragment",
            "ConstructorFragment",
            "MoonpoolFragment",
            "CyclopsEngineFragment",
            "CyclopsBridgeFragment",
            "CyclopsHullFragment"
        };

        public static readonly string[] ResourceTechTypeNames =
        {
            "LimestoneChunk",
            "SandstoneChunk",
            "ShaleChunk",
            "Lithium",
            "ScrapMetal",
            "Sulphur",
            "JellyPlant",
            "Kyanite",
            "AluminumOxide",
            "Nickel"
        };

        public static readonly string[] CreatureTechTypeNames =
        {
            "Stalker"
        };

        public static readonly string[] AlwaysTechTypeNames =
        {
            "Magnetite",
            "ExosuitPropulsionArmFragment",
            "ExosuitGrapplingArmFragment",
            "ExosuitTorpedoArmFragment",
            "BeaconFragment",
            "GravSphereFragment",
            "Gasopod",
            "Bleeder",
            "NuclearReactorFragment",
            "BaseNuclearReactorFragment"
        };

        public static readonly string[] SafeShallowsBiomes =
        {
            "SafeShallows_ShellTunnel",
            "SafeShallows_SandFlat",
            "SafeShallows_Plants",
            "SafeShallows_CaveWall",
            "SafeShallows_CaveSpecial",
            "SafeShallows_ShellTunnelHuge",
            "SafeShallows_CaveFloor",
            "SafeShallows_Grass",
            "SafeShallows_Wall",
            "SafeShallows_UniqueCreature",
            "SafeShallows_UniqueCreatureCave",
            "SafeShallows_OpenShallow_CreatureOnly",
            "SafeShallows_OpenDeep_CreatureOnly",
            "SafeShallows_TechSite",
            "SafeShallows_TechSite_Barrier",
            "SafeShallows_EscapePod",
            "SafeShallows_TechSite_Scattered"
        };

        public static readonly string[] KelpForestBiomes =
        {
            "Kelp_GrassSparse",
            "Kelp_VineBase",
            "Kelp_GrassDense",
            "Kelp_Sand",
            "Kelp_Wall",
            "Kelp_CaveWall",
            "Kelp_CaveFloor",
            "Kelp_CaveSpecial",
            "Kelp_ShellTunnel",
            "Kelp_UniqueCreature",
            "Kelp_UniqueCreatureCave",
            "Kelp_DenseVine",
            "Kelp_TechSite",
            "Kelp_TechSite_Barrier",
            "Kelp_EscapePod",
            "Kelp_TechSite_Scattered"
        };

        public static readonly string[] SparseReefBiomes =
        {
            "SparseReef_Sand",
            "SparseReef_Wall",
            "SparseReef_Coral",
            "SparseReef_Spike",
            "SparseReef_DeepWall",
            "SparseReef_EscapePod",
            "SparseReef_Techsite",
            "SparseReef_Techsite_Barrier",
            "SparseReef_Techsite_Scatter",
            "SparseReef_OpenShallow_CreatureOnly",
            "SparseReef_OpenDeep_CreatureOnly",
            "SparseReef_DeepFloor",
            "SparseReef_DeepCoral",
            "SparseReef_CaveFloor",
            "SparseReef_CaveCoral",
            "SparseReef_CaveWall"
        };

        public static readonly string[] MountainsBiomes =
        {
            "Mountains_Sand",
            "Mountains_Rock",
            "Mountains_Grass",
            "Mountains_CaveFloor",
            "Mountains_CaveWall",
            "Mountains_CaveCeiling",
            "Mountains_IslandSand",
            "Mountains_IslandRock",
            "Mountains_IslandGrass",
            "Mountains_IslandCaveFloor",
            "Mountains_IslandCaveWall",
            "Mountains_IslandCaveCeiling",
            "Mountains_ThermalVent",
            "Mountains_Birds",
            "Mountains_TechSite",
            "Mountains_TechSite_Barrier",
            "Mountains_TechSite_Scatter",
            "Mountains_EscapePod",
            "Mountains_OpenShallow_CreatureOnly",
            "Mountains_OpenDeep_CreatureOnly"
        };

        public static readonly string[] SeaTreaderPathBiomes =
        {
            "SeaTreaderPath_Path",
            "SeaTreaderPath_Sand",
            "SeaTreaderPath_Grass",
            "SeaTreaderPath_Rock",
            "SeaTreaderPath_CaveWall",
            "SeaTreaderPath_CaveFloor",
            "SeaTreaderPath_CaveCeiling",
            "SeaTreaderPath_TechSite",
            "SeaTreaderPath_TechSite_Barrier",
            "SeaTreaderPath_OpenShallow_CreatureOnly",
            "SeaTreaderPath_OpenDeep_CreatureOnly",
            "SeaTreaderPath_TechSite_Scatter"
        };

        public static readonly string[] InactiveLavaZoneCastleBiomes =
        {
            "InactiveLavaZone_CastleTunnel_Floor",
            "InactiveLavaZone_CastleTunnel_Wall",
            "InactiveLavaZone_CastleTunnel_Ceiling",
            "InactiveLavaZone_CastleChamber_Floor",
            "InactiveLavaZone_CastleChamber_Wall",
            "InactiveLavaZone_CastleChamber_Ceiling"
        };

        public static readonly string[] PrisonAquariumBiomes =
        {
            "PrisonAquarium_Sand",
            "PrisonAquarium_Grass",
            "PrisonAquarium_Rock",
            "PrisonAquarium_Coral",
            "PrisonAquarium_SpecialCoral",
            "PrisonAquarium_DeadCoral",
            "PrisonAquarium_CaveFloor",
            "PrisonAquarium_CaveWall",
            "PrisonAquarium_CaveCeiling",
            "PrisonAquarium_Open_CreatureOnly"
        };

        public static List<ModSpawnMultiplierEntry> CreateDefaultFragmentEntries()
        {
            List<ModSpawnMultiplierEntry> entries = CreateSpawnEntries(FragmentTechTypeNames, 1f);
            ApplySeededSpawnRange(entries, "SeaglideFragment", 2f, 3.5f, 0.75f);
            ApplySeededSpawnRange(entries, "ConstructorFragment", 2f, 3.5f, 0.75f);
            ApplySeededSpawnRange(entries, "MoonpoolFragment", 1.5f, 3.5f, 0.75f);
            ApplySeededSpawnRange(entries, "CyclopsBridgeFragment", 1.5f, 3f, 0.75f);
            ApplySeededSpawnRange(entries, "CyclopsHullFragment", 1.5f, 3f, 0.75f);
            ApplySeededSpawnRange(entries, "CyclopsEngineFragment", 1.5f, 3f, 0.75f);
            return entries;
        }

        public static List<ModSpawnMultiplierEntry> CreateDefaultResourceEntries()
        {
            List<ModSpawnMultiplierEntry> entries = CreateSpawnEntries(ResourceTechTypeNames, 1f);
            ApplySeededSpawnRange(entries, "ScrapMetal", 2f, 3.5f, 0.75f);
            ApplySeededSpawnRange(entries, "SandstoneChunk", 2f, 3.5f, 0.75f);
            ApplySeededSpawnRange(entries, "ShaleChunk", 2f, 3.5f, 0.75f);
            ApplySeededSpawnRange(entries, "Lithium", 2f, 3.5f, 0.75f);
            ApplySeededSpawnRange(entries, "Sulphur", 2f, 3.5f, 0.75f);
            ApplySeededSpawnRange(entries, "Kyanite", 2f, 3.5f, 0.75f);
            ApplySeededSpawnRange(entries, "AluminumOxide", 1.5f, 3f, 0.75f);
            ApplySeededSpawnRange(entries, "JellyPlant", 1.5f, 3f, 0.75f);
            ApplySeededSpawnRange(entries, "Nickel", 3f, 10f, 1f);
            return entries;
        }

        public static List<ModSpawnMultiplierEntry> CreateDefaultCreatureEntries()
        {
            return new List<ModSpawnMultiplierEntry>
            {
                new ModSpawnMultiplierEntry
                {
                    Name = "Stalker",
                    ChanceMultiplier = 0f
                }
            };
        }

        public static List<ModSpawnMultiplierEntry> CreateDefaultAlwaysEntries()
        {
            return new List<ModSpawnMultiplierEntry>
            {
                new ModSpawnMultiplierEntry { Name = "Magnetite", ChanceMultiplier = 0.10f },
                new ModSpawnMultiplierEntry { Name = "ExosuitPropulsionArmFragment", ChanceMultiplier = 0.25f },
                new ModSpawnMultiplierEntry { Name = "ExosuitGrapplingArmFragment", ChanceMultiplier = 0.25f },
                new ModSpawnMultiplierEntry { Name = "ExosuitTorpedoArmFragment", ChanceMultiplier = 0.25f },
                new ModSpawnMultiplierEntry { Name = "BeaconFragment", ChanceMultiplier = 0.25f },
                new ModSpawnMultiplierEntry { Name = "GravSphereFragment", ChanceMultiplier = 0.25f },
                new ModSpawnMultiplierEntry { Name = "Gasopod", ChanceMultiplier = 0.50f },
                new ModSpawnMultiplierEntry { Name = "Bleeder", ChanceMultiplier = 0f },
                new ModSpawnMultiplierEntry { Name = "NuclearReactorFragment", ChanceMultiplier = 0.25f },
                new ModSpawnMultiplierEntry { Name = "BaseNuclearReactorFragment", ChanceMultiplier = 0.25f }
            };
        }

        public static List<ModBiomeMultiplierEntry> CreateDefaultAlwaysBiomeEntries()
        {
            return new List<ModBiomeMultiplierEntry>
            {
                new ModBiomeMultiplierEntry
                {
                    Name = "SafeShallows_ShellTunnel",
                    ChanceMultiplier = 3f
                }
            };
        }

        public static List<ModBiomeTechMultiplierEntry> CreateDefaultAlwaysBiomeTechEntries()
        {
            List<ModBiomeTechMultiplierEntry> entries = new List<ModBiomeTechMultiplierEntry>();
            AddBiomeTechEntries(entries, KelpForestBiomes, "Salt", 0.10f);
            AddBiomeTechEntries(entries, SparseReefBiomes, "Salt", 2f);
            return entries;
        }

        public static List<ModManualCreatureSpawnEntry> CreateDefaultManualCreatureSpawns()
        {
            return new List<ModManualCreatureSpawnEntry>
            {
                new ModManualCreatureSpawnEntry
                {
                    TechTypeName = "Stalker",
                    UseSeedRange = false,
                    Amount = 2,
                    MinAmount = 2,
                    MaxAmount = 2,
                    AmountStep = 1,
                    SpawnPoints = new List<ModSpawnPointDefinition>
                    {
                        new ModSpawnPointDefinition { X = -58f, Y = -40f, Z = 230f },
                        new ModSpawnPointDefinition { X = -110f, Y = -40f, Z = 260f },
                        new ModSpawnPointDefinition { X = -178f, Y = -40f, Z = -258f },
                        new ModSpawnPointDefinition { X = -307f, Y = -75f, Z = 313f },
                        new ModSpawnPointDefinition { X = -321f, Y = -80f, Z = 220f },
                        new ModSpawnPointDefinition { X = -257f, Y = -75f, Z = 195f },
                        new ModSpawnPointDefinition { X = -260f, Y = -60f, Z = -50f },
                        new ModSpawnPointDefinition { X = 63f, Y = -40f, Z = 356f },
                        new ModSpawnPointDefinition { X = 90f, Y = -45f, Z = 303f },
                        new ModSpawnPointDefinition { X = -5f, Y = -40f, Z = 371f }
                    }
                }
            };
        }

        public static void EnsureDefaultFragmentEntries(List<ModSpawnMultiplierEntry> entries)
        {
            EnsureSpawnEntries(entries, CreateDefaultFragmentEntries());
        }

        public static void EnsureDefaultResourceEntries(List<ModSpawnMultiplierEntry> entries)
        {
            EnsureSpawnEntries(entries, CreateDefaultResourceEntries());
        }

        public static void EnsureDefaultCreatureEntries(List<ModSpawnMultiplierEntry> entries)
        {
            EnsureSpawnEntries(entries, CreateDefaultCreatureEntries());
        }

        public static void EnsureDefaultAlwaysEntries(List<ModSpawnMultiplierEntry> entries)
        {
            EnsureSpawnEntries(entries, CreateDefaultAlwaysEntries());
        }

        public static void EnsureDefaultAlwaysBiomeEntries(List<ModBiomeMultiplierEntry> entries)
        {
            EnsureBiomeEntries(entries, CreateDefaultAlwaysBiomeEntries());
        }

        public static void EnsureDefaultAlwaysBiomeTechEntries(List<ModBiomeTechMultiplierEntry> entries)
        {
            EnsureBiomeTechEntries(entries, CreateDefaultAlwaysBiomeTechEntries());
        }

        public static void EnsureDefaultManualCreatureSpawnEntries(List<ModManualCreatureSpawnEntry> entries)
        {
            EnsureManualCreatureSpawnEntries(entries, CreateDefaultManualCreatureSpawns());
        }

        public static bool SyncCurrentFragmentEntries(List<ModSpawnMultiplierEntry> entries)
        {
            return SyncSpawnEntries(entries, CreateDefaultFragmentEntries());
        }

        public static bool SyncCurrentResourceEntries(List<ModSpawnMultiplierEntry> entries)
        {
            return SyncSpawnEntries(entries, CreateDefaultResourceEntries());
        }

        public static bool SyncCurrentCreatureEntries(List<ModSpawnMultiplierEntry> entries)
        {
            return SyncSpawnEntries(entries, CreateDefaultCreatureEntries());
        }

        public static bool SyncCurrentAlwaysEntries(List<ModSpawnMultiplierEntry> entries)
        {
            return SyncSpawnEntries(entries, CreateDefaultAlwaysEntries());
        }

        public static bool SyncCurrentBiomeEntries(List<ModBiomeMultiplierEntry> entries)
        {
            return SyncBiomeEntries(entries, CreateDefaultBiomeEntries());
        }

        public static bool SyncCurrentAlwaysBiomeEntries(List<ModBiomeMultiplierEntry> entries)
        {
            return SyncBiomeEntries(entries, CreateDefaultAlwaysBiomeEntries());
        }

        public static bool SyncCurrentAlwaysBiomeTechEntries(List<ModBiomeTechMultiplierEntry> entries)
        {
            return SyncBiomeTechEntries(entries, CreateDefaultAlwaysBiomeTechEntries());
        }

        public static bool SyncCurrentManualCreatureSpawnEntries(List<ModManualCreatureSpawnEntry> entries)
        {
            return SyncManualCreatureSpawnEntries(entries, CreateDefaultManualCreatureSpawns());
        }

        public static void EnsureDefaultBiomeEntries(List<ModBiomeMultiplierEntry> entries)
        {
            EnsureBiomeEntries(entries, CreateDefaultBiomeEntries());
        }

        public static List<ModBiomeMultiplierEntry> CreateDefaultBiomeEntries()
        {
            List<ModBiomeMultiplierEntry> entries = new List<ModBiomeMultiplierEntry>();
            AddBiomeEntries(entries, SafeShallowsBiomes);
            AddBiomeEntries(entries, KelpForestBiomes);
            AddBiomeEntries(entries, SparseReefBiomes);
            AddBiomeEntries(entries, SeaTreaderPathBiomes);
            AddBiomeEntries(entries, MountainsBiomes);

            ApplySeededBiomeRange(entries, new[] { "SafeShallows_TechSite", "SafeShallows_TechSite_Barrier", "SafeShallows_TechSite_Scattered" }, 2f, 3.5f, 0.75f);
            ApplySeededBiomeRange(entries, new[] { "SafeShallows_CaveWall", "SafeShallows_CaveSpecial", "SafeShallows_Wall", "SafeShallows_CaveFloor" }, 2.0f, 3.5f, 0.75f);
            ApplySeededBiomeRange(entries, new[] { "SafeShallows_ShellTunnelHuge" }, 2.0f, 3.5f, 0.75f);
            ApplySeededBiomeRange(entries, new[] { "Kelp_GrassSparse", "Kelp_GrassDense", "Kelp_Sand", "Kelp_Wall" }, 2.0f, 3.5f, 0.75f);
            ApplySeededBiomeRange(entries, new[] { "Kelp_CaveWall", "Kelp_CaveFloor", "Kelp_CaveSpecial", "Kelp_ShellTunnel" }, 2.0f, 3.5f, 0.75f);
            ApplySeededBiomeRange(entries, new[] { "SparseReef_Wall", "SparseReef_Spike", "SparseReef_DeepWall", "SparseReef_CaveFloor", "SparseReef_CaveWall", "SparseReef_CaveCoral", "SparseReef_DeepCoral", "SparseReef_DeepFloor" }, 1.5f, 3f, 0.75f);
            ApplySeededBiomeRange(entries, new[] { "SparseReef_Techsite", "SparseReef_Techsite_Barrier", "SparseReef_Techsite_Scatter", "SparseReef_Sand" }, 1.5f, 3f, 0.75f);
            ApplySeededBiomeRange(entries, new[] { "SeaTreaderPath_Path", "SeaTreaderPath_Grass", "SeaTreaderPath_Rock", "SeaTreaderPath_CaveWall", "SeaTreaderPath_CaveFloor", "SeaTreaderPath_CaveCeiling" }, 1.5f, 3f, 0.75f);
            ApplySeededBiomeRange(entries, new[] { "SeaTreaderPath_TechSite", "SeaTreaderPath_TechSite_Barrier", "SeaTreaderPath_TechSite_Scatter", "SeaTreaderPath_Sand" }, 1.5f, 3f, 0.75f);
            ApplySeededBiomeRange(entries, new[] { "Mountains_ThermalVent", "Mountains_Rock", "Mountains_CaveFloor", "Mountains_CaveWall", "Mountains_IslandCaveWall", "Mountains_CaveCeiling", "Mountains_IslandCaveCeiling", "Mountains_IslandCaveFloor" }, 1.5f, 3f, 0.75f);
            ApplySeededBiomeRange(entries, new[] { "Mountains_Sand", "Mountains_Grass", "Mountains_IslandSand", "Mountains_IslandGrass", "Mountains_TechSite", "Mountains_TechSite_Barrier", "Mountains_TechSite_Scatter" }, 1.5f, 3f, 0.75f);

            return entries;
        }

        private static List<ModSpawnMultiplierEntry> CreateSpawnEntries(string[] names, float multiplier)
        {
            List<ModSpawnMultiplierEntry> entries = new List<ModSpawnMultiplierEntry>(names.Length);
            for (int i = 0; i < names.Length; i++)
            {
                entries.Add(new ModSpawnMultiplierEntry
                {
                    Name = names[i],
                    ChanceMultiplier = multiplier
                });
            }

            return entries;
        }

        private static void AddBiomeEntries(List<ModBiomeMultiplierEntry> entries, string[] names)
        {
            for (int i = 0; i < names.Length; i++)
            {
                entries.Add(new ModBiomeMultiplierEntry
                {
                    Name = names[i],
                    ChanceMultiplier = 1f
                });
            }
        }

        private static void AddBiomeTechEntries(List<ModBiomeTechMultiplierEntry> entries, string[] biomeNames, string techTypeName, float multiplier)
        {
            if (entries == null || biomeNames == null || string.IsNullOrEmpty(techTypeName))
            {
                return;
            }

            for (int i = 0; i < biomeNames.Length; i++)
            {
                entries.Add(new ModBiomeTechMultiplierEntry
                {
                    TechTypeName = techTypeName,
                    BiomeName = biomeNames[i],
                    ChanceMultiplier = multiplier
                });
            }
        }

        private static void ApplySeededSpawnRange(List<ModSpawnMultiplierEntry> entries, string name, float minValue, float maxValue, float step)
        {
            if (entries == null || string.IsNullOrEmpty(name))
            {
                return;
            }

            for (int i = 0; i < entries.Count; i++)
            {
                ModSpawnMultiplierEntry entry = entries[i];
                if (entry == null || !string.Equals(entry.Name, name, System.StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                entry.UseSeedRange = true;
                entry.MinChanceMultiplier = minValue;
                entry.MaxChanceMultiplier = maxValue;
                entry.ResolutionStep = step;
                return;
            }
        }

        private static void ApplySeededBiomeRange(List<ModBiomeMultiplierEntry> entries, string[] names, float minValue, float maxValue, float step)
        {
            if (entries == null || names == null)
            {
                return;
            }

            for (int i = 0; i < names.Length; i++)
            {
                string name = names[i];
                for (int j = 0; j < entries.Count; j++)
                {
                    ModBiomeMultiplierEntry entry = entries[j];
                    if (entry == null || !string.Equals(entry.Name, name, System.StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    entry.UseSeedRange = true;
                    entry.MinChanceMultiplier = minValue;
                    entry.MaxChanceMultiplier = maxValue;
                    entry.ResolutionStep = step;
                    break;
                }
            }
        }

        private static void EnsureSpawnEntries(List<ModSpawnMultiplierEntry> target, List<ModSpawnMultiplierEntry> defaults)
        {
            if (target == null || defaults == null)
            {
                return;
            }

            for (int i = 0; i < defaults.Count; i++)
            {
                ModSpawnMultiplierEntry defaultEntry = defaults[i];
                if (defaultEntry == null || string.IsNullOrEmpty(defaultEntry.Name))
                {
                    continue;
                }

                bool found = false;
                for (int j = 0; j < target.Count; j++)
                {
                    ModSpawnMultiplierEntry existing = target[j];
                    if (existing != null && string.Equals(existing.Name, defaultEntry.Name, System.StringComparison.OrdinalIgnoreCase))
                    {
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    target.Add(new ModSpawnMultiplierEntry
                    {
                        Name = defaultEntry.Name,
                        ChanceMultiplier = defaultEntry.ChanceMultiplier,
                        UseSeedRange = defaultEntry.UseSeedRange,
                        MinChanceMultiplier = defaultEntry.MinChanceMultiplier,
                        MaxChanceMultiplier = defaultEntry.MaxChanceMultiplier,
                        ResolutionStep = defaultEntry.ResolutionStep
                    });
                }
            }
        }

        private static bool SyncSpawnEntries(List<ModSpawnMultiplierEntry> target, List<ModSpawnMultiplierEntry> defaults)
        {
            if (target == null || defaults == null)
            {
                return false;
            }

            bool changed = false;
            for (int i = 0; i < defaults.Count; i++)
            {
                ModSpawnMultiplierEntry defaultEntry = defaults[i];
                if (defaultEntry == null || string.IsNullOrEmpty(defaultEntry.Name))
                {
                    continue;
                }

                for (int j = 0; j < target.Count; j++)
                {
                    ModSpawnMultiplierEntry existing = target[j];
                    if (existing == null || !string.Equals(existing.Name, defaultEntry.Name, System.StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (existing.ChanceMultiplier != defaultEntry.ChanceMultiplier ||
                        existing.UseSeedRange != defaultEntry.UseSeedRange ||
                        existing.MinChanceMultiplier != defaultEntry.MinChanceMultiplier ||
                        existing.MaxChanceMultiplier != defaultEntry.MaxChanceMultiplier ||
                        existing.ResolutionStep != defaultEntry.ResolutionStep)
                    {
                        existing.ChanceMultiplier = defaultEntry.ChanceMultiplier;
                        existing.UseSeedRange = defaultEntry.UseSeedRange;
                        existing.MinChanceMultiplier = defaultEntry.MinChanceMultiplier;
                        existing.MaxChanceMultiplier = defaultEntry.MaxChanceMultiplier;
                        existing.ResolutionStep = defaultEntry.ResolutionStep;
                        changed = true;
                    }

                    break;
                }
            }

            return changed;
        }

        private static void EnsureBiomeEntries(List<ModBiomeMultiplierEntry> target, List<ModBiomeMultiplierEntry> defaults)
        {
            if (target == null || defaults == null)
            {
                return;
            }

            for (int i = 0; i < defaults.Count; i++)
            {
                ModBiomeMultiplierEntry defaultEntry = defaults[i];
                if (defaultEntry == null || string.IsNullOrEmpty(defaultEntry.Name))
                {
                    continue;
                }

                bool found = false;
                for (int j = 0; j < target.Count; j++)
                {
                    ModBiomeMultiplierEntry existing = target[j];
                    if (existing != null && string.Equals(existing.Name, defaultEntry.Name, System.StringComparison.OrdinalIgnoreCase))
                    {
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    target.Add(new ModBiomeMultiplierEntry
                    {
                        Name = defaultEntry.Name,
                        ChanceMultiplier = defaultEntry.ChanceMultiplier,
                        UseSeedRange = defaultEntry.UseSeedRange,
                        MinChanceMultiplier = defaultEntry.MinChanceMultiplier,
                        MaxChanceMultiplier = defaultEntry.MaxChanceMultiplier,
                        ResolutionStep = defaultEntry.ResolutionStep
                    });
                }
            }
        }

        private static bool SyncBiomeEntries(List<ModBiomeMultiplierEntry> target, List<ModBiomeMultiplierEntry> defaults)
        {
            if (target == null || defaults == null)
            {
                return false;
            }

            bool changed = false;
            for (int i = 0; i < defaults.Count; i++)
            {
                ModBiomeMultiplierEntry defaultEntry = defaults[i];
                if (defaultEntry == null || string.IsNullOrEmpty(defaultEntry.Name))
                {
                    continue;
                }

                for (int j = 0; j < target.Count; j++)
                {
                    ModBiomeMultiplierEntry existing = target[j];
                    if (existing == null || !string.Equals(existing.Name, defaultEntry.Name, System.StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (existing.ChanceMultiplier != defaultEntry.ChanceMultiplier ||
                        existing.UseSeedRange != defaultEntry.UseSeedRange ||
                        existing.MinChanceMultiplier != defaultEntry.MinChanceMultiplier ||
                        existing.MaxChanceMultiplier != defaultEntry.MaxChanceMultiplier ||
                        existing.ResolutionStep != defaultEntry.ResolutionStep)
                    {
                        existing.ChanceMultiplier = defaultEntry.ChanceMultiplier;
                        existing.UseSeedRange = defaultEntry.UseSeedRange;
                        existing.MinChanceMultiplier = defaultEntry.MinChanceMultiplier;
                        existing.MaxChanceMultiplier = defaultEntry.MaxChanceMultiplier;
                        existing.ResolutionStep = defaultEntry.ResolutionStep;
                        changed = true;
                    }

                    break;
                }
            }

            return changed;
        }

        private static void EnsureBiomeTechEntries(List<ModBiomeTechMultiplierEntry> target, List<ModBiomeTechMultiplierEntry> defaults)
        {
            if (target == null || defaults == null)
            {
                return;
            }

            for (int i = 0; i < defaults.Count; i++)
            {
                ModBiomeTechMultiplierEntry defaultEntry = defaults[i];
                if (defaultEntry == null ||
                    string.IsNullOrEmpty(defaultEntry.TechTypeName) ||
                    string.IsNullOrEmpty(defaultEntry.BiomeName))
                {
                    continue;
                }

                bool found = false;
                for (int j = 0; j < target.Count; j++)
                {
                    ModBiomeTechMultiplierEntry existing = target[j];
                    if (existing != null &&
                        string.Equals(existing.TechTypeName, defaultEntry.TechTypeName, System.StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(existing.BiomeName, defaultEntry.BiomeName, System.StringComparison.OrdinalIgnoreCase))
                    {
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    target.Add(new ModBiomeTechMultiplierEntry
                    {
                        TechTypeName = defaultEntry.TechTypeName,
                        BiomeName = defaultEntry.BiomeName,
                        ChanceMultiplier = defaultEntry.ChanceMultiplier
                    });
                }
            }
        }

        private static bool SyncBiomeTechEntries(List<ModBiomeTechMultiplierEntry> target, List<ModBiomeTechMultiplierEntry> defaults)
        {
            if (target == null || defaults == null)
            {
                return false;
            }

            bool changed = false;
            for (int i = 0; i < defaults.Count; i++)
            {
                ModBiomeTechMultiplierEntry defaultEntry = defaults[i];
                if (defaultEntry == null ||
                    string.IsNullOrEmpty(defaultEntry.TechTypeName) ||
                    string.IsNullOrEmpty(defaultEntry.BiomeName))
                {
                    continue;
                }

                for (int j = 0; j < target.Count; j++)
                {
                    ModBiomeTechMultiplierEntry existing = target[j];
                    if (existing == null ||
                        !string.Equals(existing.TechTypeName, defaultEntry.TechTypeName, System.StringComparison.OrdinalIgnoreCase) ||
                        !string.Equals(existing.BiomeName, defaultEntry.BiomeName, System.StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (existing.ChanceMultiplier != defaultEntry.ChanceMultiplier)
                    {
                        existing.ChanceMultiplier = defaultEntry.ChanceMultiplier;
                        changed = true;
                    }

                    break;
                }
            }

            return changed;
        }

        private static void EnsureManualCreatureSpawnEntries(List<ModManualCreatureSpawnEntry> target, List<ModManualCreatureSpawnEntry> defaults)
        {
            if (target == null || defaults == null)
            {
                return;
            }

            for (int i = 0; i < defaults.Count; i++)
            {
                ModManualCreatureSpawnEntry defaultEntry = defaults[i];
                if (defaultEntry == null || string.IsNullOrEmpty(defaultEntry.TechTypeName))
                {
                    continue;
                }

                bool found = false;
                for (int j = 0; j < target.Count; j++)
                {
                    ModManualCreatureSpawnEntry existing = target[j];
                    if (existing != null &&
                        string.Equals(existing.TechTypeName, defaultEntry.TechTypeName, System.StringComparison.OrdinalIgnoreCase))
                    {
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    List<ModSpawnPointDefinition> spawnPoints = new List<ModSpawnPointDefinition>();
                    if (defaultEntry.SpawnPoints != null)
                    {
                        for (int k = 0; k < defaultEntry.SpawnPoints.Count; k++)
                        {
                            ModSpawnPointDefinition point = defaultEntry.SpawnPoints[k];
                            if (point == null)
                            {
                                continue;
                            }

                            spawnPoints.Add(new ModSpawnPointDefinition
                            {
                                X = point.X,
                                Y = point.Y,
                                Z = point.Z
                            });
                        }
                    }

                    target.Add(new ModManualCreatureSpawnEntry
                    {
                        TechTypeName = defaultEntry.TechTypeName,
                        UseSeedRange = defaultEntry.UseSeedRange,
                        Amount = defaultEntry.Amount,
                        MinAmount = defaultEntry.MinAmount,
                        MaxAmount = defaultEntry.MaxAmount,
                        AmountStep = defaultEntry.AmountStep,
                        SpawnPoints = spawnPoints
                    });
                }
            }
        }

        private static bool SyncManualCreatureSpawnEntries(List<ModManualCreatureSpawnEntry> target, List<ModManualCreatureSpawnEntry> defaults)
        {
            if (target == null || defaults == null)
            {
                return false;
            }

            bool changed = false;
            for (int i = 0; i < defaults.Count; i++)
            {
                ModManualCreatureSpawnEntry defaultEntry = defaults[i];
                if (defaultEntry == null || string.IsNullOrEmpty(defaultEntry.TechTypeName))
                {
                    continue;
                }

                for (int j = 0; j < target.Count; j++)
                {
                    ModManualCreatureSpawnEntry existing = target[j];
                    if (existing == null ||
                        !string.Equals(existing.TechTypeName, defaultEntry.TechTypeName, System.StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (existing.UseSeedRange != defaultEntry.UseSeedRange ||
                        existing.Amount != defaultEntry.Amount ||
                        existing.MinAmount != defaultEntry.MinAmount ||
                        existing.MaxAmount != defaultEntry.MaxAmount ||
                        existing.AmountStep != defaultEntry.AmountStep)
                    {
                        existing.UseSeedRange = defaultEntry.UseSeedRange;
                        existing.Amount = defaultEntry.Amount;
                        existing.MinAmount = defaultEntry.MinAmount;
                        existing.MaxAmount = defaultEntry.MaxAmount;
                        existing.AmountStep = defaultEntry.AmountStep;
                        changed = true;
                    }

                    if (SyncSpawnPoints(existing, defaultEntry))
                    {
                        changed = true;
                    }

                    break;
                }
            }

            return changed;
        }

        private static bool SyncSpawnPoints(ModManualCreatureSpawnEntry target, ModManualCreatureSpawnEntry defaults)
        {
            if (target == null || defaults == null)
            {
                return false;
            }

            bool changed = false;
            List<ModSpawnPointDefinition> targetPoints = target.SpawnPoints ?? new List<ModSpawnPointDefinition>();
            List<ModSpawnPointDefinition> defaultPoints = defaults.SpawnPoints ?? new List<ModSpawnPointDefinition>();

            if (target.SpawnPoints == null)
            {
                target.SpawnPoints = targetPoints;
                changed = true;
            }

            if (targetPoints.Count != defaultPoints.Count)
            {
                targetPoints.Clear();
                for (int i = 0; i < defaultPoints.Count; i++)
                {
                    ModSpawnPointDefinition point = defaultPoints[i];
                    targetPoints.Add(new ModSpawnPointDefinition { X = point.X, Y = point.Y, Z = point.Z });
                }

                return true;
            }

            for (int i = 0; i < defaultPoints.Count; i++)
            {
                ModSpawnPointDefinition targetPoint = targetPoints[i];
                ModSpawnPointDefinition defaultPoint = defaultPoints[i];
                if (targetPoint.X != defaultPoint.X || targetPoint.Y != defaultPoint.Y || targetPoint.Z != defaultPoint.Z)
                {
                    targetPoint.X = defaultPoint.X;
                    targetPoint.Y = defaultPoint.Y;
                    targetPoint.Z = defaultPoint.Z;
                    changed = true;
                }
            }

            return changed;
        }
    }
}
