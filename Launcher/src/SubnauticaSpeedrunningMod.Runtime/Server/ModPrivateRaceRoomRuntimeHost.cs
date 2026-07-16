using System;
using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Threading;
using HarmonyLib;
using SubnauticaSpeedrunningMod.Runtime.Seeds;
using SubnauticaSpeedrunningMod.Runtime.Ui;
using UWE;
using UnityEngine;

namespace SubnauticaSpeedrunningMod.Runtime
{
    internal static class ModPrivateRaceRoomRuntimeHost
    {
        private enum PrivateRaceRoomState
        {
            Idle,
            Connecting,
            Connected,
            Launching,
            Error
        }

        private static readonly string[] SupportedModes = { "Survival", "Creative" };
        private static readonly object Sync = new object();
        private const string HarmonyId = "subnautica.speedrunning.mod.private.race.room";

        private static RuntimeContext _context;
        private static bool _installed;
        private static bool _harmonyInstalled;
        private static Harmony _harmony;
        private static PrivateRaceRoomState _state = PrivateRaceRoomState.Idle;
        private static string _hostDisplayName = "Player";
        private static string _requestedRoomCode = string.Empty;
        private static bool _localIsRoomHost;
        private static int _selectedModeIndex;
        private static string _lastPreparedSeedId = string.Empty;
        private static string _lastPreparedSeedValue = string.Empty;
        private static float _lastPreparedSpawnX;
        private static float _lastPreparedSpawnZ;
        private static string _lastPreparedSpawnDescription = string.Empty;
        private static string _lastActivatedRaceToken = string.Empty;
        private static readonly Vector3 CreativeCountdownForward = new Vector3(-0.6f, -0.1f, 0.8f);
        private static GameMode _localLaunchGameMode = GameMode.None;
        private static bool _localLaunchPending;
        private static bool _localCountdownStarted;
        private static bool _localSkipIssued;
        private static bool _localCreativeCountdownLockActive;
        private static float _localWaitingStartedAt;
        private static float _localCountdownEndsAt;
        private static bool _requestInFlight;
        private static bool _hasPendingResponse;
        private static string _pendingError = string.Empty;
        private static ModMatchStateDto _pendingState;
        private static ModMatchStateDto _currentState;
        private static float _nextPollAt;
        private static PendingServerOperation _pendingServerOperation = PendingServerOperation.None;
        private static string _pendingLaunchMode = string.Empty;
        private static GameMode _pendingLaunchGameMode = GameMode.None;
        private static bool _deferredStartRequested;
        private static string _deferredStartRoomCode = string.Empty;
        private static ModPrivateRaceStartRequestDto _deferredStartRequest;

        public static void Install(RuntimeContext context)
        {
            if (_installed)
            {
                return;
            }

            _context = context;
            ModClientIdentityStore.Initialize(context);
            _installed = true;
            ModLog.Info("Private race room runtime installed.");
        }

        public static bool IsRoomActive
        {
            get
            {
                return _state == PrivateRaceRoomState.Connecting ||
                       _state == PrivateRaceRoomState.Connected ||
                       _state == PrivateRaceRoomState.Launching ||
                       _state == PrivateRaceRoomState.Error;
            }
        }

        public static bool IsConnected
        {
            get { return _state == PrivateRaceRoomState.Connected; }
        }

        public static bool IsConnecting
        {
            get { return _state == PrivateRaceRoomState.Connecting; }
        }

        public static bool IsLaunching
        {
            get { return _state == PrivateRaceRoomState.Launching; }
        }

        public static bool IsError
        {
            get { return _state == PrivateRaceRoomState.Error; }
        }

        public static string HostDisplayName
        {
            get { return _hostDisplayName; }
        }

        public static string HostDisplayNameOrDefault
        {
            get { return string.IsNullOrEmpty(_hostDisplayName) ? "Player" : _hostDisplayName; }
        }

        public static string LocalDisplayNameOrDefault
        {
            get { return string.IsNullOrEmpty(_hostDisplayName) ? "Player" : _hostDisplayName; }
        }

        public static bool IsLocalHost
        {
            get
            {
                string localPlayerId = ResolveLocalPlayerId();
                if (_currentState != null &&
                    _currentState.players != null &&
                    _currentState.players.Length > 0 &&
                    _currentState.players[0] != null &&
                    !string.IsNullOrEmpty(localPlayerId) &&
                    string.Equals(_currentState.players[0].playerId, localPlayerId, StringComparison.Ordinal))
                {
                    return true;
                }

                return _localIsRoomHost;
            }
        }

        public static int SelectedModeIndex
        {
            get { return Mathf.Clamp(_selectedModeIndex, 0, SupportedModes.Length - 1); }
        }

        public static string SelectedMode
        {
            get { return SupportedModes[SelectedModeIndex]; }
        }

        public static string[] GetSupportedModes()
        {
            return SupportedModes;
        }

        public static void ResetForMainMenu()
        {
            _state = PrivateRaceRoomState.Idle;
            _localIsRoomHost = false;
            _selectedModeIndex = 0;
            _lastPreparedSeedId = string.Empty;
            _lastPreparedSeedValue = string.Empty;
            _lastPreparedSpawnX = 0f;
            _lastPreparedSpawnZ = 0f;
            _lastPreparedSpawnDescription = string.Empty;
            _requestedRoomCode = string.Empty;
            _lastActivatedRaceToken = string.Empty;
            _requestInFlight = false;
            _hasPendingResponse = false;
            _pendingError = string.Empty;
            _pendingState = null;
            _currentState = null;
            _nextPollAt = 0f;
            _pendingServerOperation = PendingServerOperation.None;
            _pendingLaunchMode = string.Empty;
            _pendingLaunchGameMode = GameMode.None;
            _deferredStartRequested = false;
            _deferredStartRoomCode = string.Empty;
            _deferredStartRequest = null;
            ResetLocalLaunchState();
        }

