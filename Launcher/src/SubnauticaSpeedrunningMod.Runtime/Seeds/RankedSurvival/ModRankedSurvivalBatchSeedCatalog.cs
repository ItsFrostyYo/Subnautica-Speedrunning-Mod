using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace SubnauticaSpeedrunningMod.Runtime.Seeds
{
    internal static class ModRankedSurvivalBatchSeedCatalog
    {
        private const string InstalledSeedsFolderName = "Seeds";
        private const string RankedSurvivalFolderName = "RankedSurvival";
        private const string SeedDirectoryPrefix = "BS ";
        private const float ClipCMinX = -135f;
        private const float ClipCMaxX = -70f;
        private const float ClipCMinZ = 70f;
        private const float ClipCMaxZ = 100f;

        private static readonly ModSeedRuntimeProfile RankedSurvivalProfile = ModSeedRuntimeProfile.CreateRankedBatchSurvivalProfile();

        public static ModSeedRuntimeProfile Profile
        {
            get { return RankedSurvivalProfile; }
        }

        public static string GetInstalledRootPath()
        {
            return Path.Combine(Path.Combine(PathLayout.GetModRoot(), InstalledSeedsFolderName), RankedSurvivalFolderName);
        }

        public static List<string> GetAvailableSeedDirectories()
        {
            List<string> directories = new List<string>();
            string rootPath = GetInstalledRootPath();
            if (!Directory.Exists(rootPath))
            {
                return directories;
            }

            string[] children = Directory.GetDirectories(rootPath);
            for (int i = 0; i < children.Length; i++)
            {
                string childPath = children[i];
                string childName = Path.GetFileName(childPath) ?? string.Empty;
                if (!LooksLikeRankedSeedDirectory(childName) || !HasSeedData(childPath))
                {
                    continue;
                }

                directories.Add(childPath);
            }

            directories.Sort(CompareSeedDirectories);
            return directories;
        }

        public static bool TryChooseSeed(out string seedDirectoryPath, out string seedName)
        {
            seedDirectoryPath = string.Empty;
            seedName = string.Empty;

            List<string> directories = GetAvailableSeedDirectories();
            if (directories.Count == 0)
            {
                return false;
            }

            int seedIndex = directories.Count == 1
                ? 0
                : UnityEngine.Random.Range(0, directories.Count);
            seedDirectoryPath = directories[seedIndex];
            seedName = Path.GetFileName(seedDirectoryPath) ?? string.Empty;
            return !string.IsNullOrEmpty(seedDirectoryPath);
        }

        public static bool TryGetSeedDirectoryByName(string seedName, out string seedDirectoryPath)
        {
            seedDirectoryPath = string.Empty;
            if (string.IsNullOrEmpty(seedName))
            {
                return false;
            }

            List<string> directories = GetAvailableSeedDirectories();
            for (int i = 0; i < directories.Count; i++)
            {
                string candidatePath = directories[i];
                string candidateName = Path.GetFileName(candidatePath) ?? string.Empty;
                if (!string.Equals(candidateName, seedName.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                seedDirectoryPath = candidatePath;
                return true;
            }

            return false;
        }

        public static void ResolveClipCSpawn(out float x, out float z, out string description, string seedName)
        {
            x = UnityEngine.Random.Range(ClipCMinX, ClipCMaxX);
            z = UnityEngine.Random.Range(ClipCMinZ, ClipCMaxZ);

            description =
                "Ranked survival batch seed '" +
                (string.IsNullOrEmpty(seedName) ? "unknown" : seedName) +
                "' via Clip C at X=" +
                x.ToString("0.###", CultureInfo.InvariantCulture) +
                ", Z=" +
                z.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private static bool LooksLikeRankedSeedDirectory(string directoryName)
        {
            return !string.IsNullOrEmpty(directoryName) &&
                   directoryName.StartsWith(SeedDirectoryPrefix, StringComparison.OrdinalIgnoreCase);
        }

        private static bool HasSeedData(string seedDirectoryPath)
        {
            if (string.IsNullOrEmpty(seedDirectoryPath) || !Directory.Exists(seedDirectoryPath))
            {
                return false;
            }

            bool hasBatchObjects = Directory.GetFiles(seedDirectoryPath, "batch-objects-*.txt", SearchOption.TopDirectoryOnly).Length > 0;
            bool hasCellsCache = Directory.Exists(Path.Combine(seedDirectoryPath, "CellsCache"));
            return hasBatchObjects && hasCellsCache;
        }

        private static int CompareSeedDirectories(string leftPath, string rightPath)
        {
            string leftName = Path.GetFileName(leftPath) ?? string.Empty;
            string rightName = Path.GetFileName(rightPath) ?? string.Empty;

            int leftNumber;
            bool hasLeftNumber = TryGetSeedNumber(leftName, out leftNumber);
            int rightNumber;
            bool hasRightNumber = TryGetSeedNumber(rightName, out rightNumber);
            if (hasLeftNumber && hasRightNumber)
            {
                int numericComparison = leftNumber.CompareTo(rightNumber);
                if (numericComparison != 0)
                {
                    return numericComparison;
                }
            }

            return string.Compare(leftName, rightName, StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryGetSeedNumber(string directoryName, out int seedNumber)
        {
            seedNumber = 0;
            if (!LooksLikeRankedSeedDirectory(directoryName))
            {
                return false;
            }

            string numericSuffix = directoryName.Substring(SeedDirectoryPrefix.Length).Trim();
            return int.TryParse(numericSuffix, NumberStyles.Integer, CultureInfo.InvariantCulture, out seedNumber);
        }
    }
}
