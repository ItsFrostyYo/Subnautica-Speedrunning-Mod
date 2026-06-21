using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace SubnauticaSpeedrunningRanked.Shared
{
    public sealed class GameInstallValidationReport
    {
        public GameInstallValidationReport()
        {
            Errors = new List<string>();
            Warnings = new List<string>();
            ObservedValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        public IList<string> Errors { get; private set; }
        public IList<string> Warnings { get; private set; }
        public IDictionary<string, string> ObservedValues { get; private set; }

        public bool IsValid
        {
            get { return Errors.Count == 0; }
        }

        public void AddError(string message)
        {
            Errors.Add(message);
        }

        public void AddWarning(string message)
        {
            Warnings.Add(message);
        }

        public void SetObserved(string key, string value)
        {
            ObservedValues[key] = value ?? string.Empty;
        }

        public string ToDiagnosticText()
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("Subnautica Speedrunning Ranked Validation Report");
            builder.AppendLine("GeneratedAt=" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"));
            builder.AppendLine("IsValid=" + IsValid);
            builder.AppendLine();
            builder.AppendLine("[Observed]");
            foreach (KeyValuePair<string, string> pair in ObservedValues)
            {
                builder.AppendLine(pair.Key + "=" + pair.Value);
            }

            builder.AppendLine();
            builder.AppendLine("[Errors]");
            if (Errors.Count == 0)
            {
                builder.AppendLine("(none)");
            }
            else
            {
                foreach (string error in Errors)
                {
                    builder.AppendLine("- " + error);
                }
            }

            builder.AppendLine();
            builder.AppendLine("[Warnings]");
            if (Warnings.Count == 0)
            {
                builder.AppendLine("(none)");
            }
            else
            {
                foreach (string warning in Warnings)
                {
                    builder.AppendLine("- " + warning);
                }
            }

            return builder.ToString();
        }

        public string ToUserFacingMessage()
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("Subnautica Speedrunning Ranked cannot launch with this game install.");
            builder.AppendLine();
            builder.AppendLine("Required version:");
            builder.AppendLine(RankedCompatibilityProfile.RequiredVersionSummary);
            builder.AppendLine();

            if (Errors.Count > 0)
            {
                builder.AppendLine("Problems found:");
                for (int i = 0; i < Errors.Count; i++)
                {
                    builder.AppendLine((i + 1).ToString() + ". " + Errors[i]);
                }
                builder.AppendLine();
            }

            builder.AppendLine("Please download or restore the correct Subnautica 2018 speedrun version and try again.");
            return builder.ToString();
        }
    }

    public static class GameInstallValidator
    {
        public static GameInstallValidationReport Validate(string gameRoot)
        {
            GameInstallValidationReport report = new GameInstallValidationReport();
            report.SetObserved("GameRoot", gameRoot);
            report.SetObserved("ExpectedVersion", RankedCompatibilityProfile.RequiredVersionSummary);

            if (string.IsNullOrEmpty(gameRoot) || !Directory.Exists(gameRoot))
            {
                report.AddError("Game root does not exist.");
                return report;
            }

            string folderName = new DirectoryInfo(gameRoot).Name;
            report.SetObserved("ObservedFolderName", folderName);
            if (!string.Equals(folderName, RankedCompatibilityProfile.RequiredFolderName, StringComparison.OrdinalIgnoreCase))
            {
                report.AddWarning("Game folder name is '" + folderName + "' instead of '" + RankedCompatibilityProfile.RequiredFolderName + "'.");
            }

            string versionInfoPath = Path.Combine(gameRoot, "Version.info");
            string buildNumberPath = Path.Combine(gameRoot, "__buildnumber.txt");
            string buildTimePath = Path.Combine(gameRoot, "__buildtime.txt");
            string subnauticaExePath = Path.Combine(gameRoot, "Subnautica.exe");
            string subnauticaMonitorPath = Path.Combine(gameRoot, "SubnauticaMonitor.exe");
            string managedRoot = Path.Combine(gameRoot, Path.Combine("Subnautica_Data", "Managed"));
            string assemblyCSharpPath = Path.Combine(managedRoot, "Assembly-CSharp.dll");
            string assemblyCSharpFirstpassPath = Path.Combine(managedRoot, "Assembly-CSharp-firstpass.dll");
            string bepinexPath = Path.Combine(gameRoot, "BepInEx");

            ValidateFileExists(report, versionInfoPath);
            ValidateFileExists(report, buildNumberPath);
            ValidateFileExists(report, buildTimePath);
            ValidateFileExists(report, subnauticaExePath);
            ValidateFileExists(report, subnauticaMonitorPath);
            ValidateFileExists(report, assemblyCSharpPath);
            ValidateFileExists(report, assemblyCSharpFirstpassPath);

            if (Directory.Exists(bepinexPath))
            {
                report.AddError("A BepInEx folder was detected. Ranked requires a clean base game install.");
            }

            if (File.Exists(versionInfoPath))
            {
                Dictionary<string, string> versionInfo = ParseKeyValueFile(versionInfoPath);
                SetObservedIfPresent(report, versionInfo, "DisplayName");
                SetObservedIfPresent(report, versionInfo, "FolderName");
                SetObservedIfPresent(report, versionInfo, "OriginalDownload");
                SetObservedIfPresent(report, versionInfo, "Modded");

                RequireValue(report, versionInfo, "DisplayName", RankedCompatibilityProfile.RequiredDisplayName);
                RequireValue(report, versionInfo, "FolderName", RankedCompatibilityProfile.RequiredFolderName);
                RequireValue(report, versionInfo, "OriginalDownload", RankedCompatibilityProfile.RequiredOriginalDownload);
                RequireValue(report, versionInfo, "Modded", RankedCompatibilityProfile.RequiredModdedValue);
                ValidateHash(report, versionInfoPath, RankedCompatibilityProfile.RequiredVersionInfoSha256, "Version.info SHA256");
            }

            if (File.Exists(buildNumberPath))
            {
                string buildNumber = File.ReadAllText(buildNumberPath).Trim();
                report.SetObserved("BuildNumber", buildNumber);
                if (!string.Equals(buildNumber, RankedCompatibilityProfile.RequiredBuildNumber, StringComparison.Ordinal))
                {
                    report.AddError("Build number '" + buildNumber + "' does not match required build " + RankedCompatibilityProfile.RequiredBuildNumber + ".");
                }
            }

            if (File.Exists(buildTimePath))
            {
                string buildTime = File.ReadAllText(buildTimePath).Trim();
                report.SetObserved("BuildTime", buildTime);
                if (!string.Equals(buildTime, RankedCompatibilityProfile.RequiredBuildTime, StringComparison.Ordinal))
                {
                    report.AddError("Build time '" + buildTime + "' does not match the required September 2018 build.");
                }
            }

            if (File.Exists(subnauticaExePath))
            {
                FileVersionInfo version = FileVersionInfo.GetVersionInfo(subnauticaExePath);
                report.SetObserved("SubnauticaExeFileVersion", version.FileVersion ?? string.Empty);
                if (!string.Equals(version.FileVersion, RankedCompatibilityProfile.RequiredSubnauticaExeFileVersion, StringComparison.OrdinalIgnoreCase))
                {
                    report.AddError("Subnautica.exe file version '" + version.FileVersion + "' does not match required version " + RankedCompatibilityProfile.RequiredSubnauticaExeFileVersion + ".");
                }

                ValidateHash(report, subnauticaExePath, RankedCompatibilityProfile.RequiredSubnauticaExeSha256, "Subnautica.exe SHA256");
            }

            ValidateHash(report, subnauticaMonitorPath, RankedCompatibilityProfile.RequiredSubnauticaMonitorSha256, "SubnauticaMonitor.exe SHA256");
            ValidateHash(report, assemblyCSharpPath, RankedCompatibilityProfile.RequiredAssemblyCSharpSha256, "Assembly-CSharp.dll SHA256");
            ValidateHash(report, assemblyCSharpFirstpassPath, RankedCompatibilityProfile.RequiredAssemblyCSharpFirstpassSha256, "Assembly-CSharp-firstpass.dll SHA256");

            return report;
        }

        private static void ValidateFileExists(GameInstallValidationReport report, string path)
        {
            if (!File.Exists(path))
            {
                report.AddError("Required file is missing: " + path);
            }
        }

        private static void SetObservedIfPresent(GameInstallValidationReport report, IDictionary<string, string> values, string key)
        {
            string value = string.Empty;
            if (values.TryGetValue(key, out value))
            {
                report.SetObserved(key, value);
            }
        }

        private static void RequireValue(GameInstallValidationReport report, IDictionary<string, string> values, string key, string expectedValue)
        {
            string value = string.Empty;
            if (!values.TryGetValue(key, out value))
            {
                report.AddError("Version.info is missing '" + key + "'.");
                return;
            }

            if (!string.Equals(value, expectedValue, StringComparison.Ordinal))
            {
                report.AddError("Version.info " + key + "='" + value + "' does not match required value '" + expectedValue + "'.");
            }
        }

        private static void ValidateHash(GameInstallValidationReport report, string path, string expectedHash, string label)
        {
            if (!File.Exists(path))
            {
                return;
            }

            string actualHash = ComputeSha256(path);
            report.SetObserved(label, actualHash);
            if (!string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase))
            {
                report.AddError(label + " does not match the ranked-supported September 2018 build.");
            }
        }

        private static string ComputeSha256(string path)
        {
            using (FileStream stream = File.OpenRead(path))
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] hash = sha256.ComputeHash(stream);
                StringBuilder builder = new StringBuilder(hash.Length * 2);
                for (int i = 0; i < hash.Length; i++)
                {
                    builder.Append(hash[i].ToString("X2"));
                }

                return builder.ToString();
            }
        }

        private static Dictionary<string, string> ParseKeyValueFile(string path)
        {
            Dictionary<string, string> values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            string[] lines = File.ReadAllLines(path);
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                int equalsIndex = line.IndexOf('=');
                if (equalsIndex <= 0)
                {
                    continue;
                }

                string key = line.Substring(0, equalsIndex).Trim();
                string value = line.Substring(equalsIndex + 1).Trim();
                values[key] = value;
            }

            return values;
        }
    }
}