        public static void ReturnToRoomAfterGameplay()
        {
            if (_state == PrivateRaceRoomState.Idle)
            {
                return;
            }

            _state = PrivateRaceRoomState.Connected;
            ResetLocalLaunchState();
            MarkReturnedToRoom();
            _nextPollAt = 0f;
            ModLog.Info("Returned to the private race room after gameplay.");
        }

        public static void Update()
        {
            if (!_installed)
            {
                return;
            }

            ConsumePendingResponse();

            if ((_state == PrivateRaceRoomState.Connected || _state == PrivateRaceRoomState.Launching || _state == PrivateRaceRoomState.Error) &&
                !_requestInFlight &&
                _currentState != null &&
                !string.IsNullOrEmpty(_currentState.roomCode) &&
                Time.unscaledTime >= _nextPollAt)
            {
                BeginBackgroundRequest(PendingServerOperation.PollRoom, _currentState.roomCode, null);
            }

            UpdateLocalPrivateRaceLaunch();
        }

        public static string SanitizeDisplayName(string rawValue)
        {
            string trimmed = (rawValue ?? string.Empty).Trim();
            if (trimmed.Length > 24)
            {
                trimmed = trimmed.Substring(0, 24);
            }

            return trimmed;
        }

        public static string SanitizeRoomCode(string rawValue)
        {
            string trimmed = (rawValue ?? string.Empty).Trim().ToUpperInvariant();
            if (trimmed.Length > 6)
            {
                trimmed = trimmed.Substring(0, 6);
            }

            return trimmed;
        }

        public static void BeginHosting(string hostDisplayName)
        {
            if (!_installed || _context == null)
            {
                return;
            }

            if (!_context.Config.EnableNetworking || !_context.ServerApiClient.IsConfigured)
            {
                SetErrorState("Networking is not configured yet. Set EnableNetworking=true and ApiBaseUrl in loader.config.xml first.");
                return;
            }

            string sanitizedName = SanitizeDisplayName(hostDisplayName);
            if (string.IsNullOrEmpty(sanitizedName))
            {
                sanitizedName = "Player";
            }

            _hostDisplayName = sanitizedName;
            _requestedRoomCode = string.Empty;
            _localIsRoomHost = true;
            _state = PrivateRaceRoomState.Connecting;
            _lastPreparedSeedId = string.Empty;
            _lastPreparedSeedValue = string.Empty;
            _lastPreparedSpawnX = 0f;
            _lastPreparedSpawnZ = 0f;
            _lastPreparedSpawnDescription = string.Empty;
            _currentState = null;
            _pendingServerOperation = PendingServerOperation.None;
            ResetLocalLaunchState();
            BeginBackgroundRequest(PendingServerOperation.HostRoom, string.Empty, null);
            ModLog.Info("Starting private race room connection for host '" + _hostDisplayName + "'.");
        }

        public static void BeginJoining(string displayName, string roomCode)
        {
            if (!_installed || _context == null)
            {
                return;
            }

            if (!_context.Config.EnableNetworking || !_context.ServerApiClient.IsConfigured)
            {
                SetErrorState("Networking is not configured yet. Set EnableNetworking=true and ApiBaseUrl in loader.config.xml first.");
                return;
            }

            string sanitizedName = SanitizeDisplayName(displayName);
            if (string.IsNullOrEmpty(sanitizedName))
            {
                sanitizedName = "Player";
            }

            string sanitizedRoomCode = SanitizeRoomCode(roomCode);
            if (string.IsNullOrEmpty(sanitizedRoomCode))
            {
                SetErrorState("Enter a valid room code.");
                return;
            }

            _hostDisplayName = sanitizedName;
            _requestedRoomCode = sanitizedRoomCode;
            _localIsRoomHost = false;
            _state = PrivateRaceRoomState.Connecting;
            _lastPreparedSeedId = string.Empty;
            _lastPreparedSeedValue = string.Empty;
            _lastPreparedSpawnX = 0f;
            _lastPreparedSpawnZ = 0f;
            _lastPreparedSpawnDescription = string.Empty;
            _currentState = null;
            _pendingServerOperation = PendingServerOperation.None;
            ResetLocalLaunchState();
            BeginBackgroundRequest(PendingServerOperation.JoinRoom, sanitizedRoomCode, null);
            ModLog.Info("Joining private race room '" + sanitizedRoomCode + "' as '" + _hostDisplayName + "'.");
        }

        public static void LeaveRoom()
        {
            TryNotifyServerRoomLeave();
            ResetForMainMenu();
            ModClientSessionMode.SelectVanilla();
            ModLog.Info("Left private race room.");
        }

        public static void SetSelectedModeIndex(int value)
        {
            _selectedModeIndex = Mathf.Clamp(value, 0, SupportedModes.Length - 1);
        }

        public static void AdvanceSelectedMode()
        {
            if (SupportedModes.Length <= 0)
            {
                return;
            }

            _selectedModeIndex++;
            if (_selectedModeIndex >= SupportedModes.Length)
            {
                _selectedModeIndex = 0;
            }
        }

        public static void OffsetSelectedModeIndex(int delta)
        {
            if (SupportedModes.Length <= 0)
            {
                return;
            }

            int nextIndex = (_selectedModeIndex + delta) % SupportedModes.Length;
            if (nextIndex < 0)
            {
                nextIndex += SupportedModes.Length;
            }

            _selectedModeIndex = nextIndex;
        }

        public static string GetPanelTitle()
        {
            switch (_state)
            {
                case PrivateRaceRoomState.Connecting:
                    return "Connecting";
                case PrivateRaceRoomState.Connected:
                case PrivateRaceRoomState.Launching:
                    return "Private Race Room";
                default:
                    return "Host Race";
            }
        }

