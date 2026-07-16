using System;
using System.Globalization;
using System.Threading;
using SubnauticaSpeedrunningMod.Runtime.Ui;
using UnityEngine;

namespace SubnauticaSpeedrunningMod.Runtime
{
    internal static class ModPrivateRaceCountdownRuntimeHost
    {
        private const float CountdownSeconds = 3f;
        private static readonly Color AheadComparisonColor = new Color(0.58f, 1f, 0.58f, 1f);
        private static readonly Color BehindComparisonColor = new Color(1f, 0.54f, 0.54f, 1f);
        private static readonly object Sync = new object();

        private static RuntimeContext _context;
        private static bool _installed;
        private static bool _pollInFlight;
        private static bool _hasPendingPoll;
        private static string _pendingPollError = string.Empty;
        private static ModMatchStateDto _pendingPollState;
        private static ModMatchStateDto _currentState;
        private static float _nextPollAt;
        private static bool _readySent;
        private static int _lastSentSplitSequence;
        private static bool _finishSent;
        private static int _localSplitSequence;
        private static long _localElapsedMilliseconds;
        private static long _localLoadlessMilliseconds;
        private static string _localLatestSplitName = string.Empty;
        private static bool _forfeitSent;
        private static bool _raceOverMenuShown;

        public static void Install(RuntimeContext context)
        {
            if (_installed)
            {
                return;
            }

            _context = context;
            _installed = true;
        }

        public static void Update(bool multiplayerActive, bool inGameplay, bool worldReady)
        {
            if (!_installed)
            {
                return;
            }

            if (!multiplayerActive || _context == null || !_context.Config.EnableNetworking || !_context.ServerApiClient.IsConfigured)
            {
                Reset();
                return;
            }

            ConsumePendingPoll();

            string matchId = ModClientSessionMode.RankedMultiplayerMatchId;
            if (string.IsNullOrEmpty(matchId))
            {
                return;
            }

            if (!_pollInFlight && Time.unscaledTime >= _nextPollAt)
            {
                BeginPoll(matchId);
            }

            UpdateCompletionUi(inGameplay);
        }

        public static void NotifyLocalReadyForCountdown()
        {
            if (!_installed || _readySent || _context == null || !_context.Config.EnableNetworking || !_context.ServerApiClient.IsConfigured)
            {
                return;
            }

            string matchId = ModClientSessionMode.RankedMultiplayerMatchId;
            if (string.IsNullOrEmpty(matchId) || !ModClientSessionMode.IsRankedMultiplayerSelected)
            {
                return;
            }

            _readySent = true;
            _nextPollAt = 0f;
            BeginFireAndForgetRequest(delegate
            {
                _context.ServerApiClient.SetMatchReady(matchId, ModClientSessionMode.RankedMultiplayerPlayerId, true);
            });
        }

        public static bool IsMatchCompleted
        {
            get
            {
                return _currentState != null &&
                       string.Equals(_currentState.status, "Completed", StringComparison.OrdinalIgnoreCase);
            }
        }

        public static bool ShouldUseForfeitMenu
        {
            get
            {
                return ModClientSessionMode.IsRankedMultiplayerSelected && !_finishSent && !IsMatchCompleted;
            }
        }

        public static bool ShouldUseReturnToRoomMenu
        {
            get
            {
                return ModClientSessionMode.IsRankedMultiplayerSelected && (_finishSent || IsMatchCompleted);
            }
        }

