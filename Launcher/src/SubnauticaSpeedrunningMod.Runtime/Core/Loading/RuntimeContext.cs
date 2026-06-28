namespace SubnauticaSpeedrunningMod.Runtime
{
    public sealed class RuntimeContext
    {
        public string ModRoot { get; private set; }
        public string GameRoot { get; private set; }
        public string SessionId { get; private set; }
        public string LauncherVersion { get; private set; }
        public LoaderConfig Config { get; private set; }
        public ModServerApiClient ServerApiClient { get; private set; }

        public RuntimeContext(string modRoot, string gameRoot, string sessionId, string launcherVersion, LoaderConfig config, ModServerApiClient serverApiClient)
        {
            ModRoot = modRoot;
            GameRoot = gameRoot;
            SessionId = sessionId;
            LauncherVersion = launcherVersion;
            Config = config;
            ServerApiClient = serverApiClient;
        }
    }
}
