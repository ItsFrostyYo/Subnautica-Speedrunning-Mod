using System;
using System.Collections.Generic;
using UnityEngine;

namespace SubnauticaSpeedrunningRanked.Runtime.Seeds
{
    internal static class RankedForceSecondGoldRuntime
    {
        private const int SandstoneWindowSize = 6;
        private const int RequiredGolds = 2;
        private const float PendingBreakResolveDelaySeconds = 0.15f;
        private const float PendingBreakResolveTimeoutSeconds = 3f;
        private const float PendingBreakPickupRadius = 4f;
        private const float ArmedBreakWindowSeconds = 1f;
        private const float TargetSweepIntervalSeconds = 0.15f;
        private const float PendingBreakSweepIntervalSeconds = 0.15f;

        private static readonly List<PendingSandstoneBreak> PendingBreaks = new List<PendingSandstoneBreak>();
        private static readonly HashSet<int> ProcessedPickupIds = new HashSet<int>();

        private static string _activeSlotPath = string.Empty;
        private static bool _activeSlotEligible;
        private static int _sandstoneBrokenThisRun;
        private static int _goldSeenThisRun;
        private static int _nextObservationId = 1;
        private static float _nextTargetSweepAt;
        private static float _nextPendingBreakSweepAt;
        private static RankedSandstoneResourceObserver _activeForcedObserver;

        public static bool EnsureInstalled()
        {
            return false;
        }

        public static void UpdateSessionState(string saveSlot, bool isEligible)
        {
            string normalizedSlotPath = NormalizeSlot(saveSlot);
            if (string.IsNullOrEmpty(normalizedSlotPath))
            {
                ResetRunState(string.Empty);
                return;
            }

            if (!string.Equals(normalizedSlotPath, _activeSlotPath, StringComparison.OrdinalIgnoreCase))
            {
                ResetRunState(normalizedSlotPath);
            }

            if (!_activeSlotEligible && isEligible)
            {
                ResetCounters(normalizedSlotPath);
                RankedLog.Info("Force 2nd Gold armed for fresh run slot '" + normalizedSlotPath + "'.");
            }

            _activeSlotEligible = isEligible;
        }

        public static void Update()
        {
            if (!ShouldApplyRuntime())
            {
                RestoreForcedObserver(null);
                return;
            }

            if (Time.unscaledTime >= _nextTargetSweepAt)
            {
                _nextTargetSweepAt = Time.unscaledTime + TargetSweepIntervalSeconds;
                UpdateTargetedSandstone();
            }

            if (PendingBreaks.Count > 0 && Time.unscaledTime >= _nextPendingBreakSweepAt)
            {
                _nextPendingBreakSweepAt = Time.unscaledTime + PendingBreakSweepIntervalSeconds;
                ResolvePendingBreaks();
            }

            if (_sandstoneBrokenThisRun >= SandstoneWindowSize || _goldSeenThisRun >= RequiredGolds)
            {
                RestoreForcedObserver(null);
            }
        }

        public static void Reset()
        {
            ResetRunState(string.Empty);
        }

        private static void UpdateTargetedSandstone()
        {
            GUIHand hand = Player.main != null ? Player.main.GetComponent<GUIHand>() : null;
            GameObject activeTarget = hand != null ? hand.GetActiveTarget() : null;
            BreakableResource sandstone = FindSandstoneBreakable(activeTarget);
            if (sandstone == null)
            {
                if (!ShouldForceNextSandstoneToGold())
                {
                    RestoreForcedObserver(null);
                }

                return;
            }

            RankedSandstoneResourceObserver observer = sandstone.GetComponent<RankedSandstoneResourceObserver>();
            if (observer == null)
            {
                observer = sandstone.gameObject.AddComponent<RankedSandstoneResourceObserver>();
            }

            observer.Bind(sandstone);
            bool mustForceGold = ShouldForceNextSandstoneToGold();
            observer.ArmForPotentialBreak(NextObservationId(), mustForceGold);
            if (mustForceGold)
            {
                observer.ApplyForcedGold();
                RestoreForcedObserver(observer);
            }
            else
            {
                observer.RestoreOriginalState();
                if (ReferenceEquals(_activeForcedObserver, observer))
                {
                    _activeForcedObserver = null;
                }
            }
        }

