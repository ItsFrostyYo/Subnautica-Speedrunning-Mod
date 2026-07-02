namespace SubnauticaSpeedrunningMod.Shared
{
    public static class ModClientRelease
    {
        public const string ChannelName = "Beta";
        public const string SemanticVersion = "0.7.0";
        public const string DisplayVersion = "Beta-0.7.0";
        public const string NumericVersion = "0.7.0.0";
        public const string RepositoryOwner = "ItsFrostyYo";
        public const string RepositoryName = "Subnautica-Speedrunning-Mod";
        public const string ReleaseBranchName = "main";
        public const string ReleaseManifestFileName = "latest.json";
        public const string ReleaseManifestUrl =
            "https://raw.githubusercontent.com/" + RepositoryOwner + "/" + RepositoryName + "/" + ReleaseBranchName + "/release/" + ReleaseManifestFileName;
        public const string ReleaseDownloadBaseUrl =
            "https://raw.githubusercontent.com/" + RepositoryOwner + "/" + RepositoryName + "/" + ReleaseBranchName + "/release/";
    }
}
