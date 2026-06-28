using System;
using System.IO;

namespace SubnauticaSpeedrunningMod.Runtime
{
    internal static class ModLog
    {
        private static readonly object Sync = new object();
        private static string _logPath;

        public static void Initialize(string logsDirectory)
        {
            Directory.CreateDirectory(logsDirectory);
            _logPath = Path.Combine(logsDirectory, "runtime-" + DateTime.Now.ToString("yyyyMMdd") + ".log");
            Info("Logging initialized.");
        }

        public static void Info(string message)
        {
            Write("INFO", message);
        }

        public static void Warn(string message)
        {
            Write("WARN", message);
        }

        public static void Error(string message)
        {
            Write("ERROR", message);
        }

        public static void Error(string message, Exception exception)
        {
            Write("ERROR", message + " " + exception);
        }

        private static void Write(string level, string message)
        {
            lock (Sync)
            {
                if (string.IsNullOrEmpty(_logPath))
                {
                    return;
                }

                File.AppendAllText(
                    _logPath,
                    "[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + "] " + level + " " + message + Environment.NewLine);
            }
        }
    }
}
