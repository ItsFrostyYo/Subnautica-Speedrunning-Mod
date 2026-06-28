namespace SubnauticaSpeedrunningMod.Launcher;

internal static class LauncherRuntimeGuards
{
    public static void InstallGlobalHandlers(Func<FileLayout> getLayout)
    {
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception exception)
            {
                TryWriteCrashReport(getLayout(), exception);
            }
        };

        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            TryWriteCrashReport(getLayout(), args.Exception);
        };
    }

    private static void TryWriteCrashReport(FileLayout layout, Exception exception)
    {
        try
        {
            var reportPath = LauncherDiagnostics.WriteFatalCrashReport(layout, exception);
            try
            {
                LauncherLog.Error("Fatal launcher exception. Crash report: " + reportPath);
            }
            catch
            {
            }
        }
        catch
        {
        }
    }
}
