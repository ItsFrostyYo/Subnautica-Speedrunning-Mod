using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Xml.Serialization;

namespace SubnauticaSpeedrunningMod.Runtime.Seeds
{
    internal static class ModSeedStore
    {
        private const string SeedsDirectoryName = "Seeds";
        private const string AssignedSlotsDirectoryName = "AssignedSlots";
        private const string ActiveSeedFileName = "active-seed.xml";
        private const string SurvivalTemplateFileName = "survival-seed-template.xml";

        private static readonly object Sync = new object();
        private static readonly XmlSerializer SeedSerializer = new XmlSerializer(typeof(ModSeedDefinition));
        private static bool _initialized;
        private static string _seedsDirectoryPath = string.Empty;
        private static string _assignedSlotsDirectoryPath = string.Empty;
        private static string _activeSeedPath = string.Empty;
        private static ModSeedDefinition _activeSeed;
        private static ModSeedRuntimeProfile _activeProfile;
        private static ModSeedRollContext _rollContext;
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
        private static ModSeedDefinition _pendingExternalSeed;
        private static GameMode _pendingExternalSeedMode = GameMode.None;

        public static void Initialize(RuntimeContext context)
        {
            lock (Sync)
            {
                if (_initialized)
                {
                    return;
                }

                _seedsDirectoryPath = Path.Combine(Path.Combine(context.ModRoot, "Data"), SeedsDirectoryName);
                Directory.CreateDirectory(_seedsDirectoryPath);
                _assignedSlotsDirectoryPath = Path.Combine(_seedsDirectoryPath, AssignedSlotsDirectoryName);
                Directory.CreateDirectory(_assignedSlotsDirectoryPath);
                CleanupObsoleteSeedArtifacts();

                string activeSeedPath = Path.Combine(_seedsDirectoryPath, ActiveSeedFileName);
                _activeSeedPath = activeSeedPath;
                string survivalTemplatePath = Path.Combine(_seedsDirectoryPath, SurvivalTemplateFileName);

                EnsureDefaultSeedFile(activeSeedPath, ModSeedDefinition.CreateDefaultActiveSeed());
                EnsureDefaultSeedFile(survivalTemplatePath, CreateSurvivalTemplateSeed());

                _activeSeed = LoadSeedFile(activeSeedPath, ModSeedDefinition.CreateDefaultActiveSeed());
                _activeSeed.Normalize();
                bool upgradedActiveSeed = UpgradeSeedDefinitionIfNeeded(_activeSeed, enforceCurrentDefaultAlwaysValues: true);
                bool migratedCreativeSeed = MigrateLegacyCreativeSeedIfNeeded(_activeSeed);
                if (ShouldRefreshLegacyDefaultActiveSeedFile(_activeSeed))
                {
                    _activeSeed = ModSeedDefinition.CreateDefaultActiveSeed();
                    _activeSeed.Normalize();
                    SaveSeedFile(activeSeedPath, _activeSeed);
                }
                else if (migratedCreativeSeed || upgradedActiveSeed)
                {
                    SaveSeedFile(activeSeedPath, _activeSeed);
                }

                ModSeedDefinition survivalTemplate = LoadSeedFile(survivalTemplatePath, CreateSurvivalTemplateSeed());
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

                _rollContext = new ModSeedRollContext(_activeSeed);
                _activeProfile = ModSeedRuntimeProfile.Create(_activeSeed);
                _creativeSpawnResolved = false;
                _survivalSpawnResolved = false;
                _initialized = true;

                ModLog.Info("Seed system initialized from '" + _seedsDirectoryPath + "'.");
                ModLog.Info("Active seed id: '" + GetActiveSeedId() + "'.");
                ModLog.Info("Active seed value: '" + GetActiveSeedValue() + "'.");
            }
        }

