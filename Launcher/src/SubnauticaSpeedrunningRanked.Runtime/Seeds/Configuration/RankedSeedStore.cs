using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Xml.Serialization;

namespace SubnauticaSpeedrunningRanked.Runtime.Seeds
{
    internal static class RankedSeedStore
    {
        private const string SeedsDirectoryName = "Seeds";
        private const string AssignedSlotsDirectoryName = "AssignedSlots";
        private const string ActiveSeedFileName = "active-seed.xml";
        private const string SurvivalTemplateFileName = "survival-seed-template.xml";

        private static readonly object Sync = new object();
        private static readonly XmlSerializer SeedSerializer = new XmlSerializer(typeof(RankedSeedDefinition));
        private static bool _initialized;
        private static string _seedsDirectoryPath = string.Empty;
        private static string _assignedSlotsDirectoryPath = string.Empty;
        private static string _activeSeedPath = string.Empty;
        private static RankedSeedDefinition _activeSeed;
        private static RankedSeedRuntimeProfile _activeProfile;
        private static RankedSeedRollContext _rollContext;
        private static string _activeAssignedSlot = string.Empty;
        private static GameMode _activeAssignedMode = GameMode.None;
        private static bool _creativeSpawnResolved;
        private static float _creativeSpawnX;
        private static float _creativeSpawnZ;
        private static string _creativeSpawnLabel = string.Empty;
        private static bool _survivalSpawnResolved;
        private static float _survivalSpawnX;
        private static float _survivalSpawnZ;
        private static string _survivalSpawnLabel = string.Empty;

        public static void Initialize(RuntimeContext context)
        {
            lock (Sync)
            {
                if (_initialized)
                {
                    return;
                }

                _seedsDirectoryPath = Path.Combine(Path.Combine(context.RankedRoot, "Data"), SeedsDirectoryName);
                Directory.CreateDirectory(_seedsDirectoryPath);
                _assignedSlotsDirectoryPath = Path.Combine(_seedsDirectoryPath, AssignedSlotsDirectoryName);
                Directory.CreateDirectory(_assignedSlotsDirectoryPath);

                string activeSeedPath = Path.Combine(_seedsDirectoryPath, ActiveSeedFileName);
                _activeSeedPath = activeSeedPath;
                string survivalTemplatePath = Path.Combine(_seedsDirectoryPath, SurvivalTemplateFileName);

                EnsureDefaultSeedFile(activeSeedPath, RankedSeedDefinition.CreateDefaultActiveSeed());
                EnsureDefaultSeedFile(survivalTemplatePath, CreateSurvivalTemplateSeed());

                _activeSeed = LoadSeedFile(activeSeedPath, RankedSeedDefinition.CreateDefaultActiveSeed());
                _activeSeed.Normalize();
                bool upgradedActiveSeed = UpgradeSeedDefinitionIfNeeded(_activeSeed, enforceCurrentDefaultAlwaysValues: true);
                bool migratedCreativeSeed = MigrateLegacyCreativeSeedIfNeeded(_activeSeed);
                if (ShouldRefreshLegacyDefaultActiveSeedFile(_activeSeed))
                {
                    _activeSeed = RankedSeedDefinition.CreateDefaultActiveSeed();
                    _activeSeed.Normalize();
                    SaveSeedFile(activeSeedPath, _activeSeed);
                }
                else if (migratedCreativeSeed || upgradedActiveSeed)
                {
                    SaveSeedFile(activeSeedPath, _activeSeed);
                }

                RankedSeedDefinition survivalTemplate = LoadSeedFile(survivalTemplatePath, CreateSurvivalTemplateSeed());
                survivalTemplate.Normalize();
                bool upgradedSurvivalTemplate = UpgradeSeedDefinitionIfNeeded(survivalTemplate, enforceCurrentDefaultAlwaysValues: true);
                if (ShouldRefreshLegacyDefaultSurvivalTemplateFile(survivalTemplate))
                {
                    survivalTemplate = CreateSurvivalTemplateSeed();
                    survivalTemplate.Normalize();
                    SaveSeedFile(survivalTemplatePath, survivalTemplate);
                }
                else if (upgradedSurvivalTemplate)
                {
                    SaveSeedFile(survivalTemplatePath, survivalTemplate);
                }

                _rollContext = new RankedSeedRollContext(_activeSeed);
                _activeProfile = RankedSeedRuntimeProfile.Create(_activeSeed);
                _creativeSpawnResolved = false;
                _survivalSpawnResolved = false;
                _initialized = true;

                RankedLog.Info("Seed system initialized from '" + _seedsDirectoryPath + "'.");
                RankedLog.Info("Active seed id: '" + GetActiveSeedId() + "'.");
                RankedLog.Info("Active seed value: '" + GetActiveSeedValue() + "'.");
            }
        }