        private static void ResolvePendingBreaks()
        {
            if (PendingBreaks.Count == 0)
            {
                return;
            }

            Pickupable[] pickupables = UnityEngine.Object.FindObjectsOfType<Pickupable>();
            float now = Time.unscaledTime;
            for (int i = PendingBreaks.Count - 1; i >= 0; i--)
            {
                PendingSandstoneBreak pendingBreak = PendingBreaks[i];
                if (now < pendingBreak.ResolveAt)
                {
                    continue;
                }

                Pickupable matchedPickup = FindMatchingPickup(pendingBreak.Position, pickupables);
                if (matchedPickup != null)
                {
                    ProcessedPickupIds.Add(matchedPickup.gameObject.GetInstanceID());
                    RecordSandstoneOutcome(CraftData.GetTechType(matchedPickup.gameObject), pendingBreak.WasForced);
                    PendingBreaks.RemoveAt(i);
                    continue;
                }

                if (now >= pendingBreak.ExpiresAt)
                {
                    RankedLog.Warn(
                        "Unable to resolve sandstone break outcome near " +
                        FormatVector3(pendingBreak.Position) +
                        " before timeout. forced=" +
                        pendingBreak.WasForced +
                        ".");
                    PendingBreaks.RemoveAt(i);
                }
            }
        }

        private static Pickupable FindMatchingPickup(Vector3 position, Pickupable[] pickupables)
        {
            if (pickupables == null || pickupables.Length == 0)
            {
                return null;
            }

            float bestDistanceSquared = PendingBreakPickupRadius * PendingBreakPickupRadius;
            Pickupable bestPickup = null;

            for (int i = 0; i < pickupables.Length; i++)
            {
                Pickupable pickupable = pickupables[i];
                if (pickupable == null || pickupable.gameObject == null)
                {
                    continue;
                }

                int instanceId = pickupable.gameObject.GetInstanceID();
                if (ProcessedPickupIds.Contains(instanceId))
                {
                    continue;
                }

                TechType techType = CraftData.GetTechType(pickupable.gameObject);
                if (techType != TechType.Gold &&
                    techType != TechType.Silver &&
                    techType != TechType.Lead)
                {
                    continue;
                }

                float distanceSquared = (pickupable.transform.position - position).sqrMagnitude;
                if (distanceSquared > bestDistanceSquared)
                {
                    continue;
                }

                bestDistanceSquared = distanceSquared;
                bestPickup = pickupable;
            }

            return bestPickup;
        }

        private static void RecordSandstoneOutcome(TechType techType, bool wasForced)
        {
            if (_sandstoneBrokenThisRun >= SandstoneWindowSize)
            {
                return;
            }

            _sandstoneBrokenThisRun++;
            if (techType == TechType.Gold)
            {
                _goldSeenThisRun++;
            }

            RankedLog.Info(
                "Observed sandstone break " +
                _sandstoneBrokenThisRun +
                " of " +
                SandstoneWindowSize +
                ": result=" +
                techType +
                ", goldsSeen=" +
                _goldSeenThisRun +
                ", forced=" +
                wasForced +
                ".");
        }

        private static bool ShouldForceNextSandstoneToGold()
        {
            if (_sandstoneBrokenThisRun >= SandstoneWindowSize || _goldSeenThisRun >= RequiredGolds)
            {
                return false;
            }

            int nextBreakIndex = _sandstoneBrokenThisRun + 1;
            return _goldSeenThisRun + (SandstoneWindowSize - nextBreakIndex) < RequiredGolds;
        }

