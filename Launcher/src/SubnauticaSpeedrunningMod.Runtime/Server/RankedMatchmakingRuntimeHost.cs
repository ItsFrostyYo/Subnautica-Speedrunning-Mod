using System;
using System.Threading;
using UnityEngine;
using SubnauticaSpeedrunningMod.Runtime.Seeds;

namespace SubnauticaSpeedrunningMod.Runtime
{
    internal static class ModRankedMatchmakingRuntimeHost
    {
        private enum MatchmakingUiState
        {
            Idle,
            QueueStarting,
            QueueWaiting,
            MatchFound,
            Launching,
            Error
        }

        private static readonly object Sync = new object();
        private static RuntimeContext _context;
        private static bool _installed;
        private static MatchmakingUiState _state = MatchmakingUiState.Idle;
        private static string _queuedMode = string.Empty;
        private static string _ticketId = string.Empty;
        private static string _message = string.Empty;
        private static bool _requestInFlight;
        private static ModMatchmakingTicketDto _pendingTicket;
        private static ModMatchmakingTicketDto _currentTicket;
        private static string _pendingError = string.Empty;
        private static bool _hasPendingResponse;
        private static float _nextPollAt;
        private static bool _launchTriggered;

        public static void Install(RuntimeContext context)
        {
            if (_installed)
            {
                return;
            }

            _context = context;
            ModClientIdentityStore.Initialize(context);
            _installed = true;
            ModLog.Info("Ranked matchmaking runtime installed.");
        }

        public static bool IsQueuePanelVisible
        {
            get
            {
                return _state == MatchmakingUiState.QueueStarting ||
                       _state == MatchmakingUiState.QueueWaiting ||
                       _state == MatchmakingUiState.MatchFound ||
                       _state == MatchmakingUiState.Launching ||
                       _state == MatchmakingUiState.Error;
            }
        }

        public static bool IsBusy
        {
            get { return _state != MatchmakingUiState.Idle; }
        }

        public static void ResetForMainMenu()
        {
            ResetState();
        }

        public static string GetPanelTitle()
        {
            if (string.IsNullOrEmpty(_queuedMode))
            {
                return "Waiting for Matchmaking";
            }

            return "Waiting for Matchmaking (" + _queuedMode + ")";
        }

        public static string GetPanelMessage()
        {
            return string.IsNullOrEmpty(_message) ? "Searching for opponent..." : _message;
        }

        public static void StartQueue(string mode)
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

            if (_requestInFlight || _state == MatchmakingUiState.QueueWaiting || _state == MatchmakingUiState.QueueStarting)
            {
                return;
            }

            _queuedMode = string.IsNullOrEmpty(mode) ? "Creative" : mode;
            _ticketId = string.Empty;
            _message = "Joining " + _queuedMode + " matchmaking...";
            _state = MatchmakingUiState.QueueStarting;
            _launchTriggered = false;
            BeginBackgroundRequest(BackgroundOperation.Enqueue, _queuedMode, string.Empty);
        }

        public static void CancelQueue()
        {
            if (!_installed)
            {
                return;
            }

            string ticketId = _ticketId;
            string queuedMode = _queuedMode;
            ResetState();

            if (!string.IsNullOrEmpty(ticketId) && _context != null && _context.ServerApiClient.IsConfigured)
            {
                BeginBackgroundRequest(BackgroundOperation.Cancel, queuedMode, ticketId);
            }
        }

        public static void Update()
        {
            if (!_installed || _context == null)
            {
                return;
            }

            ConsumePendingResponse();

            if ((_state == MatchmakingUiState.QueueStarting || _state == MatchmakingUiState.QueueWaiting) &&
                !_requestInFlight &&
                !string.IsNullOrEmpty(_ticketId) &&
                Time.unscaledTime >= _nextPollAt)
            {
                BeginBackgroundRequest(BackgroundOperation.Poll, _queuedMode, _ticketId);
            }

            if (_state == MatchmakingUiState.MatchFound && !_launchTriggered)
            {
                TryLaunchMatchedGame();
            }
        }

