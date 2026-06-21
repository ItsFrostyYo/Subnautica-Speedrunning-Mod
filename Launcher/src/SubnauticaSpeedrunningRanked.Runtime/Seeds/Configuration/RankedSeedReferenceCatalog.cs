using System.Collections.Generic;

namespace SubnauticaSpeedrunningRanked.Runtime.Seeds
{
    internal static class RankedSeedReferenceCatalog
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

        public static List<RankedSpawnMultiplierEntry> CreateDefaultFragmentEntries()
        {
            List<RankedSpawnMultiplierEntry> entries = CreateSpawnEntries(FragmentTechTypeNames, 1f);
            ApplySeededSpawnRange(entries, "SeaglideFragment", 1.5f, 3f, 0.25f);
            ApplySeededSpawnRange(entries, "ConstructorFragment", 1.5f, 3f, 0.25f);
            ApplySeededSpawnRange(entries, "MoonpoolFragment", 1.5f, 3f, 0.25f);
            ApplySeededSpawnRange(entries, "CyclopsBridgeFragment", 1.5f, 2.5f, 0.25f);
            ApplySeededSpawnRange(entries, "CyclopsHullFragment", 1.5f, 2.5f, 0.25f);
            ApplySeededSpawnRange(entries, "CyclopsEngineFragment", 1.5f, 2.5f, 0.25f);
            return entries;
        }

        public static List<RankedSpawnMultiplierEntry> CreateDefaultResourceEntries()
        {
            List<RankedSpawnMultiplierEntry> entries = CreateSpawnEntries(ResourceTechTypeNames, 1f);
            ApplySeededSpawnRange(entries, "ScrapMetal", 1.5f, 3f, 0.5f);
            ApplySeededSpawnRange(entries, "SandstoneChunk", 1.5f, 2.25f, 0.25f);
            ApplySeededSpawnRange(entries, "ShaleChunk", 1.5f, 2.25f, 0.25f);
            ApplySeededSpawnRange(entries, "Lithium", 1.5f, 2.25f, 0.25f);
            ApplySeededSpawnRange(entries, "Sulphur", 1.5f, 2.25f, 0.25f);
            ApplySeededSpawnRange(entries, "Kyanite", 1.5f, 2.25f, 0.25f);
            ApplySeededSpawnRange(entries, "AluminumOxide", 1.5f, 2.25f, 0.25f);
            ApplySeededSpawnRange(entries, "JellyPlant", 1.5f, 2.25f, 0.25f);
            ApplySeededSpawnRange(entries, "Nickel", 1.5f, 3.5f, 0.25f);
            return entries;
        }

        public static List<RankedSpawnMultiplierEntry> CreateDefaultCreatureEntries()
        {
            return new List<RankedSpawnMultiplierEntry>
            {
                new RankedSpawnMultiplierEntry
                {
                    Name = "Stalker",
                    ChanceMultiplier = 0f
                }
            };
        }

        public static List<RankedSpawnMultiplierEntry> CreateDefaultAlwaysEntries()
        {
            return new List<RankedSpawnMultiplierEntry>
            {
                new RankedSpawnMultiplierEntry { Name = "Magnetite", ChanceMultiplier = 0.10f },
                new RankedSpawnMultiplierEntry { Name = "ExosuitPropulsionArmFragment", ChanceMultiplier = 0.25f },
                new RankedSpawnMultiplierEntry { Name = "ExosuitGrapplingArmFragment", ChanceMultiplier = 0.25f },
                new RankedSpawnMultiplierEntry { Name = "ExosuitTorpedoArmFragment", ChanceMultiplier = 0.25f },
                new RankedSpawnMultiplierEntry { Name = "BeaconFragment", ChanceMultiplier = 0.25f },
                new RankedSpawnMultiplierEntry { Name = "GravSphereFragment", ChanceMultiplier = 0.25f },
                new RankedSpawnMultiplierEntry { Name = "Gasopod", ChanceMultiplier = 0.50f },
                new RankedSpawnMultiplierEntry { Name = "Bleeder", ChanceMultiplier = 0f },
                new RankedSpawnMultiplierEntry { Name = "NuclearReactorFragment", ChanceMultiplier = 0.25f },
                new RankedSpawnMultiplierEntry { Name = "BaseNuclearReactorFragment", ChanceMultiplier = 0.25f }
            };
        }

        public static List<RankedBiomeMultiplierEntry> CreateDefaultAlwaysBiomeEntries()
        {
            return new List<RankedBiomeMultiplierEntry>
            {
                new RankedBiomeMultiplierEntry
                {
                    Name = "SafeShallows_ShellTunnel",
                    ChanceMultiplier = 2f
                }
            };
        }