        public static bool DidLocalPlayerLoseRace
        {
            get
            {
                ModMatchPlayerDto localPlayer = GetLocalPlayer();
                if (localPlayer == null)
                {
                    return false;
                }

                return IsMatchCompleted &&
                       (string.Equals(localPlayer.result, "Lose", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(localPlayer.result, "Forfeit", StringComparison.OrdinalIgnoreCase));
            }
        }

        public static bool IsLocalPlayerReady
        {
            get
            {
                ModMatchPlayerDto localPlayer = GetLocalPlayer();
                if (localPlayer != null)
                {
                    return localPlayer.ready;
                }

                return _readySent;
            }
        }

        public static bool IsOpponentReady
        {
            get
            {
                ModMatchPlayerDto opponent = GetOpponentPlayer();
                return opponent != null && opponent.ready;
            }
        }

        public static bool TryGetSharedCountdownRemainingSeconds(out float remainingSeconds)
        {
            remainingSeconds = 0f;
            if (_currentState == null ||
                !string.Equals(_currentState.status, "InProgress", StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrEmpty(_currentState.startedUtc))
            {
                return false;
            }

            DateTimeOffset startedUtc;
            if (!DateTimeOffset.TryParse(
                _currentState.startedUtc,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind | DateTimeStyles.AssumeUniversal,
                out startedUtc))
            {
                return false;
            }

            remainingSeconds = (float)(startedUtc.ToUniversalTime().AddSeconds(CountdownSeconds) - DateTimeOffset.UtcNow).TotalSeconds;
            return true;
        }

        public static void OnLocalSplitAdvanced(int sequenceNumber, string splitDisplayName, long elapsedMilliseconds, long loadlessElapsedMilliseconds)
        {
            if (!_installed || !_context.Config.EnableNetworking || !_context.ServerApiClient.IsConfigured || !ModClientSessionMode.IsRankedMultiplayerSelected)
            {
                return;
            }

            if (sequenceNumber <= _lastSentSplitSequence)
            {
                return;
            }

            _lastSentSplitSequence = sequenceNumber;
            _localSplitSequence = sequenceNumber;
            _localElapsedMilliseconds = elapsedMilliseconds;
            _localLoadlessMilliseconds = loadlessElapsedMilliseconds;
            _localLatestSplitName = splitDisplayName ?? string.Empty;

            string matchId = ModClientSessionMode.RankedMultiplayerMatchId;
            if (string.IsNullOrEmpty(matchId))
            {
                return;
            }

            ModMatchSplitRequestDto splitRequest = new ModMatchSplitRequestDto
            {
                playerId = ModClientSessionMode.RankedMultiplayerPlayerId,
                splitKey = "split-" + sequenceNumber.ToString(CultureInfo.InvariantCulture),
                splitDisplayName = splitDisplayName ?? string.Empty,
                elapsedMilliseconds = elapsedMilliseconds,
                loadlessElapsedMilliseconds = loadlessElapsedMilliseconds,
                sequenceNumber = sequenceNumber
            };

            BeginFireAndForgetRequest(delegate
            {
                _context.ServerApiClient.RecordMatchSplit(matchId, splitRequest);
            });
            _nextPollAt = 0f;

            if (!_finishSent && string.Equals(splitDisplayName, "Launched the Rocket", StringComparison.OrdinalIgnoreCase))
            {
                _finishSent = true;
                ModMatchFinishRequestDto finishRequest = new ModMatchFinishRequestDto
                {
                    playerId = ModClientSessionMode.RankedMultiplayerPlayerId,
                    elapsedMilliseconds = elapsedMilliseconds,
                    loadlessElapsedMilliseconds = loadlessElapsedMilliseconds,
                    notes = "Finished via client run tracking."
                };

                BeginFireAndForgetRequest(delegate
                {
                    _context.ServerApiClient.FinishMatch(matchId, finishRequest);
                });
                _nextPollAt = 0f;
            }
        }

        public static void NotifyForfeitIfNeeded()
        {
            if (!_installed || _forfeitSent || _finishSent || !_context.Config.EnableNetworking || !_context.ServerApiClient.IsConfigured)
            {
                return;
            }

            if (!ModClientSessionMode.IsRankedMultiplayerSelected)
            {
                return;
            }

            string matchId = ModClientSessionMode.RankedMultiplayerMatchId;
            if (string.IsNullOrEmpty(matchId))
            {
                return;
            }

            _forfeitSent = true;
            ModMatchForfeitRequestDto request = new ModMatchForfeitRequestDto
            {
                playerId = ModClientSessionMode.RankedMultiplayerPlayerId,
                reason = "Player returned to menu before finishing."
            };

            BeginFireAndForgetRequest(delegate
            {
                _context.ServerApiClient.ForfeitMatch(matchId, request);
            });
            _nextPollAt = 0f;
        }

        public static string GetOpponentStatusText()
        {
            if (!ModClientSessionMode.IsRankedMultiplayerSelected)
            {
                return string.Empty;
            }

            string opponentName = GetOpponentDisplayNameOrDefault();
            ModMatchPlayerDto opponent = GetOpponentPlayer();
            if (opponent == null)
            {
                return opponentName + " Loading";
            }

            if (!string.IsNullOrEmpty(opponent.result))
            {
                if (string.Equals(opponent.result, "Forfeit", StringComparison.OrdinalIgnoreCase))
                {
                    return opponentName + " Forfeited";
                }

                if (string.Equals(opponent.result, "Win", StringComparison.OrdinalIgnoreCase))
                {
                    return opponentName + " Won";
                }
            }

            string status = string.Empty;
            if (!string.IsNullOrEmpty(opponent.currentSplitDisplayName))
            {
                status = opponentName + " " + opponent.currentSplitDisplayName;
            }
            else if (opponent.ready)
            {
                status = opponentName + " Ready";
            }
            else if (opponent.connected)
            {
                status = opponentName + " Connected";
            }
            else
            {
                status = opponentName + " Loading";
            }

            return status;
        }

        public static string GetOpponentComparisonText()
        {
            string comparisonText;
            Color _;
            return TryBuildComparisonText(out comparisonText, out _) ? "Comparison: " + comparisonText : string.Empty;
        }

        public static Color GetOpponentComparisonColor()
        {
            string comparisonText;
            Color comparisonColor;
            return TryBuildComparisonText(out comparisonText, out comparisonColor) ? comparisonColor : Color.white;
        }

        private static void ConsumePendingPoll()
        {
            if (!_hasPendingPoll)
            {
                return;
            }

            ModMatchStateDto state;
            string error;
            lock (Sync)
            {
                state = _pendingPollState;
                error = _pendingPollError;
                _pendingPollState = null;
                _pendingPollError = string.Empty;
                _hasPendingPoll = false;
                _pollInFlight = false;
            }

            if (!string.IsNullOrEmpty(error))
            {
                ModLog.Warn("Private race state poll failed: " + error);
                _nextPollAt = Time.unscaledTime + 2f;
                return;
            }

            if (state != null)
            {
                _currentState = state;
            }

            _nextPollAt = Time.unscaledTime + GetNextPollDelaySeconds();
        }

        private static void BeginPoll(string matchId)
        {
            if (_pollInFlight)
            {
                return;
            }

            _pollInFlight = true;
            ThreadPool.QueueUserWorkItem(delegate
            {
                ModMatchStateDto state = null;
                string error = string.Empty;

                try
                {
                    state = _context.ServerApiClient.GetMatchState(matchId);
                }
                catch (Exception ex)
                {
                    error = ex.Message;
                }

                lock (Sync)
                {
                    _pendingPollState = state;
                    _pendingPollError = error;
                    _hasPendingPoll = true;
                }
            });
        }

        private static void BeginFireAndForgetRequest(Action action)
        {
            if (action == null)
            {
                return;
            }

            ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    ModLog.Warn("Private race network request failed: " + ex.Message);
                }
            });
        }

