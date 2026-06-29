using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace SubnauticaSpeedrunningMod.Runtime.RunTracking
{
    internal static class BetterRngRunVerification
    {
        private static readonly Color ValidColor = new Color(0.35f, 1f, 0.45f, 1f);
        private static readonly Color InvalidColor = new Color(1f, 0.4f, 0.4f, 1f);

        public static BetterRngRunVerificationResult Evaluate(string igtText, string rtaText)
        {
            List<string> invalidReasons = FindInvalidatingMods();
            bool isValid = invalidReasons.Count == 0;

            if (isValid)
            {
                return new BetterRngRunVerificationResult(
                    true,
                    "\u2713 Verified BetterRNG Run",
                    "IGT: " + igtText + " | RTA: " + rtaText,
                    ValidColor,
                    ValidColor);
            }

            return new BetterRngRunVerificationResult(
                false,
                "\u2717 Invalid BetterRNG Run",
                "Other mods detected | IGT: " + igtText + " | RTA: " + rtaText,
                InvalidColor,
                InvalidColor);
        }

        private static List<string> FindInvalidatingMods()
        {
            List<string> reasons = new List<string>();
            string modRoot = PathLayout.GetModRoot();
            string gameRoot = PathLayout.GetGameRoot(modRoot);

            AppendDirectoryReason(reasons, Path.Combine(modRoot, "Modules"), "extra runtime modules");
            AppendPluginReason(reasons, Path.Combine(Path.Combine(gameRoot, "BepInEx"), "plugins"), "BepInEx plugins");
            AppendQModsReason(reasons, Path.Combine(gameRoot, "QMods"), "QMods mods");
            AppendLoadedAssemblyReason(reasons, Path.Combine(Path.Combine(gameRoot, "BepInEx"), "plugins"), "loaded BepInEx plugin assembly");
            AppendLoadedAssemblyReason(reasons, Path.Combine(gameRoot, "QMods"), "loaded QMods assembly");
            AppendLoadedAssemblyReason(reasons, Path.Combine(modRoot, "Modules"), "loaded external runtime module");

            return reasons;
        }

        private static void AppendDirectoryReason(List<string> reasons, string rootPath, string label)
        {
            string firstPath;
            if (TryFindEnabledDll(rootPath, out firstPath))
            {
                ModLog.Warn("BetterRNG verification invalidated by " + label + ": " + firstPath);
                reasons.Add(label);
            }
        }

        private static void AppendPluginReason(List<string> reasons, string rootPath, string label)
        {
            string firstPath;
            if (TryFindEnabledDll(rootPath, out firstPath))
            {
                ModLog.Warn("BetterRNG verification invalidated by " + label + ": " + firstPath);
                reasons.Add(label);
            }
        }

        private static void AppendQModsReason(List<string> reasons, string rootPath, string label)
        {
            string firstPath;
            if (TryFindQModPayload(rootPath, out firstPath))
            {
                ModLog.Warn("BetterRNG verification invalidated by " + label + ": " + firstPath);
                reasons.Add(label);
            }
        }

        private static void AppendLoadedAssemblyReason(List<string> reasons, string rootPath, string label)
        {
            string assemblyPath;
            if (TryFindLoadedAssemblyUnder(rootPath, out assemblyPath))
            {
                ModLog.Warn("BetterRNG verification invalidated by " + label + ": " + assemblyPath);
                reasons.Add(label);
            }
        }

        private static bool TryFindEnabledDll(string rootPath, out string firstPath)
        {
            firstPath = string.Empty;
            if (string.IsNullOrEmpty(rootPath) || !Directory.Exists(rootPath))
            {
                return false;
            }

            foreach (string assemblyPath in Directory.GetFiles(rootPath, "*.dll", SearchOption.AllDirectories))
            {
                if (File.Exists(assemblyPath + ".disabled"))
                {
                    continue;
                }

                firstPath = assemblyPath;
                return true;
            }

            return false;
        }

        private static bool TryFindQModPayload(string rootPath, out string firstPath)
        {
            firstPath = string.Empty;
            if (string.IsNullOrEmpty(rootPath) || !Directory.Exists(rootPath))
            {
                return false;
            }

            foreach (string manifestPath in Directory.GetFiles(rootPath, "mod.json", SearchOption.AllDirectories))
            {
                firstPath = manifestPath;
                return true;
            }

            return TryFindEnabledDll(rootPath, out firstPath);
        }

        private static bool TryFindLoadedAssemblyUnder(string rootPath, out string assemblyPath)
        {
            assemblyPath = string.Empty;
            if (string.IsNullOrEmpty(rootPath) || !Directory.Exists(rootPath))
            {
                return false;
            }

            string normalizedRootPath = NormalizeRootPath(rootPath);
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                Assembly assembly = assemblies[i];
                if (assembly == null)
                {
                    continue;
                }

                string location;
                try
                {
                    location = assembly.Location;
                }
                catch
                {
                    continue;
                }

                if (string.IsNullOrEmpty(location))
                {
                    continue;
                }

                string normalizedLocation = NormalizeRootPath(location);
                if (!normalizedLocation.StartsWith(normalizedRootPath, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                assemblyPath = location;
                return true;
            }

            return false;
        }

        private static string NormalizeRootPath(string path)
        {
            string fullPath = Path.GetFullPath(path)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return fullPath + Path.DirectorySeparatorChar;
        }
    }

    internal struct BetterRngRunVerificationResult
    {
        public BetterRngRunVerificationResult(bool isValid, string title, string detail, Color titleColor, Color detailColor)
        {
            IsValid = isValid;
            Title = title ?? string.Empty;
            Detail = detail ?? string.Empty;
            TitleColor = titleColor;
            DetailColor = detailColor;
        }

        public bool IsValid { get; private set; }

        public string Title { get; private set; }

        public string Detail { get; private set; }

        public Color TitleColor { get; private set; }

        public Color DetailColor { get; private set; }
    }
}
