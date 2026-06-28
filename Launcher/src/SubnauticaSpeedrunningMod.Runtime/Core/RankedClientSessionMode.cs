namespace SubnauticaSpeedrunningMod.Runtime
{
    internal enum ModClientLaunchMode
    {
        Vanilla,
        ModMultiplayer,
        ModSingleplayerPractice
    }

    internal static class ModClientSessionMode
    {
        private static ModClientLaunchMode _launchMode = ModClientLaunchMode.Vanilla;

        public static ModClientLaunchMode LaunchMode
        {
            get { return _launchMode; }
        }

        public static bool IsRankedSingleplayerPracticeSelected
        {
            get { return _launchMode == ModClientLaunchMode.ModSingleplayerPractice; }
        }

        public static void SelectVanilla()
        {
            _launchMode = ModClientLaunchMode.Vanilla;
        }

        public static void SelectRankedMultiplayer()
        {
            _launchMode = ModClientLaunchMode.ModMultiplayer;
        }

        public static void SelectRankedSingleplayerPractice()
        {
            _launchMode = ModClientLaunchMode.ModSingleplayerPractice;
        }

        public static void ResetForMainMenu()
        {
            _launchMode = ModClientLaunchMode.Vanilla;
        }
    }
}