        public static bool EnsureSeedForSaveContext(string saveSlot, GameMode mode, bool continueMode, bool createIfMissing)
        {
            lock (Sync)
            {
                if (!_initialized || string.IsNullOrEmpty(saveSlot) || !IsRealSaveSlot(saveSlot) || !IsSupportedSeedMode(mode))
                {
                    return false;
                }

                if (string.Equals(_activeAssignedSlot, saveSlot, StringComparison.OrdinalIgnoreCase) &&
                    _activeAssignedMode == mode)
                {
                    return false;
                }

                if (continueMode &&
                    string.Equals(_activeAssignedSlot, saveSlot, StringComparison.OrdinalIgnoreCase) &&
                    _activeAssignedMode == mode)
                {
                    return false;
                }

                string slotSeedPath = string.Empty;
                bool createdSlotSeed = false;
                bool forceFreshSlotSeed = !continueMode && createIfMissing;

                ModSeedDefinition slotSeed;
                if (forceFreshSlotSeed)
                {
                    DeleteExistingSlotSeedFiles(saveSlot);
                    slotSeed = CreateSeedForSlot(saveSlot, mode, continueMode: false);
                    MaterializeSeedDefinition(slotSeed);
                    slotSeedPath = GetSlotSeedFilePath(saveSlot, slotSeed.SeedId);
                    SaveSeedFile(slotSeedPath, slotSeed);
                    createdSlotSeed = true;
                }
                else if (TryLoadExistingSlotSeed(saveSlot, mode, continueMode, out slotSeed, out slotSeedPath))
                {
                }
                else if (!createIfMissing)
                {
                    return false;
                }
                else
                {
                    slotSeed = CreateSeedForSlot(saveSlot, mode, continueMode);
                    MaterializeSeedDefinition(slotSeed);
                    slotSeedPath = GetSlotSeedFilePath(saveSlot, slotSeed.SeedId);
                    SaveSeedFile(slotSeedPath, slotSeed);
                    createdSlotSeed = true;
                }

                bool upgraded = UpgradeSeedDefinitionIfNeeded(slotSeed, enforceCurrentDefaultAlwaysValues: false);
                bool migratedLegacy = MigrateLegacyCreativeSeedIfNeeded(slotSeed);
                bool materialized = MaterializeSeedDefinition(slotSeed);
                if (upgraded || migratedLegacy || materialized)
                {
                    SaveSeedFile(slotSeedPath, slotSeed);
                }

                SetActiveSeed(slotSeed);
                _activeAssignedSlot = saveSlot;
                _activeAssignedMode = mode;
                SaveSeedFile(_activeSeedPath, _activeSeed);

                if (createdSlotSeed)
                {
                    ModLog.Info(
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
                    ModLog.Info(
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
                _activeAssignedSlot = string.Empty;
                _activeAssignedMode = GameMode.None;
                _creativeSpawnResolved = false;
                _creativeSpawnLabel = string.Empty;
                _survivalSpawnResolved = false;
                _survivalSpawnLabel = string.Empty;
            }
        }

        public static void PreparePendingExternalSeed(GameMode mode, string seedId, string seedValue, string description)
        {
            lock (Sync)
            {
                if (!_initialized || !IsSupportedSeedMode(mode))
                {
                    return;
                }

                ModSeedDefinition seedDefinition = ModSeedDefinition.CreateDefaultActiveSeed();
                seedDefinition.SeedId = string.IsNullOrEmpty(seedId) ? GetSeedIdForMode(mode) : seedId;
                seedDefinition.SeedValue = string.IsNullOrEmpty(seedValue) ? seedDefinition.SeedId + "-match" : seedValue;
                seedDefinition.Description = string.IsNullOrEmpty(description) ? ("External " + mode + " multiplayer seed.") : description;
                seedDefinition.Creative = ModCreativeSeedDefinition.CreateDefault();
                seedDefinition.Survival = ModSurvivalSeedDefinition.CreateTemplate();
                seedDefinition.Normalize();
                _pendingExternalSeed = seedDefinition;
                _pendingExternalSeedMode = mode;
                ModLog.Info("Prepared pending external seed '" + seedDefinition.SeedId + "' / '" + seedDefinition.SeedValue + "' for mode '" + mode + "'.");
            }
        }

        public static bool HasActiveSeedAssignment()
        {
            lock (Sync)
            {
                return _initialized &&
                    !string.IsNullOrEmpty(_activeAssignedSlot) &&
                    _activeAssignedMode != GameMode.None;
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

                ModCreativeSeedDefinition creative = _activeSeed.Creative;
                creative.Normalize();

                if (!_creativeSpawnResolved)
                {
                    ResolveCreativeSpawn(creative);
                    _creativeSpawnResolved = true;
                    string rangeSuffix = string.IsNullOrEmpty(_creativeSpawnLabel) ? "." : " using range '" + _creativeSpawnLabel + "'.";
                    ModLog.Info(
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

        public static ModSeedRuntimeProfile GetActiveProfile()
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

                ModSurvivalSpawnDefinition survivalSpawn = _activeSeed.Survival.Spawn;
                survivalSpawn.Normalize();

                if (!_survivalSpawnResolved)
                {
                    ResolveSurvivalSpawn(survivalSpawn);
                    _survivalSpawnResolved = true;
                    string rangeSuffix = string.IsNullOrEmpty(_survivalSpawnLabel) ? "." : " using range '" + _survivalSpawnLabel + "'.";
                    ModLog.Info(
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

        public static bool IsSeedContextActive(string saveSlot, GameMode mode)
        {
            lock (Sync)
            {
                if (!_initialized || string.IsNullOrEmpty(saveSlot))
                {
                    return false;
                }

                return string.Equals(_activeAssignedSlot, saveSlot, StringComparison.OrdinalIgnoreCase) &&
                       _activeAssignedMode == mode;
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

        private static ModSeedDefinition CreateSeedForSlot(string saveSlot, GameMode mode, bool continueMode)
        {
            if (_pendingExternalSeed != null && _pendingExternalSeedMode == mode)
            {
                ModSeedDefinition externalSeed = CloneSeedDefinition(_pendingExternalSeed);
                _pendingExternalSeed = null;
                _pendingExternalSeedMode = GameMode.None;
                externalSeed.Normalize();
                return externalSeed;
            }

            ModSeedDefinition seedDefinition = ModSeedDefinition.CreateDefaultActiveSeed();
            seedDefinition.SeedId = GetSeedIdForMode(mode);
            seedDefinition.SeedValue = BuildSeedValue(saveSlot, mode, continueMode);
            seedDefinition.Description =
                (continueMode ? "Migrated" : "Generated") + " " +
                mode +
                " ranked singleplayer seed for save slot '" +
                saveSlot +
                "'.";
            seedDefinition.Creative = ModCreativeSeedDefinition.CreateDefault();
            seedDefinition.Survival = ModSurvivalSeedDefinition.CreateTemplate();
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

        private static string GetSlotSeedFilePath(string saveSlot, string seedId)
        {
            return Path.Combine(
                _assignedSlotsDirectoryPath,
                SanitizeToken(saveSlot) + "__" + SanitizeToken(seedId) + ".xml");
        }

        private static string GetLegacySlotSeedFilePath(string saveSlot)
        {
            return Path.Combine(_assignedSlotsDirectoryPath, SanitizeToken(saveSlot) + ".xml");
        }

        private static bool TryLoadExistingSlotSeed(string saveSlot, GameMode mode, bool continueMode, out ModSeedDefinition seedDefinition, out string seedPath)
        {
            seedDefinition = null;
            seedPath = string.Empty;

            string resolvedSeedPath;
            if (TryResolveExistingSlotSeedPath(saveSlot, mode, out resolvedSeedPath))
            {
                seedPath = resolvedSeedPath;
                seedDefinition = LoadSeedFile(seedPath, CreateSeedForSlot(saveSlot, mode, continueMode));
                return true;
            }

            string legacySlotSeedPath = GetLegacySlotSeedFilePath(saveSlot);
            if (File.Exists(legacySlotSeedPath))
            {
                seedDefinition = LoadSeedFile(legacySlotSeedPath, CreateSeedForSlot(saveSlot, mode, continueMode));
                seedPath = GetSlotSeedFilePath(saveSlot, seedDefinition.SeedId);
                SaveSeedFile(seedPath, seedDefinition);
                return true;
            }

            return false;
        }

        private static bool TryResolveExistingSlotSeedPath(string saveSlot, GameMode mode, out string seedPath)
        {
            seedPath = string.Empty;

            string slotToken = SanitizeToken(saveSlot);
            string exactPath = GetSlotSeedFilePath(saveSlot, GetSeedIdForMode(mode));
            if (File.Exists(exactPath))
            {
                seedPath = exactPath;
                return true;
            }

            string[] matchingFiles = Directory.GetFiles(_assignedSlotsDirectoryPath, slotToken + "__*.xml");
            for (int i = 0; i < matchingFiles.Length; i++)
            {
                string candidatePath = matchingFiles[i];
                ModSeedDefinition candidateSeed = LoadSeedFile(candidatePath, null);
                if (candidateSeed == null)
                {
                    continue;
                }

                candidateSeed.Normalize();
                if (DoesSeedIdMatchMode(candidateSeed.SeedId, mode))
                {
                    seedPath = candidatePath;
                    return true;
                }
            }

            return false;
        }

        private static bool DoesSeedIdMatchMode(string seedId, GameMode mode)
        {
            if (string.Equals(seedId, GetSeedIdForMode(mode), StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            switch (mode)
            {
                case GameMode.Creative:
                    return string.Equals(seedId, "Creative-Multiplayer", StringComparison.OrdinalIgnoreCase);
                case GameMode.Survival:
                    return string.Equals(seedId, "Survival-Multiplayer", StringComparison.OrdinalIgnoreCase);
                case GameMode.Hardcore:
                    return string.Equals(seedId, "Hardcore-Multiplayer", StringComparison.OrdinalIgnoreCase);
                default:
                    return false;
            }
        }

        private static void DeleteExistingSlotSeedFiles(string saveSlot)
        {
            if (string.IsNullOrEmpty(saveSlot) || !Directory.Exists(_assignedSlotsDirectoryPath))
            {
                return;
            }

            string slotToken = SanitizeToken(saveSlot);
            string[] matchingFiles = Directory.GetFiles(_assignedSlotsDirectoryPath, slotToken + "__*.xml");
            for (int i = 0; i < matchingFiles.Length; i++)
            {
                string path = matchingFiles[i];
                try
                {
                    File.Delete(path);
                    ModLog.Info("Deleted stale slot seed file '" + Path.GetFileName(path) + "' before creating a fresh modded save seed.");
                }
                catch (Exception ex)
                {
                    ModLog.Warn("Failed to delete stale slot seed file '" + path + "': " + ex.Message);
                }
            }

            string legacyPath = GetLegacySlotSeedFilePath(saveSlot);
            if (!string.IsNullOrEmpty(legacyPath) && File.Exists(legacyPath))
            {
                try
                {
                    File.Delete(legacyPath);
                    ModLog.Info("Deleted stale legacy slot seed file '" + Path.GetFileName(legacyPath) + "' before creating a fresh modded save seed.");
                }
                catch (Exception ex)
                {
                    ModLog.Warn("Failed to delete stale legacy slot seed file '" + legacyPath + "': " + ex.Message);
                }
            }
        }

        private static void SetActiveSeed(ModSeedDefinition seedDefinition)
        {
            _activeSeed = seedDefinition ?? ModSeedDefinition.CreateDefaultActiveSeed();
            _activeSeed.Normalize();
            _rollContext = new ModSeedRollContext(_activeSeed);
            _activeProfile = ModSeedRuntimeProfile.Create(_activeSeed);
            _creativeSpawnResolved = false;
            _creativeSpawnLabel = string.Empty;
            _survivalSpawnResolved = false;
            _survivalSpawnLabel = string.Empty;
        }

        private static ModSeedDefinition CreateSurvivalTemplateSeed()
        {
            return new ModSeedDefinition
            {
                SeedId = "Survival-Template",
                SeedValue = "survival-template-default",
                Description = "Template file for ranked Survival seed tuning. Edit multipliers after confirming exact route goals.",
                Creative = ModCreativeSeedDefinition.CreateDefault(),
                Survival = ModSurvivalSeedDefinition.CreateTemplate()
            };
        }

        private static ModSeedDefinition CloneSeedDefinition(ModSeedDefinition seedDefinition)
        {
            if (seedDefinition == null)
            {
                return ModSeedDefinition.CreateDefaultActiveSeed();
            }

            using (MemoryStream stream = new MemoryStream())
            {
                SeedSerializer.Serialize(stream, seedDefinition);
                stream.Position = 0L;
                ModSeedDefinition clone = SeedSerializer.Deserialize(stream) as ModSeedDefinition;
                return clone ?? ModSeedDefinition.CreateDefaultActiveSeed();
            }
        }

        private static void EnsureDefaultSeedFile(string path, ModSeedDefinition seedDefinition)
        {
            if (File.Exists(path))
            {
                return;
            }

            SaveSeedFile(path, seedDefinition);
        }

        private static ModSeedDefinition LoadSeedFile(string path, ModSeedDefinition fallback)
        {
            try
            {
                using (FileStream stream = File.OpenRead(path))
                {
                    ModSeedDefinition value = SeedSerializer.Deserialize(stream) as ModSeedDefinition;
                    if (value != null)
                    {
                        value.Normalize();
                    }

                    return value ?? fallback;
                }
            }
            catch (Exception ex)
            {
                ModLog.Error("Failed to load seed file at '" + path + "'. Falling back to defaults.", ex);
                if (fallback != null)
                {
                    fallback.Normalize();
                }

                return fallback;
            }
        }

        private static void SaveSeedFile(string path, ModSeedDefinition seedDefinition)
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
                ModLog.Error("Failed to write default seed file at '" + path + "'.", ex);
            }
        }

        private static void CleanupObsoleteSeedArtifacts()
        {
            TryDeleteObsoletePath(Path.Combine(_seedsDirectoryPath, "pending-shared-seed.xml"), recursive: false);
            TryDeleteObsoletePath(Path.Combine(_seedsDirectoryPath, "last-consumed-shared-seed.xml"), recursive: false);
            TryDeleteObsoletePath(Path.Combine(_seedsDirectoryPath, "Surveys"), recursive: true);

            if (!Directory.Exists(_assignedSlotsDirectoryPath))
            {
                return;
            }

            string[] obsoleteBetterRngFiles = Directory.GetFiles(_assignedSlotsDirectoryPath, "*__betterrng-*.xml");
            for (int i = 0; i < obsoleteBetterRngFiles.Length; i++)
            {
                TryDeleteObsoletePath(obsoleteBetterRngFiles[i], recursive: false);
            }
        }

        private static void TryDeleteObsoletePath(string path, bool recursive)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path) && !Directory.Exists(path))
            {
                return;
            }

            try
            {
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, recursive);
                }
                else
                {
                    File.Delete(path);
                }
            }
            catch (Exception ex)
            {
                ModLog.Warn("Unable to delete obsolete seed artifact '" + path + "': " + ex.Message);
            }
        }

        private static bool MaterializeSeedDefinition(ModSeedDefinition seedDefinition)
        {
            if (seedDefinition == null)
            {
                return false;
            }

            seedDefinition.Normalize();
            bool changed = false;
            ModSeedRollContext rollContext = new ModSeedRollContext(seedDefinition);

            changed |= NormalizeSingleplayerSurvivalSpawn(seedDefinition.Survival == null ? null : seedDefinition.Survival.Spawn);
            changed |= MaterializeSpawnEntries(seedDefinition.Survival == null ? null : seedDefinition.Survival.Fragments, "Fragments", rollContext);
            changed |= MaterializeSpawnEntries(seedDefinition.Survival == null ? null : seedDefinition.Survival.Resources, "Resources", rollContext);
            changed |= MaterializeSpawnEntries(seedDefinition.Survival == null ? null : seedDefinition.Survival.Creatures, "Creatures", rollContext);
            changed |= MaterializeSpawnEntries(seedDefinition.Survival == null ? null : seedDefinition.Survival.Always, "Always", rollContext);
            changed |= MaterializeBiomeEntries(seedDefinition.Survival == null ? null : seedDefinition.Survival.Biomes, "Biomes", rollContext);
            changed |= MaterializeBiomeEntries(seedDefinition.Survival == null ? null : seedDefinition.Survival.AlwaysBiomeMultipliers, "AlwaysBiomes", rollContext);
            changed |= MaterializeManualCreatureSpawnEntries(seedDefinition.Survival == null ? null : seedDefinition.Survival.ManualCreatureSpawns, rollContext);

            return changed;
        }

        private static bool NormalizeSingleplayerSurvivalSpawn(ModSurvivalSpawnDefinition survivalSpawn)
        {
            if (survivalSpawn == null)
            {
                return false;
            }

            bool changed = false;
            survivalSpawn.Normalize();

            List<ModCreativeSpawnRangeDefinition> clipCRanges = new List<ModCreativeSpawnRangeDefinition>();
            for (int i = 0; i < survivalSpawn.Ranges.Count; i++)
            {
                ModCreativeSpawnRangeDefinition range = survivalSpawn.Ranges[i];
                if (range != null && string.Equals(range.Name, "Clip C", StringComparison.OrdinalIgnoreCase))
                {
                    clipCRanges.Add(range);
                }
            }

            if (clipCRanges.Count > 0 && clipCRanges.Count != survivalSpawn.Ranges.Count)
            {
                survivalSpawn.Ranges = clipCRanges;
                changed = true;
            }

            if (survivalSpawn.Ranges.Count == 1 && survivalSpawn.Ranges[0] != null && !ApproximatelyEquals(survivalSpawn.Ranges[0].Weight, 1f))
            {
                survivalSpawn.Ranges[0].Weight = 1f;
                changed = true;
            }

            if (survivalSpawn.SpawnMode != ModCreativeSpawnMode.WeightedRanges)
            {
                survivalSpawn.SpawnMode = ModCreativeSpawnMode.WeightedRanges;
                changed = true;
            }

            return changed;
        }

        private static bool MaterializeSpawnEntries(List<ModSpawnMultiplierEntry> entries, string groupName, ModSeedRollContext rollContext)
        {
            if (entries == null)
            {
                return false;
            }

            bool changed = false;
            for (int i = 0; i < entries.Count; i++)
            {
                ModSpawnMultiplierEntry entry = entries[i];
                if (entry == null || string.IsNullOrEmpty(entry.Name))
                {
                    continue;
                }

                float resolvedValue = entry.UseSeedRange
                    ? Math.Max(0f, rollContext.NextSteppedFloat("survival|" + groupName + "|" + entry.Name, entry.MinChanceMultiplier, entry.MaxChanceMultiplier, entry.ResolutionStep))
                    : Math.Max(0f, entry.ChanceMultiplier);

                if (!ApproximatelyEquals(entry.ChanceMultiplier, resolvedValue))
                {
                    entry.ChanceMultiplier = resolvedValue;
                    changed = true;
                }

                if (entry.UseSeedRange)
                {
                    entry.UseSeedRange = false;
                    changed = true;
                }

                if (!ApproximatelyEquals(entry.MinChanceMultiplier, resolvedValue))
                {
                    entry.MinChanceMultiplier = resolvedValue;
                    changed = true;
                }

                if (!ApproximatelyEquals(entry.MaxChanceMultiplier, resolvedValue))
                {
                    entry.MaxChanceMultiplier = resolvedValue;
                    changed = true;
                }

                entry.Normalize();
            }

            return changed;
        }

        private static bool MaterializeBiomeEntries(List<ModBiomeMultiplierEntry> entries, string groupName, ModSeedRollContext rollContext)
        {
            if (entries == null)
            {
                return false;
            }

            bool changed = false;
            for (int i = 0; i < entries.Count; i++)
            {
                ModBiomeMultiplierEntry entry = entries[i];
                if (entry == null || string.IsNullOrEmpty(entry.Name))
                {
                    continue;
                }

                float resolvedValue = entry.UseSeedRange
                    ? Math.Max(0f, rollContext.NextSteppedFloat("survival|" + groupName + "|" + entry.Name, entry.MinChanceMultiplier, entry.MaxChanceMultiplier, entry.ResolutionStep))
                    : Math.Max(0f, entry.ChanceMultiplier);

                if (!ApproximatelyEquals(entry.ChanceMultiplier, resolvedValue))
                {
                    entry.ChanceMultiplier = resolvedValue;
                    changed = true;
                }

                if (entry.UseSeedRange)
                {
                    entry.UseSeedRange = false;
                    changed = true;
                }

                if (!ApproximatelyEquals(entry.MinChanceMultiplier, resolvedValue))
                {
                    entry.MinChanceMultiplier = resolvedValue;
                    changed = true;
                }

                if (!ApproximatelyEquals(entry.MaxChanceMultiplier, resolvedValue))
                {
                    entry.MaxChanceMultiplier = resolvedValue;
                    changed = true;
                }

                entry.Normalize();
            }

            return changed;
        }

        private static bool MaterializeManualCreatureSpawnEntries(List<ModManualCreatureSpawnEntry> entries, ModSeedRollContext rollContext)
        {
            if (entries == null)
            {
                return false;
            }

            bool changed = false;
            for (int i = 0; i < entries.Count; i++)
            {
                ModManualCreatureSpawnEntry entry = entries[i];
                if (entry == null || string.IsNullOrEmpty(entry.TechTypeName))
                {
                    continue;
                }

                int resolvedAmount = entry.UseSeedRange
                    ? Math.Max(0, rollContext.NextSteppedInt("survival|ManualCreatureSpawns|" + entry.TechTypeName + "|Amount", entry.MinAmount, entry.MaxAmount, entry.AmountStep))
                    : Math.Max(0, entry.Amount);

                if (entry.Amount != resolvedAmount)
                {
                    entry.Amount = resolvedAmount;
                    changed = true;
                }

                if (entry.UseSeedRange)
                {
                    entry.UseSeedRange = false;
                    changed = true;
                }

                if (entry.MinAmount != resolvedAmount)
                {
                    entry.MinAmount = resolvedAmount;
                    changed = true;
                }

                if (entry.MaxAmount != resolvedAmount)
                {
                    entry.MaxAmount = resolvedAmount;
                    changed = true;
                }

                entry.Normalize();
            }

            return changed;
        }

        private static void ResolveCreativeSpawn(ModCreativeSeedDefinition creative)
        {
            _creativeSpawnLabel = string.Empty;

            if (creative.SpawnMode == ModCreativeSpawnMode.WeightedRanges && creative.Ranges != null && creative.Ranges.Count > 0)
            {
                ModCreativeSpawnRangeDefinition range = SelectWeightedRange("creative-spawn-range", creative.Ranges);
                if (range != null)
                {
                    _creativeSpawnLabel = range.Name ?? string.Empty;
                    _creativeSpawnX = NextFloat("creative-spawn-x|" + _creativeSpawnLabel, range.MinX, range.MaxX);
                    _creativeSpawnZ = NextFloat("creative-spawn-z|" + _creativeSpawnLabel, range.MinZ, range.MaxZ);
                    return;
                }
            }

            if (creative.SpawnMode == ModCreativeSpawnMode.RandomRange)
            {
                _creativeSpawnX = NextFloat("creative-spawn-x", creative.MinX, creative.MaxX);
                _creativeSpawnZ = NextFloat("creative-spawn-z", creative.MinZ, creative.MaxZ);
                return;
            }

            _creativeSpawnX = creative.FixedX;
            _creativeSpawnZ = creative.FixedZ;
        }

        private static void ResolveSurvivalSpawn(ModSurvivalSpawnDefinition survival)
        {
            _survivalSpawnLabel = string.Empty;

            if (survival.SpawnMode == ModCreativeSpawnMode.WeightedRanges && survival.Ranges != null && survival.Ranges.Count > 0)
            {
                ModCreativeSpawnRangeDefinition range = SelectWeightedRange("survival-spawn-range", survival.Ranges);
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
                ModCreativeSpawnRangeDefinition fallbackRange = survival.Ranges[0];
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

        private static ModCreativeSpawnRangeDefinition SelectWeightedRange(string scope, System.Collections.Generic.List<ModCreativeSpawnRangeDefinition> ranges)
        {
            if (_rollContext == null)
            {
                return ranges != null && ranges.Count > 0 ? ranges[0] : null;
            }

            return _rollContext.SelectWeightedRange(scope, ranges, range => range.Weight);
        }

        private static bool MigrateLegacyCreativeSeedIfNeeded(ModSeedDefinition seedDefinition)
        {
            if (seedDefinition == null || !string.Equals(seedDefinition.SeedId, ModSeedDefinition.LegacyDefaultActiveSeedId, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            seedDefinition.SeedId = ModSeedDefinition.DefaultActiveSeedId;
            if (string.IsNullOrEmpty(seedDefinition.SeedValue) ||
                string.Equals(seedDefinition.SeedValue, ModSeedDefinition.LegacyDefaultActiveSeedValue, StringComparison.OrdinalIgnoreCase))
            {
                seedDefinition.SeedValue = ModSeedDefinition.DefaultActiveSeedValue;
            }

            if (string.IsNullOrEmpty(seedDefinition.Description) ||
                seedDefinition.Description.IndexOf("Creative test seed", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                seedDefinition.Description = ModSeedDefinition.DefaultActiveSeedDescription;
            }

            return true;
        }

        private static bool ShouldRefreshLegacyDefaultActiveSeedFile(ModSeedDefinition seedDefinition)
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

        private static bool ShouldRefreshLegacyDefaultSurvivalTemplateFile(ModSeedDefinition seedDefinition)
        {
            if (seedDefinition == null || !string.Equals(seedDefinition.SeedId, "Survival-Template", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return IsLegacyStructuredSurvivalDefinition(seedDefinition.Survival);
        }

        private static bool IsLegacyStructuredSurvivalDefinition(ModSurvivalSeedDefinition survival)
        {
            if (survival == null)
            {
                return true;
            }

            if (survival.TemplateVersion < ModSurvivalSeedDefinition.CurrentTemplateVersion)
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

        private static bool MatchesCurrentCreativeDefault(ModCreativeSeedDefinition creative)
        {
            if (creative == null)
            {
                return false;
            }

            return creative.SpawnMode == ModCreativeSpawnMode.RandomRange &&
                   ApproximatelyEquals(creative.MinX, -100f) &&
                   ApproximatelyEquals(creative.MaxX, -30f) &&
                   ApproximatelyEquals(creative.MinZ, -430f) &&
                   ApproximatelyEquals(creative.MaxZ, -330f);
        }

        private static bool UpgradeSeedDefinitionIfNeeded(ModSeedDefinition seedDefinition, bool enforceCurrentDefaultAlwaysValues)
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
                seedDefinition.Survival = ModSurvivalSeedDefinition.CreateTemplate();
                changed = true;
            }

            if (seedDefinition.Survival.Defaults == null)
            {
                seedDefinition.Survival.Defaults = ModSurvivalDefaultsDefinition.CreateDefault();
                changed = true;
            }

            bool isDefaultSeed =
                IsDefaultActiveSeedId(seedDefinition.SeedId) ||
                string.Equals(seedDefinition.SeedId, "Survival-Template", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(seedDefinition.SeedId, "survival-template", StringComparison.OrdinalIgnoreCase);

            int previousTemplateVersion = seedDefinition.Survival.TemplateVersion;
            if (isDefaultSeed && seedDefinition.Survival.TemplateVersion < ModSurvivalSeedDefinition.CurrentTemplateVersion)
            {
                seedDefinition.Survival = ModSurvivalSeedDefinition.CreateTemplate();
                changed = true;
            }
            else if (previousTemplateVersion < ModSurvivalSeedDefinition.CurrentTemplateVersion)
            {
                if (ModSeedReferenceCatalog.SyncCurrentFragmentEntries(seedDefinition.Survival.Fragments))
                {
                    changed = true;
                }

                if (ModSeedReferenceCatalog.SyncCurrentResourceEntries(seedDefinition.Survival.Resources))
                {
                    changed = true;
                }

                if (ModSeedReferenceCatalog.SyncCurrentCreatureEntries(seedDefinition.Survival.Creatures))
                {
                    changed = true;
                }

                if (ModSeedReferenceCatalog.SyncCurrentBiomeEntries(seedDefinition.Survival.Biomes))
                {
                    changed = true;
                }

                if (ModSeedReferenceCatalog.SyncCurrentAlwaysEntries(seedDefinition.Survival.Always))
                {
                    changed = true;
                }

                if (ModSeedReferenceCatalog.SyncCurrentAlwaysBiomeEntries(seedDefinition.Survival.AlwaysBiomeMultipliers))
                {
                    changed = true;
                }

                if (ModSeedReferenceCatalog.SyncCurrentAlwaysBiomeTechEntries(seedDefinition.Survival.AlwaysBiomeTechMultipliers))
                {
                    changed = true;
                }

                if (ModSeedReferenceCatalog.SyncCurrentManualCreatureSpawnEntries(seedDefinition.Survival.ManualCreatureSpawns))
                {
                    changed = true;
                }

                if (ApplyCurrentSharedDefaults(seedDefinition.Survival.Defaults))
                {
                    changed = true;
                }
            }

            if (seedDefinition.Survival.TemplateVersion < ModSurvivalSeedDefinition.CurrentTemplateVersion)
            {
                seedDefinition.Survival.TemplateVersion = ModSurvivalSeedDefinition.CurrentTemplateVersion;
                changed = true;
            }

            if (seedDefinition.Survival.Always == null)
            {
                seedDefinition.Survival.Always = ModSeedReferenceCatalog.CreateDefaultAlwaysEntries();
                changed = true;
            }

            if (seedDefinition.Survival.AlwaysBiomeMultipliers == null)
            {
                seedDefinition.Survival.AlwaysBiomeMultipliers = ModSeedReferenceCatalog.CreateDefaultAlwaysBiomeEntries();
                changed = true;
            }

            if (seedDefinition.Survival.AlwaysBiomeTechMultipliers == null)
            {
                seedDefinition.Survival.AlwaysBiomeTechMultipliers = ModSeedReferenceCatalog.CreateDefaultAlwaysBiomeTechEntries();
                changed = true;
            }

            if (seedDefinition.Survival.ManualCreatureSpawns == null)
            {
                seedDefinition.Survival.ManualCreatureSpawns = ModSeedReferenceCatalog.CreateDefaultManualCreatureSpawns();
                changed = true;
            }

            int previousAlwaysCount = seedDefinition.Survival.Always.Count;
            ModSeedReferenceCatalog.EnsureDefaultAlwaysEntries(seedDefinition.Survival.Always);
            if (seedDefinition.Survival.Always.Count != previousAlwaysCount)
            {
                changed = true;
            }

            int previousAlwaysBiomeCount = seedDefinition.Survival.AlwaysBiomeMultipliers.Count;
            ModSeedReferenceCatalog.EnsureDefaultAlwaysBiomeEntries(seedDefinition.Survival.AlwaysBiomeMultipliers);
            if (seedDefinition.Survival.AlwaysBiomeMultipliers.Count != previousAlwaysBiomeCount)
            {
                changed = true;
            }

            int previousAlwaysBiomeTechCount = seedDefinition.Survival.AlwaysBiomeTechMultipliers.Count;
            ModSeedReferenceCatalog.EnsureDefaultAlwaysBiomeTechEntries(seedDefinition.Survival.AlwaysBiomeTechMultipliers);
            if (seedDefinition.Survival.AlwaysBiomeTechMultipliers.Count != previousAlwaysBiomeTechCount)
            {
                changed = true;
            }

            int previousManualCreatureSpawnCount = seedDefinition.Survival.ManualCreatureSpawns.Count;
            ModSeedReferenceCatalog.EnsureDefaultManualCreatureSpawnEntries(seedDefinition.Survival.ManualCreatureSpawns);
            if (seedDefinition.Survival.ManualCreatureSpawns.Count != previousManualCreatureSpawnCount)
            {
                changed = true;
            }

            if (ModSeedReferenceCatalog.SyncCurrentManualCreatureSpawnEntries(seedDefinition.Survival.ManualCreatureSpawns))
            {
                changed = true;
            }

            if (enforceCurrentDefaultAlwaysValues && isDefaultSeed)
            {
                if (!MatchesAlwaysDefaults(seedDefinition.Survival.Always))
                {
                    seedDefinition.Survival.Always = ModSeedReferenceCatalog.CreateDefaultAlwaysEntries();
                    changed = true;
                }
            }

            return changed;
        }

        private static bool MatchesAlwaysDefaults(System.Collections.Generic.List<ModSpawnMultiplierEntry> entries)
        {
            System.Collections.Generic.List<ModSpawnMultiplierEntry> defaults = ModSeedReferenceCatalog.CreateDefaultAlwaysEntries();
            if (entries == null || entries.Count != defaults.Count)
            {
                return false;
            }

            for (int i = 0; i < defaults.Count; i++)
            {
                ModSpawnMultiplierEntry defaultEntry = defaults[i];
                ModSpawnMultiplierEntry match = null;
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

        private static bool ApplyCurrentSharedDefaults(ModSurvivalDefaultsDefinition defaults)
        {
            if (defaults == null)
            {
                return false;
            }

            ModSurvivalDefaultsDefinition currentDefaults = ModSurvivalDefaultsDefinition.CreateDefault();
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

        private static string GetCreativeSpawnDescription(ModCreativeSeedDefinition creative, float x, float z)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("Creative seed '");
            builder.Append(GetActiveSeedId());
            builder.Append("' ");

            if (creative.SpawnMode == ModCreativeSpawnMode.WeightedRanges)
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

            if (creative.SpawnMode == ModCreativeSpawnMode.RandomRange)
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

        private static string GetSurvivalSpawnDescription(ModSurvivalSpawnDefinition survival, float x, float z)
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
            return string.Equals(seedId, ModSeedDefinition.DefaultActiveSeedId, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(seedId, ModSeedDefinition.LegacyDefaultActiveSeedId, StringComparison.OrdinalIgnoreCase);
        }

    }
}
