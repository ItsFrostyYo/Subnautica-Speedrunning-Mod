using System;
using System.IO;
using System.Diagnostics;
using SubnauticaSpeedrunningMod.Shared;

namespace SubnauticaSpeedrunningMod.Runtime
{
    internal static class ModLog
    {
        private static readonly object Sync = new object();
        private static string _logPath;

        public static void Initialize(string logsDirectory)
        {
            Directory.CreateDirectory(logsDirectory);
            int processId = Process.GetCurrentProcess().Id;
            _logPath = Path.Combine(
                logsDirectory,
                "runtime-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-p" + processId + ".log");
            Info("Runtime logging initialized. Version=" + ModClientRelease.DisplayVersion +
                ", ProcessId=" + processId +
                ", Machine='" + Environment.MachineName + "'.");
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

                try
                {
                    File.AppendAllText(
                        _logPath,
                        "[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + "] " + level + " " + (message ?? string.Empty) + Environment.NewLine);
                }
                catch
                {
                    // Logging must never interrupt the game or a menu action.
                }
            }
        }
    }
}