        public static List<RankedBiomeTechMultiplierEntry> CreateDefaultAlwaysBiomeTechEntries()
        {
            List<RankedBiomeTechMultiplierEntry> entries = new List<RankedBiomeTechMultiplierEntry>();
            AddBiomeTechEntries(entries, KelpForestBiomes, "Salt", 0.10f);
            AddBiomeTechEntries(entries, SparseReefBiomes, "Salt", 2f);
            return entries;
        }

        public static List<RankedManualCreatureSpawnEntry> CreateDefaultManualCreatureSpawns()
        {
            return new List<RankedManualCreatureSpawnEntry>
            {
                new RankedManualCreatureSpawnEntry
                {
                    TechTypeName = "Stalker",
                    UseSeedRange = true,
                    Amount = 3,
                    MinAmount = 2,
                    MaxAmount = 4,
                    AmountStep = 1,
                    SpawnPoints = new List<RankedSpawnPointDefinition>
                    {
                        new RankedSpawnPointDefinition { X = -58f, Y = -40f, Z = 230f },
                        new RankedSpawnPointDefinition { X = -110f, Y = -40f, Z = 260f },
                        new RankedSpawnPointDefinition { X = -178f, Y = -40f, Z = -258f },
                        new RankedSpawnPointDefinition { X = -307f, Y = -75f, Z = 313f },
                        new RankedSpawnPointDefinition { X = -321f, Y = -80f, Z = 220f },
                        new RankedSpawnPointDefinition { X = -257f, Y = -75f, Z = 195f },
                        new RankedSpawnPointDefinition { X = -260f, Y = -60f, Z = -50f },
                        new RankedSpawnPointDefinition { X = 63f, Y = -40f, Z = 356f },
                        new RankedSpawnPointDefinition { X = 90f, Y = -45f, Z = 303f },
                        new RankedSpawnPointDefinition { X = -5f, Y = -40f, Z = 371f }
                    }
                }
            };
        }

        public static void EnsureDefaultFragmentEntries(List<RankedSpawnMultiplierEntry> entries)
        {
            EnsureSpawnEntries(entries, CreateDefaultFragmentEntries());
        }

        public static void EnsureDefaultResourceEntries(List<RankedSpawnMultiplierEntry> entries)
        {
            EnsureSpawnEntries(entries, CreateDefaultResourceEntries());
        }

        public static void EnsureDefaultCreatureEntries(List<RankedSpawnMultiplierEntry> entries)
        {
            EnsureSpawnEntries(entries, CreateDefaultCreatureEntries());
        }

        public static void EnsureDefaultAlwaysEntries(List<RankedSpawnMultiplierEntry> entries)
        {
            EnsureSpawnEntries(entries, CreateDefaultAlwaysEntries());
        }

        public static void EnsureDefaultAlwaysBiomeEntries(List<RankedBiomeMultiplierEntry> entries)
        {
            EnsureBiomeEntries(entries, CreateDefaultAlwaysBiomeEntries());
        }

        public static void EnsureDefaultAlwaysBiomeTechEntries(List<RankedBiomeTechMultiplierEntry> entries)
        {
            EnsureBiomeTechEntries(entries, CreateDefaultAlwaysBiomeTechEntries());
        }

        public static void EnsureDefaultManualCreatureSpawnEntries(List<RankedManualCreatureSpawnEntry> entries)
        {
            EnsureManualCreatureSpawnEntries(entries, CreateDefaultManualCreatureSpawns());
        }

        public static void EnsureDefaultBiomeEntries(List<RankedBiomeMultiplierEntry> entries)
        {
            EnsureBiomeEntries(entries, CreateDefaultBiomeEntries());
        }

