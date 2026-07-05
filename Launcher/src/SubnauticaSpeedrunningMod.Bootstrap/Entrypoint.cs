using System;
using System.IO;
using System.Diagnostics;
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
                if (TryHandoffDirectLaunchToLauncher(modRoot, logPath))
                {
                    return;
                }

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

        private static bool TryHandoffDirectLaunchToLauncher(string modRoot, string logPath)
        {
            try
            {
                string launcherVersion = Environment.GetEnvironmentVariable("MOD_LAUNCHER_VERSION") ?? string.Empty;
                if (!string.IsNullOrEmpty(launcherVersion) && launcherVersion.Trim().Length > 0)
                {
                    return false;
                }

                string launcherPath = Path.Combine(modRoot, "Launch Mod.exe");
                if (!File.Exists(launcherPath))
                {
                    Write(logPath, "Direct launch handoff skipped because launcher was not found at " + launcherPath);
                    return false;
                }

                Process currentProcess = Process.GetCurrentProcess();
                var startInfo = new ProcessStartInfo
                {
                    FileName = launcherPath,
                    WorkingDirectory = Path.GetDirectoryName(launcherPath) ?? modRoot,
                    UseShellExecute = true
                };

                string arguments = "--handoff-pid " + currentProcess.Id;
                string[] currentArgs = Environment.GetCommandLineArgs();
                for (int i = 1; i < currentArgs.Length; i++)
                {
                    arguments += " " + QuoteArgument(currentArgs[i]);
                }

                startInfo.Arguments = arguments;
                Write(logPath, "Direct Subnautica.exe launch detected. Handing off to launcher.");
                Process.Start(startInfo);
                Environment.Exit(0);
                return true;
            }
            catch (Exception ex)
            {
                Write(logPath, "Direct launch handoff failed, continuing bootstrap normally: " + ex);
                return false;
            }
        }

        private static string QuoteArgument(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return "\"\"";
            }

            if (value.IndexOfAny(new[] { ' ', '\t', '"' }) < 0)
            {
                return value;
            }

            return "\"" + value.Replace("\"", "\\\"") + "\"";
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
