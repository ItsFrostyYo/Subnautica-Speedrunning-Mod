using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace SubnauticaSpeedrunningRanked.Runtime.Seeds
{
    internal sealed class RankedSeedRollContext
    {
        private readonly string _rootKey;

        public RankedSeedRollContext(RankedSeedDefinition definition)
        {
            string seedId = definition != null && !string.IsNullOrEmpty(definition.SeedId) ? definition.SeedId : "ranked-seed";
            string seedValue = definition != null && !string.IsNullOrEmpty(definition.SeedValue) ? definition.SeedValue : seedId + "-default";
            RankedSeedDefinition.NormalizeDeterministicAlias(ref seedId, ref seedValue);
            _rootKey = seedId + "|" + seedValue;
        }

        public float NextFloat(string scope, float minValue, float maxValue)
        {
            if (minValue > maxValue)
            {
                float swap = minValue;
                minValue = maxValue;
                maxValue = swap;
            }

            if (Math.Abs(maxValue - minValue) <= 0.0001f)
            {
                return minValue;
            }

            double unit = RollUnit(scope);
            return (float)(minValue + (maxValue - minValue) * unit);
        }

        public float NextSteppedFloat(string scope, float minValue, float maxValue, float step)
        {
            if (minValue > maxValue)
            {
                float swap = minValue;
                minValue = maxValue;
                maxValue = swap;
            }

            if (step <= 0f)
            {
                step = 0.001f;
            }

            if (Math.Abs(maxValue - minValue) <= 0.0001f)
            {
                return RoundToStep(minValue, step);
            }

            int bucketCount = Math.Max(1, (int)Math.Round((maxValue - minValue) / step, MidpointRounding.AwayFromZero));
            int bucketIndex = Math.Min(bucketCount, (int)Math.Floor(RollUnit(scope) * (bucketCount + 1)));
            return RoundToStep(minValue + bucketIndex * step, step);
        }

        public int NextSteppedInt(string scope, int minValue, int maxValue, int step)
        {
            if (minValue > maxValue)
            {
                int swap = minValue;
                minValue = maxValue;
                maxValue = swap;
            }

            if (step <= 0)
            {
                step = 1;
            }

            if (maxValue == minValue)
            {
                return minValue;
            }

            int bucketCount = Math.Max(1, (int)Math.Round((maxValue - minValue) / (double)step, MidpointRounding.AwayFromZero));
            int bucketIndex = Math.Min(bucketCount, (int)Math.Floor(RollUnit(scope) * (bucketCount + 1)));
            return minValue + bucketIndex * step;
        }

        public T SelectWeightedRange<T>(string scope, IList<T> ranges, Func<T, float> weightSelector) where T : class
        {
            if (ranges == null || ranges.Count == 0 || weightSelector == null)
            {
                return null;
            }

            float totalWeight = 0f;
            for (int i = 0; i < ranges.Count; i++)
            {
                T range = ranges[i];
                if (range == null)
                {
                    continue;
                }

                float weight = Math.Max(0f, weightSelector(range));
                totalWeight += weight;
            }

            if (totalWeight <= 0f)
            {
                return ranges[0];
            }

            float roll = NextFloat(scope, 0f, totalWeight);
            float cursor = 0f;
            for (int i = 0; i < ranges.Count; i++)
            {
                T range = ranges[i];
                if (range == null)
                {
                    continue;
                }

                cursor += Math.Max(0f, weightSelector(range));
                if (roll <= cursor)
                {
                    return range;
                }
            }

            return ranges[ranges.Count - 1];
        }

        private double RollUnit(string scope)
        {
            ulong hash = ComputeDeterministicHash(_rootKey + "|" + (scope ?? string.Empty));
            return (hash & 0xFFFFFFFFFFFFUL) / (double)0x1000000000000UL;
        }

        private static float RoundToStep(float value, float step)
        {
            if (step <= 0f)
            {
                return value;
            }

            decimal decimalStep = (decimal)step;
            decimal buckets = Math.Round((decimal)value / decimalStep, MidpointRounding.AwayFromZero);
            return (float)(buckets * decimalStep);
        }

        private static ulong ComputeDeterministicHash(string value)
        {
            unchecked
            {
                const ulong offset = 14695981039346656037UL;
                const ulong prime = 1099511628211UL;
                ulong hash = offset;
                string normalized = string.IsNullOrEmpty(value) ? "seed" : value;
                for (int i = 0; i < normalized.Length; i++)
                {
                    hash ^= normalized[i];
                    hash *= prime;
                }

                return hash;
            }
        }
    }
}