        private static BreakableResource FindSandstoneBreakable(GameObject target)
        {
            if (target == null)
            {
                return null;
            }

            BreakableResource direct = target.GetComponent<BreakableResource>();
            if (IsSandstoneBreakable(direct))
            {
                return direct;
            }

            BreakableResource ancestor = Utils.FindAncestorWithComponent<BreakableResource>(target);
            return IsSandstoneBreakable(ancestor) ? ancestor : null;
        }

        private static bool IsSandstoneBreakable(BreakableResource breakable)
        {
            if (breakable == null || breakable.defaultPrefab == null || breakable.prefabList == null || breakable.prefabList.Count == 0)
            {
                return false;
            }

            if (CraftData.GetTechType(breakable.defaultPrefab) != TechType.Lead)
            {
                return false;
            }

            bool foundGold = false;
            bool foundSilver = false;
            for (int i = 0; i < breakable.prefabList.Count; i++)
            {
                BreakableResource.RandomPrefab choice = breakable.prefabList[i];
                if (choice == null || choice.prefab == null)
                {
                    continue;
                }

                TechType techType = CraftData.GetTechType(choice.prefab);
                if (techType == TechType.Gold)
                {
                    foundGold = true;
                }
                else if (techType == TechType.Silver)
                {
                    foundSilver = true;
                }
            }

            return foundGold && foundSilver;
        }

        internal static void ReportPotentialSandstoneBreak(Vector3 position, int observationId, bool wasForced)
        {
            for (int i = 0; i < PendingBreaks.Count; i++)
            {
                if (PendingBreaks[i].ObservationId == observationId)
                {
                    return;
                }
            }

            PendingBreaks.Add(new PendingSandstoneBreak
            {
                ObservationId = observationId,
                Position = position,
                WasForced = wasForced,
                ResolveAt = Time.unscaledTime + PendingBreakResolveDelaySeconds,
                ExpiresAt = Time.unscaledTime + PendingBreakResolveTimeoutSeconds
            });
        }

        private static int NextObservationId()
        {
            return _nextObservationId++;
        }

        private static void RestoreForcedObserver(RankedSandstoneResourceObserver allowedObserver)
        {
            if (_activeForcedObserver != null && !ReferenceEquals(_activeForcedObserver, allowedObserver))
            {
                _activeForcedObserver.RestoreOriginalState();
            }

            _activeForcedObserver = allowedObserver;
        }

        internal static void NotifyObserverDestroyed(RankedSandstoneResourceObserver observer)
        {
            if (ReferenceEquals(_activeForcedObserver, observer))
            {
                _activeForcedObserver = null;
            }
        }

        private static bool ShouldApplyRuntime()
        {
            RankedSeedRuntimeProfile profile = RankedSeedRuntimeHost.GetProfile();
            return _activeSlotEligible &&
                   profile != null &&
                   profile.ForceSecondGold &&
                   RankedSeedRuntimeHost.IsSurvivalLikeMode() &&
                   Player.main != null;
        }

        private static void ResetRunState(string slotPath)
        {
            _activeSlotPath = NormalizeSlot(slotPath);
            _activeSlotEligible = false;
            _sandstoneBrokenThisRun = 0;
            _goldSeenThisRun = 0;
            _nextObservationId = 1;
            _nextTargetSweepAt = 0f;
            _nextPendingBreakSweepAt = 0f;
            PendingBreaks.Clear();
            ProcessedPickupIds.Clear();
            RestoreForcedObserver(null);
        }

        private static void ResetCounters(string slotPath)
        {
            _activeSlotPath = NormalizeSlot(slotPath);
            _sandstoneBrokenThisRun = 0;
            _goldSeenThisRun = 0;
            _nextObservationId = 1;
            PendingBreaks.Clear();
            ProcessedPickupIds.Clear();
            RestoreForcedObserver(null);
        }

        private static string NormalizeSlot(string saveSlot)
        {
            if (string.IsNullOrEmpty(saveSlot))
            {
                return string.Empty;
            }

            try
            {
                return System.IO.Path.GetFullPath(saveSlot).TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar);
            }
            catch
            {
                return saveSlot.Trim();
            }
        }

