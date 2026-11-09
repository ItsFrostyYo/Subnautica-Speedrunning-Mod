using System;

namespace SubnauticaSpeedrunningMod.Runtime.Seeds.Ruleset8
{
    // Shared by the runtime and offline checker. Keep the PRNG/hash domains unchanged.
    internal static class GenerationRules
    {
        internal const int CurrentVersion = 8;
        internal const string NickelClassId = "7815b1b7-2830-418b-9b5d-19949b0ae9ec";

        internal static float SpawnWeight(string classId, float original, int ruleset)
        {
            return ruleset >= 8 && string.Equals(classId, NickelClassId, StringComparison.Ordinal)
                ? original * 2f : original;
        }
    }
}
