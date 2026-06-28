using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using SubnauticaSpeedrunningMod.Shared;

namespace Doorstop
{
    public static class Entrypoint
    {
        private static bool _started;
        private static readonly object Sync = new object();

        public static void Start()
        {
            lock (Sync)
            {
                if (_started)
                {
                    return;
                }

                _started = true;
            }

            SubnauticaSpeedrunningMod.Bootstrap.Bootstrapper.Start();
        }
    }
}

namespace SubnauticaSpeedrunningMod.Bootstrap
{
    internal static class Bootstrapper
    {
        public static void Start()
        {
            try
            {
                string bootstrapDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                string modRoot = Directory.GetParent(bootstrapDirectory).FullName;
                string gameRoot = Directory.GetParent(modRoot).FullName;
                string logsDirectory = Path.Combine(modRoot, "Logs");
                string crashDirectory = Path.Combine(logsDirectory, "CrashReports");
                Directory.CreateDirectory(logsDirectory);
                Directory.CreateDirectory(crashDirectory);

                string logPath = Path.Combine(logsDirectory, "bootstrap.log");
                Write(logPath, "Bootstrap starting.");
                Write(logPath, "Game root: " + gameRoot);

                GameInstallValidationReport validationReport = GameInstallValidator.Validate(gameRoot);
                if (!validationReport.IsValid)
                {
                    string validationPath = Path.Combine(
                        crashDirectory,
                        "bootstrap-version-validation-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".txt");
                    File.WriteAllText(validationPath, validationReport.ToDiagnosticText());
                    Write(logPath, "Version validation failed. Report: " + validationPath);
                    ShowValidationFailure(validationReport, validationPath);
                    Environment.FailFast(validationReport.ToUserFacingMessage());
                    return;
                }

                string runtimePath = Path.Combine(
                    Path.Combine(modRoot, "Runtime"),
                    "SubnauticaSpeedrunningMod.Runtime.dll");
                if (!File.Exists(runtimePath))
                {
                    Write(logPath, "Runtime assembly missing at " + runtimePath);
                    return;
                }

                Assembly runtimeAssembly = Assembly.LoadFrom(runtimePath);
                Type runtimeEntryType = runtimeAssembly.GetType("SubnauticaSpeedrunningMod.Runtime.RuntimeEntry", true);
                MethodInfo initializeMethod = runtimeEntryType.GetMethod("Initialize", BindingFlags.Public | BindingFlags.Static);
                if (initializeMethod == null)
                {
                    Write(logPath, "RuntimeEntry.Initialize was not found.");
                    return;
                }

                initializeMethod.Invoke(null, null);
                Write(logPath, "Runtime initialize invoked.");
            }
            catch (Exception ex)
            {
                try
                {
                    string assemblyPath = Assembly.GetExecutingAssembly().Location;
                    string bootstrapDirectory = Path.GetDirectoryName(assemblyPath);
                    string modRoot = Directory.GetParent(bootstrapDirectory).FullName;
                    string logsDirectory = Path.Combine(modRoot, "Logs");
                    string crashDirectory = Path.Combine(logsDirectory, "CrashReports");
                    Directory.CreateDirectory(logsDirectory);
                    Directory.CreateDirectory(crashDirectory);
                    string logPath = Path.Combine(logsDirectory, "bootstrap.log");
                    Write(logPath, "Bootstrap failure: " + ex);
                    string crashPath = Path.Combine(
                        crashDirectory,
                        "bootstrap-crash-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".txt");
                    File.WriteAllText(crashPath, ex.ToString());
                }
                catch
                {
                }
            }
        }

        private static void Write(string logPath, string message)
        {
            File.AppendAllText(
                logPath,
                "[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + "] " + message + Environment.NewLine);
        }

        private static void ShowValidationFailure(GameInstallValidationReport validationReport, string validationPath)
        {
            try
            {
                MessageBox.Show(
                    validationReport.ToUserFacingMessage() + Environment.NewLine + Environment.NewLine + "Report: " + validationPath,
                    "Wrong Subnautica Version",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error,
                    MessageBoxDefaultButton.Button1);
            }
            catch
            {
            }
        }
    }
}
