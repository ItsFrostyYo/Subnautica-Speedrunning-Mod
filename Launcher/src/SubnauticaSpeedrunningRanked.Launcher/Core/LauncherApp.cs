using System.Diagnostics;
using System.IO;
using System.Reflection;
using SubnauticaSpeedrunningRanked.Shared;

namespace SubnauticaSpeedrunningRanked.Launcher;

internal static class LauncherApp
{
    public static int Run(string[] args)
    {
        var layout = FileLayout.Discover();
        LauncherRuntimeGuards.InstallGlobalHandlers(() => layout);
        layout.EnsureDirectories();
        LauncherLog.Initialize(layout.LogsDirectory);
        LauncherLog.Info("Launcher starting.");
        LauncherLog.Info("Ranked root: " + layout.RankedRoot);
        LauncherLog.Info("Game root: " + layout.GameRoot);

        if (!File.Exists(layout.GameExecutablePath))
        {
            LauncherLog.Error("Subnautica.exe was not found at " + layout.GameExecutablePath);
            Console.Error.WriteLine("Subnautica.exe was not found beside the ranked folder.");
            return 2;
        }

        layout.EnsureDefaultConfig();

        if (!File.Exists(layout.BootstrapAssemblyPath))
        {
            LauncherLog.Error("Bootstrap assembly missing at " + layout.BootstrapAssemblyPath);
            Console.Error.WriteLine("Bootstrap assembly is missing. Publish the launcher files into the game folder first.");
            return 3;
        }

        if (!File.Exists(layout.RuntimeAssemblyPath))
        {
            LauncherLog.Error("Runtime assembly missing at " + layout.RuntimeAssemblyPath);
            Console.Error.WriteLine("Runtime assembly is missing. Publish the launcher files into the game folder first.");
            return 4;
        }

        if (!File.Exists(layout.DoorstopLibraryPath))
        {
            LauncherLog.Error("winhttp.dll missing at " + layout.DoorstopLibraryPath);
            Console.Error.WriteLine("The native Doorstop transport is missing. Copy winhttp.dll and .doorstop_version into the game root.");
            return 5;
        }

        var validationReport = GameInstallValidator.Validate(layout.GameRoot);
        if (!validationReport.IsValid)
        {
            var reportPath = LauncherDiagnostics.WriteValidationReport(layout, validationReport, "version-validation-failed");
            LauncherLog.Error("Game version validation failed. Report: " + reportPath);
            LauncherAlert.ShowError(
                "Wrong Subnautica Version",
                validationReport.ToUserFacingMessage() + Environment.NewLine + Environment.NewLine + "Report: " + reportPath);
            return 10;
        }

        foreach (var warning in validationReport.Warnings)
        {
            LauncherLog.Warn("Validation warning: " + warning);
        }

        LauncherLog.Info("Game version validation passed.");
        DoorstopConfigWriter.Write(layout);

        if (args.Any(static arg => string.Equals(arg, "--setup-only", StringComparison.OrdinalIgnoreCase)))
        {
            LauncherLog.Info("Setup-only mode complete.");
            return 0;
        }

        var forwardedArguments = args
            .Where(static arg => !string.Equals(arg, "--setup-only", StringComparison.OrdinalIgnoreCase))
            .Select(QuoteIfNeeded);
        var sessionId = Guid.NewGuid().ToString("N");

        var startInfo = new ProcessStartInfo
        {
            FileName = layout.GameExecutablePath,
            Arguments = string.Join(" ", forwardedArguments),
            WorkingDirectory = layout.GameRoot,
            UseShellExecute = false
        };

        startInfo.Environment["RANKED_ROOT"] = layout.RankedRoot;
        startInfo.Environment["RANKED_GAME_ROOT"] = layout.GameRoot;
        startInfo.Environment["RANKED_SESSION_ID"] = sessionId;
        startInfo.Environment["RANKED_LAUNCHER_VERSION"] =
            typeof(LauncherApp).Assembly.GetName().Version?.ToString() ?? "unknown";

        if (TryFindExistingGameProcess(layout.GameExecutablePath, out var existingProcessId))
        {
            LauncherLog.Error("Refusing to launch because Subnautica is already running with process id " + existingProcessId + ".");
            LauncherAlert.ShowError(
                "Subnautica Already Running",
                "Subnautica is already running. Close the current game before launching Ranked again.");
            return 11;
        }

        LauncherLog.Info("Launching game.");
        LauncherLog.Info("Session id: " + sessionId);
        using var process = Process.Start(startInfo);
        if (process is null)
        {
            LauncherLog.Error("Process.Start returned null.");
            Console.Error.WriteLine("The game process could not be started.");
            return 6;
        }

        LauncherLog.Info("Game launched with process id " + process.Id + ".");
        return 0;
    }

    private static string QuoteIfNeeded(string value)
    {
        if (value.Contains(' ') || value.Contains('"'))
        {
            return "\"" + value.Replace("\"", "\\\"") + "\"";
        }

        return value;
    }

    private static bool TryFindExistingGameProcess(string expectedExecutablePath, out int processId)
    {
        processId = 0;
        string processName = Path.GetFileNameWithoutExtension(expectedExecutablePath);
        Process[] processes = Process.GetProcessesByName(processName);
        for (int i = 0; i < processes.Length; i++)
        {
            Process process = processes[i];
            try
            {
                string processPath = process.MainModule?.FileName;
                if (!string.IsNullOrEmpty(processPath) &&
                    string.Equals(processPath, expectedExecutablePath, StringComparison.OrdinalIgnoreCase))
                {
                    processId = process.Id;
                    return true;
                }
            }
            catch
            {
                // Some processes may not expose MainModule; ignore and continue.
            }
            finally
            {
                process.Dispose();
            }
        }

        return false;
    }
}
