using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using SubnauticaSpeedrunningRanked.Shared;

namespace SubnauticaSpeedrunningRanked.Runtime
{
    internal static class RuntimeCrashReporter
    {
        public static void InstallGlobalHandlers(RuntimeContext context)
        {
            AppDomain.CurrentDomain.UnhandledException += delegate(object sender, UnhandledExceptionEventArgs args)
            {
                Exception exception = args.ExceptionObject as Exception;
                WriteCrashReport(context, exception, "UnhandledException", args.IsTerminating);
            };
        }

        public static string WriteCrashReport(RuntimeContext context, Exception exception, string reason, bool isTerminating)
        {
            string crashDirectory = Path.Combine(Path.Combine(context.RankedRoot, "Logs"), "CrashReports");
            Directory.CreateDirectory(crashDirectory);

            string path = Path.Combine(
                crashDirectory,
                "runtime-crash-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".txt");

            StringBuilder builder = new StringBuilder();
            builder.AppendLine("Subnautica Speedrunning Ranked Runtime Crash Report");
            builder.AppendLine("GeneratedAt=" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"));
            builder.AppendLine("Reason=" + reason);
            builder.AppendLine("IsTerminating=" + isTerminating);
            builder.AppendLine("RankedRoot=" + context.RankedRoot);
            builder.AppendLine("GameRoot=" + context.GameRoot);
            builder.AppendLine("LauncherVersion=" + context.LauncherVersion);
            builder.AppendLine("SessionId=" + context.SessionId);
            builder.AppendLine("RankedEnvironmentName=" + context.Config.RankedEnvironmentName);
            builder.AppendLine("EnableNetworking=" + context.Config.EnableNetworking);
            builder.AppendLine("ApiBaseUrl=" + context.Config.ApiBaseUrl);
            builder.AppendLine("EnableCrashUpload=" + context.Config.EnableCrashUpload);
            builder.AppendLine();

            GameInstallValidationReport validationReport = GameInstallValidator.Validate(context.GameRoot);
            builder.AppendLine(validationReport.ToDiagnosticText());
            builder.AppendLine();
            builder.AppendLine("[LoadedAssemblies]");
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                try
                {
                    builder.AppendLine(assemblies[i].FullName);
                }
                catch
                {
                }
            }

            builder.AppendLine();
            builder.AppendLine(exception != null ? exception.ToString() : "(no exception object)");

            File.WriteAllText(path, builder.ToString());
            return path;
        }
    }
}