        private static float GetNextPollDelaySeconds()
        {
            if (_currentState == null)
            {
                return 0.12f;
            }

            if (!string.Equals(_currentState.status, "Completed", StringComparison.OrdinalIgnoreCase))
            {
                return 0.12f;
            }

            return 0.35f;
        }

        private static ModMatchPlayerDto GetLocalPlayer()
        {
            if (_currentState == null || _currentState.players == null)
            {
                return null;
            }

            string localPlayerId = ModClientSessionMode.RankedMultiplayerPlayerId;
            for (int i = 0; i < _currentState.players.Length; i++)
            {
                ModMatchPlayerDto player = _currentState.players[i];
                if (player == null)
                {
                    continue;
                }

                if (string.Equals(player.playerId, localPlayerId, StringComparison.Ordinal))
                {
                    return player;
                }
            }

            return null;
        }

        private static ModMatchPlayerDto GetOpponentPlayer()
        {
            if (_currentState == null || _currentState.players == null)
            {
                return null;
            }

            string localPlayerId = ModClientSessionMode.RankedMultiplayerPlayerId;
            for (int i = 0; i < _currentState.players.Length; i++)
            {
                ModMatchPlayerDto player = _currentState.players[i];
                if (player == null)
                {
                    continue;
                }

                if (!string.Equals(player.playerId, localPlayerId, StringComparison.Ordinal))
                {
                    return player;
                }
            }

            return null;
        }

