using System.Diagnostics;

namespace SubnauticaSpeedrunningRanked.Launcher;

internal static class Program
{
    public static int Main(string[] args)
    {
        FileLayout layout = null;
        try
        {
            layout = FileLayout.Discover();
            return LauncherApp.Run(args);
        }
        catch (Exception ex)
        {
            try
            {
                var reportPath = LauncherDiagnostics.WriteFatalCrashReport(layout, ex);
                Console.Error.WriteLine("A launcher crash report was written to:");
                Console.Error.WriteLine(reportPath);
            }
            catch
            {
                // Best-effort only.
            }

            Console.Error.WriteLine(ex);
            return 99;
        }
    }
}
