using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace SubnauticaSpeedrunningMod.Runtime.Practice
{
    internal static class ModPracticeSuperSeaglideRuntime
    {
        private static readonly MethodInfo PlayerUpdateIsUnderwaterMethod =
            typeof(Player).GetMethod("UpdateIsUnderwater", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        private static readonly FieldInfo PlayerControllerUnderWaterField =
            typeof(PlayerController).GetField("underWater", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly FieldInfo PlayerControllerNextUnderWaterStateField =
            typeof(PlayerController).GetField("nextUnderWaterState", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly MethodInfo PlayerControllerHandleUnderWaterStateMethod =
            typeof(PlayerController).GetMethod("HandleUnderWaterState", BindingFlags.Instance | BindingFlags.NonPublic);

        private const string HarmonyId = "SubnauticaSpeedrunningMod.Practice.SuperSeaglide";

        private static Harmony _harmony;
        private static bool _installAttempted;
        private static bool _available = true;
        private static string _lastPracticeSlot = string.Empty;
        private static bool _activationLogged;
        private static bool _waitingForUnderwaterLogged;
        private static bool _seaglideSpeedLogged;
        private static bool _activationApplied;
        private static float _nextApplyAttemptAt;
        private static float _activationAppliedAt;
        private static bool _restoredHybridStateLogged;
        private static bool _controllerHookActive;
        private static bool _handoffCompleted;

        private const float StabilizationWindowSeconds = 0.75f;

        public static bool EnsureInstalled()
        {
            if (_installAttempted)
            {
                return _available;
            }

            _installAttempted = true;

            if (PlayerUpdateIsUnderwaterMethod == null ||
                PlayerControllerUnderWaterField == null ||
                PlayerControllerNextUnderWaterStateField == null ||
                PlayerControllerHandleUnderWaterStateMethod == null)
            {
                _available = false;
                ModLog.Warn("Practice Super Seaglide runtime unavailable; required player state members could not be resolved.");
                return false;
            }

            try
            {
                _harmony = new Harmony(HarmonyId);
                MethodInfo postfix = typeof(ModPracticeSuperSeaglideRuntime).GetMethod(
                    nameof(HandleUnderWaterStatePostfix),
                    BindingFlags.Static | BindingFlags.NonPublic);
                _harmony.Patch(PlayerControllerHandleUnderWaterStateMethod, postfix: new HarmonyMethod(postfix));
            }
            catch (Exception ex)
            {
                _available = false;
                ModLog.Warn("Practice Super Seaglide runtime could not install controller hook: " + ex.Message);
                return false;
            }

            ModLog.Info("Practice Super Seaglide runtime is ready.");
            return true;
        }

        public static void Update(bool gameplayActive)
        {
            if (!EnsureInstalled())
            {
                return;
            }

            if (!ModClientSessionMode.PracticeSaveStartsWithSuperSeaglide)
            {
                ResetTransientState();
                return;
            }

            string currentSlot = Utils.GetSavegameDir() ?? string.Empty;
            if (!string.Equals(_lastPracticeSlot, currentSlot, StringComparison.Ordinal))
            {
                _lastPracticeSlot = currentSlot;
                _activationLogged = false;
                _waitingForUnderwaterLogged = false;
                _seaglideSpeedLogged = false;
                _activationApplied = false;
                _nextApplyAttemptAt = 0f;
                _activationAppliedAt = 0f;
                _restoredHybridStateLogged = false;
                _controllerHookActive = true;
                _handoffCompleted = false;
            }

            if (!gameplayActive || Player.main == null || Player.main.playerController == null)
            {
                return;
            }

            if (Time.unscaledTime < _nextApplyAttemptAt)
            {
                return;
            }

            _nextApplyAttemptAt = Time.unscaledTime + 0.1f;

            Player player = Player.main;
            PlayerController controller = player.playerController;

            if (_handoffCompleted)
            {
                return;
            }

            if (_activationApplied)
            {
                EnsureHybridState(player, controller, currentSlot);
                return;
            }

            TryRefreshUnderwaterState(player);

            if (!player.IsUnderwaterForSwimming())
            {
                if (!_waitingForUnderwaterLogged)
                {
                    _waitingForUnderwaterLogged = true;
                    ModLog.Info("Practice Super Seaglide is waiting for underwater swimming state on save '" + currentSlot + "'.");
                }

                return;
            }

            Pickupable heldSeaglide;
            bool hasPoweredSeaglide = TryGetHeldPoweredSeaglide(out heldSeaglide);
            ApplyMotorModeForHeldTool(player, controller, hasPoweredSeaglide);
            ForceGroundControllerSwimState(player, controller, false);

            bool active = IsSuperSeaglideStateActive(player, controller);
            if (active && !_activationLogged)
            {
                _activationApplied = true;
                _activationAppliedAt = Time.unscaledTime;
                _controllerHookActive = true;
                _activationLogged = true;
                ModLog.Info(
                    "Practice Super Seaglide forced for save '" +
                    currentSlot +
                    "'. motorMode='" +
                    player.motorMode +
                    "', heldSeaglide=" +
                    hasPoweredSeaglide +
                    ", playerPos=" +
                    player.transform.position +
                    ".");
            }
        }

        private static void ForceGroundControllerSwimState(Player player, PlayerController controller, bool resetVelocity)
        {
            PlayerMotor groundController = controller.groundController;
            PlayerMotor underWaterController = controller.underWaterController;
            if (groundController == null || underWaterController == null)
            {
                return;
            }

            PlayerControllerUnderWaterField.SetValue(controller, true);
            PlayerControllerNextUnderWaterStateField.SetValue(controller, true);

            groundController.SetUnderWater(newUnderWater: true);
            underWaterController.SetUnderWater(newUnderWater: true);

            underWaterController.SetEnabled(enabled: false);
            groundController.SetEnabled(enabled: controller.enabled);

            controller.activeController = groundController;
            if (resetVelocity)
            {
                controller.velocity = Vector3.zero;
                groundController.SetVelocity(Vector3.zero);
                underWaterController.SetVelocity(Vector3.zero);

                Rigidbody playerRigidbody = player.GetComponent<Rigidbody>();
                if (playerRigidbody != null)
                {
                    playerRigidbody.velocity = Vector3.zero;
                    playerRigidbody.angularVelocity = Vector3.zero;
                }
            }
        }

        private static bool IsSuperSeaglideStateActive(Player player, PlayerController controller)
        {
            return player != null &&
                   controller != null &&
                   !player.IsInSub() &&
                   player.IsUnderwaterForSwimming() &&
                   controller.activeController == controller.groundController;
        }

        private static void TryRefreshUnderwaterState(Player player)
        {
            if (player == null)
            {
                return;
            }

            try
            {
                PlayerUpdateIsUnderwaterMethod.Invoke(player, null);
                player.playerController.ForceControllerSize();
            }
            catch (Exception ex)
            {
                ModLog.Warn("Practice Super Seaglide could not refresh underwater state: " + ex.Message);
            }
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

        private static void UpdateMotorModeForHeldTool(Player player, PlayerController controller, string currentSlot)
        {
            Pickupable heldSeaglide;
            bool hasPoweredSeaglide = TryGetHeldPoweredSeaglide(out heldSeaglide);
            Player.MotorMode targetMode = hasPoweredSeaglide ? Player.MotorMode.Seaglide : Player.MotorMode.Dive;
            if (player.motorMode != targetMode)
            {
                ApplyMotorModeForHeldTool(player, controller, hasPoweredSeaglide);
                if (hasPoweredSeaglide)
                {
                    ModLog.Info("Practice Super Seaglide speed layer enabled for save '" + currentSlot + "'.");
                    _seaglideSpeedLogged = true;
                }
                else if (_seaglideSpeedLogged)
                {
                    ModLog.Info("Practice Super Seaglide speed layer disabled for save '" + currentSlot + "'.");
                    _seaglideSpeedLogged = false;
                }
            }
        }

        private static void EnsureHybridState(Player player, PlayerController controller, string currentSlot)
        {
            if (player == null || controller == null)
            {
                return;
            }

            bool shouldReinforce = Time.unscaledTime - _activationAppliedAt <= StabilizationWindowSeconds;
            if (!shouldReinforce)
            {
                _controllerHookActive = false;
                _activationApplied = false;
                _handoffCompleted = true;
                ModLog.Info("Practice Super Seaglide runtime handed control back to vanilla for save '" + currentSlot + "'.");
                return;
            }

            TryRefreshUnderwaterState(player);
            ForceGroundControllerSwimState(player, controller, false);

            if (!_restoredHybridStateLogged && IsSuperSeaglideStateActive(player, controller))
            {
                _restoredHybridStateLogged = true;
                ModLog.Info("Practice Super Seaglide hybrid state reinforced for save '" + currentSlot + "'.");
            }
        }

        private static void HandleUnderWaterStatePostfix(PlayerController __instance)
        {
            if (__instance == null ||
                !ModClientSessionMode.PracticeSaveStartsWithSuperSeaglide ||
                !_controllerHookActive)
            {
                return;
            }

            Player player = Player.main;
            if (player == null ||
                player.playerController != __instance ||
                !player.IsUnderwaterForSwimming() ||
                player.GetMode() != Player.Mode.Normal)
            {
                return;
            }

            ForceGroundControllerSwimState(player, __instance, false);
        }

        private static void ApplyMotorModeForHeldTool(Player player, PlayerController controller, bool hasPoweredSeaglide)
        {
            Player.MotorMode targetMode = hasPoweredSeaglide ? Player.MotorMode.Seaglide : Player.MotorMode.Dive;
            player.motorMode = targetMode;
            controller.SetMotorMode(targetMode);
        }

        private static void ResetTransientState()
        {
            _lastPracticeSlot = string.Empty;
            _activationLogged = false;
            _waitingForUnderwaterLogged = false;
            _seaglideSpeedLogged = false;
            _activationApplied = false;
            _nextApplyAttemptAt = 0f;
            _activationAppliedAt = 0f;
            _restoredHybridStateLogged = false;
            _controllerHookActive = false;
            _handoffCompleted = false;
        }
    }
}
