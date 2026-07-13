using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using SubnauticaSpeedrunningMod.Runtime.Seeds;
using UWE;
using UnityEngine;

namespace SubnauticaSpeedrunningMod.Runtime
{
    internal static class ModConsoleCommandsRuntime
    {
        private const string HarmonyId = "subnautica.speedrunning.mod.consolecommands";
        private const float ApplyRetryIntervalSeconds = 0.1f;
        private const float TransientStabilizationSeconds = 0.75f;
        private const int BatchEntityLevels = 4;
        private const float AwakenBatchTimeoutSeconds = 20f;
        private const float RankedConsoleBlockedMessageCooldownSeconds = 1f;

        private static readonly MethodInfo PlayerUpdateIsUnderwaterMethod =
            typeof(Player).GetMethod("UpdateIsUnderwater", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        private static readonly MethodInfo DevConsoleSubmitMethod =
            typeof(DevConsole).GetMethod("Submit", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly FieldInfo PlayerControllerUnderWaterField =
            typeof(PlayerController).GetField("underWater", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly FieldInfo PlayerControllerNextUnderWaterStateField =
            typeof(PlayerController).GetField("nextUnderWaterState", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly MethodInfo PlayerControllerHandleUnderWaterStateMethod =
            typeof(PlayerController).GetMethod("HandleUnderWaterState", BindingFlags.Instance | BindingFlags.NonPublic);

        private static bool _installAttempted;
        private static bool _installed;
        private static bool _available = true;
        private static Harmony _harmony;
        private static BatchBorderRenderer _batchBorderRenderer;
        private static bool _batchBordersEnabled;
        private static bool _batchBordersThroughWallsEnabled;
        private static int _batchBorderSpan = 15;
        private static bool _ssgPermanentEnabled;
        private static bool _ssgTransientRequested;
        private static bool _ssgTransientStabilizing;
        private static bool _ssgControllerHookActive;
        private static bool _ssgAppliedOnce;
        private static float _nextApplyAttemptAt;
        private static float _transientAppliedAt;
        private static bool _awakenBatchRunning;
        private static float _lastRankedConsoleBlockedMessageAt = -1000f;

        public static bool EnsureInstalled()
        {
            if (_installAttempted)
            {
                return _installed && _available;
            }

            _installAttempted = true;

            try
            {
                MethodInfo postfix = typeof(ModConsoleCommandsRuntime).GetMethod(
                    nameof(HandleUnderWaterStatePostfix),
                    BindingFlags.Static | BindingFlags.NonPublic);
                MethodInfo submitPrefix = typeof(ModConsoleCommandsRuntime).GetMethod(
                    nameof(DevConsoleSubmitPrefix),
                    BindingFlags.Static | BindingFlags.NonPublic);
                if (PlayerControllerHandleUnderWaterStateMethod == null || DevConsoleSubmitMethod == null || postfix == null || submitPrefix == null)
                {
                    _available = false;
                    ModLog.Warn("Console command runtime unavailable; required player controller or dev console hooks could not be resolved.");
                    return false;
                }

                _harmony = new Harmony(HarmonyId);
                _harmony.Patch(PlayerControllerHandleUnderWaterStateMethod, postfix: new HarmonyMethod(postfix));
                _harmony.Patch(DevConsoleSubmitMethod, prefix: new HarmonyMethod(submitPrefix));
                _installed = true;
                ModLog.Info("Installed custom console command runtime.");
                return true;
            }
            catch (Exception ex)
            {
                _available = false;
                ModLog.Warn("Failed to install custom console command runtime: " + ex.Message);
                return false;
            }
        }

        public static void Update()
        {
            if (_batchBordersEnabled || _batchBordersThroughWallsEnabled)
            {
                EnsureBatchBorderRenderer();
            }

            UpdateSuperSeaglide();
        }

        private static bool DevConsoleSubmitPrefix(string value, ref bool __result)
        {
            try
            {
                if (ShouldBlockCommandsForRanked())
                {
                    ShowRankedConsoleBlockedMessage();
                    __result = false;
                    return false;
                }

                if (!TryHandleConsoleCommand(value))
                {
                    return true;
                }

                ModLog.Info("Handled custom console command through Submit hook: " + value);
                __result = true;
                return false;
            }
            catch (Exception ex)
            {
                ModLog.Error("Console hook failed for '" + value + "': " + ex);
                __result = false;
                return false;
            }
        }

        private static bool ShouldBlockCommandsForRanked()
        {
            return ModSeedRuntimeHost.IsRankedSingleplayerSeedActive() ||
                   ModSeedRuntimeHost.IsRankedMultiplayerSeedActive();
        }

        private static void ShowRankedConsoleBlockedMessage()
        {
            if (Time.unscaledTime - _lastRankedConsoleBlockedMessageAt < RankedConsoleBlockedMessageCooldownSeconds)
            {
                return;
            }

            _lastRankedConsoleBlockedMessageAt = Time.unscaledTime;
            ErrorMessage.AddMessage("Console commands are disabled for ranked runs.");
            ModLog.Info("Blocked console command while ranked session was active.");
        }

        private static bool TryHandleConsoleCommand(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }

            string[] split = value.Trim().Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (split.Length == 0)
            {
                return false;
            }

            string command = split[0].ToLowerInvariant();
            switch (command)
            {
                case "ssg":
                    OnConsoleCommand_ssg();
                    return true;
                case "ssgp":
                    OnConsoleCommand_ssgp();
                    return true;
                case "batchborders":
                    HandleBatchBorderCommand(false, split.Length > 1 ? split[1] : null);
                    return true;
                case "batchbordersw":
                    HandleBatchBorderCommand(true, split.Length > 1 ? split[1] : null);
                    return true;
                case "scout":
                    OnConsoleCommand_scout();
                    return true;
                case "awakenbatch":
                    OnConsoleCommand_awakenbatch();
                    return true;
                default:
                    return false;
            }
        }

        private static void OnConsoleCommand_ssg()
        {
            Player player = Player.main;
            PlayerController controller = player == null ? null : player.playerController;
            if (IsSuperSeaglideStateActive(player, controller) || _ssgPermanentEnabled || _ssgTransientRequested || _ssgTransientStabilizing)
            {
                DisableSuperSeaglideAndRestore();
                ErrorMessage.AddDebug("Speedrunning Mod: Super Seaglide disabled.");
                return;
            }

            _ssgTransientRequested = true;
            _ssgTransientStabilizing = false;
            _ssgAppliedOnce = false;
            _ssgControllerHookActive = true;
            _nextApplyAttemptAt = 0f;
            _transientAppliedAt = 0f;
            ErrorMessage.AddDebug("Speedrunning Mod: Super Seaglide queued.");
        }

        private static void OnConsoleCommand_ssgp()
        {
            _ssgPermanentEnabled = !_ssgPermanentEnabled;
            if (_ssgPermanentEnabled)
            {
                _ssgTransientRequested = true;
                _ssgTransientStabilizing = false;
                _ssgAppliedOnce = false;
                _ssgControllerHookActive = true;
                _nextApplyAttemptAt = 0f;
                _transientAppliedAt = 0f;
                ErrorMessage.AddDebug("Speedrunning Mod: Permanent Super Seaglide enabled.");
                return;
            }

            DisableSuperSeaglideAndRestore();
            ErrorMessage.AddDebug("Speedrunning Mod: Permanent Super Seaglide disabled.");
        }

        private static void OnConsoleCommand_batchborders()
        {
            HandleBatchBorderCommand(false, null);
        }

        private static void OnConsoleCommand_batchbordersw()
        {
            HandleBatchBorderCommand(true, null);
        }

        private static void OnConsoleCommand_scout()
        {
            GameModeUtils.ActivateCheat(GameModeOption.NoOxygen);
            GameModeUtils.ActivateCheat(GameModeOption.NoCost);

            if (NoDamageConsoleCommand.main != null)
            {
                NoDamageConsoleCommand.main.SetNoDamageCheat(true);
            }
            else
            {
                DevConsole.SendConsoleCommand("nodamage");
            }

            ErrorMessage.AddDebug("Speedrunning Mod: Scout cheats enabled.");
        }

        private static void OnConsoleCommand_awakenbatch()
        {
            if (_awakenBatchRunning)
            {
                ErrorMessage.AddDebug("Speedrunning Mod: awakenbatch is already running.");
                return;
            }

            _awakenBatchRunning = true;
            CoroutineHost.StartCoroutine(AwakenCurrentBatchCoroutine());
        }

        private static void EnsureBatchBorderRenderer()
        {
            Camera camera = MainCamera.camera;
            if (camera == null)
            {
                return;
            }

            if (_batchBorderRenderer == null || _batchBorderRenderer.gameObject != camera.gameObject)
            {
                _batchBorderRenderer = camera.gameObject.GetComponent<BatchBorderRenderer>();
                if (_batchBorderRenderer == null)
                {
                    _batchBorderRenderer = camera.gameObject.AddComponent<BatchBorderRenderer>();
                }
            }

            _batchBorderRenderer.enabled = _batchBordersEnabled || _batchBordersThroughWallsEnabled;
            _batchBorderRenderer.ShowThroughWalls = _batchBordersThroughWallsEnabled;
            _batchBorderRenderer.BatchSpan = _batchBorderSpan;
        }

        private static void HandleBatchBorderCommand(bool throughWalls, string argument)
        {
            string normalizedArgument = string.IsNullOrEmpty(argument) ? string.Empty : argument.Trim();
            if (string.Equals(normalizedArgument, "off", StringComparison.OrdinalIgnoreCase))
            {
                _batchBordersEnabled = false;
                _batchBordersThroughWallsEnabled = false;
                EnsureBatchBorderRenderer();
                if (_batchBorderRenderer != null)
                {
                    _batchBorderRenderer.enabled = false;
                }

                ErrorMessage.AddDebug("Speedrunning Mod: Batch borders disabled.");
                return;
            }

            if (!string.IsNullOrEmpty(normalizedArgument))
            {
                int parsedSpan;
                if (!int.TryParse(normalizedArgument, out parsedSpan) || parsedSpan < 1 || parsedSpan > 99)
                {
                    ErrorMessage.AddDebug("Speedrunning Mod: Use batchborders <1-99>, batchbordersw <1-99>, or batchborders off.");
                    return;
                }

                _batchBorderSpan = parsedSpan;
                _batchBordersEnabled = !throughWalls;
                _batchBordersThroughWallsEnabled = throughWalls;
            }
            else if (throughWalls)
            {
                _batchBordersThroughWallsEnabled = !_batchBordersThroughWallsEnabled;
                if (_batchBordersThroughWallsEnabled)
                {
                    _batchBordersEnabled = false;
                }
            }
            else
            {
                _batchBordersEnabled = !_batchBordersEnabled;
                if (_batchBordersEnabled)
                {
                    _batchBordersThroughWallsEnabled = false;
                }
            }

            EnsureBatchBorderRenderer();

            if (_batchBorderRenderer != null)
            {
                _batchBorderRenderer.enabled = _batchBordersEnabled || _batchBordersThroughWallsEnabled;
                _batchBorderRenderer.ShowThroughWalls = _batchBordersThroughWallsEnabled;
                _batchBorderRenderer.BatchSpan = _batchBorderSpan;
            }

            if (_batchBordersEnabled || _batchBordersThroughWallsEnabled)
            {
                ErrorMessage.AddDebug(
                    "Speedrunning Mod: Batch borders " +
                    (_batchBordersThroughWallsEnabled ? "through walls " : string.Empty) +
                    "enabled (" + _batchBorderSpan + "x" + _batchBorderSpan + "x" + _batchBorderSpan + ").");
            }
            else
            {
                ErrorMessage.AddDebug("Speedrunning Mod: Batch borders disabled.");
            }
        }

        private static IEnumerator AwakenCurrentBatchCoroutine()
        {
            LargeWorldStreamer streamer = LargeWorldStreamer.main;
            Camera camera = MainCamera.camera;
            if (streamer == null || camera == null || streamer.cellManager == null)
            {
                ErrorMessage.AddDebug("Speedrunning Mod: world streamer is not ready.");
                _awakenBatchRunning = false;
                yield break;
            }

            Int3 batch = streamer.GetContainingBatch(camera.transform.position);
            if (!streamer.CheckBatch(batch))
            {
                ErrorMessage.AddDebug("Speedrunning Mod: current camera batch is invalid.");
                _awakenBatchRunning = false;
                yield break;
            }

            Int3.Bounds batchBounds = streamer.GetBatchBlockBounds(batch);
            int initialQueueLength = streamer.cellManager.GetQueueLength();
            ModLog.Info("awakenbatch starting for batch " + batch + ".");
            ErrorMessage.AddDebug("Speedrunning Mod: awakening batch " + batch + "...");

            for (int level = 0; level < BatchEntityLevels; level++)
            {
                streamer.cellManager.ShowEntities(batchBounds, level);
            }

            float startedAt = Time.unscaledTime;
            bool queuedAnyWork = streamer.cellManager.GetQueueLength() > initialQueueLength;
            while (!streamer.cellManager.IsIdle())
            {
                queuedAnyWork = true;
                if (Time.unscaledTime - startedAt > AwakenBatchTimeoutSeconds)
                {
                    ModLog.Warn("awakenbatch timed out for batch " + batch + ".");
                    ErrorMessage.AddDebug("Speedrunning Mod: awakenbatch timed out for " + batch + ".");
                    _awakenBatchRunning = false;
                    yield break;
                }

                yield return null;
            }

            if (!queuedAnyWork)
            {
                ErrorMessage.AddDebug("Speedrunning Mod: batch " + batch + " had no new entity work.");
            }
            else
            {
                ModLog.Info("awakenbatch completed for batch " + batch + ".");
                ErrorMessage.AddDebug("Speedrunning Mod: batch " + batch + " fully awakened.");
            }

            _awakenBatchRunning = false;
        }

        private static void UpdateSuperSeaglide()
        {
            if (!_ssgPermanentEnabled && !_ssgTransientRequested && !_ssgTransientStabilizing)
            {
                return;
            }

            Player player = Player.main;
            PlayerController controller = player == null ? null : player.playerController;
            if (player == null || controller == null || player.GetMode() != Player.Mode.Normal)
            {
                return;
            }

            if (Time.unscaledTime < _nextApplyAttemptAt)
            {
                return;
            }

            _nextApplyAttemptAt = Time.unscaledTime + ApplyRetryIntervalSeconds;

            TryRefreshUnderwaterState(player);
            if (!player.IsUnderwaterForSwimming())
            {
                return;
            }

            bool hasPoweredSeaglide = TryGetHeldPoweredSeaglide(out _);
            ApplyMotorModeForHeldTool(player, controller, hasPoweredSeaglide);
            ForceGroundControllerSwimState(player, controller, false);

            if (!_ssgAppliedOnce && IsSuperSeaglideStateActive(player, controller))
            {
                _ssgAppliedOnce = true;
                _transientAppliedAt = Time.unscaledTime;
                if (!_ssgPermanentEnabled)
                {
                    _ssgTransientRequested = false;
                    _ssgTransientStabilizing = true;
                }
            }

            if (_ssgPermanentEnabled)
            {
                return;
            }

            if (_ssgTransientStabilizing && Time.unscaledTime - _transientAppliedAt > TransientStabilizationSeconds)
            {
                _ssgTransientStabilizing = false;
                _ssgControllerHookActive = false;
            }
        }

        private static void DisableSuperSeaglideAndRestore()
        {
            _ssgPermanentEnabled = false;
            _ssgTransientRequested = false;
            _ssgTransientStabilizing = false;
            _ssgControllerHookActive = false;
            _ssgAppliedOnce = false;
            _nextApplyAttemptAt = 0f;
            _transientAppliedAt = 0f;

            Player player = Player.main;
            PlayerController controller = player == null ? null : player.playerController;
            if (player == null || controller == null)
            {
                return;
            }

            try
            {
                player.precursorOutOfWater = false;
                DevConsole.SendConsoleCommand("resetmotormode");
                TryRefreshUnderwaterState(player);
                if (PlayerControllerHandleUnderWaterStateMethod != null)
                {
                    PlayerControllerHandleUnderWaterStateMethod.Invoke(controller, null);
                }
            }
            catch (Exception ex)
            {
                ModLog.Warn("Could not restore vanilla player controller state: " + ex.Message);
            }
        }

        private static void HandleUnderWaterStatePostfix(PlayerController __instance)
        {
            if (!_ssgControllerHookActive || __instance == null)
            {
                return;
            }

            Player player = Player.main;
            if (player == null || player.playerController != __instance || !player.IsUnderwaterForSwimming() || player.GetMode() != Player.Mode.Normal)
            {
                return;
            }

            ForceGroundControllerSwimState(player, __instance, false);
        }

        private static void TryRefreshUnderwaterState(Player player)
        {
            if (player == null)
            {
                return;
            }

            try
            {
                if (PlayerUpdateIsUnderwaterMethod != null)
                {
                    PlayerUpdateIsUnderwaterMethod.Invoke(player, null);
                }

                if (player.playerController != null)
                {
                    player.playerController.ForceControllerSize();
                }
            }
            catch
            {
            }
        }

        private static void ForceGroundControllerSwimState(Player player, PlayerController controller, bool resetVelocity)
        {
            if (player == null || controller == null || controller.groundController == null || controller.underWaterController == null)
            {
                return;
            }

            if (PlayerControllerUnderWaterField != null)
            {
                PlayerControllerUnderWaterField.SetValue(controller, true);
            }

            if (PlayerControllerNextUnderWaterStateField != null)
            {
                PlayerControllerNextUnderWaterStateField.SetValue(controller, true);
            }

            controller.groundController.SetUnderWater(true);
            controller.underWaterController.SetUnderWater(true);
            controller.underWaterController.SetEnabled(false);
            controller.groundController.SetEnabled(controller.enabled);
            controller.activeController = controller.groundController;

            if (!resetVelocity)
            {
                return;
            }

            controller.velocity = Vector3.zero;
            controller.groundController.SetVelocity(Vector3.zero);
            controller.underWaterController.SetVelocity(Vector3.zero);

            Rigidbody playerRigidbody = player.GetComponent<Rigidbody>();
            if (playerRigidbody != null)
            {
                playerRigidbody.velocity = Vector3.zero;
                playerRigidbody.angularVelocity = Vector3.zero;
            }
        }

        private static bool IsSuperSeaglideStateActive(Player player, PlayerController controller)
        {
            return player != null &&
                   controller != null &&
                   player.IsUnderwaterForSwimming() &&
                   controller.activeController == controller.groundController;
        }

        private static bool TryGetHeldPoweredSeaglide(out Pickupable heldSeaglide)
        {
            heldSeaglide = null;
            if (Inventory.main == null)
            {
                return false;
            }

            Pickupable held = Inventory.main.GetHeld();
            if (held == null || held.gameObject == null || held.gameObject.GetComponent<Seaglide>() == null)
            {
                return false;
            }

            EnergyMixin energyMixin = held.gameObject.GetComponent<EnergyMixin>();
            if (energyMixin == null || energyMixin.IsDepleted())
            {
                return false;
            }

            heldSeaglide = held;
            return true;
        }

        private static void ApplyMotorModeForHeldTool(Player player, PlayerController controller, bool hasPoweredSeaglide)
        {
            if (player == null || controller == null)
            {
                return;
            }

            Player.MotorMode targetMode = hasPoweredSeaglide ? Player.MotorMode.Seaglide : Player.MotorMode.Dive;
            player.motorMode = targetMode;
            controller.SetMotorMode(targetMode);
        }

        private sealed class BatchBorderRenderer : MonoBehaviour
        {
            private const float OutlineThickness = 0.08f;
            private static Material _lineMaterial;

            public bool ShowThroughWalls { get; set; }
            public int BatchSpan { get; set; }

            private void OnRenderObject()
            {
                if (!enabled || Camera.current != MainCamera.camera)
                {
                    return;
                }

                LargeWorldStreamer streamer = LargeWorldStreamer.main;
                Player player = Player.main;
                if (streamer == null || player == null)
                {
                    return;
                }

                EnsureMaterial();
                if (_lineMaterial == null)
                {
                    return;
                }

                Int3 currentBatch = streamer.GetContainingBatch(player.transform.position);
                if (!streamer.CheckBatch(currentBatch))
                {
                    return;
                }

                _lineMaterial.SetPass(0);
                GL.PushMatrix();
                GL.Begin(GL.LINES);

                int span = Mathf.Max(1, BatchSpan);
                int negativeRadius = (span - 1) / 2;
                int positiveRadius = span - negativeRadius - 1;

                for (int x = -negativeRadius; x <= positiveRadius; x++)
                {
                    for (int y = -negativeRadius; y <= positiveRadius; y++)
                    {
                        for (int z = -negativeRadius; z <= positiveRadius; z++)
                        {
                            Int3 batch = new Int3(currentBatch.x + x, currentBatch.y + y, currentBatch.z + z);
                            if (!streamer.CheckBatch(batch))
                            {
                                continue;
                            }

                            bool isCurrent = batch == currentBatch;
                            Color color = isCurrent
                                ? new Color(1f, 0.34f, 0.14f, 1f)
                                : new Color(1f, 0.56f, 0.18f, 0.82f);
                            DrawBatchBox(streamer.GetBatchMins(batch), streamer.GetBatchMaxs(batch), color);
                        }
                    }
                }

                GL.End();
                GL.PopMatrix();
            }

            private static void EnsureMaterial()
            {
                if (_lineMaterial != null)
                {
                    return;
                }

                Shader shader = Shader.Find("Hidden/Internal-Colored");
                if (shader == null)
                {
                    return;
                }

                _lineMaterial = new Material(shader);
                _lineMaterial.hideFlags = HideFlags.HideAndDontSave;
                _lineMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                _lineMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                _lineMaterial.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
                _lineMaterial.SetInt("_ZWrite", 0);
            }

            private static void DrawBatchBox(Vector3 mins, Vector3 maxs, Color color)
            {
                Vector3[] offsets =
                {
                    Vector3.zero,
                    new Vector3(OutlineThickness, 0f, 0f),
                    new Vector3(-OutlineThickness, 0f, 0f),
                    new Vector3(0f, OutlineThickness, 0f),
                    new Vector3(0f, -OutlineThickness, 0f)
                };

                for (int i = 0; i < offsets.Length; i++)
                {
                    GL.Color(i == 0 ? color : new Color(color.r, color.g, color.b, color.a * 0.55f));
                    DrawBatchBoxPass(mins + offsets[i], maxs + offsets[i]);
                }
            }

            private static void DrawBatchBoxPass(Vector3 mins, Vector3 maxs)
            {
                Vector3 a = new Vector3(mins.x, mins.y, mins.z);
                Vector3 b = new Vector3(maxs.x, mins.y, mins.z);
                Vector3 c = new Vector3(maxs.x, mins.y, maxs.z);
                Vector3 d = new Vector3(mins.x, mins.y, maxs.z);
                Vector3 e = new Vector3(mins.x, maxs.y, mins.z);
                Vector3 f = new Vector3(maxs.x, maxs.y, mins.z);
                Vector3 g = new Vector3(maxs.x, maxs.y, maxs.z);
                Vector3 h = new Vector3(mins.x, maxs.y, maxs.z);

                DrawEdge(a, b);
                DrawEdge(b, c);
                DrawEdge(c, d);
                DrawEdge(d, a);
                DrawEdge(e, f);
                DrawEdge(f, g);
                DrawEdge(g, h);
                DrawEdge(h, e);
                DrawEdge(a, e);
                DrawEdge(b, f);
                DrawEdge(c, g);
                DrawEdge(d, h);
            }

            private static void DrawEdge(Vector3 start, Vector3 end)
            {
                GL.Vertex(start);
                GL.Vertex(end);
            }

            private void LateUpdate()
            {
                if (_lineMaterial != null)
                {
                    _lineMaterial.SetInt(
                        "_ZTest",
                        (int)(ShowThroughWalls
                            ? UnityEngine.Rendering.CompareFunction.Always
                            : UnityEngine.Rendering.CompareFunction.LessEqual));
                }
            }
        }
    }
}
