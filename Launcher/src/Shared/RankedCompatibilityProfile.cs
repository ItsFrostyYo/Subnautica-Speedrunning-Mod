namespace SubnauticaSpeedrunningRanked.Shared
{
    public static class RankedCompatibilityProfile
    {
        public const string RequiredFolderName = "Subnautica2018";
        public const string RequiredDisplayName = "2018";
        public const string RequiredOriginalDownload = "Subnautica_Sep2018";
        public const string RequiredModdedValue = "False";
        public const string RequiredBuildNumber = "247";
        public const string RequiredBuildTime = "9/29/2018 4:27:46 PM";
        public const string RequiredSubnauticaExeFileVersion = "5.6.2.0";

        public const string RequiredSubnauticaExeSha256 =
            "89695869B88D22CBC8847F6597BCDD7541FAFF773211BBA1F08C6809D90A64A6";
        public const string RequiredAssemblyCSharpSha256 =
            "1667EC5EF4475659FAD2487BFD67BFF1F1560721304B1605629898870849ABB7";
        public const string RequiredAssemblyCSharpFirstpassSha256 =
            "665BEAC19210492CB21365841DD5073A87BEE0D6406A359D4E72BC9613B3449B";
        public const string RequiredSubnauticaMonitorSha256 =
            "8B647CBAE3992E4CD35874876174DD05ACCEF316EF14AA6D93076467AB1EDB8D";
        public const string RequiredVersionInfoSha256 =
            "1DF53D7D9C2BD4258FE03B95C3DBB25282724E973E6354E5975DB4F4BCC68657";

        public static string RequiredVersionSummary
        {
            get
            {
                return "clean official Subnautica September 2018 speedrun build "
                    + RequiredBuildNumber
                    + " (FolderName="
                    + RequiredFolderName
                    + ", OriginalDownload="
                    + RequiredOriginalDownload
                    + ")";
            }
        }
    }
}