        public static bool EnsureSeedForSaveContext(string saveSlot, GameMode mode, bool continueMode)
        {
            lock (Sync)
            {
                if (!_initialized || string.IsNullOrEmpty(saveSlot) || !IsRealSaveSlot(saveSlot) || !IsSupportedSeedMode(mode))
                {
                    return false;
                }

                if (string.Equals(_activeAssignedSlot, saveSlot, StringComparison.OrdinalIgnoreCase) && _activeAssignedMode == mode)
                {
                    return false;
                }

                string slotSeedPath = GetSlotSeedFilePath(saveSlot, mode);
                string legacySlotSeedPath = GetLegacySlotSeedFilePath(saveSlot);
                bool createdSlotSeed = false;

                RankedSeedDefinition slotSeed;
                if (File.Exists(slotSeedPath))
                {
                    slotSeed = LoadSeedFile(slotSeedPath, CreateSeedForSlot(saveSlot, mode, continueMode));
                }
                else if (File.Exists(legacySlotSeedPath))
                {
                    slotSeed = LoadSeedFile(legacySlotSeedPath, CreateSeedForSlot(saveSlot, mode, continueMode));
                    SaveSeedFile(slotSeedPath, slotSeed);
                }
                else
                {
                    slotSeed = CreateSeedForSlot(saveSlot, mode, continueMode);
                    SaveSeedFile(slotSeedPath, slotSeed);
                    createdSlotSeed = true;
                }

                bool upgraded = UpgradeSeedDefinitionIfNeeded(slotSeed, enforceCurrentDefaultAlwaysValues: false);
                bool migratedLegacy = MigrateLegacyCreativeSeedIfNeeded(slotSeed);
                if (upgraded || migratedLegacy)
                {
                    SaveSeedFile(slotSeedPath, slotSeed);
                }

                SetActiveSeed(slotSeed);
                _activeAssignedSlot = saveSlot;
                _activeAssignedMode = mode;
                SaveSeedFile(_activeSeedPath, _activeSeed);

                if (createdSlotSeed)
                {
                    RankedLog.Info(
                        "Assigned new " +
                        mode +
                        " slot seed '" +
                        _activeSeed.SeedId +
                        "' / '" +
                        _activeSeed.SeedValue +
                        "' to save slot '" +
                        saveSlot +
                        "'.");
                }
                else
                {
                    RankedLog.Info(
                        "Loaded persisted " +
                        mode +
                        " slot seed '" +
                        _activeSeed.SeedId +
                        "' / '" +
                        _activeSeed.SeedValue +
                        "' for save slot '" +
                        saveSlot +
                        "'.");
                }

                return true;
            }
        }

        public static void ResetSessionSelections()
        {
            lock (Sync)
            {
                _creativeSpawnResolved = false;
                _creativeSpawnLabel = string.Empty;
                _survivalSpawnResolved = false;
                _survivalSpawnLabel = string.Empty;
            }
        }

        public static bool TryGetCreativeSpawnCoordinates(out float x, out float z, out string description)
        {
            lock (Sync)
            {
                x = 0f;
                z = 0f;
                description = string.Empty;

                if (!_initialized || _activeSeed == null || _activeSeed.Creative == null)
                {
                    return false;
                }

                RankedCreativeSeedDefinition creative = _activeSeed.Creative;
                creative.Normalize();

                if (!_creativeSpawnResolved)
                {
                    ResolveCreativeSpawn(creative);
                    _creativeSpawnResolved = true;
                    string rangeSuffix = string.IsNullOrEmpty(_creativeSpawnLabel) ? "." : " using range '" + _creativeSpawnLabel + "'.";
                    RankedLog.Info(
                        "Resolved Creative spawn from seed '" + GetActiveSeedId() +
                        "' at X=" + _creativeSpawnX.ToString("0.###", CultureInfo.InvariantCulture) +
                        ", Z=" + _creativeSpawnZ.ToString("0.###", CultureInfo.InvariantCulture) +
                        rangeSuffix);
                }

                x = _creativeSpawnX;
                z = _creativeSpawnZ;
                description = GetCreativeSpawnDescription(creative, x, z);
                return true;
            }
        }

