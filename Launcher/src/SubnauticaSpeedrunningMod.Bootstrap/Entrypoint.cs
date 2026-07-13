using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
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
                string logsRootDirectory = Path.Combine(modRoot, "Logs");
                string logsDirectory = Path.Combine(logsRootDirectory, "Bootstrap");
                string crashDirectory = Path.Combine(logsRootDirectory, "CrashReports");
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
                    string logsRootDirectory = Path.Combine(modRoot, "Logs");
                    string logsDirectory = Path.Combine(logsRootDirectory, "Bootstrap");
                    string crashDirectory = Path.Combine(logsRootDirectory, "CrashReports");
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
            string launcherVersion = Environment.GetEnvironmentVariable("MOD_LAUNCHER_VERSION") ?? string.Empty;
            if (string.IsNullOrEmpty(launcherVersion) || launcherVersion.Trim().Length == 0)
            {
                Write(logPath, "Direct Subnautica.exe launch detected. Continuing with in-process runtime initialization.");
            }

            return false;
        }

        private static void ShowValidationFailure(GameInstallValidationReport validationReport, string validationPath)
        {
            try
            {
                NativeMessageBox.ShowOk(
                    validationReport.ToUserFacingMessage() + Environment.NewLine + Environment.NewLine + "Report: " + validationPath,
                    "Wrong Subnautica Version");
            }
            catch
            {
            }
        }

        private static class NativeMessageBox
        {
            private const uint MbOk = 0x00000000u;
            private const uint MbYesNo = 0x00000004u;
            private const uint MbIconError = 0x00000010u;
            private const uint MbIconInformation = 0x00000040u;
            private const uint MbTopMost = 0x00040000u;
            public const int IdYes = 6;

            [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
            private static extern int MessageBox(IntPtr hWnd, string text, string caption, uint type);

            public static void ShowOk(string text, string caption)
            {
                MessageBox(IntPtr.Zero, text ?? string.Empty, caption ?? string.Empty, MbOk | MbIconError | MbTopMost);
            }

            public static int ShowYesNo(string text, string caption)
            {
                return MessageBox(
                    IntPtr.Zero,
                    text ?? string.Empty,
                    caption ?? string.Empty,
                    MbYesNo | MbIconInformation | MbTopMost);
            }
        }
    }
}
