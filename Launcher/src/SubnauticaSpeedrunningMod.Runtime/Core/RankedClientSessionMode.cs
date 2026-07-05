namespace SubnauticaSpeedrunningMod.Runtime
{
    internal enum ModClientLaunchMode
    {
        Vanilla,
        ModMultiplayer,
        ModSingleplayerPractice,
        ModBetterRngSingleplayer,
        ModPracticeSave
    }

    internal static class ModClientSessionMode
    {
        private static ModClientLaunchMode _launchMode = ModClientLaunchMode.Vanilla;
        private static string _practiceCategory = string.Empty;
        private static string _practiceSaveId = string.Empty;
        private static bool _practiceSaveTimerEnabled;
        private static bool _practiceSaveStartsWithSuperSeaglide;

        public static ModClientLaunchMode LaunchMode
        {
            get { return _launchMode; }
        }

        public static bool IsRankedSingleplayerPracticeSelected
        {
            get { return _launchMode == ModClientLaunchMode.ModSingleplayerPractice; }
        }

        public static bool IsRankedMultiplayerSelected
        {
            get { return _launchMode == ModClientLaunchMode.ModMultiplayer; }
        }

        public static bool IsBetterRngSingleplayerSelected
        {
            get { return _launchMode == ModClientLaunchMode.ModBetterRngSingleplayer; }
        }

        public static bool IsPracticeSaveSelected
        {
            get { return _launchMode == ModClientLaunchMode.ModPracticeSave && !string.IsNullOrEmpty(_practiceSaveId); }
        }

        public static bool IsPracticeSaveTimerEnabled
        {
            get { return IsPracticeSaveSelected && _practiceSaveTimerEnabled; }
        }

        public static string PracticeCategory
        {
            get { return _practiceCategory; }
        }

        public static string PracticeSaveId
        {
            get { return _practiceSaveId; }
        }

        public static bool PracticeSaveStartsWithSuperSeaglide
        {
            get { return IsPracticeSaveSelected && _practiceSaveStartsWithSuperSeaglide; }
        }

        public static void SelectVanilla()
        {
            _launchMode = ModClientLaunchMode.Vanilla;
            ClearPracticeSelection();
        }

        public static void SelectRankedMultiplayer()
        {
            _launchMode = ModClientLaunchMode.ModMultiplayer;
            ClearPracticeSelection();
        }

        public static void SelectRankedSingleplayerPractice()
        {
            _launchMode = ModClientLaunchMode.ModSingleplayerPractice;
            ClearPracticeSelection();
        }

        public static void SelectBetterRngSingleplayer()
        {
            _launchMode = ModClientLaunchMode.ModBetterRngSingleplayer;
            ClearPracticeSelection();
        }

        public static void SelectPracticeSave(string category, string saveId, bool enableTimer, bool startsWithSuperSeaglide)
        {
            _launchMode = ModClientLaunchMode.ModPracticeSave;
            _practiceCategory = category ?? string.Empty;
            _practiceSaveId = saveId ?? string.Empty;
            _practiceSaveTimerEnabled = enableTimer;
            _practiceSaveStartsWithSuperSeaglide = startsWithSuperSeaglide;
        }

        public static void ResetForMainMenu()
        {
            _launchMode = ModClientLaunchMode.Vanilla;
            ClearPracticeSelection();
        }

        private static void ClearPracticeSelection()
        {
            _practiceCategory = string.Empty;
            _practiceSaveId = string.Empty;
            _practiceSaveTimerEnabled = false;
            _practiceSaveStartsWithSuperSeaglide = false;
        }
    }
}