        public static RankedSeedRuntimeProfile GetActiveProfile()
        {
            lock (Sync)
            {
                return _activeProfile;
            }
        }

        public static bool TryGetSurvivalSpawnCoordinates(out float x, out float z, out string description)
        {
            lock (Sync)
            {
                x = 0f;
                z = 0f;
                description = string.Empty;

                if (!_initialized || _activeSeed == null || _activeSeed.Survival == null || _activeSeed.Survival.Spawn == null)
                {
                    return false;
                }

                RankedSurvivalSpawnDefinition survivalSpawn = _activeSeed.Survival.Spawn;
                survivalSpawn.Normalize();

                if (!_survivalSpawnResolved)
                {
                    ResolveSurvivalSpawn(survivalSpawn);
                    _survivalSpawnResolved = true;
                    string rangeSuffix = string.IsNullOrEmpty(_survivalSpawnLabel) ? "." : " using range '" + _survivalSpawnLabel + "'.";
                    RankedLog.Info(
                        "Resolved Survival spawn from seed '" + GetActiveSeedId() +
                        "' at X=" + _survivalSpawnX.ToString("0.###", CultureInfo.InvariantCulture) +
                        ", Z=" + _survivalSpawnZ.ToString("0.###", CultureInfo.InvariantCulture) +
                        rangeSuffix);
                }

                x = _survivalSpawnX;
                z = _survivalSpawnZ;
                description = GetSurvivalSpawnDescription(survivalSpawn, x, z);
                return true;
            }
        }

        public static string GetActiveSeedId()
        {
            return _activeSeed == null || string.IsNullOrEmpty(_activeSeed.SeedId)
                ? "unknown-seed"
                : _activeSeed.SeedId;
        }

        public static string GetActiveSeedValue()
        {
            return _activeSeed == null || string.IsNullOrEmpty(_activeSeed.SeedValue)
                ? "unknown-seed-value"
                : _activeSeed.SeedValue;
        }

        private static RankedSeedDefinition CreateSeedForSlot(string saveSlot, GameMode mode, bool continueMode)
        {
            RankedSeedDefinition seedDefinition = RankedSeedDefinition.CreateDefaultActiveSeed();
            seedDefinition.SeedId = GetSeedIdForMode(mode);
            seedDefinition.SeedValue = BuildSeedValue(saveSlot, mode, continueMode);
            seedDefinition.Description = (continueMode ? "Migrated" : "Generated") + " " + mode + " seed for save slot '" + saveSlot + "'.";
            seedDefinition.Creative = RankedCreativeSeedDefinition.CreateDefault();
            seedDefinition.Survival = RankedSurvivalSeedDefinition.CreateTemplate();
            seedDefinition.Normalize();
            return seedDefinition;
        }

        private static string BuildSeedValue(string saveSlot, GameMode mode, bool continueMode)
        {
            string modeToken = mode.ToString().ToLowerInvariant();
            string slotToken = SanitizeToken(saveSlot);
            string stateToken = continueMode ? "migrated" : "fresh";
            string timestampToken = DateTime.UtcNow.ToString("yyyyMMddHHmmssfff", CultureInfo.InvariantCulture);
            string uniqueToken = Guid.NewGuid().ToString("N").Substring(0, 8);
            return modeToken + "-" + stateToken + "-" + slotToken + "-" + timestampToken + "-" + uniqueToken;
        }

        private static string GetSeedIdForMode(GameMode mode)
        {
            switch (mode)
            {
                case GameMode.Creative:
                    return "Creative-Singleplayer";
                case GameMode.Hardcore:
                    return "Hardcore-Singleplayer";
                case GameMode.Survival:
                default:
                    return "Survival-Singleplayer";
            }
        }

