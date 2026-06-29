using System;
using System.Collections.Generic;
using System.IO;

namespace SubnauticaSpeedrunningMod.Runtime.Localization
{
    internal static class ModLanguageFileRuntimeHost
    {
        private const string LanguageFolderName = "LanguageFiles";
        private const string UnmanagedRootFolderName = "SNUnmanagedData";
        private const string SyncMarkerFileName = "SubnauticaSpeedrunningMod.synced.txt";

        private static readonly StringComparer FileNameComparer = StringComparer.OrdinalIgnoreCase;

        public static void Initialize(string modRoot, string gameRoot)
        {
            SyncInstalledLanguageFiles(modRoot, gameRoot);
        }

        private static void SyncInstalledLanguageFiles(string modRoot, string gameRoot)
        {
            string sourceDirectory = GetSourceDirectory(modRoot);
            string targetDirectory = GetTargetDirectory(gameRoot);
            if (string.IsNullOrEmpty(sourceDirectory) ||
                string.IsNullOrEmpty(targetDirectory) ||
                !Directory.Exists(sourceDirectory))
            {
                return;
            }

            try
            {
                Directory.CreateDirectory(targetDirectory);

                string markerPath = Path.Combine(targetDirectory, SyncMarkerFileName);
                HashSet<string> previouslySyncedFiles = LoadMarker(markerPath);
                HashSet<string> currentFiles = new HashSet<string>(FileNameComparer);
                string[] sourceFiles = Directory.GetFiles(sourceDirectory, "*.json", SearchOption.TopDirectoryOnly);
                Array.Sort(sourceFiles, FileNameComparer);
                for (int i = 0; i < sourceFiles.Length; i++)
                {
                    string sourcePath = sourceFiles[i];
                    string fileName = Path.GetFileName(sourcePath);
                    if (string.IsNullOrEmpty(fileName))
                    {
                        continue;
                    }

                    currentFiles.Add(fileName);
                    File.Copy(sourcePath, Path.Combine(targetDirectory, fileName), overwrite: true);
                }

                foreach (string staleFileName in previouslySyncedFiles)
                {
                    if (currentFiles.Contains(staleFileName))
                    {
                        continue;
                    }

                    string stalePath = Path.Combine(targetDirectory, staleFileName);
                    if (File.Exists(stalePath))
                    {
                        File.Delete(stalePath);
                    }
                }

                File.WriteAllLines(markerPath, new List<string>(currentFiles).ToArray());
                ModLog.Info("Synchronized " + currentFiles.Count + " custom language file(s) into SNUnmanagedData/LanguageFiles.");
            }
            catch (Exception ex)
            {
                ModLog.Warn("Failed to synchronize custom language files: " + ex.Message);
            }
        }

        private static HashSet<string> LoadMarker(string markerPath)
        {
            HashSet<string> names = new HashSet<string>(FileNameComparer);
            if (!File.Exists(markerPath))
            {
                return names;
            }

            string[] lines = File.ReadAllLines(markerPath);
            for (int i = 0; i < lines.Length; i++)
            {
                string line = (lines[i] ?? string.Empty).Trim();
                if (!string.IsNullOrEmpty(line))
                {
                    names.Add(line);
                }
            }

            return names;
        }

        private static string GetSourceDirectory(string modRoot)
        {
            if (string.IsNullOrEmpty(modRoot))
            {
                return string.Empty;
            }

            return Path.Combine(modRoot, LanguageFolderName);
        }

        private static string GetTargetDirectory(string gameRoot)
        {
            if (string.IsNullOrEmpty(gameRoot))
            {
                return string.Empty;
            }

            return Path.Combine(Path.Combine(gameRoot, UnmanagedRootFolderName), LanguageFolderName);
        }
    }
}
