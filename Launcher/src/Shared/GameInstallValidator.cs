using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace SubnauticaSpeedrunningMod.Shared
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
            builder.AppendLine("Subnautica Speedrunning Mod Validation Report");
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
            builder.AppendLine("Subnautica Speedrunning Mod cannot launch with this game install.");
            builder.AppendLine();
            builder.AppendLine("Required version:");
            builder.AppendLine(ModCompatibilityProfile.RequiredVersionSummary);
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
            report.SetObserved("ExpectedVersion", ModCompatibilityProfile.RequiredVersionSummary);

            if (string.IsNullOrEmpty(gameRoot) || !Directory.Exists(gameRoot))
            {
                report.AddError("Game root does not exist.");
                return report;
            }

            string folderName = new DirectoryInfo(gameRoot).Name;
            report.SetObserved("ObservedFolderName", folderName);

            string buildNumberPath = Path.Combine(gameRoot, "__buildnumber.txt");
            string buildTimePath = Path.Combine(gameRoot, "__buildtime.txt");
            string subnauticaExePath = Path.Combine(gameRoot, "Subnautica.exe");
            string subnauticaMonitorPath = Path.Combine(gameRoot, "SubnauticaMonitor.exe");
            string managedRoot = Path.Combine(gameRoot, Path.Combine("Subnautica_Data", "Managed"));
            string assemblyCSharpPath = Path.Combine(managedRoot, "Assembly-CSharp.dll");
            string assemblyCSharpFirstpassPath = Path.Combine(managedRoot, "Assembly-CSharp-firstpass.dll");
            string bepinexPath = Path.Combine(gameRoot, "BepInEx");

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

            if (File.Exists(buildNumberPath))
            {
                string buildNumber = File.ReadAllText(buildNumberPath).Trim();
                report.SetObserved("BuildNumber", buildNumber);
                if (!string.Equals(buildNumber, ModCompatibilityProfile.RequiredBuildNumber, StringComparison.Ordinal))
                {
                    report.AddError("Build number '" + buildNumber + "' does not match required build " + ModCompatibilityProfile.RequiredBuildNumber + ".");
                }
            }

            if (File.Exists(buildTimePath))
            {
                string buildTime = File.ReadAllText(buildTimePath).Trim();
                report.SetObserved("BuildTime", buildTime);
                if (!string.Equals(buildTime, ModCompatibilityProfile.RequiredBuildTime, StringComparison.Ordinal))
                {
                    report.AddError("Build time '" + buildTime + "' does not match the required September 2018 build.");
                }
            }

            if (File.Exists(subnauticaExePath))
            {
                FileVersionInfo version = FileVersionInfo.GetVersionInfo(subnauticaExePath);
                report.SetObserved("SubnauticaExeFileVersion", version.FileVersion ?? string.Empty);
                if (!string.Equals(version.FileVersion, ModCompatibilityProfile.RequiredSubnauticaExeFileVersion, StringComparison.OrdinalIgnoreCase))
                {
                    report.AddError("Subnautica.exe file version '" + version.FileVersion + "' does not match required version " + ModCompatibilityProfile.RequiredSubnauticaExeFileVersion + ".");
                }

                ValidateHash(report, subnauticaExePath, ModCompatibilityProfile.RequiredSubnauticaExeSha256, "Subnautica.exe SHA256");
            }

            ValidateHash(report, subnauticaMonitorPath, ModCompatibilityProfile.RequiredSubnauticaMonitorSha256, "SubnauticaMonitor.exe SHA256");
            ValidateHash(report, assemblyCSharpPath, ModCompatibilityProfile.RequiredAssemblyCSharpSha256, "Assembly-CSharp.dll SHA256");
            ValidateHash(report, assemblyCSharpFirstpassPath, ModCompatibilityProfile.RequiredAssemblyCSharpFirstpassSha256, "Assembly-CSharp-firstpass.dll SHA256");

            return report;
        }

        private static void ValidateFileExists(GameInstallValidationReport report, string path)
        {
            if (!File.Exists(path))
            {
                report.AddError("Required file is missing: " + path);
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
    }
}