        private static string SanitizeToken(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return "slot";
            }

            StringBuilder builder = new StringBuilder(value.Length);
            for (int i = 0; i < value.Length; i++)
            {
                char character = value[i];
                if (char.IsLetterOrDigit(character))
                {
                    builder.Append(char.ToLowerInvariant(character));
                }
                else if (character == '-' || character == '_')
                {
                    builder.Append(character);
                }
            }

            return builder.Length == 0 ? "slot" : builder.ToString();
        }

        private static string GetSlotSeedFilePath(string saveSlot, GameMode mode)
        {
            return Path.Combine(
                _assignedSlotsDirectoryPath,
                SanitizeToken(saveSlot) + "__" + SanitizeToken(GetSeedIdForMode(mode)) + ".xml");
        }

        private static string GetLegacySlotSeedFilePath(string saveSlot)
        {
            return Path.Combine(_assignedSlotsDirectoryPath, SanitizeToken(saveSlot) + ".xml");
        }

        private static void SetActiveSeed(RankedSeedDefinition seedDefinition)
        {
            _activeSeed = seedDefinition ?? RankedSeedDefinition.CreateDefaultActiveSeed();
            _activeSeed.Normalize();
            _rollContext = new RankedSeedRollContext(_activeSeed);
            _activeProfile = RankedSeedRuntimeProfile.Create(_activeSeed);
            _creativeSpawnResolved = false;
            _creativeSpawnLabel = string.Empty;
            _survivalSpawnResolved = false;
            _survivalSpawnLabel = string.Empty;
        }

        private static RankedSeedDefinition CreateSurvivalTemplateSeed()
        {
            return new RankedSeedDefinition
            {
                SeedId = "Survival-Template",
                SeedValue = "survival-template-default",
                Description = "Template file for ranked Survival seed tuning. Edit multipliers after confirming exact route goals.",
                Creative = RankedCreativeSeedDefinition.CreateDefault(),
                Survival = RankedSurvivalSeedDefinition.CreateTemplate()
            };
        }

        private static void EnsureDefaultSeedFile(string path, RankedSeedDefinition seedDefinition)
        {
            if (File.Exists(path))
            {
                return;
            }

            SaveSeedFile(path, seedDefinition);
        }

        private static RankedSeedDefinition LoadSeedFile(string path, RankedSeedDefinition fallback)
        {
            try
            {
                using (FileStream stream = File.OpenRead(path))
                {
                    RankedSeedDefinition value = SeedSerializer.Deserialize(stream) as RankedSeedDefinition;
                    if (value != null)
                    {
                        value.Normalize();
                    }

                    return value ?? fallback;
                }
            }
            catch (Exception ex)
            {
                RankedLog.Error("Failed to load seed file at '" + path + "'. Falling back to defaults.", ex);
                fallback.Normalize();
                return fallback;
            }
        }

        private static void SaveSeedFile(string path, RankedSeedDefinition seedDefinition)
        {
            try
            {
                if (seedDefinition != null)
                {
                    seedDefinition.Normalize();
                }

                using (FileStream stream = File.Create(path))
                {
                    SeedSerializer.Serialize(stream, seedDefinition);
                }
            }
            catch (Exception ex)
            {
                RankedLog.Error("Failed to write default seed file at '" + path + "'.", ex);
            }
        }

        private static void ResolveCreativeSpawn(RankedCreativeSeedDefinition creative)
        {
            _creativeSpawnLabel = string.Empty;

            if (creative.SpawnMode == RankedCreativeSpawnMode.WeightedRanges && creative.Ranges != null && creative.Ranges.Count > 0)
            {
                RankedCreativeSpawnRangeDefinition range = SelectWeightedRange("creative-spawn-range", creative.Ranges);
                if (range != null)
                {
                    _creativeSpawnLabel = range.Name ?? string.Empty;
                    _creativeSpawnX = NextFloat("creative-spawn-x|" + _creativeSpawnLabel, range.MinX, range.MaxX);
                    _creativeSpawnZ = NextFloat("creative-spawn-z|" + _creativeSpawnLabel, range.MinZ, range.MaxZ);
                    return;
                }
            }

            if (creative.SpawnMode == RankedCreativeSpawnMode.RandomRange)
            {
                _creativeSpawnX = NextFloat("creative-spawn-x", creative.MinX, creative.MaxX);
                _creativeSpawnZ = NextFloat("creative-spawn-z", creative.MinZ, creative.MaxZ);
                return;
            }

            _creativeSpawnX = creative.FixedX;
            _creativeSpawnZ = creative.FixedZ;
        }