        public static List<RankedBiomeMultiplierEntry> CreateDefaultBiomeEntries()
        {
            List<RankedBiomeMultiplierEntry> entries = new List<RankedBiomeMultiplierEntry>();
            AddBiomeEntries(entries, SafeShallowsBiomes);
            AddBiomeEntries(entries, KelpForestBiomes);
            AddBiomeEntries(entries, SparseReefBiomes);
            AddBiomeEntries(entries, SeaTreaderPathBiomes);
            AddBiomeEntries(entries, MountainsBiomes);
            AddBiomeEntries(entries, InactiveLavaZoneCastleBiomes);

            ApplySeededBiomeRange(entries, new[] { "SafeShallows_TechSite", "SafeShallows_TechSite_Barrier", "SafeShallows_TechSite_Scattered" }, 1.5f, 3f, 0.25f);
            ApplySeededBiomeRange(entries, new[] { "SafeShallows_CaveWall", "SafeShallows_CaveSpecial", "SafeShallows_Wall", "SafeShallows_CaveFloor" }, 1.5f, 3f, 0.25f);
            ApplySeededBiomeRange(entries, new[] { "SafeShallows_ShellTunnelHuge" }, 1.5f, 3f, 0.25f);
            ApplySeededBiomeRange(entries, new[] { "Kelp_GrassSparse", "Kelp_GrassDense", "Kelp_Sand", "Kelp_Wall" }, 1.5f, 3f, 0.25f);
            ApplySeededBiomeRange(entries, new[] { "Kelp_CaveWall", "Kelp_CaveFloor", "Kelp_CaveSpecial", "Kelp_ShellTunnel" }, 1.5f, 3f, 0.25f);
            ApplySeededBiomeRange(entries, new[] { "SparseReef_Wall", "SparseReef_Spike", "SparseReef_DeepWall", "SparseReef_CaveFloor", "SparseReef_CaveWall", "SparseReef_CaveCoral", "SparseReef_DeepCoral", "SparseReef_DeepFloor" }, 1.5f, 3f, 0.25f);
            ApplySeededBiomeRange(entries, new[] { "SparseReef_Techsite", "SparseReef_Techsite_Barrier", "SparseReef_Techsite_Scatter", "SparseReef_Sand" }, 1.5f, 3f, 0.25f);
            ApplySeededBiomeRange(entries, new[] { "SeaTreaderPath_Path", "SeaTreaderPath_Grass", "SeaTreaderPath_Rock", "SeaTreaderPath_CaveWall", "SeaTreaderPath_CaveFloor", "SeaTreaderPath_CaveCeiling" }, 1.5f, 3f, 0.25f);
            ApplySeededBiomeRange(entries, new[] { "SeaTreaderPath_TechSite", "SeaTreaderPath_TechSite_Barrier", "SeaTreaderPath_TechSite_Scatter", "SeaTreaderPath_Sand" }, 1.5f, 3f, 0.25f);
            ApplySeededBiomeRange(entries, new[] { "Mountains_ThermalVent", "Mountains_Rock", "Mountains_CaveFloor", "Mountains_CaveWall", "Mountains_IslandCaveWall", "Mountains_CaveCeiling", "Mountains_IslandCaveCeiling", "Mountains_IslandCaveFloor" }, 1.5f, 3f, 0.25f);
            ApplySeededBiomeRange(entries, new[] { "Mountains_Sand", "Mountains_Grass", "Mountains_IslandSand", "Mountains_IslandGrass", "Mountains_TechSite", "Mountains_TechSite_Barrier", "Mountains_TechSite_Scatter" }, 1.5f, 3f, 0.25f);
            ApplySeededBiomeRange(entries, InactiveLavaZoneCastleBiomes, 1.5f, 3f, 0.25f);

            return entries;
        }

        private static List<RankedSpawnMultiplierEntry> CreateSpawnEntries(string[] names, float multiplier)
        {
            List<RankedSpawnMultiplierEntry> entries = new List<RankedSpawnMultiplierEntry>(names.Length);
            for (int i = 0; i < names.Length; i++)
            {
                entries.Add(new RankedSpawnMultiplierEntry
                {
                    Name = names[i],
                    ChanceMultiplier = multiplier
                });
            }

            return entries;
        }

        private static void AddBiomeEntries(List<RankedBiomeMultiplierEntry> entries, string[] names)
        {
            for (int i = 0; i < names.Length; i++)
            {
                entries.Add(new RankedBiomeMultiplierEntry
                {
                    Name = names[i],
                    ChanceMultiplier = 1f
                });
            }
        }

        private static void AddBiomeTechEntries(List<RankedBiomeTechMultiplierEntry> entries, string[] biomeNames, string techTypeName, float multiplier)
        {
            if (entries == null || biomeNames == null || string.IsNullOrEmpty(techTypeName))
            {
                return;
            }

            for (int i = 0; i < biomeNames.Length; i++)
            {
                entries.Add(new RankedBiomeTechMultiplierEntry
                {
                    TechTypeName = techTypeName,
                    BiomeName = biomeNames[i],
                    ChanceMultiplier = multiplier
                });
            }
        }