        public static string GetStatusMessage()
        {
            if (_currentState != null && string.Equals(_currentState.status, "Completed", StringComparison.OrdinalIgnoreCase))
            {
                bool opponentReturned = HasOpponentReturnedToRoom();
                bool localReturned = HasLocalReturnedToRoom();
                if (HasJoiner() && localReturned && opponentReturned)
                {
                    return IsLocalHost
                        ? "Both players returned. Start another race when ready."
                        : "Waiting for the host to start another race.";
                }

                return HasJoiner()
                    ? "Waiting for the other player to return to the room."
                    : "Race finished.";
            }

            switch (_state)
            {
                case PrivateRaceRoomState.Connecting:
                    return "Connecting to private room...";
                case PrivateRaceRoomState.Launching:
                    return "Starting " + SelectedMode + " race...";
                case PrivateRaceRoomState.Connected:
                    if (_currentState != null &&
                        string.Equals(_currentState.status, "WaitingForHostStart", StringComparison.OrdinalIgnoreCase))
                    {
                        return IsLocalHost
                            ? "Room ready. Choose a mode and start when ready."
                            : "Room ready. Waiting for host to start.";
                    }

                    if (_currentState != null &&
                        string.Equals(_currentState.status, "WaitingForReady", StringComparison.OrdinalIgnoreCase))
                    {
                        return "Room ready. Waiting for host to start.";
                    }

                    if (_currentState != null &&
                        string.Equals(_currentState.status, "InProgress", StringComparison.OrdinalIgnoreCase))
                    {
                        return "Race loading. Waiting for both players to be ready.";
                    }

                    return HasJoiner()
                        ? "Room ready. Waiting for host to start."
                        : "Waiting for one joiner.";
                case PrivateRaceRoomState.Error:
                    return string.IsNullOrEmpty(_pendingError) ? "Connection failed." : _pendingError;
                default:
                    return string.Empty;
            }
        }

        public static string GetRoomCodeText()
        {
            if (_currentState != null && !string.IsNullOrEmpty(_currentState.roomCode))
            {
                return _currentState.roomCode;
            }

            if (!string.IsNullOrEmpty(_requestedRoomCode))
            {
                return _requestedRoomCode;
            }

            return _state == PrivateRaceRoomState.Connecting ? "Generating..." : "Unavailable";
        }

        public static string GetRoomHostDisplayText()
        {
            if (_currentState != null &&
                _currentState.players != null &&
                _currentState.players.Length > 0 &&
                _currentState.players[0] != null &&
                !string.IsNullOrEmpty(_currentState.players[0].displayName))
            {
                return _currentState.players[0].displayName;
            }

            return HostDisplayNameOrDefault;
        }

        public static string GetJoinerDisplayText()
        {
            ModMatchPlayerDto joiner = GetJoinerPlayer();
            return joiner != null && !string.IsNullOrEmpty(joiner.displayName)
                ? joiner.displayName
                : "Waiting for Player 2";
        }

        public static string GetLocalPlayerDisplayText()
        {
            ModMatchPlayerDto localPlayer = GetPlayerById(ResolveLocalPlayerId());
            if (localPlayer != null && !string.IsNullOrEmpty(localPlayer.displayName))
            {
                return localPlayer.displayName;
            }

            return LocalDisplayNameOrDefault;
        }

        public static bool HasJoinedOpponent
        {
            get { return GetJoinerPlayer() != null; }
        }

        public static bool CanStartRace()
        {
            if (_state == PrivateRaceRoomState.Connecting ||
                _state == PrivateRaceRoomState.Error ||
                _state == PrivateRaceRoomState.Launching ||
                !IsLocalHost ||
                _currentState == null ||
                string.IsNullOrEmpty(_currentState.roomCode) ||
                !HasJoinedOpponent)
            {
                return false;
            }

            bool waitingForFreshStart =
                string.Equals(_currentState.status, "WaitingForHostStart", StringComparison.OrdinalIgnoreCase);
            if (string.Equals(_currentState.status, "Completed", StringComparison.OrdinalIgnoreCase))
            {
                return HasLocalReturnedToRoom() && HasOpponentReturnedToRoom();
            }

            return waitingForFreshStart;
        }

        public static void StartLocalRace()
        {
            ModLog.Info("Private race room start requested. State=" + _state + ", mode=" + SelectedMode + ".");

            if (!_installed || _context == null)
            {
                ModLog.Warn("Private race room start ignored because runtime was unavailable.");
                return;
            }

            if (!CanStartRace())
            {
                ModLog.Warn("Private race room start ignored because the room was not ready.");
                return;
            }

            uGUI_MainMenu menu = uGUI_MainMenu.main;
            if (menu == null)
            {
                ModLog.Warn("Private race room could not start because the main menu was unavailable.");
                return;
            }

            string mode = SelectedMode;
            GameMode gameMode = string.Equals(mode, "Creative", StringComparison.OrdinalIgnoreCase)
                ? GameMode.Creative
                : GameMode.Survival;

            if (!PrepareSelectedSeed(gameMode, out _lastPreparedSeedId, out _lastPreparedSeedValue))
            {
                return;
            }

            _state = PrivateRaceRoomState.Launching;
            _pendingLaunchMode = mode;
            _pendingLaunchGameMode = gameMode;
            ModPrivateRaceStartRequestDto startRequest = new ModPrivateRaceStartRequestDto
            {
                playerId = ResolveLocalPlayerId(),
                mode = mode,
                seedMultiplier = 1d,
                spawnProfile = gameMode == GameMode.Creative ? "creative-range" : "clip-c",
                seedId = _lastPreparedSeedId ?? string.Empty,
                seedValue = _lastPreparedSeedValue ?? string.Empty,
                spawnX = _lastPreparedSpawnX,
                spawnZ = _lastPreparedSpawnZ
            };

            string roomCode = _currentState == null ? string.Empty : _currentState.roomCode;
            if (_requestInFlight)
            {
                _deferredStartRequested = true;
                _deferredStartRoomCode = roomCode;
                _deferredStartRequest = startRequest;
                ModLog.Info("Queued private race start while a room request was still in flight.");
                return;
            }

            BeginBackgroundRequest(PendingServerOperation.StartRoom, roomCode, startRequest);
        }

