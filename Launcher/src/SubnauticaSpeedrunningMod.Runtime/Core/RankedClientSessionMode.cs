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
        private static string _multiplayerMode = string.Empty;
        private static string _multiplayerMatchId = string.Empty;
        private static string _multiplayerPlayerId = string.Empty;
        private static string _multiplayerOpponentPlayerId = string.Empty;
        private static string _multiplayerOpponentDisplayName = string.Empty;
        private static string _multiplayerSeedId = string.Empty;
        private static string _multiplayerSeedValue = string.Empty;
        private static bool _privateRaceLocalSessionActive;
        private static bool _privateRaceLocalHost;
        private static string _privateRaceLocalDisplayName = string.Empty;

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

        public static string RankedMultiplayerMode
        {
            get { return _multiplayerMode; }
        }

        public static string RankedMultiplayerMatchId
        {
            get { return _multiplayerMatchId; }
        }

        public static string RankedMultiplayerPlayerId
        {
            get { return _multiplayerPlayerId; }
        }

        public static string RankedMultiplayerOpponentPlayerId
        {
            get { return _multiplayerOpponentPlayerId; }
        }

        public static string RankedMultiplayerOpponentDisplayName
        {
            get { return _multiplayerOpponentDisplayName; }
        }

        public static string RankedMultiplayerSeedId
        {
            get { return _multiplayerSeedId; }
        }

        public static string RankedMultiplayerSeedValue
        {
            get { return _multiplayerSeedValue; }
        }

        public static bool IsPrivateRaceLocalSessionActive
        {
            get { return _privateRaceLocalSessionActive; }
        }

        public static bool IsPrivateRaceLocalHost
        {
            get { return _privateRaceLocalHost; }
        }

        public static string PrivateRaceLocalDisplayName
        {
            get { return _privateRaceLocalDisplayName; }
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
            ClearMultiplayerSelection();
        }

        public static void SelectRankedMultiplayer()
        {
            _launchMode = ModClientLaunchMode.ModMultiplayer;
            ClearPracticeSelection();
            ClearMultiplayerSelection();
        }

        public static void SelectRankedMultiplayer(string mode)
        {
            _launchMode = ModClientLaunchMode.ModMultiplayer;
            ClearPracticeSelection();
            _multiplayerMode = mode ?? string.Empty;
        }

        public static void ActivateRankedMultiplayerMatch(
            string mode,
            string matchId,
            string playerId,
            string opponentPlayerId,
            string opponentDisplayName,
            string seedId,
            string seedValue)
        {
            _launchMode = ModClientLaunchMode.ModMultiplayer;
            ClearPracticeSelection();
            _multiplayerMode = mode ?? string.Empty;
            _multiplayerMatchId = matchId ?? string.Empty;
            _multiplayerPlayerId = playerId ?? string.Empty;
            _multiplayerOpponentPlayerId = opponentPlayerId ?? string.Empty;
            _multiplayerOpponentDisplayName = opponentDisplayName ?? string.Empty;
            _multiplayerSeedId = seedId ?? string.Empty;
            _multiplayerSeedValue = seedValue ?? string.Empty;
        }

        public static void AttachPrivateRaceLocalRoom(string displayName, bool isHost)
        {
            _privateRaceLocalSessionActive = true;
            _privateRaceLocalHost = isHost;
            _privateRaceLocalDisplayName = displayName ?? string.Empty;
        }

        public static void ActivateLocalPrivateRace(
            string mode,
            string displayName,
            string seedId,
            string seedValue)
        {
            _launchMode = ModClientLaunchMode.ModMultiplayer;
            ClearPracticeSelection();
            _multiplayerMode = mode ?? string.Empty;
            _multiplayerMatchId = "local-private-race";
            _multiplayerPlayerId = "local-host";
            _multiplayerOpponentPlayerId = "pending-joiner";
            _multiplayerOpponentDisplayName = "Waiting for Opponent";
            _multiplayerSeedId = seedId ?? string.Empty;
            _multiplayerSeedValue = seedValue ?? string.Empty;
            _privateRaceLocalSessionActive = true;
            _privateRaceLocalHost = true;
            _privateRaceLocalDisplayName = displayName ?? string.Empty;
        }

        public static void SelectRankedSingleplayerPractice()
        {
            _launchMode = ModClientLaunchMode.ModSingleplayerPractice;
            ClearPracticeSelection();
            ClearMultiplayerSelection();
        }

        public static void SelectBetterRngSingleplayer()
        {
            _launchMode = ModClientLaunchMode.ModBetterRngSingleplayer;
            ClearPracticeSelection();
            ClearMultiplayerSelection();
        }

        public static void SelectPracticeSave(string category, string saveId, bool enableTimer, bool startsWithSuperSeaglide)
        {
            _launchMode = ModClientLaunchMode.ModPracticeSave;
            _practiceCategory = category ?? string.Empty;
            _practiceSaveId = saveId ?? string.Empty;
            _practiceSaveTimerEnabled = enableTimer;
            _practiceSaveStartsWithSuperSeaglide = startsWithSuperSeaglide;
            ClearMultiplayerSelection();
        }

        public static void ResetForMainMenu()
        {
            _launchMode = ModClientLaunchMode.Vanilla;
            ClearPracticeSelection();
            ClearMultiplayerSelection();
        }

        private static void ClearPracticeSelection()
        {
            _practiceCategory = string.Empty;
            _practiceSaveId = string.Empty;
            _practiceSaveTimerEnabled = false;
            _practiceSaveStartsWithSuperSeaglide = false;
        }

        private static void ClearMultiplayerSelection()
        {
            _multiplayerMode = string.Empty;
            _multiplayerMatchId = string.Empty;
            _multiplayerPlayerId = string.Empty;
            _multiplayerOpponentPlayerId = string.Empty;
            _multiplayerOpponentDisplayName = string.Empty;
            _multiplayerSeedId = string.Empty;
            _multiplayerSeedValue = string.Empty;
            _privateRaceLocalSessionActive = false;
            _privateRaceLocalHost = false;
            _privateRaceLocalDisplayName = string.Empty;
        }
    }
}
