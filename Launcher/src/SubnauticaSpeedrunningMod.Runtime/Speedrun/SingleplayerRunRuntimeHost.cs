using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
using SubnauticaSpeedrunningMod.Runtime;
using SubnauticaSpeedrunningMod.Runtime.Practice;
using SubnauticaSpeedrunningMod.Runtime.Seeds;
using SubnauticaSpeedrunningMod.Runtime.Ui;

namespace SubnauticaSpeedrunningMod.Runtime.RunTracking
{
    internal static class SingleplayerRunRuntimeHost
    {
        private static readonly Color SingleplayerRunTitleColor = new Color(1f, 0.76f, 0.36f, 1f);
        private static readonly ConsistentScreenshotClipRuntime ConsistentScreenshotClip = new ConsistentScreenshotClipRuntime();
        private static readonly ModSeedLifepodPlacementRuntime SeededLifepodPlacement = new ModSeedLifepodPlacementRuntime();
        private static readonly FieldInfo PlayerInfectionCuredField = typeof(Player).GetField("timePlayerInfectionCured", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo PrecursorDisableGunTerminalUsingPlayerField = typeof(PrecursorDisableGunTerminal).GetField("usingPlayer", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly float[] PortalBounds = { 240f, 250f, -1590f, -1580f, -2000f, 2000f };
        private static readonly float[] ClipADeathBounds = { 33f, 65f, -20f, -8f, 118f, 96f };
        private static readonly float[] ClipCDeathBounds = { -155f, -133f, -20f, -10f, 73f, 96f };
        private static readonly float[] KelpLeaveBounds = { -212f, 27f, -100f, 100f, 159f, 177f };
        private static readonly float[] MountainDescendBounds = { 475f, 534f, -510f, -191f, 745f, 810f };
        private static readonly string[] AuroraBiomes = { "crashedShip", "generatorRoom" };
        private static readonly string[] SparseDeathBiomes = { "sparseReef", "seaTreaderPath", "seaTreaderPath_wreck" };

        private static bool _installed;
        private static ModGameplayRuntimeBehaviour _runtimeBehaviour;
        private static string _lastSceneName = string.Empty;
        private static ModGameStateKind _state = ModGameStateKind.Booting;
        private static bool _portalLoading;
        private static bool _deathLoading;
        private static bool _playerDiedThisFrame;
        private static bool _lastPlayerAlive = true;
        private static string _lastAliveBiomeName = string.Empty;
        private static Vector3 _lastAlivePosition;
        private static bool _hasLastAlivePosition;
        private static bool _timerArmed;
        private static bool _timerRunning;
        private static bool _timerCompleted;
        private static bool _lastLaunchStarted;
        private static double _elapsedSeconds;
        private static double _realTimeElapsedSeconds;
        private static int _portalLoadCount;
        private static ModGameplaySnapshot _lastSnapshot;
        private static bool _haveLoggedSnapshot;
        private static string _lastActivatedSeedSaveSlot = string.Empty;
        private static GameMode _lastActivatedSeedMode = GameMode.None;
        private static float _lastCureStartTime;
        private static string _currentSaveSlot = string.Empty;
        private static string _currentGameModeName = string.Empty;
        private static ModSingleplayerRunMode _activeRunMode = ModSingleplayerRunMode.None;
        private static int _runProgressIndex;
        private static string _runProgressSubtitle = "Run has Not Started";
        private static bool _survivalIntroObserved;
        private static bool _lastSeaglideUnlocked;
        private static bool _lastIonBatteryUnlocked;
        private static TechType _lastStartedCraftTechType = TechType.None;
        private static TechType _lastCraftedTechType = TechType.None;
        private static readonly Dictionary<int, TechType> _crafterInProgressTechTypes = new Dictionary<int, TechType>();
        private static float _nextCrafterScanAt;
        private static PrecursorDisableGunTerminal[] _cachedGunTerminals;
        private static float _nextGunTerminalRefreshAt;
        private static string _verificationTitle = string.Empty;
        private static string _verificationDetail = string.Empty;
        private static Color _verificationTitleColor = Color.white;
        private static Color _verificationDetailColor = Color.white;
        private static bool _verificationVisible;

        public static void Install(RuntimeContext context)
        {
            if (_installed)
            {
                return;
            }

            SceneManager.sceneLoaded += OnSceneLoaded;
            PrecursorTeleporter.TeleportEventStart += OnPortalLoadingStarted;
            PrecursorTeleporter.TeleportEventEnd += OnPortalLoadingEnded;
            _installed = true;
            ModLog.Info("Singleplayer run tracking host installed.");
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            _runtimeBehaviour = null;
            AttachSceneRuntimeBehaviour(scene);
            _lastSceneName = scene.name;

            if (string.Equals(scene.name, "XMenu", StringComparison.Ordinal))
            {
                ResetTimerSession();
            }
        }

        private static void OnPortalLoadingStarted()
        {
            if (!_portalLoading)
            {
                _portalLoading = true;
                _portalLoadCount++;
                ModLog.Info("Detected precursor portal loading start.");
            }
        }

        private static void OnPortalLoadingEnded()
        {
            if (_portalLoading)
            {
                _portalLoading = false;
                ModLog.Info("Detected precursor portal loading end.");
            }
        }

        private static void OnRuntimeUpdate()
        {
            UpdateSaveContext();
            ModPracticeSaveProtectionRuntime.EnsureInstalled();
            ModPracticeSuperSeaglideRuntime.EnsureInstalled();
            ModSeedRuntimeHost.EnsureStartupHooksInstalled();
            RefreshActiveSeedForSaveContext();
            bool rankedPracticeActive = ModSeedRuntimeHost.IsRankedSingleplayerSeedActive();
            bool betterRngTimedActive = IsBetterRngTimedRunActive();
            bool practiceSaveTimedActive = IsPracticeSaveTimedRunActive();
            UpdateDeathLoadingState();
            UpdateGameplayState();
            ModPracticeSuperSeaglideRuntime.Update(_state == ModGameStateKind.InGame);
            ModSeedRuntimeHost.UpdateSharedRuleState(
                _currentSaveSlot,
                _state == ModGameStateKind.MainMenu,
                Utils.GetContinueMode());
            SeededLifepodPlacement.Update(rankedPracticeActive && ShouldApplySeededLifepodPlacement());

            bool seedWorldActive =
                rankedPracticeActive &&
                _state != ModGameStateKind.Booting &&
                _state != ModGameStateKind.MainMenu;
            ModSeedWorldRuntime.Update(seedWorldActive, rankedPracticeActive && _state == ModGameStateKind.InGame);

            ConsistentScreenshotClip.Update(rankedPracticeActive && IsCreativeSingleplayerRunActive());

            if (_state == ModGameStateKind.Booting || _state == ModGameStateKind.MainMenu)
            {
                ResetMenuOnlyPollingState();
                ModOverlayRuntime.SetTimer(FormatTimer(_elapsedSeconds), false);
                ModOverlayRuntime.SetRunStatus(GetRunStatusTitle(), GetRunStatusSubtitle(), SingleplayerRunTitleColor, false);
                ModOverlayRuntime.SetVerification(string.Empty, string.Empty, Color.white, Color.white, false);
                return;
            }

            if (!rankedPracticeActive && !betterRngTimedActive && !practiceSaveTimedActive)
            {
                ResetMenuOnlyPollingState();
                ModOverlayRuntime.SetTimer(FormatTimer(0d), false);
                ModOverlayRuntime.SetRunStatus(GetRunStatusTitle(), GetRunStatusSubtitle(), SingleplayerRunTitleColor, false);
                ModOverlayRuntime.SetVerification(string.Empty, string.Empty, Color.white, Color.white, false);
                return;
            }

            if (rankedPracticeActive)
            {
                UpdateCraftingState();
            }

            UpdateTimerLifecycle();
            if (rankedPracticeActive)
            {
                UpdateRunProgress();
            }

            ModOverlayRuntime.SetTimer(FormatTimer(_elapsedSeconds), ShouldShowTimerUi());
            ModOverlayRuntime.SetRunStatus(GetRunStatusTitle(), GetRunStatusSubtitle(), SingleplayerRunTitleColor, ShouldShowRunStatus());
            ModOverlayRuntime.SetVerification(
                _verificationTitle,
                _verificationDetail,
                _verificationTitleColor,
                _verificationDetailColor,
                _verificationVisible);
        }

        private static void UpdateSaveContext()
        {
            string saveSlot = Utils.GetSavegameDir() ?? string.Empty;
            string gameModeName = Player.main != null ? Utils.GetLegacyGameMode().ToString() : string.Empty;

            if (!string.Equals(_currentSaveSlot, saveSlot, StringComparison.Ordinal) ||
                !string.Equals(_currentGameModeName, gameModeName, StringComparison.Ordinal))
            {
                ModLog.Info("Active save context changed: slot='" + saveSlot + "', gameMode='" + gameModeName + "'.");
                _currentSaveSlot = saveSlot;
                _currentGameModeName = gameModeName;
            }
        }

        private static void RefreshActiveSeedForSaveContext()
        {
            string saveSlot = Utils.GetSavegameDir() ?? string.Empty;
            if (string.IsNullOrEmpty(saveSlot))
            {
                return;
            }

            string activeSceneName = SceneManager.GetActiveScene().name;
            bool isMainMenuScene =
                string.Equals(activeSceneName, "XMenu", StringComparison.Ordinal) ||
                uGUI_MainMenu.main != null;
            if (isMainMenuScene)
            {
                return;
            }

            GameMode mode = Utils.GetLegacyGameMode();
            if (mode != GameMode.Creative && mode != GameMode.Survival && mode != GameMode.Hardcore)
            {
                return;
            }

            if (ModClientSessionMode.IsBetterRngSingleplayerSelected)
            {
                _lastActivatedSeedSaveSlot = string.Empty;
                _lastActivatedSeedMode = GameMode.None;
                ModSeedStore.ResetSessionSelections();
                return;
            }

            bool createIfMissing =
                !Utils.GetContinueMode() &&
                ModClientSessionMode.IsRankedSingleplayerPracticeSelected;

            bool seedEnsured = ModSeedStore.EnsureSeedForSaveContext(saveSlot, mode, Utils.GetContinueMode(), createIfMissing);
            if (!seedEnsured && !ModSeedStore.IsSeedContextActive(saveSlot, mode))
            {
                return;
            }

            bool seedContextChanged =
                !string.Equals(_lastActivatedSeedSaveSlot, saveSlot, StringComparison.OrdinalIgnoreCase) ||
                _lastActivatedSeedMode != mode;
            if (!seedContextChanged)
            {
                return;
            }

            _lastActivatedSeedSaveSlot = saveSlot;
            _lastActivatedSeedMode = mode;
            ModSeedWorldRuntime.Reset();
            SeededLifepodPlacement.Reset();
            ModLog.Info("Activated slot-backed seed context for save slot '" + saveSlot + "' in mode '" + mode + "'.");

            if (!Utils.GetContinueMode() && ModSeedRuntimeHost.IsRankedSingleplayerSeedActive())
            {
                Vector3 seededStartPoint;
                string description;
                if (ModSeedRuntimeHost.TryResolveSeededFreshRunStartPoint(out seededStartPoint, out description))
                {
                    SeededLifepodPlacement.Schedule(seededStartPoint.x, seededStartPoint.z, description);
                }
            }
        }

        private static void UpdateGameplayState()
        {
            ModGameplaySnapshot snapshot = BuildSnapshot();
            ModGameStateKind nextState = DetermineState(snapshot);
            if (nextState != _state)
            {
                ModLog.Info("Gameplay state changed: " + _state + " -> " + nextState + ".");
                _state = nextState;
            }

            if (!_haveLoggedSnapshot || !_lastSnapshot.Equals(snapshot))
            {
                ModLog.Info("Gameplay snapshot: " + snapshot.ToLogString());
                _lastSnapshot = snapshot;
                _haveLoggedSnapshot = true;
            }
        }

        private static bool ShouldApplySeededLifepodPlacement()
        {
            if (Utils.GetContinueMode())
            {
                return false;
            }

            return _state == ModGameStateKind.LoadingNewGame ||
                   _state == ModGameStateKind.InGame;
        }

        private static void UpdateDeathLoadingState()
        {
            _playerDiedThisFrame = false;
            CacheAliveDeathContext();
            bool playerAlive = IsPlayerAlive();
            if (_lastPlayerAlive && !playerAlive)
            {
                _playerDiedThisFrame = true;
                _deathLoading = true;
                ModLog.Info(
                    "Detected death loading start. lastAliveBiome='" +
                    _lastAliveBiomeName +
                    "', lastAlivePosition=" +
                    FormatVector3(_lastAlivePosition) +
                    ".");
            }
            else if (!_lastPlayerAlive && playerAlive && _deathLoading)
            {
                _deathLoading = false;
                ModLog.Info("Detected death loading end.");
            }

            _lastPlayerAlive = playerAlive;
        }

        private static ModGameplaySnapshot BuildSnapshot()
        {
            bool hasPlayer = Player.main != null;
            string sceneName = SceneManager.GetActiveScene().name;
            bool isMainMenuScene = string.Equals(sceneName, "XMenu", StringComparison.Ordinal) || uGUI_MainMenu.main != null;
            bool loadingVisible = uGUI.main != null && uGUI.main.loading != null && uGUI.main.loading.IsLoading;
            bool worldReady = LargeWorldStreamer.main != null && LargeWorldStreamer.main.IsReady();
            bool continueMode = Utils.GetContinueMode();
            bool creativeMode = Utils.GetLegacyGameMode() == GameMode.Creative;
            bool inEscapePod = hasPlayer && Player.main.currentEscapePod != null;
            bool pdaOpen = IsPdaOpen();
            bool pauseMenuOpen = IngameMenu.main != null && IngameMenu.main.gameObject.activeInHierarchy;
            bool introActive = IntroVignette.isIntroActive;
            bool cinematicActive = hasPlayer && Player.main.cinematicModeActive;

            return new ModGameplaySnapshot(
                sceneName,
                isMainMenuScene,
                loadingVisible,
                worldReady,
                continueMode,
                creativeMode,
                inEscapePod,
                pdaOpen,
                pauseMenuOpen,
                introActive,
                cinematicActive,
                hasPlayer,
                _portalLoading,
                _deathLoading);
        }

        private static ModGameStateKind DetermineState(ModGameplaySnapshot snapshot)
        {
            string activeSceneName = SceneManager.GetActiveScene().name;
            if (snapshot.IsMainMenuScene)
            {
                return ModGameStateKind.MainMenu;
            }

            if (!snapshot.HasPlayer && !snapshot.IsLoadingVisible && !snapshot.IsWorldReady)
            {
                return ModGameStateKind.Booting;
            }

            if (snapshot.IsPortalLoading)
            {
                return ModGameStateKind.PortalLoading;
            }

            if (snapshot.IsDeathLoading)
            {
                return ModGameStateKind.DeathLoading;
            }

            if (!snapshot.HasPlayer || snapshot.IsLoadingVisible)
            {
                if (!string.Equals(activeSceneName, "Main", StringComparison.OrdinalIgnoreCase))
                {
                    return ModGameStateKind.Booting;
                }

                return snapshot.IsContinueMode ? ModGameStateKind.LoadingSave : ModGameStateKind.LoadingNewGame;
            }

            if (!snapshot.IsWorldReady && _state != ModGameStateKind.InGame)
            {
                if (!string.Equals(activeSceneName, "Main", StringComparison.OrdinalIgnoreCase))
                {
                    return ModGameStateKind.Booting;
                }

                return snapshot.IsContinueMode ? ModGameStateKind.LoadingSave : ModGameStateKind.LoadingNewGame;
            }

            return ModGameStateKind.InGame;
        }

        private static void UpdateTimerLifecycle()
        {
            ModSingleplayerRunMode currentMode = ResolveCurrentRunMode();
            if (currentMode != ModSingleplayerRunMode.None)
            {
                _activeRunMode = currentMode;
            }
            else if (!_timerRunning && !_timerCompleted)
            {
                _activeRunMode = ModSingleplayerRunMode.None;
            }

            bool betterRngTimedRun = IsBetterRngTimedRunActive();
            bool practiceSaveTimedRun = IsPracticeSaveTimedRunActive();
            if (_timerRunning && !_timerCompleted)
            {
                _realTimeElapsedSeconds += Time.unscaledDeltaTime;

                bool shouldPauseDisplayedTimer = betterRngTimedRun
                    ? (_portalLoading && _portalLoadCount > 1)
                    : practiceSaveTimedRun
                        ? (_portalLoading || _deathLoading)
                    : (_portalLoading || _deathLoading);
                if (!shouldPauseDisplayedTimer)
                {
                    _elapsedSeconds += Time.unscaledDeltaTime;
                }
            }

            if (LaunchRocket.isLaunching && !_lastLaunchStarted && _timerRunning && !_timerCompleted)
            {
                _timerCompleted = true;
                _timerRunning = false;
                ModLog.Info("Speedrun timer completed on rocket launch at " + FormatTimer(_elapsedSeconds) + ".");

                if (betterRngTimedRun)
                {
                    ShowBetterRngVerification();
                }
            }

            _lastLaunchStarted = LaunchRocket.isLaunching;

            if (_timerCompleted)
            {
                return;
            }

            switch (_activeRunMode)
            {
                case ModSingleplayerRunMode.Creative:
                    UpdateCreativeTimerLifecycle();
                    break;
                case ModSingleplayerRunMode.Practice:
                    UpdatePracticeSaveTimerLifecycle();
                    break;
                case ModSingleplayerRunMode.Survival:
                case ModSingleplayerRunMode.Hardcore:
                    UpdateSurvivalTimerLifecycle();
                    break;
                default:
                    if (!_timerRunning)
                    {
                        if (_timerArmed && string.Equals(SceneManager.GetActiveScene().name, "XMenu", StringComparison.Ordinal))
                        {
                            ModLog.Info("Speedrun timer disarmed after returning to main menu.");
                        }

                        _timerArmed = false;
                        _elapsedSeconds = 0d;
                        _realTimeElapsedSeconds = 0d;
                    }

                    break;
            }
        }

        private static void UpdateCreativeTimerLifecycle()
        {
            if (ShouldArmCreativeNewGameTimer())
            {
                if (!_timerArmed)
                {
                    _timerArmed = true;
                    ModLog.Info("Speedrun timer armed for fresh Creative singleplayer start.");
                }
            }
            else if (!_timerRunning)
            {
                _timerArmed = false;
                _elapsedSeconds = 0d;
                _realTimeElapsedSeconds = 0d;
            }

            if (_timerArmed && !_timerRunning)
            {
                string startReason;
                if (TryGetCreativeTimerStartReason(out startReason))
                {
                    _timerRunning = true;
                    _verificationVisible = false;
                    ModLog.Info("Speedrun timer started due to " + startReason + ".");
                }
            }
        }

        private static void UpdatePracticeSaveTimerLifecycle()
        {
            if (!ShouldArmPracticeSaveTimer())
            {
                if (!_timerRunning)
                {
                    _timerArmed = false;
                    _elapsedSeconds = 0d;
                    _realTimeElapsedSeconds = 0d;
                }

                return;
            }

            if (!_timerArmed)
            {
                _timerArmed = true;
                ModLog.Info("Speedrun timer armed for practice save '" + ModClientSessionMode.PracticeSaveId + "'.");
            }

            if (_timerArmed && !_timerRunning)
            {
                string startReason;
                if (TryGetCreativeTimerStartReason(out startReason))
                {
                    _timerRunning = true;
                    _verificationVisible = false;
                    ModLog.Info("Practice save timer started due to " + startReason + ".");
                }
            }
        }

        private static void UpdateSurvivalTimerLifecycle()
        {
            if (!ShouldArmSurvivalNewGameTimer())
            {
                if (!_timerRunning)
                {
                    _timerArmed = false;
                    _survivalIntroObserved = false;
                    _elapsedSeconds = 0d;
                    _realTimeElapsedSeconds = 0d;
                }

                return;
            }

            if (!_timerArmed)
            {
                _timerArmed = true;
                ModLog.Info("Speedrun timer armed for fresh " + _activeRunMode + " singleplayer start.");
            }

            if (IntroVignette.isIntroActive || (Player.main != null && Player.main.cinematicModeActive))
            {
                _survivalIntroObserved = true;
            }

            if (_timerArmed &&
                !_timerRunning &&
                _survivalIntroObserved &&
                !IntroVignette.isIntroActive &&
                Player.main != null &&
                !Player.main.cinematicModeActive)
            {
                _timerRunning = true;
                _verificationVisible = false;
                ModLog.Info("Speedrun timer started due to intro cutscene ending and player control being restored.");
            }
        }

        private static void ShowBetterRngVerification()
        {
            BetterRngRunVerificationResult verification =
                BetterRngRunVerification.Evaluate(FormatTimer(_elapsedSeconds), FormatTimer(_realTimeElapsedSeconds));
            _verificationTitle = verification.Title;
            _verificationDetail = verification.Detail;
            _verificationTitleColor = verification.TitleColor;
            _verificationDetailColor = verification.DetailColor;
            _verificationVisible = true;
            ModLog.Info(
                "BetterRNG run verification resolved: valid=" +
                verification.IsValid +
                ", igt=" +
                FormatTimer(_elapsedSeconds) +
                ", rta=" +
                FormatTimer(_realTimeElapsedSeconds) +
                ".");
        }

        private static bool ShouldArmCreativeNewGameTimer()
        {
            if (Player.main == null)
            {
                return false;
            }

            if (Utils.GetContinueMode())
            {
                return false;
            }

            if (Utils.GetLegacyGameMode() != GameMode.Creative)
            {
                return false;
            }

            if (_state != ModGameStateKind.InGame)
            {
                return false;
            }

            if (IntroVignette.isIntroActive || Player.main.cinematicModeActive)
            {
                return false;
            }

            return Player.main.currentEscapePod != null;
        }

        private static bool ShouldArmSurvivalNewGameTimer()
        {
            if (Player.main == null)
            {
                return false;
            }

            if (Utils.GetContinueMode())
            {
                return false;
            }

            GameMode mode = Utils.GetLegacyGameMode();
            if (mode != GameMode.Survival && mode != GameMode.Hardcore)
            {
                return false;
            }

            if (_state != ModGameStateKind.InGame)
            {
                return false;
            }

            return Player.main.currentEscapePod != null;
        }

        private static bool ShouldArmPracticeSaveTimer()
        {
            if (Player.main == null)
            {
                return false;
            }

            if (!ModClientSessionMode.IsPracticeSaveTimerEnabled)
            {
                return false;
            }

            GameMode mode = Utils.GetLegacyGameMode();
            if (mode != GameMode.Survival && mode != GameMode.Hardcore)
            {
                return false;
            }

            if (_state != ModGameStateKind.InGame)
            {
                return false;
            }

            return true;
        }

        private static bool TryGetCreativeTimerStartReason(out string reason)
        {
            reason = null;
            if (Player.main == null)
            {
                return false;
            }

            Vector3 moveDirection = GameInput.GetMoveDirection();
            if (Mathf.Abs(moveDirection.x) > 0.01f || Mathf.Abs(moveDirection.y) > 0.01f || Mathf.Abs(moveDirection.z) > 0.01f)
            {
                reason = "movement input";
                return true;
            }

            if (GameInput.GetButtonDown(GameInput.Button.Jump))
            {
                reason = "jump input";
                return true;
            }

            if (GameInput.GetButtonDown(GameInput.Button.PDA))
            {
                reason = "PDA button press";
                return true;
            }

            GUIHand hand = Player.main.GetComponent<GUIHand>();
            GameObject activeTarget = hand != null ? hand.GetActiveTarget() : null;
            if (activeTarget != null && GameInput.GetButtonDown(GameInput.Button.LeftHand))
            {
                reason = "interaction with " + activeTarget.name;
                return true;
            }

            return false;
        }

        private static void UpdateRunProgress()
        {
            bool seaglideUnlocked = HasKnownTech(TechType.Seaglide) || HasKnownTech(TechType.SeaglideBlueprint);
            bool ionBatteryUnlocked = HasKnownTech(TechType.PrecursorIonBattery);
            bool rocketBaseUnlocked = HasKnownTech(TechType.RocketBase);
            bool ionBatteryJustUnlocked = ionBatteryUnlocked && !_lastIonBatteryUnlocked;

            _lastSeaglideUnlocked = seaglideUnlocked;
            _lastIonBatteryUnlocked = ionBatteryUnlocked;

            if (_timerRunning)
            {
                AdvanceRunProgress(1, "Started Run", "timer start");
            }

            switch (_activeRunMode)
            {
                case ModSingleplayerRunMode.Creative:
                    UpdateCreativeRunProgress();
                    break;
                case ModSingleplayerRunMode.Survival:
                    UpdateSurvivalRunProgress(seaglideUnlocked, ionBatteryUnlocked, ionBatteryJustUnlocked, rocketBaseUnlocked);
                    break;
                case ModSingleplayerRunMode.Hardcore:
                    UpdateHardcoreRunProgress();
                    break;
            }
        }

        private static void UpdateCreativeRunProgress()
        {
            if (_portalLoading && _timerRunning)
            {
                AdvanceRunProgress(2, "Entered Portal", "portal loading start");
            }

            float cureStartTime = GetCureStartTime();
            if (cureStartTime > 0f && cureStartTime > _lastCureStartTime)
            {
                _lastCureStartTime = cureStartTime;
                AdvanceRunProgress(3, "Cured", "enzyme cure interaction");
            }

            if (_timerRunning && _runProgressIndex >= 3 && IsQepDeactivationInProgress())
            {
                AdvanceRunProgress(4, "Deactivated QEP", "QEP deactivation interaction");
            }

            if (LaunchRocket.isLaunching)
            {
                AdvanceRunProgress(5, "Launched the Rocket", "rocket launch");
            }
        }

        private static void UpdateSurvivalRunProgress(
            bool seaglideUnlocked,
            bool ionBatteryUnlocked,
            bool ionBatteryJustUnlocked,
            bool rocketBaseUnlocked)
        {
            if (_runProgressIndex < 2 && _lastCraftedTechType == TechType.Seaglide)
            {
                AdvanceRunProgress(2, "Crafted Seaglide", "crafted Seaglide");
            }

            if (_playerDiedThisFrame && _runProgressIndex < 3 && (IsWithinBounds(ClipADeathBounds, true) || IsWithinBounds(ClipCDeathBounds, true)))
            {
                AdvanceRunProgress(3, "Flooded Base", "death inside Clip A/C flooded base bounds");
            }

            if (_runProgressIndex < 4 && IsWithinBounds(KelpLeaveBounds) && HasInventoryItem(TechType.CreepvinePiece))
            {
                AdvanceRunProgress(4, "Kelp Forest Leave", "kelp leave bounds with Creepvine Sample");
            }

            if (_playerDiedThisFrame && _runProgressIndex < 5 && rocketBaseUnlocked && IsCurrentBiomeAny(AuroraBiomes, true))
            {
                AdvanceRunProgress(5, "Completed Aurora", "death in Aurora after Rocket Base unlock");
            }

            if (_runProgressIndex < 6 && IsWithinBounds(MountainDescendBounds))
            {
                AdvanceRunProgress(6, "Descended Mountain", "entered mountain descend bounds");
            }

            if (_runProgressIndex < 7 && ionBatteryUnlocked && ionBatteryJustUnlocked)
            {
                AdvanceRunProgress(7, "Ion Battery Unlocked", "Ion Battery blueprint unlock");
            }

            float cureStartTime = GetCureStartTime();
            if (cureStartTime > 0f && cureStartTime > _lastCureStartTime)
            {
                _lastCureStartTime = cureStartTime;
                AdvanceRunProgress(8, "Cured", "enzyme cure interaction");
            }

            if (_playerDiedThisFrame && _runProgressIndex < 9 && string.Equals(GetCurrentBiomeName(true), "Precursor_Gun_ControlRoom", StringComparison.Ordinal))
            {
                AdvanceRunProgress(9, "QEP Completed", "death in QEP control room");
            }

            if (_playerDiedThisFrame && _runProgressIndex < 10 && IsCurrentBiomeAny(SparseDeathBiomes, true))
            {
                AdvanceRunProgress(10, "Sparse Reef Complete", "death in Sparse Reef / Sea Treader's Path");
            }

            if (_runProgressIndex < 11 && _lastStartedCraftTechType == TechType.Cyclops)
            {
                AdvanceRunProgress(11, "Built Cyclops", "started Cyclops construction");
            }

            if (LaunchRocket.isLaunching)
            {
                AdvanceRunProgress(12, "Launched the Rocket", "rocket launch");
            }
        }

        private static void UpdateHardcoreRunProgress()
        {
            if (LaunchRocket.isLaunching)
            {
                AdvanceRunProgress(2, "Launched the Rocket", "rocket launch");
            }
        }

        private static void AdvanceRunProgress(int nextIndex, string subtitle, string reason)
        {
            if (nextIndex <= _runProgressIndex)
            {
                return;
            }

            _runProgressIndex = nextIndex;
            _runProgressSubtitle = subtitle;
            ModLog.Info("Run progress advanced to step " + nextIndex + " ('" + subtitle + "') due to " + reason + ".");
        }

        private static float GetCureStartTime()
        {
            if (Player.main == null || PlayerInfectionCuredField == null)
            {
                return 0f;
            }

            try
            {
                object value = PlayerInfectionCuredField.GetValue(Player.main);
                return value is float ? (float)value : 0f;
            }
            catch
            {
                return 0f;
            }
        }

        private static bool IsQepDeactivationInProgress()
        {
            if (PrecursorDisableGunTerminalUsingPlayerField == null)
            {
                return false;
            }

            if (_cachedGunTerminals == null || _cachedGunTerminals.Length == 0 || Time.unscaledTime >= _nextGunTerminalRefreshAt)
            {
                _cachedGunTerminals = UnityEngine.Object.FindObjectsOfType<PrecursorDisableGunTerminal>();
                _nextGunTerminalRefreshAt = Time.unscaledTime + 2f;
            }

            for (int i = 0; i < _cachedGunTerminals.Length; i++)
            {
                PrecursorDisableGunTerminal terminal = _cachedGunTerminals[i];
                if (terminal == null)
                {
                    continue;
                }

                try
                {
                    if (PrecursorDisableGunTerminalUsingPlayerField.GetValue(terminal) != null)
                    {
                        return true;
                    }
                }
                catch
                {
                }
            }

            return false;
        }

        private static void UpdateCraftingState()
        {
            if (Time.unscaledTime < _nextCrafterScanAt)
            {
                return;
            }

            _nextCrafterScanAt = Time.unscaledTime + 0.25f;
            _lastStartedCraftTechType = TechType.None;

            CrafterLogic[] crafters;
            try
            {
                crafters = UnityEngine.Object.FindObjectsOfType<CrafterLogic>();
            }
            catch
            {
                return;
            }

            HashSet<int> seen = new HashSet<int>();
            for (int i = 0; i < crafters.Length; i++)
            {
                CrafterLogic crafter = crafters[i];
                if (crafter == null)
                {
                    continue;
                }

                int id = crafter.GetInstanceID();
                seen.Add(id);

                if (crafter.inProgress)
                {
                    TechType existingInProgressTechType;
                    if (!_crafterInProgressTechTypes.TryGetValue(id, out existingInProgressTechType) || existingInProgressTechType != crafter.currentTechType)
                    {
                        _lastStartedCraftTechType = crafter.currentTechType;
                        ModLog.Info("Detected craft start via runtime polling: " + crafter.currentTechType + ".");
                    }

                    _crafterInProgressTechTypes[id] = crafter.currentTechType;
                    continue;
                }

                TechType completedTechType;
                if (_crafterInProgressTechTypes.TryGetValue(id, out completedTechType) && completedTechType != TechType.None)
                {
                    _lastCraftedTechType = completedTechType;
                    _crafterInProgressTechTypes.Remove(id);
                    ModLog.Info("Detected crafted TechType via runtime polling: " + completedTechType + ".");
                }
            }

            List<int> staleIds = null;
            foreach (KeyValuePair<int, TechType> pair in _crafterInProgressTechTypes)
            {
                if (seen.Contains(pair.Key))
                {
                    continue;
                }

                if (staleIds == null)
                {
                    staleIds = new List<int>();
                }

                staleIds.Add(pair.Key);
            }

            if (staleIds == null)
            {
                return;
            }

            for (int i = 0; i < staleIds.Count; i++)
            {
                _crafterInProgressTechTypes.Remove(staleIds[i]);
            }
        }

        private static bool ShouldShowTimerUi()
        {
            if (_timerCompleted || _timerRunning || _timerArmed)
            {
                return true;
            }

            return _activeRunMode != ModSingleplayerRunMode.None &&
                (_state == ModGameStateKind.InGame ||
                 _state == ModGameStateKind.PortalLoading ||
                 _state == ModGameStateKind.DeathLoading);
        }

        private static bool ShouldShowRunStatus()
        {
            if (IsBetterRngTimedRunActive() || IsPracticeSaveTimedRunActive())
            {
                return false;
            }

            return (_activeRunMode != ModSingleplayerRunMode.None &&
                (_state == ModGameStateKind.InGame ||
                 _state == ModGameStateKind.PortalLoading ||
                 _state == ModGameStateKind.DeathLoading)) ||
                _timerRunning ||
                _timerCompleted ||
                _timerArmed;
        }

        private static bool IsCreativeSingleplayerRunActive()
        {
            return _activeRunMode == ModSingleplayerRunMode.Creative &&
                Player.main != null &&
                _state == ModGameStateKind.InGame &&
                !Utils.GetContinueMode();
        }

        private static string GetRunStatusTitle()
        {
            string modeLabel = GetRunModeLabel();
            return string.IsNullOrEmpty(modeLabel)
                ? "In Singleplayer Run"
                : "In Singleplayer Run (" + modeLabel + ")";
        }

        private static string GetRunStatusSubtitle()
        {
            return string.IsNullOrEmpty(_runProgressSubtitle) ? "Run has Not Started" : _runProgressSubtitle;
        }

        private static ModSingleplayerRunMode ResolveCurrentRunMode()
        {
            bool rankedPracticeActive = ModSeedRuntimeHost.IsRankedSingleplayerSeedActive();
            bool betterRngTimedActive = IsBetterRngTimedRunActive();
            bool practiceSaveTimedActive = IsPracticeSaveTimedRunActive();
            if (!rankedPracticeActive && !betterRngTimedActive && !practiceSaveTimedActive)
            {
                return ModSingleplayerRunMode.None;
            }

            if (_state == ModGameStateKind.MainMenu || _state == ModGameStateKind.Booting)
            {
                return ModSingleplayerRunMode.None;
            }

            if (Utils.GetContinueMode() && !practiceSaveTimedActive)
            {
                return ModSingleplayerRunMode.None;
            }

            GameMode mode = Utils.GetLegacyGameMode();
            switch (mode)
            {
                case GameMode.Creative:
                    return rankedPracticeActive
                        ? ModSingleplayerRunMode.Creative
                        : ModSingleplayerRunMode.None;
                case GameMode.Survival:
                    if (practiceSaveTimedActive)
                    {
                        return ModSingleplayerRunMode.Practice;
                    }

                    return ModSingleplayerRunMode.Survival;
                case GameMode.Hardcore:
                    if (practiceSaveTimedActive)
                    {
                        return ModSingleplayerRunMode.Practice;
                    }

                    return ModSingleplayerRunMode.Hardcore;
                default:
                    return ModSingleplayerRunMode.None;
            }
        }

        private static bool IsBetterRngTimedRunActive()
        {
            if (!ModClientSessionMode.IsBetterRngSingleplayerSelected)
            {
                return false;
            }

            if (_state == ModGameStateKind.Booting || _state == ModGameStateKind.MainMenu)
            {
                return false;
            }

            GameMode mode = Utils.GetLegacyGameMode();
            return mode == GameMode.Survival || mode == GameMode.Hardcore;
        }

        private static bool IsPracticeSaveTimedRunActive()
        {
            if (!ModClientSessionMode.IsPracticeSaveTimerEnabled)
            {
                return false;
            }

            if (_state == ModGameStateKind.Booting || _state == ModGameStateKind.MainMenu)
            {
                return false;
            }

            GameMode mode = Utils.GetLegacyGameMode();
            return mode == GameMode.Survival || mode == GameMode.Hardcore;
        }

        private static string GetRunModeLabel()
        {
            switch (_activeRunMode)
            {
                case ModSingleplayerRunMode.Creative:
                    return "Creative";
                case ModSingleplayerRunMode.Practice:
                    return "Practice";
                case ModSingleplayerRunMode.Survival:
                    return "Survival";
                case ModSingleplayerRunMode.Hardcore:
                    return "Hardcore";
                default:
                    return string.Empty;
            }
        }

        private static bool HasKnownTech(TechType techType)
        {
            try
            {
                return KnownTech.Contains(techType);
            }
            catch
            {
                return false;
            }
        }

        private static bool HasInventoryItem(TechType techType)
        {
            return Inventory.main != null && Inventory.main.GetPickupCount(techType) > 0;
        }

        private static void CacheAliveDeathContext()
        {
            if (Player.main == null || !IsPlayerAlive())
            {
                return;
            }

            _lastAlivePosition = Player.main.transform.position;
            _hasLastAlivePosition = true;
            _lastAliveBiomeName = ResolveCurrentBiomeName();
        }

        private static string GetCurrentBiomeName(bool useLastAliveBiome = false)
        {
            if (useLastAliveBiome && !string.IsNullOrEmpty(_lastAliveBiomeName))
            {
                return _lastAliveBiomeName;
            }

            return ResolveCurrentBiomeName();
        }

        private static string ResolveCurrentBiomeName()
        {
            if (Player.main == null)
            {
                return string.Empty;
            }

            try
            {
                string playerBiome = Player.main.GetBiomeString();
                if (!string.IsNullOrEmpty(playerBiome))
                {
                    return playerBiome;
                }
            }
            catch
            {
            }

            if (LargeWorld.main == null)
            {
                return string.Empty;
            }

            try
            {
                return LargeWorld.main.GetBiome(Player.main.transform.position) ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static bool IsCurrentBiomeAny(IList<string> names, bool useLastAliveBiome = false)
        {
            string biomeName = GetCurrentBiomeName(useLastAliveBiome);
            for (int i = 0; i < names.Count; i++)
            {
                if (string.Equals(biomeName, names[i], StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsWithinBounds(float[] bounds, bool useLastAlivePosition = false)
        {
            if (bounds == null || bounds.Length < 6)
            {
                return false;
            }

            Vector3 position;
            if (useLastAlivePosition)
            {
                if (!_hasLastAlivePosition)
                {
                    return false;
                }

                position = _lastAlivePosition;
            }
            else
            {
                if (Player.main == null)
                {
                    return false;
                }

                position = Player.main.transform.position;
            }

            return position.x >= Mathf.Min(bounds[0], bounds[1]) &&
                position.x <= Mathf.Max(bounds[0], bounds[1]) &&
                position.y >= Mathf.Min(bounds[2], bounds[3]) &&
                position.y <= Mathf.Max(bounds[2], bounds[3]) &&
                position.z >= Mathf.Min(bounds[4], bounds[5]) &&
                position.z <= Mathf.Max(bounds[4], bounds[5]);
        }

        private static string FormatVector3(Vector3 value)
        {
            return string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                "({0:0.###}, {1:0.###}, {2:0.###})",
                value.x,
                value.y,
                value.z);
        }

        private static string FormatTimer(double elapsedSeconds)
        {
            TimeSpan span = TimeSpan.FromSeconds(elapsedSeconds);
            int totalHours = (int)span.TotalHours;
            if (totalHours > 0)
            {
                return string.Format(
                    System.Globalization.CultureInfo.InvariantCulture,
                    "{0}:{1:00}:{2:00}.{3:000}",
                    totalHours,
                    span.Minutes,
                    span.Seconds,
                    span.Milliseconds);
            }

            if (span.TotalMinutes >= 1d)
            {
                return string.Format(
                    System.Globalization.CultureInfo.InvariantCulture,
                    "{0}:{1:00}.{2:000}",
                    (int)span.TotalMinutes,
                    span.Seconds,
                    span.Milliseconds);
            }

            return string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                "{0}.{1:000}",
                span.Seconds,
                span.Milliseconds);
        }

        private static bool IsPlayerAlive()
        {
            if (Player.main == null || Player.main.liveMixin == null)
            {
                return true;
            }

            return Player.main.liveMixin.IsAlive();
        }

        private static bool IsCraftingMenuOpen()
        {
            return uGUI.main != null &&
                uGUI.main.craftingMenu != null &&
                uGUI.main.craftingMenu.gameObject.activeInHierarchy;
        }

        private static bool IsPdaOpen()
        {
            return Player.main != null &&
                Player.main.GetPDA() != null &&
                Player.main.GetPDA().isInUse;
        }

        private static void ResetMenuOnlyPollingState()
        {
            _lastStartedCraftTechType = TechType.None;
            _lastCraftedTechType = TechType.None;
            _crafterInProgressTechTypes.Clear();
            _nextCrafterScanAt = 0f;
            _cachedGunTerminals = null;
            _nextGunTerminalRefreshAt = 0f;
            ConsistentScreenshotClip.Reset();
        }

        private static void ResetTimerSession()
        {
            _portalLoading = false;
            _portalLoadCount = 0;
            _deathLoading = false;
            _lastAliveBiomeName = string.Empty;
            _lastAlivePosition = Vector3.zero;
            _hasLastAlivePosition = false;
            _timerArmed = false;
            _timerRunning = false;
            _timerCompleted = false;
            _lastLaunchStarted = false;
            _elapsedSeconds = 0d;
            _realTimeElapsedSeconds = 0d;
            _lastActivatedSeedSaveSlot = string.Empty;
            _lastActivatedSeedMode = GameMode.None;
            _lastCureStartTime = 0f;
            _currentSaveSlot = string.Empty;
            _currentGameModeName = string.Empty;
            _activeRunMode = ModSingleplayerRunMode.None;
            _runProgressIndex = 0;
            _runProgressSubtitle = "Run has Not Started";
            _survivalIntroObserved = false;
            _lastSeaglideUnlocked = false;
            _lastIonBatteryUnlocked = false;
            _lastStartedCraftTechType = TechType.None;
            _lastCraftedTechType = TechType.None;
            _crafterInProgressTechTypes.Clear();
            _nextCrafterScanAt = 0f;
            _cachedGunTerminals = null;
            _nextGunTerminalRefreshAt = 0f;
            _verificationTitle = string.Empty;
            _verificationDetail = string.Empty;
            _verificationTitleColor = Color.white;
            _verificationDetailColor = Color.white;
            _verificationVisible = false;
            ModSeedStore.ResetSessionSelections();
            ModSeedWorldRuntime.Reset();
            SeededLifepodPlacement.Reset();
            ConsistentScreenshotClip.Reset();
        }

        private static void AttachSceneRuntimeBehaviour(Scene scene)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                GameObject root = roots[i];
                if (root == null)
                {
                    continue;
                }

                _runtimeBehaviour = root.GetComponent<ModGameplayRuntimeBehaviour>();
                if (_runtimeBehaviour == null)
                {
                    _runtimeBehaviour = root.AddComponent<ModGameplayRuntimeBehaviour>();
                }

                if (_runtimeBehaviour != null)
                {
                    ModLog.Info("Attached ranked gameplay runtime behaviour to scene root '" + root.name + "'.");
                    return;
                }
            }
        }

        private enum ModGameStateKind
        {
            Booting,
            MainMenu,
            LoadingNewGame,
            LoadingSave,
            InGame,
            PortalLoading,
            DeathLoading
        }

        private enum ModSingleplayerRunMode
        {
            None,
            Creative,
            Practice,
            Survival,
            Hardcore
        }

        private struct ModGameplaySnapshot : IEquatable<ModGameplaySnapshot>
        {
            public ModGameplaySnapshot(
                string sceneName,
                bool isMainMenuScene,
                bool isLoadingVisible,
                bool isWorldReady,
                bool isContinueMode,
                bool isCreativeMode,
                bool isInEscapePod,
                bool isPdaOpen,
                bool isPauseMenuOpen,
                bool isIntroActive,
                bool isCinematicActive,
                bool hasPlayer,
                bool isPortalLoading,
                bool isDeathLoading)
            {
                SceneName = sceneName;
                IsMainMenuScene = isMainMenuScene;
                IsLoadingVisible = isLoadingVisible;
                IsWorldReady = isWorldReady;
                IsContinueMode = isContinueMode;
                IsCreativeMode = isCreativeMode;
                IsInEscapePod = isInEscapePod;
                IsPdaOpen = isPdaOpen;
                IsPauseMenuOpen = isPauseMenuOpen;
                IsIntroActive = isIntroActive;
                IsCinematicActive = isCinematicActive;
                HasPlayer = hasPlayer;
                IsPortalLoading = isPortalLoading;
                IsDeathLoading = isDeathLoading;
            }

            public string SceneName { get; private set; }

            public bool IsMainMenuScene { get; private set; }

            public bool IsLoadingVisible { get; private set; }

            public bool IsWorldReady { get; private set; }

            public bool IsContinueMode { get; private set; }

            public bool IsCreativeMode { get; private set; }

            public bool IsInEscapePod { get; private set; }

            public bool IsPdaOpen { get; private set; }

            public bool IsPauseMenuOpen { get; private set; }

            public bool IsIntroActive { get; private set; }

            public bool IsCinematicActive { get; private set; }

            public bool HasPlayer { get; private set; }

            public bool IsPortalLoading { get; private set; }

            public bool IsDeathLoading { get; private set; }

            public bool Equals(ModGameplaySnapshot other)
            {
                return string.Equals(SceneName, other.SceneName, StringComparison.Ordinal) &&
                    IsMainMenuScene == other.IsMainMenuScene &&
                    IsLoadingVisible == other.IsLoadingVisible &&
                    IsWorldReady == other.IsWorldReady &&
                    IsContinueMode == other.IsContinueMode &&
                    IsCreativeMode == other.IsCreativeMode &&
                    IsInEscapePod == other.IsInEscapePod &&
                    IsPdaOpen == other.IsPdaOpen &&
                    IsPauseMenuOpen == other.IsPauseMenuOpen &&
                    IsIntroActive == other.IsIntroActive &&
                    IsCinematicActive == other.IsCinematicActive &&
                    HasPlayer == other.HasPlayer &&
                    IsPortalLoading == other.IsPortalLoading &&
                    IsDeathLoading == other.IsDeathLoading;
            }

            public string ToLogString()
            {
                return "scene=" + SceneName +
                    ", hasPlayer=" + HasPlayer +
                    ", loadingVisible=" + IsLoadingVisible +
                    ", worldReady=" + IsWorldReady +
                    ", continueMode=" + IsContinueMode +
                    ", creativeMode=" + IsCreativeMode +
                    ", inEscapePod=" + IsInEscapePod +
                    ", pdaOpen=" + IsPdaOpen +
                    ", pauseMenuOpen=" + IsPauseMenuOpen +
                    ", introActive=" + IsIntroActive +
                    ", cinematicActive=" + IsCinematicActive +
                    ", portalLoading=" + IsPortalLoading +
                    ", deathLoading=" + IsDeathLoading;
            }
        }

        private sealed class ModGameplayRuntimeBehaviour : MonoBehaviour
        {
            private void Update()
            {
                if (ReferenceEquals(_runtimeBehaviour, this))
                {
                    OnRuntimeUpdate();
                }
            }
        }
    }
}
