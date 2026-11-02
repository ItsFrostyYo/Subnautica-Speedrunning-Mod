using System;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace SubnauticaSpeedrunningMod.Runtime.Seeds.Ruleset7
{
    internal static class DeterministicHash
    {
        private const ulong Offset = 14695981039346656037UL;
        private const ulong Prime = 1099511628211UL;

        internal static ulong SeedFromText(string text)
        {
            if (text == null)
            {
                text = string.Empty;
            }

            long signed;
            if (long.TryParse(text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out signed))
            {
                return unchecked((ulong)signed);
            }

            return Add(Offset, text);
        }

        internal static ulong Begin(ulong worldSeed, string domain)
        {
            return Add(Add(Offset, worldSeed), domain);
        }

        internal static ulong Add(ulong hash, string value)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
            for (int i = 0; i < bytes.Length; i++)
            {
                hash ^= bytes[i];
                hash *= Prime;
            }

            hash ^= 0xff;
            hash *= Prime;
            return hash;
        }

        internal static ulong Add(ulong hash, int value)
        {
            unchecked
            {
                hash ^= (byte)value;
                hash *= Prime;
                hash ^= (byte)(value >> 8);
                hash *= Prime;
                hash ^= (byte)(value >> 16);
                hash *= Prime;
                hash ^= (byte)(value >> 24);
                hash *= Prime;
            }

            return hash;
        }

        internal static ulong Add(ulong hash, ulong value)
        {
            hash = Add(hash, unchecked((int)value));
            return Add(hash, unchecked((int)(value >> 32)));
        }

        internal static ulong Add(ulong hash, float value)
        {
            return Add(hash, Quantize(value));
        }

        internal static ulong Add(ulong hash, Vector3 value)
        {
            hash = Add(hash, value.x);
            hash = Add(hash, value.y);
            return Add(hash, value.z);
        }

        internal static ulong Add(ulong hash, Quaternion value)
        {
            hash = Add(hash, value.x);
            hash = Add(hash, value.y);
            hash = Add(hash, value.z);
            return Add(hash, value.w);
        }

        internal static int ToUnitySeed(ulong hash)
        {
            ulong mixed = Mix(hash);
            int result = unchecked((int)(mixed ^ (mixed >> 32)));
            return result == 0 ? unchecked((int)0x6d2b79f5) : result;
        }

        internal static ulong Mix(ulong value)
        {
            unchecked
            {
                value += 0x9E3779B97F4A7C15UL;
                value = (value ^ (value >> 30)) * 0xBF58476D1CE4E5B9UL;
                value = (value ^ (value >> 27)) * 0x94D049BB133111EBUL;
                return value ^ (value >> 31);
            }
        }

        internal static float NextUnitFloat(ref ulong state)
        {
            state = Mix(state);
            uint value = (uint)(state >> 40);
            return value / 16777216f;
        }

        private static int Quantize(float value)
        {
            if (float.IsNaN(value))
            {
                return int.MinValue;
            }

            if (float.IsPositiveInfinity(value))
            {
                return int.MaxValue;
            }

            if (float.IsNegativeInfinity(value))
            {
                return int.MinValue + 1;
            }

            return (int)Math.Round(value * 1000.0, MidpointRounding.AwayFromZero);
        }
    }
}

