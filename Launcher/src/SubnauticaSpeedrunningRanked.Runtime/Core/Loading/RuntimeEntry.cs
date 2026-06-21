using System;
using System.Collections.Generic;
using System.IO;
using SubnauticaSpeedrunningRanked.Runtime.Seeds;
using SubnauticaSpeedrunningRanked.Shared;

namespace SubnauticaSpeedrunningRanked.Runtime
{
    public static class RuntimeEntry
    {
        private static bool _initialized;
        private static readonly object Sync = new object();

        public static void Initialize()
        {
            lock (Sync)
            {
                if (_initialized)
                {
                    return;
                }

                _initialized = true;
            }

            string rankedRoot = PathLayout.GetRankedRoot();
            RankedLog.Initialize(Path.Combine(rankedRoot, "Logs"));
            RankedLog.Info("Runtime initializing.");
            RankedLog.Info("Ranked root: " + rankedRoot);
            string gameRoot = PathLayout.GetGameRoot(rankedRoot);
            RankedLog.Info("Game root: " + gameRoot);

            AppDomain.CurrentDomain.AssemblyLoad += CurrentDomain_AssemblyLoad;

            LoaderConfig config = LoaderConfigStore.Load(
                Path.Combine(Path.Combine(rankedRoot, "Config"), "loader.config.xml"));
            string sessionId = Environment.GetEnvironmentVariable("RANKED_SESSION_ID") ?? string.Empty;
            if (string.IsNullOrEmpty(sessionId))
            {
                sessionId = Guid.NewGuid().ToString("N");
            }

            string launcherVersion = Environment.GetEnvironmentVariable("RANKED_LAUNCHER_VERSION") ?? "direct-launch";
            RuntimeContext context = new RuntimeContext(
                rankedRoot,
                gameRoot,
                sessionId,
                launcherVersion,
                config,
                new RankedServerApiClient(config.ApiBaseUrl));
            RuntimeCrashReporter.InstallGlobalHandlers(context);

            GameInstallValidationReport validationReport = GameInstallValidator.Validate(gameRoot);
            if (!validationReport.IsValid)
            {
                string reportPath = RuntimeCrashReporter.WriteCrashReport(
                    context,
                    new RuntimeValidationException(validationReport),
                    "VersionValidationFailed",
                    true);
                RankedLog.Error("Runtime version validation failed. Crash report: " + reportPath);
                throw new RuntimeValidationException(validationReport);
            }

            for (int i = 0; i < validationReport.Warnings.Count; i++)
            {
                RankedLog.Warn("Validation warning: " + validationReport.Warnings[i]);
            }

            RankedLog.Info("Game version validation passed.");
            RankedLog.Info("Session id: " + sessionId);
            RankedLog.Info("Launcher version: " + launcherVersion);
            RankedLog.Info("Networking enabled: " + config.EnableNetworking);
            RankedLog.Info("Ranked environment: " + config.RankedEnvironmentName);
            RankedLog.Info("Crash upload enabled: " + config.EnableCrashUpload);
            RankedLog.Info("Module folder: " + config.ModuleFolder);

            RankedSeedStore.Initialize(context);

            IList<IRankedModule> builtInModules = BuiltInModuleLoader.LoadModules(context);
            IList<IRankedModule> externalModules = ModuleLoader.LoadModules(context);
            RankedLog.Info("Loaded built-in module count: " + builtInModules.Count);
            RankedLog.Info("Loaded external module count: " + externalModules.Count);
        }

        private static void CurrentDomain_AssemblyLoad(object sender, AssemblyLoadEventArgs args)
        {
            try
            {
                RankedLog.Info("Assembly loaded: " + args.LoadedAssembly.FullName);
            }
            catch
            {
            }
        }
    }
}