        private static string FormatVector3(Vector3 position)
        {
            return position.x.ToString("0.###") + "," + position.y.ToString("0.###") + "," + position.z.ToString("0.###");
        }

        private struct PendingSandstoneBreak
        {
            public int ObservationId;
            public Vector3 Position;
            public bool WasForced;
            public float ResolveAt;
            public float ExpiresAt;
        }

        internal sealed class RankedSandstoneResourceObserver : MonoBehaviour
        {
            private BreakableResource _breakable;
            private GameObject _originalDefaultPrefab;
            private float[] _originalChances;
            private bool _initialized;
            private bool _armed;
            private bool _forceApplied;
            private int _observationId;
            private float _lastArmedAt;

            public void Bind(BreakableResource breakable)
            {
                if (_initialized || breakable == null)
                {
                    return;
                }

                _breakable = breakable;
                _originalDefaultPrefab = breakable.defaultPrefab;
                _originalChances = new float[breakable.prefabList.Count];
                for (int i = 0; i < breakable.prefabList.Count; i++)
                {
                    _originalChances[i] = breakable.prefabList[i] != null ? breakable.prefabList[i].chance : 0f;
                }

                _initialized = true;
            }

            public void ArmForPotentialBreak(int observationId, bool forceGold)
            {
                if (!_initialized)
                {
                    return;
                }

                _armed = true;
                _observationId = observationId;
                _lastArmedAt = Time.unscaledTime;
                if (forceGold)
                {
                    ApplyForcedGold();
                }
                else
                {
                    RestoreOriginalState();
                }
            }

            public void ApplyForcedGold()
            {
                if (!_initialized || _forceApplied || _breakable == null || _breakable.prefabList == null || _breakable.prefabList.Count == 0)
                {
                    return;
                }

                GameObject goldPrefab = null;
                for (int i = 0; i < _breakable.prefabList.Count; i++)
                {
                    BreakableResource.RandomPrefab choice = _breakable.prefabList[i];
                    if (choice == null || choice.prefab == null)
                    {
                        continue;
                    }

                    if (CraftData.GetTechType(choice.prefab) == TechType.Gold)
                    {
                        goldPrefab = choice.prefab;
                        break;
                    }
                }

                if (goldPrefab == null)
                {
                    return;
                }

                _breakable.defaultPrefab = goldPrefab;
                for (int i = 0; i < _breakable.prefabList.Count; i++)
                {
                    BreakableResource.RandomPrefab choice = _breakable.prefabList[i];
                    if (choice == null)
                    {
                        continue;
                    }

                    choice.chance = choice.prefab == goldPrefab ? 1f : 0f;
                }

                _forceApplied = true;
            }

            public void RestoreOriginalState()
            {
                if (!_initialized || !_forceApplied || _breakable == null || _breakable.prefabList == null)
                {
                    return;
                }

                _breakable.defaultPrefab = _originalDefaultPrefab;
                int count = Mathf.Min(_breakable.prefabList.Count, _originalChances != null ? _originalChances.Length : 0);
                for (int i = 0; i < count; i++)
                {
                    BreakableResource.RandomPrefab choice = _breakable.prefabList[i];
                    if (choice == null)
                    {
                        continue;
                    }

                    choice.chance = _originalChances[i];
                }

                _forceApplied = false;
            }

            private void OnDisable()
            {
                if (_armed && Time.unscaledTime - _lastArmedAt <= ArmedBreakWindowSeconds)
                {
                    RankedForceSecondGoldRuntime.ReportPotentialSandstoneBreak(transform.position, _observationId, _forceApplied);
                }

                RestoreOriginalState();
                _armed = false;
                _observationId = 0;
            }

            private void OnDestroy()
            {
                RankedForceSecondGoldRuntime.NotifyObserverDestroyed(this);
            }
        }
    }
}