        private static bool PrepareSelectedSeed(GameMode mode, out string seedId, out string seedValue)
        {
            seedId = string.Empty;
            seedValue = string.Empty;

            if (mode == GameMode.Creative)
            {
                string utcStamp = DateTime.UtcNow.ToString("yyyyMMddHHmmssfff", CultureInfo.InvariantCulture);
                seedId = "LocalPrivateRace-Creative";
                seedValue = "creative-" + utcStamp;
                _lastPreparedSpawnX = 0f;
                _lastPreparedSpawnZ = 0f;
                _lastPreparedSpawnDescription = "Creative race spawn resolved from shared seed.";
                ModSeedStore.PreparePendingExternalSeed(
                    GameMode.Creative,
                    seedId,
                    seedValue,
                    "Local private Creative race seed for host '" + HostDisplayNameOrDefault + "'.");
                ModLog.Info("Prepared local Creative private race seed '" + seedValue + "'.");
                return true;
            }

            string seedDirectoryPath;
            string seedName;
            if (!ModRankedSurvivalBatchSeedCatalog.TryChooseSeed(out seedDirectoryPath, out seedName))
            {
                ShowStartError(
                    "Ranked survival batch seed files are missing. Add folders like 'BS 1' and 'BS 2' inside '" +
                    ModRankedSurvivalBatchSeedCatalog.GetInstalledRootPath() +
                    "'.");
                return false;
            }

            float spawnX;
            float spawnZ;
            string spawnDescription;
            ModRankedSurvivalBatchSeedCatalog.ResolveClipCSpawn(out spawnX, out spawnZ, out spawnDescription, seedName);
            ModRankedSurvivalBatchSeedRuntime.PreparePendingExternalSelection(seedDirectoryPath, seedName, spawnX, spawnZ, spawnDescription);
            seedId = string.IsNullOrEmpty(seedName) ? "BS ?" : seedName;
            seedValue = seedId +
                "|clipc|" +
                spawnX.ToString("0.###", CultureInfo.InvariantCulture) +
                "|" +
                spawnZ.ToString("0.###", CultureInfo.InvariantCulture) +
                "|" +
                DateTime.UtcNow.ToString("yyyyMMddHHmmssfff", CultureInfo.InvariantCulture);
            _lastPreparedSpawnX = spawnX;
            _lastPreparedSpawnZ = spawnZ;
            _lastPreparedSpawnDescription = spawnDescription;
            ModLog.Info("Prepared local Survival private race seed '" + seedId + "' with spawn " + spawnDescription + ".");
            return true;
        }

        private static void ShowStartError(string message)
        {
            _state = PrivateRaceRoomState.Connected;
            ResetLocalLaunchState();
            ModLog.Warn(message);
            if (uGUI.main != null && uGUI.main.confirmation != null)
            {
                uGUI.main.confirmation.Show(message, delegate(bool confirmed)
                    {
                    });
            }
        }

        private static void ConsumePendingResponse()
        {
            if (!_hasPendingResponse)
            {
                return;
            }

            ModMatchStateDto state;
            string pendingError;
            PendingServerOperation completedOperation;
            lock (Sync)
            {
                state = _pendingState;
                pendingError = _pendingError;
                completedOperation = _pendingServerOperation;
                _pendingState = null;
                _pendingError = string.Empty;
                _pendingServerOperation = PendingServerOperation.None;
                _hasPendingResponse = false;
                _requestInFlight = false;
            }

            if (!string.IsNullOrEmpty(pendingError))
            {
                SetErrorState(pendingError);
                return;
            }

            if (state == null)
            {
                return;
            }

            _currentState = state;
            _nextPollAt = Time.unscaledTime + 0.12f;

            if (_state == PrivateRaceRoomState.Connecting)
            {
                _state = PrivateRaceRoomState.Connected;
                ModLog.Info("Private race room connected. Room code=" + GetRoomCodeText() + ".");
            }
            else if (_state == PrivateRaceRoomState.Error && _currentState != null)
            {
                _state = PrivateRaceRoomState.Connected;
            }

            if (completedOperation == PendingServerOperation.PollRoom)
            {
                string joinerName = GetJoinerPlayer() == null ? "none" : (GetJoinerPlayer().displayName ?? "unknown");
                int playerCount = _currentState.players == null ? 0 : _currentState.players.Length;
                ModLog.Info(
                    "Private race room poll updated. Room code=" + GetRoomCodeText() +
                    ", players=" + playerCount +
                    ", joiner=" + joinerName +
                    ", status=" + (_currentState.status ?? string.Empty) + ".");
            }

            if (completedOperation == PendingServerOperation.StartRoom)
            {
                BeginLocalRaceLaunchFromCurrentState();
            }
            else if (ShouldBeginRemoteRaceLaunch())
            {
                BeginLocalRaceLaunchFromCurrentState();
            }

            TryFlushDeferredStartRequest();
        }

