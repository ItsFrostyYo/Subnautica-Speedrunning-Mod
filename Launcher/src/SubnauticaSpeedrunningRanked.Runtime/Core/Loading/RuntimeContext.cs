namespace SubnauticaSpeedrunningRanked.Runtime
{
    public sealed class RuntimeContext
    {
        public string RankedRoot { get; private set; }
        public string GameRoot { get; private set; }
        public string SessionId { get; private set; }
        public string LauncherVersion { get; private set; }
        public LoaderConfig Config { get; private set; }
        public RankedServerApiClient ServerApiClient { get; private set; }

        public RuntimeContext(string rankedRoot, string gameRoot, string sessionId, string launcherVersion, LoaderConfig config, RankedServerApiClient serverApiClient)
        {
            RankedRoot = rankedRoot;
            GameRoot = gameRoot;
            SessionId = sessionId;
            LauncherVersion = launcherVersion;
            Config = config;
            ServerApiClient = serverApiClient;
        }
    }
}
