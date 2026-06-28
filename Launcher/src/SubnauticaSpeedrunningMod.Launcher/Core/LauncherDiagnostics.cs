using System.Text;
using SubnauticaSpeedrunningMod.Shared;

namespace SubnauticaSpeedrunningMod.Launcher;

internal static class LauncherDiagnostics
{
    public static string WriteValidationReport(FileLayout layout, GameInstallValidationReport report, string prefix)
    {
        Directory.CreateDirectory(layout.CrashReportsDirectory);
        var path = Path.Combine(
            layout.CrashReportsDirectory,
            $"{prefix}-{DateTime.Now:yyyyMMdd-HHmmss}.txt");

        var builder = new StringBuilder();
        builder.AppendLine(report.ToDiagnosticText());
        builder.AppendLine();
        builder.AppendLine("[Launcher]");
        builder.AppendLine("LauncherVersion=" + LauncherVersion.DisplayVersion);
        builder.AppendLine("ModRoot=" + layout.ModRoot);
        builder.AppendLine("GameRoot=" + layout.GameRoot);
        builder.AppendLine("BootstrapAssemblyPath=" + layout.BootstrapAssemblyPath);
        builder.AppendLine("RuntimeAssemblyPath=" + layout.RuntimeAssemblyPath);

        File.WriteAllText(path, builder.ToString());
        return path;
    }

    public static string WriteFatalCrashReport(FileLayout layout, Exception exception)
    {
        var crashDirectory = layout != null
            ? layout.CrashReportsDirectory
            : Path.Combine(AppContext.BaseDirectory, "CrashReports");

        Directory.CreateDirectory(crashDirectory);
        var path = Path.Combine(
            crashDirectory,
            $"launcher-crash-{DateTime.Now:yyyyMMdd-HHmmss}.txt");

        var builder = new StringBuilder();
        builder.AppendLine("Subnautica Speedrunning Mod Launcher Crash Report");
        builder.AppendLine("GeneratedAt=" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"));
        builder.AppendLine("LauncherVersion=" + LauncherVersion.DisplayVersion);
        if (layout is not null)
        {
            builder.AppendLine("ModRoot=" + layout.ModRoot);
            builder.AppendLine("GameRoot=" + layout.GameRoot);
        }

        builder.AppendLine();
        builder.AppendLine(exception.ToString());
        File.WriteAllText(path, builder.ToString());
        return path;
    }
}