        private static void BeginBackgroundRequest(PendingServerOperation operation, string roomCode, ModPrivateRaceStartRequestDto startRequest)
        {
            if (_requestInFlight || _context == null)
            {
                return;
            }

            _requestInFlight = true;
            ThreadPool.QueueUserWorkItem(delegate
            {
                ModMatchStateDto state = null;
                string error = string.Empty;

                try
                {
                    switch (operation)
                    {
                        case PendingServerOperation.HostRoom:
                            state = _context.ServerApiClient.HostPrivateRace(ResolveLocalPlayerId(), HostDisplayNameOrDefault, string.Empty);
                            break;
                        case PendingServerOperation.JoinRoom:
                            state = _context.ServerApiClient.JoinPrivateRace(
                                roomCode,
                                new ModPrivateRaceJoinRequestDto
                                {
                                    playerId = ResolveLocalPlayerId(),
                                    displayName = LocalDisplayNameOrDefault
                                });
                            break;
                        case PendingServerOperation.PollRoom:
                            state = _context.ServerApiClient.GetPrivateRaceRoom(roomCode);
                            break;
                        case PendingServerOperation.StartRoom:
                            state = _context.ServerApiClient.StartPrivateRace(roomCode, startRequest);
                            break;
                    }
                }
                catch (Exception ex)
                {
                    error = ex.Message;
                    ModLog.Warn("Private race room request failed: " + ex.Message);
                }

                lock (Sync)
                {
                    _pendingState = state;
                    _pendingError = error;
                    _pendingServerOperation = operation;
                    _hasPendingResponse = true;
                }
            });
        }

        private static void TryFlushDeferredStartRequest()
        {
            if (!_deferredStartRequested || _requestInFlight || _context == null)
            {
                return;
            }

            if (_state == PrivateRaceRoomState.Error || _currentState == null || string.IsNullOrEmpty(_deferredStartRoomCode))
            {
                _deferredStartRequested = false;
                _deferredStartRoomCode = string.Empty;
                _deferredStartRequest = null;
                return;
            }

            ModPrivateRaceStartRequestDto startRequest = _deferredStartRequest;
            string roomCode = _deferredStartRoomCode;
            _deferredStartRequested = false;
            _deferredStartRoomCode = string.Empty;
            _deferredStartRequest = null;
            BeginBackgroundRequest(PendingServerOperation.StartRoom, roomCode, startRequest);
        }

        private static void BeginLocalRaceLaunchFromCurrentState()
        {
            if (_currentState == null)
            {
                SetErrorState("The private room did not return a match snapshot.");
                return;
            }

            string activationToken = BuildRaceActivationToken(_currentState);
            if (string.IsNullOrEmpty(activationToken) || string.Equals(_lastActivatedRaceToken, activationToken, StringComparison.Ordinal))
            {
                return;
            }

            ModMatchPlayerDto host = GetPlayerById(ResolveLocalPlayerId());
            ModMatchPlayerDto joiner = GetJoinerPlayer();
            string opponentPlayerId = joiner == null ? string.Empty : joiner.playerId ?? string.Empty;
            string opponentDisplayName = joiner == null ? "Waiting for Opponent" : joiner.displayName ?? "Waiting for Opponent";
            string mode = !string.IsNullOrEmpty(_currentState.mode)
                ? _currentState.mode
                : (string.IsNullOrEmpty(_pendingLaunchMode) ? SelectedMode : _pendingLaunchMode);
            GameMode gameMode = _pendingLaunchGameMode;
            if (gameMode == GameMode.None)
            {
                gameMode = string.Equals(mode, "Creative", StringComparison.OrdinalIgnoreCase)
                    ? GameMode.Creative
                    : GameMode.Survival;
            }

            if (!PrepareRaceSeedFromCurrentState(gameMode))
            {
                return;
            }

            EnsureHarmonyHooksInstalled();

            _localLaunchPending = true;
            _localCountdownStarted = false;
            _localSkipIssued = false;
            _localWaitingStartedAt = 0f;
            _localCountdownEndsAt = 0f;
            _localLaunchGameMode = gameMode;
            _lastActivatedRaceToken = activationToken;

            ModClientSessionMode.ActivateRankedMultiplayerMatch(
                mode,
                _currentState.matchId ?? string.Empty,
                host == null ? ResolveLocalPlayerId() : host.playerId ?? ResolveLocalPlayerId(),
                opponentPlayerId,
                opponentDisplayName,
                _lastPreparedSeedId,
                _lastPreparedSeedValue);
            ModClientSessionMode.AttachPrivateRaceLocalRoom(GetLocalPlayerDisplayText(), IsLocalHost);

            uGUI_MainMenu menu = uGUI_MainMenu.main;
            if (menu == null)
            {
                SetErrorState("Private room was ready, but the main menu was unavailable.");
                return;
            }

            if (gameMode == GameMode.Creative)
            {
                menu.OnButtonCreative();
            }
            else
            {
                menu.OnButtonSurvival();
            }
        }

        private static void SetErrorState(string message)
        {
            _state = PrivateRaceRoomState.Error;
            _pendingError = string.IsNullOrEmpty(message) ? "Private room connection failed." : message;
            _nextPollAt = Time.unscaledTime + 0.5f;
            ModLog.Warn(_pendingError);
        }