        private static string GetOpponentDisplayNameOrDefault()
        {
            ModMatchPlayerDto opponent = GetOpponentPlayer();
            if (opponent != null && !string.IsNullOrEmpty(opponent.displayName))
            {
                return opponent.displayName;
            }

            if (!string.IsNullOrEmpty(ModClientSessionMode.RankedMultiplayerOpponentDisplayName))
            {
                return ModClientSessionMode.RankedMultiplayerOpponentDisplayName;
            }

            return "Opponent";
        }

        private static bool TryBuildComparisonText(out string comparisonText, out Color comparisonColor)
        {
            comparisonText = string.Empty;
            comparisonColor = Color.white;

            ModMatchPlayerDto localPlayer = GetLocalPlayer();
            ModMatchPlayerDto opponent = GetOpponentPlayer();
            if (localPlayer == null || opponent == null)
            {
                return false;
            }

            int comparableSequence;
            if (!TryGetLatestComparableSequence(localPlayer, opponent, out comparableSequence) || comparableSequence <= 0)
            {
                return false;
            }

            long localTime;
            long opponentTime;
            if (!TryGetPlayerComparableTime(localPlayer, comparableSequence, out localTime) ||
                !TryGetPlayerComparableTime(opponent, comparableSequence, out opponentTime))
            {
                return false;
            }

            if (opponentTime <= 0 || localTime <= 0)
            {
                return false;
            }

            long difference = localTime - opponentTime;
            bool ahead = difference < 0;
            long absoluteDifference = Math.Abs(difference);
            TimeSpan span = TimeSpan.FromMilliseconds(absoluteDifference);
            string prefix = ahead ? "-" : "+";
            comparisonColor = ahead ? AheadComparisonColor : BehindComparisonColor;

            if (span.TotalMinutes >= 1d)
            {
                comparisonText = prefix + ((int)span.TotalMinutes).ToString(CultureInfo.InvariantCulture) + ":" +
                    span.Seconds.ToString("00", CultureInfo.InvariantCulture) + "." +
                    (span.Milliseconds / 100).ToString(CultureInfo.InvariantCulture);
                return true;
            }

            comparisonText = prefix + span.Seconds.ToString(CultureInfo.InvariantCulture) + "." +
                (span.Milliseconds / 100).ToString(CultureInfo.InvariantCulture);
            return true;
        }

        private static bool TryGetLatestComparableSequence(ModMatchPlayerDto localPlayer, ModMatchPlayerDto opponent, out int comparableSequence)
        {
            comparableSequence = 0;
            if (localPlayer == null || opponent == null)
            {
                return false;
            }

            int localHighest = GetHighestKnownSplitSequence(localPlayer, _localSplitSequence);
            int opponentHighest = GetHighestKnownSplitSequence(opponent, 0);
            int maxCommon = Math.Min(localHighest, opponentHighest);
            for (int sequenceNumber = maxCommon; sequenceNumber >= 1; sequenceNumber--)
            {
                long localTime;
                long opponentTime;
                if (!TryGetPlayerComparableTime(localPlayer, sequenceNumber, out localTime) ||
                    !TryGetPlayerComparableTime(opponent, sequenceNumber, out opponentTime))
                {
                    continue;
                }

                if (localTime <= 0 || opponentTime <= 0)
                {
                    continue;
                }

                comparableSequence = sequenceNumber;
                return true;
            }

            return false;
        }

        private static int GetHighestKnownSplitSequence(ModMatchPlayerDto player, int fallbackValue)
        {
            int highest = fallbackValue;
            if (player == null)
            {
                return highest;
            }

            if (player.splitCount > highest)
            {
                highest = player.splitCount;
            }

            int parsedSequence;
            if (TryParseSequenceNumber(player.currentSplitKey, out parsedSequence) && parsedSequence > highest)
            {
                highest = parsedSequence;
            }

            return highest;
        }