        private static void ResolveSurvivalSpawn(RankedSurvivalSpawnDefinition survival)
        {
            _survivalSpawnLabel = string.Empty;

            if (survival.SpawnMode == RankedCreativeSpawnMode.WeightedRanges && survival.Ranges != null && survival.Ranges.Count > 0)
            {
                RankedCreativeSpawnRangeDefinition range = SelectWeightedRange("survival-spawn-range", survival.Ranges);
                if (range != null)
                {
                    _survivalSpawnLabel = range.Name ?? string.Empty;
                    _survivalSpawnX = NextFloat("survival-spawn-x|" + _survivalSpawnLabel, range.MinX, range.MaxX);
                    _survivalSpawnZ = NextFloat("survival-spawn-z|" + _survivalSpawnLabel, range.MinZ, range.MaxZ);
                    return;
                }
            }

            if (survival.Ranges != null && survival.Ranges.Count > 0)
            {
                RankedCreativeSpawnRangeDefinition fallbackRange = survival.Ranges[0];
                if (fallbackRange != null)
                {
                    _survivalSpawnLabel = fallbackRange.Name ?? string.Empty;
                    _survivalSpawnX = NextFloat("survival-fallback-spawn-x|" + _survivalSpawnLabel, fallbackRange.MinX, fallbackRange.MaxX);
                    _survivalSpawnZ = NextFloat("survival-fallback-spawn-z|" + _survivalSpawnLabel, fallbackRange.MinZ, fallbackRange.MaxZ);
                    return;
                }
            }

            _survivalSpawnX = -125f;
            _survivalSpawnZ = 75f;
        }

        private static RankedCreativeSpawnRangeDefinition SelectWeightedRange(string scope, System.Collections.Generic.List<RankedCreativeSpawnRangeDefinition> ranges)
        {
            if (_rollContext == null)
            {
                return ranges != null && ranges.Count > 0 ? ranges[0] : null;
            }

            return _rollContext.SelectWeightedRange(scope, ranges, range => range.Weight);
        }