        private static bool ShouldBeginRemoteRaceLaunch()
        {
            if (_currentState == null || IsLocalHost)
            {
                return false;
            }

            if (!string.Equals(_currentState.status, "WaitingForReady", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(_currentState.status, "InProgress", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return !string.Equals(BuildRaceActivationToken(_currentState), _lastActivatedRaceToken, StringComparison.Ordinal);
        }

        private static string BuildRaceActivationToken(ModMatchStateDto state)
        {
            if (state == null)
            {
                return string.Empty;
            }

            return (state.matchId ?? string.Empty) +
                "|" +
                (state.mode ?? string.Empty) +
                "|" +
                (state.seedId ?? string.Empty) +
                "|" +
                (state.seedValue ?? string.Empty) +
                "|" +
                state.spawnX.ToString("0.###", CultureInfo.InvariantCulture) +
                "|" +
                state.spawnZ.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private static bool PrepareRaceSeedFromCurrentState(GameMode gameMode)
        {
            if (_currentState == null)
            {
                return false;
            }

            string seedId = string.IsNullOrEmpty(_currentState.seedId) ? _lastPreparedSeedId : _currentState.seedId.Trim();
            string seedValue = string.IsNullOrEmpty(_currentState.seedValue) ? _lastPreparedSeedValue : _currentState.seedValue.Trim();
            if (string.IsNullOrEmpty(seedId))
            {
                seedId = gameMode == GameMode.Creative ? "PrivateRace-Creative" : "BS 1";
            }

            if (string.IsNullOrEmpty(seedValue))
            {
                seedValue = seedId;
            }

            if (gameMode == GameMode.Creative)
            {
                _lastPreparedSeedId = seedId;
                _lastPreparedSeedValue = seedValue;
                _lastPreparedSpawnX = _currentState.spawnX;
                _lastPreparedSpawnZ = _currentState.spawnZ;
                _lastPreparedSpawnDescription = "Creative race spawn resolved from shared seed.";
                ModSeedStore.PreparePendingExternalSeed(
                    GameMode.Creative,
                    seedId,
                    seedValue,
                    "Private room Creative race seed from room " + GetRoomCodeText() + ".");
                return true;
            }

            string seedDirectoryPath;
            if (!ModRankedSurvivalBatchSeedCatalog.TryGetSeedDirectoryByName(seedId, out seedDirectoryPath))
            {
                SetErrorState("Could not find the ranked survival seed '" + seedId + "' in " + ModRankedSurvivalBatchSeedCatalog.GetInstalledRootPath() + ".");
                return false;
            }

            float spawnX = Mathf.Abs(_currentState.spawnX) > float.Epsilon ? _currentState.spawnX : _lastPreparedSpawnX;
            float spawnZ = Mathf.Abs(_currentState.spawnZ) > float.Epsilon ? _currentState.spawnZ : _lastPreparedSpawnZ;
            if ((Mathf.Abs(spawnX) <= float.Epsilon && Mathf.Abs(spawnZ) <= float.Epsilon) &&
                TryParseSpawnFromSeedValue(seedValue, out float parsedSpawnX, out float parsedSpawnZ))
            {
                spawnX = parsedSpawnX;
                spawnZ = parsedSpawnZ;
            }
            string spawnDescription =
                "Private room survival spawn at X=" +
                spawnX.ToString("0.###", CultureInfo.InvariantCulture) +
                ", Z=" +
                spawnZ.ToString("0.###", CultureInfo.InvariantCulture) +
                " for seed '" +
                seedId +
                "'.";

            _lastPreparedSeedId = seedId;
            _lastPreparedSeedValue = seedValue;
            _lastPreparedSpawnX = spawnX;
            _lastPreparedSpawnZ = spawnZ;
            _lastPreparedSpawnDescription = spawnDescription;
            ModRankedSurvivalBatchSeedRuntime.PreparePendingExternalSelection(seedDirectoryPath, seedId, spawnX, spawnZ, spawnDescription);
            return true;
        }

        private static bool TryParseSpawnFromSeedValue(string seedValue, out float spawnX, out float spawnZ)
        {
            spawnX = 0f;
            spawnZ = 0f;
            if (string.IsNullOrEmpty(seedValue))
            {
                return false;
            }

            string[] parts = seedValue.Split('|');
            if (parts.Length < 4)
            {
                return false;
            }

            return float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out spawnX) &&
                   float.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out spawnZ);
        }

        private static void TryNotifyServerRoomLeave()
        {
            if (_context == null || _context.ServerApiClient == null || !_context.ServerApiClient.IsConfigured || _currentState == null)
            {
                return;
            }

            string roomCode = _currentState.roomCode ?? string.Empty;
            string playerId = ResolveLocalPlayerId();
            if (string.IsNullOrEmpty(roomCode) || string.IsNullOrEmpty(playerId))
            {
                return;
            }

            ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    _context.ServerApiClient.LeavePrivateRace(roomCode, playerId);
                }
                catch (Exception ex)
                {
                    ModLog.Warn("Private race room leave notification failed: " + ex.Message);
                }
            });
        }

        private static string ResolveLocalPlayerId()
        {
            ModClientIdentity identity = ModClientIdentityStore.GetIdentity();
            return identity == null ? string.Empty : identity.PlayerId ?? string.Empty;
        }

        private static bool HasJoiner()
        {
            return GetJoinerPlayer() != null;
        }

        private static ModMatchPlayerDto GetPlayerById(string playerId)
        {
            if (_currentState == null || _currentState.players == null || string.IsNullOrEmpty(playerId))
            {
                return null;
            }

            for (int i = 0; i < _currentState.players.Length; i++)
            {
                ModMatchPlayerDto player = _currentState.players[i];
                if (player != null && string.Equals(player.playerId, playerId, StringComparison.Ordinal))
                {
                    return player;
                }
            }

            return null;
        }

