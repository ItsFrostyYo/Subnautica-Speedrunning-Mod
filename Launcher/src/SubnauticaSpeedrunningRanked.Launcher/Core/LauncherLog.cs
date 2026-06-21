namespace SubnauticaSpeedrunningRanked.Launcher;

internal static class LauncherLog
{
    private static readonly object Sync = new();
    private static string _logPath;

    public static void Initialize(string logsDirectory)
    {
        Directory.CreateDirectory(logsDirectory);
        _logPath = Path.Combine(logsDirectory, $"launcher-{DateTime.Now:yyyyMMdd}.log");
        Info("Logging initialized.");
    }

    public static void Info(string message)
    {
        Write("INFO", message);
    }

    public static void Error(string message)
    {
        Write("ERROR", message);
    }

    public static void Warn(string message)
    {
        Write("WARN", message);
    }

    private static void Write(string level, string message)
    {
        lock (Sync)
        {
            var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {level} {message}";
            Console.WriteLine(line);
            if (!string.IsNullOrEmpty(_logPath))
            {
                File.AppendAllText(_logPath, line + Environment.NewLine);
            }
        }
    }
}