        private static bool MigrateLegacyCreativeSeedIfNeeded(RankedSeedDefinition seedDefinition)
        {
            if (seedDefinition == null || !string.Equals(seedDefinition.SeedId, RankedSeedDefinition.LegacyDefaultActiveSeedId, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            seedDefinition.SeedId = RankedSeedDefinition.DefaultActiveSeedId;
            if (string.IsNullOrEmpty(seedDefinition.SeedValue) ||
                string.Equals(seedDefinition.SeedValue, RankedSeedDefinition.LegacyDefaultActiveSeedValue, StringComparison.OrdinalIgnoreCase))
            {
                seedDefinition.SeedValue = RankedSeedDefinition.DefaultActiveSeedValue;
            }

            if (string.IsNullOrEmpty(seedDefinition.Description) ||
                seedDefinition.Description.IndexOf("Creative test seed", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                seedDefinition.Description = RankedSeedDefinition.DefaultActiveSeedDescription;
            }

            return true;
        }

        private static bool ShouldRefreshLegacyDefaultActiveSeedFile(RankedSeedDefinition seedDefinition)
        {
            if (seedDefinition == null || !IsDefaultActiveSeedId(seedDefinition.SeedId))
            {
                return false;
            }

            if (IsLegacyStructuredSurvivalDefinition(seedDefinition.Survival))
            {
                return true;
            }

            return !MatchesCurrentCreativeDefault(seedDefinition.Creative);
        }

        private static bool ShouldRefreshLegacyDefaultSurvivalTemplateFile(RankedSeedDefinition seedDefinition)
        {
            if (seedDefinition == null || !string.Equals(seedDefinition.SeedId, "Survival-Template", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return IsLegacyStructuredSurvivalDefinition(seedDefinition.Survival);
        }

        private static bool IsLegacyStructuredSurvivalDefinition(RankedSurvivalSeedDefinition survival)
        {
            if (survival == null)
            {
                return true;
            }

            if (survival.TemplateVersion < RankedSurvivalSeedDefinition.CurrentTemplateVersion)
            {
                return true;
            }

            if (survival.Spawn == null)
            {
                return true;
            }

            if (survival.Defaults == null)
            {
                return true;
            }

            if (survival.Fragments == null || survival.Fragments.Count == 0)
            {
                return true;
            }

            if (survival.Resources == null || survival.Resources.Count == 0)
            {
                return true;
            }

            if (survival.Creatures == null || survival.Creatures.Count == 0)
            {
                return true;
            }

            if (survival.Biomes == null || survival.Biomes.Count == 0)
            {
                return true;
            }

            if (survival.Always == null || survival.Always.Count == 0)
            {
                return true;
            }

            if (survival.AlwaysBiomeMultipliers == null || survival.AlwaysBiomeMultipliers.Count == 0)
            {
                return true;
            }

            if (survival.AlwaysBiomeTechMultipliers == null || survival.AlwaysBiomeTechMultipliers.Count == 0)
            {
                return true;
            }

            return survival.ManualCreatureSpawns == null || survival.ManualCreatureSpawns.Count == 0;
        }

        private static bool MatchesCurrentCreativeDefault(RankedCreativeSeedDefinition creative)
        {
            if (creative == null)
            {
                return false;
            }

            return creative.SpawnMode == RankedCreativeSpawnMode.RandomRange &&
                   ApproximatelyEquals(creative.MinX, -100f) &&
                   ApproximatelyEquals(creative.MaxX, -30f) &&
                   ApproximatelyEquals(creative.MinZ, -430f) &&
                   ApproximatelyEquals(creative.MaxZ, -330f);
        }

        private static bool UpgradeSeedDefinitionIfNeeded(RankedSeedDefinition seedDefinition, bool enforceCurrentDefaultAlwaysValues)
        {
            if (seedDefinition == null)
            {
                return false;
            }

            bool changed = false;

            if (string.IsNullOrEmpty(seedDefinition.SeedValue))
            {
                seedDefinition.SeedValue = seedDefinition.SeedId + "-default";
                changed = true;
            }

            if (seedDefinition.Survival == null)
            {
                seedDefinition.Survival = RankedSurvivalSeedDefinition.CreateTemplate();
                changed = true;
            }

            if (seedDefinition.Survival.Defaults == null)
            {
                seedDefinition.Survival.Defaults = RankedSurvivalDefaultsDefinition.CreateDefault();
                changed = true;
            }

            bool isDefaultSeed =
                IsDefaultActiveSeedId(seedDefinition.SeedId) ||
                string.Equals(seedDefinition.SeedId, "Survival-Template", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(seedDefinition.SeedId, "survival-template", StringComparison.OrdinalIgnoreCase);

            int previousTemplateVersion = seedDefinition.Survival.TemplateVersion;
            if (isDefaultSeed && seedDefinition.Survival.TemplateVersion < RankedSurvivalSeedDefinition.CurrentTemplateVersion)
            {
                seedDefinition.Survival = RankedSurvivalSeedDefinition.CreateTemplate();
                changed = true;
            }
            else if (previousTemplateVersion < RankedSurvivalSeedDefinition.CurrentTemplateVersion)
            {
                if (ApplyCurrentSharedDefaults(seedDefinition.Survival.Defaults))
                {
                    changed = true;
                }
            }

            if (seedDefinition.Survival.TemplateVersion < RankedSurvivalSeedDefinition.CurrentTemplateVersion)
            {
                seedDefinition.Survival.TemplateVersion = RankedSurvivalSeedDefinition.CurrentTemplateVersion;
                changed = true;
            }

            if (seedDefinition.Survival.Always == null)
            {
                seedDefinition.Survival.Always = RankedSeedReferenceCatalog.CreateDefaultAlwaysEntries();
                changed = true;
            }

            if (seedDefinition.Survival.AlwaysBiomeMultipliers == null)
            {
                seedDefinition.Survival.AlwaysBiomeMultipliers = RankedSeedReferenceCatalog.CreateDefaultAlwaysBiomeEntries();
                changed = true;
            }

            if (seedDefinition.Survival.AlwaysBiomeTechMultipliers == null)
            {
                seedDefinition.Survival.AlwaysBiomeTechMultipliers = RankedSeedReferenceCatalog.CreateDefaultAlwaysBiomeTechEntries();
                changed = true;
            }

            if (seedDefinition.Survival.ManualCreatureSpawns == null)
            {
                seedDefinition.Survival.ManualCreatureSpawns = RankedSeedReferenceCatalog.CreateDefaultManualCreatureSpawns();
                changed = true;
            }

            int previousAlwaysCount = seedDefinition.Survival.Always.Count;
            RankedSeedReferenceCatalog.EnsureDefaultAlwaysEntries(seedDefinition.Survival.Always);
            if (seedDefinition.Survival.Always.Count != previousAlwaysCount)
            {
                changed = true;
            }

            int previousAlwaysBiomeCount = seedDefinition.Survival.AlwaysBiomeMultipliers.Count;
            RankedSeedReferenceCatalog.EnsureDefaultAlwaysBiomeEntries(seedDefinition.Survival.AlwaysBiomeMultipliers);
            if (seedDefinition.Survival.AlwaysBiomeMultipliers.Count != previousAlwaysBiomeCount)
            {
                changed = true;
            }

            int previousAlwaysBiomeTechCount = seedDefinition.Survival.AlwaysBiomeTechMultipliers.Count;
            RankedSeedReferenceCatalog.EnsureDefaultAlwaysBiomeTechEntries(seedDefinition.Survival.AlwaysBiomeTechMultipliers);
            if (seedDefinition.Survival.AlwaysBiomeTechMultipliers.Count != previousAlwaysBiomeTechCount)
            {
                changed = true;
            }

            int previousManualCreatureSpawnCount = seedDefinition.Survival.ManualCreatureSpawns.Count;
            RankedSeedReferenceCatalog.EnsureDefaultManualCreatureSpawnEntries(seedDefinition.Survival.ManualCreatureSpawns);
            if (seedDefinition.Survival.ManualCreatureSpawns.Count != previousManualCreatureSpawnCount)
            {
                changed = true;
            }

            if (enforceCurrentDefaultAlwaysValues && isDefaultSeed)
            {
                if (!MatchesAlwaysDefaults(seedDefinition.Survival.Always))
                {
                    seedDefinition.Survival.Always = RankedSeedReferenceCatalog.CreateDefaultAlwaysEntries();
                    changed = true;
                }
            }

            return changed;
        }

        private static bool MatchesAlwaysDefaults(System.Collections.Generic.List<RankedSpawnMultiplierEntry> entries)
        {
            System.Collections.Generic.List<RankedSpawnMultiplierEntry> defaults = RankedSeedReferenceCatalog.CreateDefaultAlwaysEntries();
            if (entries == null || entries.Count != defaults.Count)
            {
                return false;
            }

            for (int i = 0; i < defaults.Count; i++)
            {
                RankedSpawnMultiplierEntry defaultEntry = defaults[i];
                RankedSpawnMultiplierEntry match = null;
                for (int j = 0; j < entries.Count; j++)
                {
                    if (entries[j] != null && string.Equals(entries[j].Name, defaultEntry.Name, StringComparison.OrdinalIgnoreCase))
                    {
                        match = entries[j];
                        break;
                    }
                }

                if (match == null || !ApproximatelyEquals(match.ChanceMultiplier, defaultEntry.ChanceMultiplier))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool ApplyCurrentSharedDefaults(RankedSurvivalDefaultsDefinition defaults)
        {
            if (defaults == null)
            {
                return false;
            }

            RankedSurvivalDefaultsDefinition currentDefaults = RankedSurvivalDefaultsDefinition.CreateDefault();
            bool changed = false;

            if (defaults.DisableFishSchools != currentDefaults.DisableFishSchools)
            {
                defaults.DisableFishSchools = currentDefaults.DisableFishSchools;
                changed = true;
            }

            if (defaults.BlockCreaturesInPrisonAquarium != currentDefaults.BlockCreaturesInPrisonAquarium)
            {
                defaults.BlockCreaturesInPrisonAquarium = currentDefaults.BlockCreaturesInPrisonAquarium;
                changed = true;
            }

            if (defaults.RestrictStalkersToKelpForest != currentDefaults.RestrictStalkersToKelpForest)
            {
                defaults.RestrictStalkersToKelpForest = currentDefaults.RestrictStalkersToKelpForest;
                changed = true;
            }

            if (!ApproximatelyEquals(defaults.StalkerToothDropProbability, currentDefaults.StalkerToothDropProbability))
            {
                defaults.StalkerToothDropProbability = currentDefaults.StalkerToothDropProbability;
                changed = true;
            }

            if (defaults.FixBadMetal != currentDefaults.FixBadMetal)
            {
                defaults.FixBadMetal = currentDefaults.FixBadMetal;
                changed = true;
            }

            if (defaults.StalkerBitesDropTeeth != currentDefaults.StalkerBitesDropTeeth)
            {
                defaults.StalkerBitesDropTeeth = currentDefaults.StalkerBitesDropTeeth;
                changed = true;
            }

            if (defaults.ForceSecondGold != currentDefaults.ForceSecondGold)
            {
                defaults.ForceSecondGold = currentDefaults.ForceSecondGold;
                changed = true;
            }

            return changed;
        }

        private static float NextFloat(string scope, float minValue, float maxValue)
        {
            if (_rollContext == null)
            {
                return minValue;
            }

            return _rollContext.NextFloat(scope, minValue, maxValue);
        }

        private static string GetCreativeSpawnDescription(RankedCreativeSeedDefinition creative, float x, float z)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("Creative seed '");
            builder.Append(GetActiveSeedId());
            builder.Append("' ");

            if (creative.SpawnMode == RankedCreativeSpawnMode.WeightedRanges)
            {
                builder.Append("weighted spawn");
                if (!string.IsNullOrEmpty(_creativeSpawnLabel))
                {
                    builder.Append(" via ");
                    builder.Append(_creativeSpawnLabel);
                }
                builder.Append(" at X=");
                builder.Append(x.ToString("0.###", CultureInfo.InvariantCulture));
                builder.Append(", Z=");
                builder.Append(z.ToString("0.###", CultureInfo.InvariantCulture));
                return builder.ToString();
            }

            if (creative.SpawnMode == RankedCreativeSpawnMode.RandomRange)
            {
                builder.Append("random range spawn at X=");
                builder.Append(x.ToString("0.###", CultureInfo.InvariantCulture));
                builder.Append(", Z=");
                builder.Append(z.ToString("0.###", CultureInfo.InvariantCulture));
                return builder.ToString();
            }

            builder.Append("fixed spawn at X=");
            builder.Append(x.ToString("0.###", CultureInfo.InvariantCulture));
            builder.Append(", Z=");
            builder.Append(z.ToString("0.###", CultureInfo.InvariantCulture));
            return builder.ToString();
        }

        private static string GetSurvivalSpawnDescription(RankedSurvivalSpawnDefinition survival, float x, float z)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("Survival seed '");
            builder.Append(GetActiveSeedId());
            builder.Append("' ");
            builder.Append("weighted spawn");
            if (!string.IsNullOrEmpty(_survivalSpawnLabel))
            {
                builder.Append(" via ");
                builder.Append(_survivalSpawnLabel);
            }

            builder.Append(" at X=");
            builder.Append(x.ToString("0.###", CultureInfo.InvariantCulture));
            builder.Append(", Z=");
            builder.Append(z.ToString("0.###", CultureInfo.InvariantCulture));
            return builder.ToString();
        }

        private static bool ApproximatelyEquals(float left, float right)
        {
            return Math.Abs(left - right) <= 0.01f;
        }

        private static bool IsSupportedSeedMode(GameMode mode)
        {
            return mode == GameMode.Creative || mode == GameMode.Survival || mode == GameMode.Hardcore;
        }

        private static bool IsRealSaveSlot(string saveSlot)
        {
            return !string.IsNullOrEmpty(saveSlot) &&
                   saveSlot.StartsWith("slot", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsDefaultActiveSeedId(string seedId)
        {
            return string.Equals(seedId, RankedSeedDefinition.DefaultActiveSeedId, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(seedId, RankedSeedDefinition.LegacyDefaultActiveSeedId, StringComparison.OrdinalIgnoreCase);
        }
    }
}
