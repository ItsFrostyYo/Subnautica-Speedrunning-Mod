namespace SubnauticaSpeedrunningRanked.Launcher;

internal sealed class FileLayout
{
    public string RankedRoot { get; private set; } = "";
    public string GameRoot { get; private set; } = "";
    public string BootstrapDirectory { get; private set; } = "";
    public string RuntimeDirectory { get; private set; } = "";
    public string ConfigDirectory { get; private set; } = "";
    public string LogsDirectory { get; private set; } = "";
    public string CrashReportsDirectory { get; private set; } = "";
    public string ModulesDirectory { get; private set; } = "";
    public string CacheDirectory { get; private set; } = "";
    public string DataDirectory { get; private set; } = "";
    public string DoorstopConfigPath { get; private set; } = "";
    public string DoorstopLibraryPath { get; private set; } = "";
    public string DoorstopVersionPath { get; private set; } = "";
    public string GameExecutablePath { get; private set; } = "";
    public string BootstrapAssemblyPath { get; private set; } = "";
    public string RuntimeAssemblyPath { get; private set; } = "";
    public string LoaderConfigPath { get; private set; } = "";

    public static FileLayout Discover()
    {
        var baseDirectory = Path.GetFullPath(AppContext.BaseDirectory)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var directoryName = new DirectoryInfo(baseDirectory).Name;

        string rankedRoot;
        string gameRoot;
        if (string.Equals(directoryName, "SubnauticaSpeedrunningRanked", StringComparison.OrdinalIgnoreCase))
        {
            rankedRoot = baseDirectory;
            var rankedDirectory = new DirectoryInfo(rankedRoot);
            gameRoot = rankedDirectory.Parent?.FullName
                ?? throw new InvalidOperationException("The launcher must be inside a game-local SubnauticaSpeedrunningRanked folder.");
        }
        else
        {
            gameRoot = baseDirectory;
            rankedRoot = Path.Combine(gameRoot, "SubnauticaSpeedrunningRanked");
            if (!Directory.Exists(rankedRoot))
            {
                throw new InvalidOperationException(
                    "The launcher could not find a SubnauticaSpeedrunningRanked folder beside Subnautica.exe.");
            }
        }

        return new FileLayout
        {
            RankedRoot = rankedRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            GameRoot = gameRoot,
            BootstrapDirectory = Path.Combine(rankedRoot, "Bootstrap"),
            RuntimeDirectory = Path.Combine(rankedRoot, "Runtime"),
            ConfigDirectory = Path.Combine(rankedRoot, "Config"),
            LogsDirectory = Path.Combine(rankedRoot, "Logs"),
            CrashReportsDirectory = Path.Combine(rankedRoot, Path.Combine("Logs", "CrashReports")),
            ModulesDirectory = Path.Combine(rankedRoot, "Modules"),
            CacheDirectory = Path.Combine(rankedRoot, "Cache"),
            DataDirectory = Path.Combine(rankedRoot, "Data"),
            DoorstopConfigPath = Path.Combine(gameRoot, "doorstop_config.ini"),
            DoorstopLibraryPath = Path.Combine(gameRoot, "winhttp.dll"),
            DoorstopVersionPath = Path.Combine(gameRoot, ".doorstop_version"),
            GameExecutablePath = Path.Combine(gameRoot, "Subnautica.exe"),
            BootstrapAssemblyPath = Path.Combine(rankedRoot, "Bootstrap", "SubnauticaSpeedrunningRanked.Bootstrap.dll"),
            RuntimeAssemblyPath = Path.Combine(rankedRoot, "Runtime", "SubnauticaSpeedrunningRanked.Runtime.dll"),
            LoaderConfigPath = Path.Combine(rankedRoot, "Config", "loader.config.xml")
        };
    }

    public void EnsureDirectories()
    {
        Directory.CreateDirectory(BootstrapDirectory);
        Directory.CreateDirectory(RuntimeDirectory);
        Directory.CreateDirectory(ConfigDirectory);
        Directory.CreateDirectory(LogsDirectory);
        Directory.CreateDirectory(CrashReportsDirectory);
        Directory.CreateDirectory(ModulesDirectory);
        Directory.CreateDirectory(CacheDirectory);
        Directory.CreateDirectory(DataDirectory);
    }

    public void EnsureDefaultConfig()
    {
        if (File.Exists(LoaderConfigPath))
        {
            return;
        }

        var defaultConfig = """
<?xml version="1.0" encoding="utf-8"?>
<LoaderConfig>
  <EnableNetworking>false</EnableNetworking>
  <ApiBaseUrl>https://example.invalid/</ApiBaseUrl>
  <RankedEnvironmentName>production</RankedEnvironmentName>
  <EnableCrashUpload>false</EnableCrashUpload>
  <ModuleFolder>Modules</ModuleFolder>
  <LogLevel>Info</LogLevel>
</LoaderConfig>
""";

        File.WriteAllText(LoaderConfigPath, defaultConfig);
    }
}