        private static void ApplySeededSpawnRange(List<RankedSpawnMultiplierEntry> entries, string name, float minValue, float maxValue, float step)
        {
            if (entries == null || string.IsNullOrEmpty(name))
            {
                return;
            }

            for (int i = 0; i < entries.Count; i++)
            {
                RankedSpawnMultiplierEntry entry = entries[i];
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

        private static void ApplySeededBiomeRange(List<RankedBiomeMultiplierEntry> entries, string[] names, float minValue, float maxValue, float step)
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
                    RankedBiomeMultiplierEntry entry = entries[j];
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

        private static void EnsureSpawnEntries(List<RankedSpawnMultiplierEntry> target, List<RankedSpawnMultiplierEntry> defaults)
        {
            if (target == null || defaults == null)
            {
                return;
            }

            for (int i = 0; i < defaults.Count; i++)
            {
                RankedSpawnMultiplierEntry defaultEntry = defaults[i];
                if (defaultEntry == null || string.IsNullOrEmpty(defaultEntry.Name))
                {
                    continue;
                }

                bool found = false;
                for (int j = 0; j < target.Count; j++)
                {
                    RankedSpawnMultiplierEntry existing = target[j];
                    if (existing != null && string.Equals(existing.Name, defaultEntry.Name, System.StringComparison.OrdinalIgnoreCase))
                    {
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    target.Add(new RankedSpawnMultiplierEntry
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

        private static void EnsureBiomeEntries(List<RankedBiomeMultiplierEntry> target, List<RankedBiomeMultiplierEntry> defaults)
        {
            if (target == null || defaults == null)
            {
                return;
            }

            for (int i = 0; i < defaults.Count; i++)
            {
                RankedBiomeMultiplierEntry defaultEntry = defaults[i];
                if (defaultEntry == null || string.IsNullOrEmpty(defaultEntry.Name))
                {
                    continue;
                }

                bool found = false;
                for (int j = 0; j < target.Count; j++)
                {
                    RankedBiomeMultiplierEntry existing = target[j];
                    if (existing != null && string.Equals(existing.Name, defaultEntry.Name, System.StringComparison.OrdinalIgnoreCase))
                    {
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    target.Add(new RankedBiomeMultiplierEntry
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

        private static void EnsureBiomeTechEntries(List<RankedBiomeTechMultiplierEntry> target, List<RankedBiomeTechMultiplierEntry> defaults)
        {
            if (target == null || defaults == null)
            {
                return;
            }

            for (int i = 0; i < defaults.Count; i++)
            {
                RankedBiomeTechMultiplierEntry defaultEntry = defaults[i];
                if (defaultEntry == null ||
                    string.IsNullOrEmpty(defaultEntry.TechTypeName) ||
                    string.IsNullOrEmpty(defaultEntry.BiomeName))
                {
                    continue;
                }

                bool found = false;
                for (int j = 0; j < target.Count; j++)
                {
                    RankedBiomeTechMultiplierEntry existing = target[j];
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
                    target.Add(new RankedBiomeTechMultiplierEntry
                    {
                        TechTypeName = defaultEntry.TechTypeName,
                        BiomeName = defaultEntry.BiomeName,
                        ChanceMultiplier = defaultEntry.ChanceMultiplier
                    });
                }
            }
        }

        private static void EnsureManualCreatureSpawnEntries(List<RankedManualCreatureSpawnEntry> target, List<RankedManualCreatureSpawnEntry> defaults)
        {
            if (target == null || defaults == null)
            {
                return;
            }

            for (int i = 0; i < defaults.Count; i++)
            {
                RankedManualCreatureSpawnEntry defaultEntry = defaults[i];
                if (defaultEntry == null || string.IsNullOrEmpty(defaultEntry.TechTypeName))
                {
                    continue;
                }

                bool found = false;
                for (int j = 0; j < target.Count; j++)
                {
                    RankedManualCreatureSpawnEntry existing = target[j];
                    if (existing != null &&
                        string.Equals(existing.TechTypeName, defaultEntry.TechTypeName, System.StringComparison.OrdinalIgnoreCase))
                    {
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    List<RankedSpawnPointDefinition> spawnPoints = new List<RankedSpawnPointDefinition>();
                    if (defaultEntry.SpawnPoints != null)
                    {
                        for (int k = 0; k < defaultEntry.SpawnPoints.Count; k++)
                        {
                            RankedSpawnPointDefinition point = defaultEntry.SpawnPoints[k];
                            if (point == null)
                            {
                                continue;
                            }

                            spawnPoints.Add(new RankedSpawnPointDefinition
                            {
                                X = point.X,
                                Y = point.Y,
                                Z = point.Z
                            });
                        }
                    }

                    target.Add(new RankedManualCreatureSpawnEntry
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
    }
}
