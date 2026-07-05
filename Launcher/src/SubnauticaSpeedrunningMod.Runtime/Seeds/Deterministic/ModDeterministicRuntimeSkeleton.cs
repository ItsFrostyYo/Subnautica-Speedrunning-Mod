using System;
using System.Collections.Generic;
using UnityEngine;

namespace SubnauticaSpeedrunningMod.Runtime.Seeds
{
    internal sealed class ModDeterministicSlotResolutionScope
    {
        public string SourceKind { get; set; }

        public string PoolHint { get; set; }

        public string BiomeName { get; set; }

        public string SlotTypeName { get; set; }

        public int BatchX { get; set; }

        public int BatchY { get; set; }

        public int BatchZ { get; set; }

        public int CellX { get; set; }

        public int CellY { get; set; }

        public int CellZ { get; set; }

        public int CellLevel { get; set; }

        public int PlaceholderSlotIndex { get; set; }

        public Vector3 WorldPosition { get; set; }

        public Vector3 LocalPosition { get; set; }

        public float Density { get; set; }

        public string BuildStableSlotKey()
        {
            return
                (SourceKind ?? "Unknown") + "|" +
                (BiomeName ?? "UnknownBiome") + "|" +
                (SlotTypeName ?? "UnknownSlotType") + "|" +
                BatchX + ":" + BatchY + ":" + BatchZ + "|" +
                CellX + ":" + CellY + ":" + CellZ + ":" + CellLevel + "|" +
                PlaceholderSlotIndex + "|" +
                WorldPosition.x.ToString("0.###") + "," +
                WorldPosition.y.ToString("0.###") + "," +
                WorldPosition.z.ToString("0.###");
        }
    }

    internal static class ModDeterministicSeedRuntimeContext
    {
        [ThreadStatic]
        private static ModDeterministicSlotResolutionScope _current;

        public static void Begin(ModDeterministicSlotResolutionScope scope)
        {
            _current = scope;
        }

        public static void End()
        {
            _current = null;
        }

        public static bool TryGetCurrent(out ModDeterministicSlotResolutionScope scope)
        {
            scope = _current;
            return scope != null;
        }
    }

    internal sealed class ModDeterministicSeedPlan
    {
        public ModDeterministicSeedPlan(ModDeterministicSlotSurveyCatalog survey, ModDeterministicPoolRuleSet ruleSet)
        {
            Survey = survey;
            RuleSet = ruleSet;
        }

        public ModDeterministicSlotSurveyCatalog Survey { get; private set; }

        public ModDeterministicPoolRuleSet RuleSet { get; private set; }
    }

    internal static class ModDeterministicSeedResolver
    {
        public static ModDeterministicSeedPlan CreatePlan(
            ModDeterministicSlotSurveyCatalog survey,
            ModDeterministicPoolRuleSet ruleSet)
        {
            if (survey != null)
            {
                survey.Normalize();
            }

            if (ruleSet != null)
            {
                ruleSet.Normalize();
            }

            return new ModDeterministicSeedPlan(survey, ruleSet);
        }

        public static ModDeterministicSeedManifest ResolveManifest(
            ModDeterministicSeedPlan plan,
            string seedId,
            string seedValue)
        {
            ModDeterministicSeedManifest manifest = new ModDeterministicSeedManifest
            {
                ManifestId = (seedId ?? "seed") + "-manifest",
                SeedId = seedId,
                SeedValue = seedValue,
                RuleSetId = plan != null && plan.RuleSet != null ? plan.RuleSet.RuleSetId : string.Empty,
                SurveyId = plan != null && plan.Survey != null ? plan.Survey.SurveyId : string.Empty,
                Entries = new List<ModDeterministicSeedManifestEntry>()
            };

            manifest.Normalize();
            return manifest;
        }

        public static bool TryResolveExactEntry(
            ModDeterministicSeedManifest manifest,
            string stableSlotKey,
            out ModDeterministicSeedManifestEntry entry)
        {
            entry = null;
            if (manifest == null || manifest.Entries == null || string.IsNullOrEmpty(stableSlotKey))
            {
                return false;
            }

            for (int i = 0; i < manifest.Entries.Count; i++)
            {
                ModDeterministicSeedManifestEntry current = manifest.Entries[i];
                if (current == null)
                {
                    continue;
                }

                if (string.Equals(current.StableSlotKey, stableSlotKey, StringComparison.OrdinalIgnoreCase))
                {
                    entry = current;
                    return true;
                }
            }

            return false;
        }
    }
}
