using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using SubnauticaSpeedrunningMod.Shared;

namespace SubnauticaSpeedrunningMod.Launcher;

internal static class LauncherApp
{
    public static int Run(string[] args)
    {
        var layout = FileLayout.Discover();
        LauncherRuntimeGuards.InstallGlobalHandlers(() => layout);
        layout.EnsureDirectories();
        LauncherLog.Initialize(layout.LogsDirectory);
        LauncherLog.Info("Launcher starting.");
        LauncherLog.Info("Launcher version: " + LauncherVersion.DisplayVersion);
        LauncherLog.Info("Mod root: " + layout.ModRoot);
        LauncherLog.Info("Game root: " + layout.GameRoot);

        int handoffProcessId = ParseHandoffProcessId(args);
        if (handoffProcessId > 0)
        {
            LauncherLog.Info("Launcher received direct-launch handoff from process id " + handoffProcessId + ".");
            if (!WaitForHandoffProcessExit(handoffProcessId))
            {
                LauncherAlert.ShowError(
                    "Launcher Handoff Failed",
                    "The original Subnautica process did not close in time for the launcher handoff. Close Subnautica completely, then launch the mod again.");
                return 12;
            }

            WaitForLaunchCooldown(layout.GameExecutablePath, handoffProcessId);
        }

        if (!File.Exists(layout.GameExecutablePath))
        {
            LauncherLog.Error("Subnautica.exe was not found at " + layout.GameExecutablePath);
            Console.Error.WriteLine("Subnautica.exe was not found beside the mod folder.");
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

        NativeTransportRepairService.EnsureInstalled(layout);

        if (!File.Exists(layout.DoorstopLibraryPath))
        {
            LauncherLog.Error("winhttp.dll missing at " + layout.DoorstopLibraryPath);
            Console.Error.WriteLine("The native Doorstop transport is missing. Copy winhttp.dll and .doorstop_version into the game root.");
            return 5;
        }

        if (!File.Exists(layout.DoorstopVersionPath))
        {
            LauncherLog.Error(".doorstop_version missing at " + layout.DoorstopVersionPath);
            Console.Error.WriteLine("The native Doorstop version marker is missing. Copy .doorstop_version into the game root.");
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
        if (LauncherUpdateService.TryRunUpdateFlow(layout, args))
        {
            LauncherLog.Info("Updater launched; exiting current launcher instance.");
            return 0;
        }

        DoorstopConfigWriter.Write(layout);

        if (args.Any(static arg => string.Equals(arg, "--setup-only", StringComparison.OrdinalIgnoreCase)))
        {
            LauncherLog.Info("Setup-only mode complete.");
            return 0;
        }

        var forwardedArguments = args
            .Where(static arg => !string.Equals(arg, "--setup-only", StringComparison.OrdinalIgnoreCase))
            .Where(static arg => !string.Equals(arg, "--handoff-pid", StringComparison.OrdinalIgnoreCase))
            .Where((arg, index) => !IsHandoffPidValue(args, index))
            .Select(QuoteIfNeeded);
        var sessionId = Guid.NewGuid().ToString("N");

        var startInfo = new ProcessStartInfo
        {
            FileName = layout.GameExecutablePath,
            Arguments = string.Join(" ", forwardedArguments),
            WorkingDirectory = layout.GameRoot,
            UseShellExecute = false
        };

        startInfo.Environment["MOD_ROOT"] = layout.ModRoot;
        startInfo.Environment["MOD_GAME_ROOT"] = layout.GameRoot;
        startInfo.Environment["MOD_SESSION_ID"] = sessionId;
        startInfo.Environment["MOD_LAUNCHER_VERSION"] = LauncherVersion.DisplayVersion;

        if (TryFindExistingGameProcess(layout.GameExecutablePath, handoffProcessId, out var existingProcessId))
        {
            LauncherLog.Error("Refusing to launch because Subnautica is already running with process id " + existingProcessId + ".");
            LauncherAlert.ShowError(
                "Subnautica Already Running",
                "Subnautica is already running. Close the current game before launching the mod again.");
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

    private static int ParseHandoffProcessId(string[] args)
    {
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (!string.Equals(args[i], "--handoff-pid", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            int processId;
            if (int.TryParse(args[i + 1], out processId) && processId > 0)
            {
                return processId;
            }
        }

        return 0;
    }

    private static bool IsHandoffPidValue(string[] args, int index)
    {
        return index > 0 &&
               string.Equals(args[index - 1], "--handoff-pid", StringComparison.OrdinalIgnoreCase);
    }

    private static string QuoteIfNeeded(string value)
    {
        if (value.Contains(' ') || value.Contains('"'))
        {
            return "\"" + value.Replace("\"", "\\\"") + "\"";
        }

        return value;
    }

    private static bool WaitForHandoffProcessExit(int handoffProcessId)
    {
        try
        {
            using var process = Process.GetProcessById(handoffProcessId);
            LauncherLog.Info("Waiting for handed-off game process " + handoffProcessId + " to exit.");
            if (!process.WaitForExit(15000))
            {
                LauncherLog.Warn("Handoff process " + handoffProcessId + " did not exit within 15 seconds.");
                return false;
            }

            LauncherLog.Info("Handoff process " + handoffProcessId + " exited.");
            return true;
        }
        catch (ArgumentException)
        {
            LauncherLog.Info("Handoff process " + handoffProcessId + " already exited before launcher wait began.");
            return true;
        }
        catch (Exception ex)
        {
            LauncherLog.Warn("Failed while waiting for handoff process " + handoffProcessId + ": " + ex.Message);
            return false;
        }
    }

    private static void WaitForLaunchCooldown(string expectedExecutablePath, int ignoredProcessId)
    {
        string processName = Path.GetFileNameWithoutExtension(expectedExecutablePath);
        LauncherLog.Info("Applying launch cooldown after handoff for process name " + processName + ".");

        DateTime deadline = DateTime.UtcNow.AddSeconds(5);
        while (DateTime.UtcNow < deadline)
        {
            if (!IsAnyMatchingGameProcessRunning(processName, ignoredProcessId))
            {
                Thread.Sleep(1500);
                LauncherLog.Info("Launch cooldown complete.");
                return;
            }

            Thread.Sleep(200);
        }

        LauncherLog.Warn("Launch cooldown timed out; proceeding with relaunch.");
    }

    private static bool IsAnyMatchingGameProcessRunning(string processName, int ignoredProcessId)
    {
        Process[] processes = Process.GetProcessesByName(processName);
        for (int i = 0; i < processes.Length; i++)
        {
            Process process = processes[i];
            try
            {
                if (ignoredProcessId > 0 && process.Id == ignoredProcessId)
                {
                    continue;
                }

                return true;
            }
            catch
            {
            }
            finally
            {
                process.Dispose();
            }
        }

        return false;
    }

    private static bool TryFindExistingGameProcess(string expectedExecutablePath, int ignoredProcessId, out int processId)
    {
        processId = 0;
        string processName = Path.GetFileNameWithoutExtension(expectedExecutablePath);
        Process[] processes = Process.GetProcessesByName(processName);
        for (int i = 0; i < processes.Length; i++)
        {
            Process process = processes[i];
            try
            {
                if (ignoredProcessId > 0 && process.Id == ignoredProcessId)
                {
                    continue;
                }

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
