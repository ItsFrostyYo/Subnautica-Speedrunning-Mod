using UnityEngine;

namespace SubnauticaSpeedrunningMod.Runtime.Seeds.Ruleset7
{
    internal sealed class UnityRandomScopeState
    {
        internal bool Active;
        internal UnityEngine.Random.State Previous;
        internal ulong Key;
    }

    internal static class UnityRandomScope
    {
        internal static UnityRandomScopeState Begin(ulong seed)
        {
            UnityRandomScopeState state = new UnityRandomScopeState();
            if (!SeedRuntime.Active)
            {
                return state;
            }

            state.Active = true;
            state.Key = seed;
            state.Previous = UnityEngine.Random.state;
            UnityEngine.Random.InitState(DeterministicHash.ToUnitySeed(seed));
            return state;
        }

        internal static void End(UnityRandomScopeState state)
        {
            if (state != null && state.Active)
            {
                UnityEngine.Random.state = state.Previous;
            }
        }
    }

    internal struct SlotContextData
    {
        internal ulong Key;
    }

    internal static class SlotContext
    {
        private static bool hasCurrent;
        private static SlotContextData current;

        internal static SlotContextData Push(EntitySlotsPlaceholder owner, EntitySlotData slot, int index)
        {
            ulong hash = SeedRuntime.DeriveSlot(owner.transform.TransformPoint(slot.localPosition),
                (int)slot.biomeType, (int)slot.allowedTypes, slot.density);
            current = new SlotContextData { Key = hash };
            hasCurrent = true;
            return current;
        }

        internal static bool TryGet(out SlotContextData value)
        {
            value = current;
            return hasCurrent;
        }

        internal static void Clear()
        {
            hasCurrent = false;
            current = new SlotContextData();
        }
    }
}