        private static void ConsumePendingResponse()
        {
            if (!_hasPendingResponse)
            {
                return;
            }

            ModMatchmakingTicketDto ticket;
            string pendingError;
            lock (Sync)
            {
                ticket = _pendingTicket;
                pendingError = _pendingError;
                _pendingTicket = null;
                _pendingError = string.Empty;
                _hasPendingResponse = false;
                _requestInFlight = false;
            }

            if (!string.IsNullOrEmpty(pendingError))
            {
                SetErrorState(pendingError);
                return;
            }

            if (ticket == null)
            {
                return;
            }

            _ticketId = ticket.ticketId ?? string.Empty;
            _queuedMode = string.IsNullOrEmpty(ticket.mode) ? _queuedMode : ticket.mode;
            _message = string.IsNullOrEmpty(ticket.message) ? "Searching for opponent..." : ticket.message;
            _currentTicket = ticket;

            string status = ticket.status ?? string.Empty;
            if (string.Equals(status, "Matched", StringComparison.OrdinalIgnoreCase) && ticket.match != null)
            {
                _state = MatchmakingUiState.MatchFound;
                _nextPollAt = 0f;
                return;
            }

            if (string.Equals(status, "Cancelled", StringComparison.OrdinalIgnoreCase))
            {
                ResetState();
                return;
            }

            _state = MatchmakingUiState.QueueWaiting;
            _nextPollAt = Time.unscaledTime + 1.25f;
        }

        private static void TryLaunchMatchedGame()
        {
            ModMatchmakingTicketDto ticket = GetLatestTicketSnapshot();
            if (ticket == null || ticket.match == null)
            {
                return;
            }

            if (!string.Equals(ticket.match.mode ?? string.Empty, "Creative", StringComparison.OrdinalIgnoreCase))
            {
                SetErrorState("Only Creative ranked multiplayer is enabled right now.");
                return;
            }

            _launchTriggered = true;
            _state = MatchmakingUiState.Launching;
            _message = "Match found. Launching Creative ranked match...";

            ModSeedStore.PreparePendingExternalSeed(
                GameMode.Creative,
                ticket.match.seedId ?? "Creative-Multiplayer",
                ticket.match.seedValue ?? string.Empty,
                "Server assigned Creative ranked multiplayer seed for match '" + (ticket.match.matchId ?? string.Empty) + "'.");

            ModClientSessionMode.ActivateRankedMultiplayerMatch(
                ticket.match.mode ?? "Creative",
                ticket.match.matchId ?? string.Empty,
                ticket.match.playerId ?? string.Empty,
                ticket.match.opponentPlayerId ?? string.Empty,
                ticket.match.opponentDisplayName ?? string.Empty,
                ticket.match.seedId ?? string.Empty,
                ticket.match.seedValue ?? string.Empty);

            uGUI_MainMenu menu = uGUI_MainMenu.main;
            if (menu == null)
            {
                SetErrorState("Match found, but the main menu was not available to start the run.");
                return;
            }

            menu.OnButtonCreative();
        }

        private static ModMatchmakingTicketDto GetLatestTicketSnapshot()
        {
            return _currentTicket;
        }

        private static void BeginBackgroundRequest(BackgroundOperation operation, string mode, string ticketId)
        {
            if (_requestInFlight || _context == null)
            {
                return;
            }

            _requestInFlight = true;
            ThreadPool.QueueUserWorkItem(delegate
            {
                ModMatchmakingTicketDto response = null;
                string error = string.Empty;

                try
                {
                    ModClientIdentity identity = ModClientIdentityStore.GetIdentity();
                    switch (operation)
                    {
                        case BackgroundOperation.Enqueue:
                            response = _context.ServerApiClient.EnqueueMatchmaking(identity, mode);
                            break;
                        case BackgroundOperation.Cancel:
                            response = _context.ServerApiClient.CancelMatchmaking(ticketId, identity.PlayerId);
                            break;
                        case BackgroundOperation.Poll:
                            response = _context.ServerApiClient.GetMatchmakingTicket(ticketId);
                            break;
                    }
                }
                catch (Exception ex)
                {
                    error = ex.Message;
                    ModLog.Warn("Matchmaking request failed: " + ex.Message);
                }

                lock (Sync)
                {
                    _pendingTicket = response;
                    _pendingError = error;
                    _hasPendingResponse = true;
                }
            });
        }

        private static void ResetState()
        {
            _state = MatchmakingUiState.Idle;
            _queuedMode = string.Empty;
            _ticketId = string.Empty;
            _message = string.Empty;
            _requestInFlight = false;
            _pendingTicket = null;
            _currentTicket = null;
            _pendingError = string.Empty;
            _hasPendingResponse = false;
            _nextPollAt = 0f;
            _launchTriggered = false;
        }

        private static void SetErrorState(string message)
        {
            _state = MatchmakingUiState.Error;
            _message = string.IsNullOrEmpty(message) ? "Matchmaking failed." : message;
            _requestInFlight = false;
            _nextPollAt = 0f;
        }

        private enum BackgroundOperation
        {
            Enqueue,
            Poll,
            Cancel
        }
    }
}