        private static bool TryGetPlayerComparableTime(ModMatchPlayerDto player, int sequenceNumber, out long comparableTime)
        {
            comparableTime = 0L;
            if (player == null || sequenceNumber <= 0)
            {
                return false;
            }

            if (_currentState != null && _currentState.splits != null)
            {
                for (int i = _currentState.splits.Length - 1; i >= 0; i--)
                {
                    ModMatchSplitDto split = _currentState.splits[i];
                    if (split == null ||
                        !string.Equals(split.playerId, player.playerId, StringComparison.Ordinal) ||
                        split.sequenceNumber != sequenceNumber)
                    {
                        continue;
                    }

                    comparableTime = split.loadlessElapsedMilliseconds > 0
                        ? split.loadlessElapsedMilliseconds
                        : split.elapsedMilliseconds;
                    return comparableTime > 0;
                }
            }

            string targetSplitKey = "split-" + sequenceNumber.ToString(CultureInfo.InvariantCulture);
            if (string.Equals(player.currentSplitKey, targetSplitKey, StringComparison.Ordinal))
            {
                comparableTime = player.currentLoadlessElapsedMilliseconds > 0
                    ? player.currentLoadlessElapsedMilliseconds
                    : player.currentSplitElapsedMilliseconds;
                return comparableTime > 0;
            }

            if (string.Equals(player.playerId, ModClientSessionMode.RankedMultiplayerPlayerId, StringComparison.Ordinal) &&
                sequenceNumber == _localSplitSequence)
            {
                comparableTime = _localLoadlessMilliseconds > 0
                    ? _localLoadlessMilliseconds
                    : _localElapsedMilliseconds;
                return comparableTime > 0;
            }

            return false;
        }

        private static bool TryParseSequenceNumber(string splitKey, out int sequenceNumber)
        {
            sequenceNumber = 0;
            if (string.IsNullOrEmpty(splitKey) || !splitKey.StartsWith("split-", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return int.TryParse(splitKey.Substring("split-".Length), NumberStyles.Integer, CultureInfo.InvariantCulture, out sequenceNumber);
        }

        private static void UpdateCompletionUi(bool inGameplay)
        {
            if (!inGameplay || !ModClientSessionMode.IsRankedMultiplayerSelected || !IsMatchCompleted)
            {
                _raceOverMenuShown = false;
                ModOverlayRuntime.SetTopCenterMessage(string.Empty, string.Empty, Color.white, Color.white, false);
                return;
            }

            ModMatchPlayerDto localPlayer = GetLocalPlayer();
            if (localPlayer == null)
            {
                return;
            }

            if (string.Equals(localPlayer.result, "Win", StringComparison.OrdinalIgnoreCase))
            {
                _finishSent = true;
                string detailText = string.Equals(GetOpponentPlayer() != null ? GetOpponentPlayer().result : string.Empty, "Forfeit", StringComparison.OrdinalIgnoreCase)
                    ? GetOpponentDisplayNameOrDefault() + " forfeited."
                    : "Return to room when ready.";
                ModOverlayRuntime.SetTopCenterMessage("You Win", detailText, AheadComparisonColor, Color.white, true);
                return;
            }

            if (!string.Equals(localPlayer.result, "Lose", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(localPlayer.result, "Forfeit", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            _finishSent = true;
            if (_raceOverMenuShown)
            {
                return;
            }

            string titleText = string.Equals(localPlayer.result, "Forfeit", StringComparison.OrdinalIgnoreCase)
                ? "Race Forfeited"
                : "Opponent Won";
            ModOverlayRuntime.SetTopCenterMessage(titleText, "Return to room when ready.", BehindComparisonColor, Color.white, true);
            ModOverlayRuntime.SetCenterMessage(string.Empty, Color.white, false);
            try
            {
                if (IngameMenu.main != null)
                {
                    IngameMenu.main.Open();
                    IngameMenu.main.ChangeSubscreen("Main");
                    _raceOverMenuShown = true;
                }
            }
            catch (Exception ex)
            {
                ModLog.Warn("Failed to open race-over menu: " + ex.Message);
            }
        }

        private static void Reset()
        {
            _pollInFlight = false;
            _hasPendingPoll = false;
            _pendingPollError = string.Empty;
            _pendingPollState = null;
            _currentState = null;
            _nextPollAt = 0f;
            _readySent = false;
            _lastSentSplitSequence = 0;
            _finishSent = false;
            _localSplitSequence = 0;
            _localElapsedMilliseconds = 0L;
            _localLoadlessMilliseconds = 0L;
            _localLatestSplitName = string.Empty;
            _forfeitSent = false;
            _raceOverMenuShown = false;
            ModOverlayRuntime.SetTopCenterMessage(string.Empty, string.Empty, Color.white, Color.white, false);
            ModOverlayRuntime.SetCenterMessage(string.Empty, Color.white, false);
        }
    }
}
