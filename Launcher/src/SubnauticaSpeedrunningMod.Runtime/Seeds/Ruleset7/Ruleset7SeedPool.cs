using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace SubnauticaSpeedrunningMod.Runtime.Seeds.Ruleset7
{
    internal static class Ruleset7SeedPool
    {
        internal const int RulesetVersion = 7;
        private const string FileName = "1000SurvivalGlitchedAny%.csv";
        private static readonly object Sync = new object();
        private static readonly Random Random = new Random();
        private static string[] _seeds;

        internal static string Choose()
        {
            string[] seeds = Load();
            lock (Sync)
            {
                return seeds[Random.Next(seeds.Length)];
            }
        }

        internal static string[] Load()
        {
            if (_seeds != null)
            {
                return _seeds;
            }

            string path = Path.Combine(Path.Combine(PathLayout.GetModRoot(), "Seeds"), FileName);
            if (!File.Exists(path))
            {
                throw new FileNotFoundException("Ruleset 7 race seed pool is missing.", path);
            }

            List<string> values = new List<string>();
            HashSet<string> unique = new HashSet<string>(StringComparer.Ordinal);
            string[] lines = File.ReadAllLines(path);
            for (int i = 0; i < lines.Length; i++)
            {
                string value = (lines[i] ?? string.Empty).Trim().TrimStart('\ufeff');
                long numeric;
                if (value.Length == 0 || !long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out numeric))
                {
                    continue;
                }

                value = numeric.ToString(CultureInfo.InvariantCulture);
                if (unique.Add(value))
                {
                    values.Add(value);
                }
            }

            if (values.Count == 0)
            {
                // Subnautica's September 2018 Unity/Mono profile cannot resolve
                // System.IO.InvalidDataException even though the desktop build SDK can.
                // IOException is available in both runtimes and preserves the intended
                // failure category without preventing this type from loading at all.
                throw new IOException("Ruleset 7 race seed pool contains no numeric seeds: " + path);
            }

            _seeds = values.ToArray();
            ModLog.Info("Loaded " + _seeds.Length + " unique Ruleset 7 Survival race seeds from '" + path + "'.");
            return _seeds;
        }
    }
}