        private static ModMatchPlayerDto GetJoinerPlayer()
        {
            if (_currentState == null || _currentState.players == null)
            {
                return null;
            }

            string localPlayerId = ResolveLocalPlayerId();
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

        private static bool HasLocalReturnedToRoom()
        {
            ModMatchPlayerDto localPlayer = GetPlayerById(ResolveLocalPlayerId());
            return localPlayer != null && !localPlayer.ready;
        }

        private static bool HasOpponentReturnedToRoom()
        {
            ModMatchPlayerDto opponent = GetJoinerPlayer();
            return opponent != null && !opponent.ready;
        }

        private static void MarkReturnedToRoom()
        {
            if (_context == null || !_context.Config.EnableNetworking || !_context.ServerApiClient.IsConfigured)
            {
                return;
            }

            string matchId = _currentState == null ? string.Empty : _currentState.matchId ?? string.Empty;
            string playerId = ResolveLocalPlayerId();
            if (string.IsNullOrEmpty(matchId) || string.IsNullOrEmpty(playerId))
            {
                return;
            }

            ModMatchPlayerDto localPlayer = GetPlayerById(playerId);
            if (localPlayer != null)
            {
                localPlayer.ready = false;
            }

            BeginFireAndForgetReadyReset(matchId, playerId);
        }

        private static void BeginFireAndForgetReadyReset(string matchId, string playerId)
        {
            ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    _context.ServerApiClient.SetMatchReady(matchId, playerId, false);
                }
                catch (Exception ex)
                {
                    ModLog.Warn("Failed to notify the server that the player returned to the private room: " + ex.Message);
                }
            });
        }

        private static void UpdateLocalPrivateRaceLaunch()
        {
            if (!ModClientSessionMode.IsPrivateRaceLocalSessionActive || !_localLaunchPending)
            {
                return;
            }

            EnsureHarmonyHooksInstalled();

            if (_localLaunchGameMode == GameMode.Creative)
            {
                UpdateCreativeCountdown();
                return;
            }

            uGUI_SceneIntro intro = uGUI.main != null ? uGUI.main.intro : null;
            if (intro == null)
            {
                return;
            }

            if (!intro.showing)
            {
                if ((_localCountdownStarted || _localSkipIssued) &&
                    !IntroVignette.isIntroActive &&
                    Player.main != null &&
                    !Player.main.cinematicModeActive)
                {
                    ResetLocalLaunchState();
                    ModLog.Info("Local private race intro countdown completed.");
                }

                return;
            }

            if (!IsIntroReadyForCountdown())
            {
                HideIntroPromptText(intro);
                ModOverlayRuntime.SetCenterMessage("Loading...", Color.white, true);
                return;
            }

            EnsureSurvivalIntroCountdownState();
            HideIntroPromptText(intro);
            ModPrivateRaceCountdownRuntimeHost.NotifyLocalReadyForCountdown();

            if (!ModPrivateRaceCountdownRuntimeHost.IsLocalPlayerReady)
            {
                ModOverlayRuntime.SetCenterMessage("Loading...", Color.white, true);
                return;
            }

            if (!ModPrivateRaceCountdownRuntimeHost.IsOpponentReady)
            {
                ModOverlayRuntime.SetCenterMessage("Waiting for Opponent", Color.white, true);
                return;
            }

            float remainingSeconds;
            if (!ModPrivateRaceCountdownRuntimeHost.TryGetSharedCountdownRemainingSeconds(out remainingSeconds))
            {
                ModOverlayRuntime.SetCenterMessage("Waiting for Race Start", Color.white, true);
                return;
            }

            _localCountdownStarted = true;
            int secondsRemaining = Mathf.CeilToInt(remainingSeconds);
            if (secondsRemaining > 0)
            {
                ModOverlayRuntime.SetCenterMessage(secondsRemaining.ToString(CultureInfo.InvariantCulture), Color.white, true);
                return;
            }

            ModOverlayRuntime.SetCenterMessage(string.Empty, Color.white, false);
            if (!_localSkipIssued)
            {
                _localSkipIssued = true;
                CoroutineHost.StartCoroutine(ForceSkipIntroWhenReady(intro));
            }
        }

        private static bool IsIntroReadyForCountdown()
        {
            return Player.main != null &&
                   EscapePod.main != null &&
                   LargeWorldStreamer.main != null &&
                   LargeWorldStreamer.main.IsReady() &&
                   string.Equals(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name, "Main", StringComparison.OrdinalIgnoreCase);
        }

        private static void EnsureSurvivalIntroCountdownState()
        {
            EscapePod escapePod = EscapePod.main;
            if (escapePod == null || escapePod.IsPlayingIntroCinematic())
            {
                return;
            }

            try
            {
                escapePod.TriggerIntroCinematic();
            }
            catch (Exception ex)
            {
                ModLog.Warn("Failed to trigger intro cinematic for local private race countdown: " + ex.Message);
            }
        }

        private static void HideIntroPromptText(uGUI_SceneIntro intro)
        {
            if (intro == null)
            {
                return;
            }

            if (intro.mainText != null)
            {
                intro.mainText.SetState(enabled: false);
            }

            if (intro.skipText != null)
            {
                intro.skipText.SetState(enabled: false);
            }
        }

        private static IEnumerator ForceSkipIntroWhenReady(uGUI_SceneIntro intro)
        {
            if (intro == null || !intro.showing)
            {
                yield break;
            }

            EscapePod escapePod = EscapePod.main;
            if (escapePod != null && !escapePod.IsPlayingIntroCinematic())
            {
                ModLog.Info("Starting intro cinematic for local private race skip.");
                escapePod.TriggerIntroCinematic();
            }

            float timeoutAt = Time.unscaledTime + 2f;
            while (Time.unscaledTime < timeoutAt)
            {
                bool cinematicActive =
                    (escapePod != null && escapePod.IsPlayingIntroCinematic()) ||
                    IntroVignette.isIntroActive ||
                    (Player.main != null && Player.main.cinematicModeActive);
                if (cinematicActive)
                {
                    break;
                }

                yield return null;
            }

            if (intro == null || !intro.showing)
            {
                yield break;
            }

            ModLog.Info("Force-skipping local private race intro after countdown.");
            intro.Stop(isInterrupted: true);
        }

        private static void UpdateCreativeCountdown()
        {
            if (!IsCreativeCountdownReady())
            {
                return;
            }

            LockCreativeCountdownPlayer();
            AlignCreativeCountdownView();
            ModPrivateRaceCountdownRuntimeHost.NotifyLocalReadyForCountdown();

            if (!ModPrivateRaceCountdownRuntimeHost.IsLocalPlayerReady)
            {
                ModOverlayRuntime.SetCenterMessage("Loading...", Color.white, true);
                return;
            }

            if (!ModPrivateRaceCountdownRuntimeHost.IsOpponentReady)
            {
                ModOverlayRuntime.SetCenterMessage("Waiting for Opponent", Color.white, true);
                return;
            }

            float remainingSeconds;
            if (!ModPrivateRaceCountdownRuntimeHost.TryGetSharedCountdownRemainingSeconds(out remainingSeconds))
            {
                ModOverlayRuntime.SetCenterMessage("Waiting for Race Start", Color.white, true);
                return;
            }

            _localCountdownStarted = true;
            int secondsRemaining = Mathf.CeilToInt(remainingSeconds);
            if (secondsRemaining > 0)
            {
                AlignCreativeCountdownView();
                ModOverlayRuntime.SetCenterMessage(secondsRemaining.ToString(CultureInfo.InvariantCulture), Color.white, true);
                return;
            }

            UnlockCreativeCountdownPlayer();
            ModOverlayRuntime.SetCenterMessage(string.Empty, Color.white, false);
            ResetLocalLaunchState();
            ModLog.Info("Local private race Creative countdown completed.");
        }

        private static bool IsCreativeCountdownReady()
        {
            return Player.main != null &&
                   _localLaunchGameMode == GameMode.Creative &&
                   string.Equals(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name, "Main", StringComparison.OrdinalIgnoreCase) &&
                   (uGUI.main == null || uGUI.main.loading == null || !uGUI.main.loading.IsLoading) &&
                   LargeWorldStreamer.main != null &&
                   LargeWorldStreamer.main.IsReady() &&
                   LargeWorldStreamer.main.IsWorldSettled();
        }

        private static void LockCreativeCountdownPlayer()
        {
            if (_localCreativeCountdownLockActive || Player.main == null)
            {
                return;
            }

            try
            {
                Player.main.cinematicModeActive = true;
                if (Player.main.playerController != null)
                {
                    Player.main.playerController.inputEnabled = false;
                }

                if (Inventory.main != null && Inventory.main.quickSlots != null)
                {
                    Inventory.main.quickSlots.SetIgnoreHotkeyInput(ignore: true);
                }

                PDA pda = Player.main.GetPDA();
                if (pda != null)
                {
                    pda.SetIgnorePDAInput(ignore: true);
                }

                _localCreativeCountdownLockActive = true;
            }
            catch (Exception ex)
            {
                ModLog.Warn("Failed to lock Creative countdown player state: " + ex.Message);
            }
        }

        private static void UnlockCreativeCountdownPlayer()
        {
            if (!_localCreativeCountdownLockActive || Player.main == null)
            {
                return;
            }

            try
            {
                if (Player.main.playerController != null)
                {
                    Player.main.playerController.inputEnabled = true;
                    Player.main.playerController.SetEnabled(enabled: true);
                }

                Player.main.cinematicModeActive = false;

                if (Inventory.main != null && Inventory.main.quickSlots != null)
                {
                    Inventory.main.quickSlots.SetIgnoreHotkeyInput(ignore: false);
                }

                PDA pda = Player.main.GetPDA();
                if (pda != null)
                {
                    pda.SetIgnorePDAInput(ignore: false);
                }
            }
            catch (Exception ex)
            {
                ModLog.Warn("Failed to unlock Creative countdown player state: " + ex.Message);
            }
            finally
            {
                _localCreativeCountdownLockActive = false;
            }
        }

        private static void AlignCreativeCountdownView()
        {
            if (Player.main == null)
            {
                return;
            }

            try
            {
                Vector3 forward = CreativeCountdownForward.normalized;
                float yaw = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
                Player.main.transform.rotation = Quaternion.Euler(0f, yaw, 0f);

                MainCameraControl cameraControl = MainCameraControl.main;
                if (cameraControl != null)
                {
                    cameraControl.rotationX = yaw;
                    cameraControl.rotationY = 0f;
                    cameraControl.camRotationX = 0f;
                    cameraControl.camRotationY = 0f;
                    cameraControl.transform.localEulerAngles = new Vector3(0f, yaw, 0f);
                }
            }
            catch (Exception ex)
            {
                ModLog.Warn("Failed to align Creative countdown view: " + ex.Message);
            }
        }

        private static void ResetLocalLaunchState()
        {
            UnlockCreativeCountdownPlayer();
            _localLaunchGameMode = GameMode.None;
            _localLaunchPending = false;
            _localCountdownStarted = false;
            _localSkipIssued = false;
            _localWaitingStartedAt = 0f;
            _localCountdownEndsAt = 0f;
            _pendingLaunchMode = string.Empty;
            _pendingLaunchGameMode = GameMode.None;
            ModOverlayRuntime.SetCenterMessage(string.Empty, Color.white, false);
        }

        private static void EnsureHarmonyHooksInstalled()
        {
            if (_harmonyInstalled)
            {
                return;
            }

            try
            {
                MethodInfo sceneIntroStop = typeof(uGUI_SceneIntro).GetMethod("Stop", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new[] { typeof(bool) }, null);
                MethodInfo stopPrefix = typeof(ModPrivateRaceRoomRuntimeHost).GetMethod(nameof(SceneIntroStopPrefix), BindingFlags.Static | BindingFlags.NonPublic);
                if (sceneIntroStop == null || stopPrefix == null)
                {
                    ModLog.Warn("Private race intro skip guard could not be installed because required scene intro methods were unavailable.");
                    return;
                }

                _harmony = new Harmony(HarmonyId);
                _harmony.Patch(sceneIntroStop, prefix: new HarmonyMethod(stopPrefix));
                _harmonyInstalled = true;
                ModLog.Info("Private race intro skip guard installed.");
            }
            catch (Exception ex)
            {
                ModLog.Warn("Failed to install private race intro skip guard: " + ex.Message);
            }
        }

        private static bool SceneIntroStopPrefix()
        {
            if (!ModClientSessionMode.IsPrivateRaceLocalSessionActive || !_localLaunchPending)
            {
                return true;
            }

            if (_localLaunchGameMode != GameMode.Survival)
            {
                return true;
            }

            return _localSkipIssued;
        }

        private enum PendingServerOperation
        {
            None,
            HostRoom,
            JoinRoom,
            PollRoom,
            StartRoom
        }
    }
}
