using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace SubnauticaSpeedrunningMod.Runtime.Seeds.Ruleset8
{
    internal static class SeedRuntime
    {
        internal const string LegacyMetadataFileName = "subnautica-set-seed.txt";
        internal const string MetadataPattern = "seed-*-R-*.tsv";
        internal const int RulesetVersion = GenerationRules.CurrentVersion;
        internal const string ExpectedAssemblyHash = "1667ec5ef4475659fad2487bfd67bff1f1560721304b1605629898870849abb7";

        private static readonly Dictionary<string, int> EventCounters = new Dictionary<string, int>(StringComparer.Ordinal);
        private static readonly Dictionary<int, CreatureOriginRecord> CreatureOrigins = new Dictionary<int, CreatureOriginRecord>();
        private static readonly Dictionary<int, CreatureAIStreamRecord> CreatureAIStreams = new Dictionary<int, CreatureAIStreamRecord>();
        private static string pendingSeedText = "0";
        private static bool active;
        private static string seedText = "0";
        private static ulong seedValue;
        private static string currentSlot = string.Empty;
        private static bool hasLifepodPosition;
        private static Vector3 lifepodPosition;

        internal static bool Active { get { return active; } }
        internal static string SeedText { get { return seedText; } }
        internal static ulong SeedValue { get { return seedValue; } }
        internal static bool IgnoreKnownFragments { get { return false; } }
        internal static bool HasLifepodPosition { get { return hasLifepodPosition; } }
        internal static Vector3 LifepodPosition { get { return lifepodPosition; } }



        internal static void SetPendingSeed(string value)
        {
            string normalized = string.IsNullOrEmpty(value) ? "0" : value.Trim();
            pendingSeedText = normalized;
            seedText = normalized;
            seedValue = DeterministicHash.SeedFromText(normalized);
            ModLog.Info("Ruleset 8 pending seed set to '" + seedText + "'.");
        }

        internal static void Deactivate()
        {
            active = false;
            currentSlot = string.Empty;
            EventCounters.Clear();
            CreatureOrigins.Clear();
            CreatureAIStreams.Clear();
            hasLifepodPosition = false;
            lifepodPosition = Vector3.zero;
        }

        internal static void ActivatePendingForNewGame()
        {
            if (!Ruleset8RaceSession.ShouldActivateForNewGame())
            {
                Deactivate();
                return;
            }

            currentSlot = Utils.GetSavegameDir() ?? string.Empty;
            seedText = pendingSeedText;
            seedValue = DeterministicHash.SeedFromText(seedText);
            hasLifepodPosition = false;
            lifepodPosition = Vector3.zero;
            EventCounters.Clear();
            CreatureOrigins.Clear();
            CreatureAIStreams.Clear();
            ActivateGlobalStream();
        }

        internal static void BindCurrentSave()
        {
            if (!Ruleset8RaceSession.ShouldActivateForNewGame())
            {
                Deactivate();
                return;
            }

            currentSlot = Utils.GetSavegameDir() ?? string.Empty;
            seedText = pendingSeedText;
            seedValue = DeterministicHash.SeedFromText(seedText);
            EventCounters.Clear();
            CreatureOrigins.Clear();
            CreatureAIStreams.Clear();
            hasLifepodPosition = false;
            lifepodPosition = Vector3.zero;
            ActivateGlobalStream();
            ModLog.Info("Ruleset 8 seed '" + seedText + "' bound to save slot '" + currentSlot + "'.");
        }

        internal static ulong Derive(string domain)
        {
            return DeterministicHash.Begin(seedValue, domain);
        }

        internal static ulong DeriveSlot(IEntitySlot slot)
        {
            SlotContextData context;
            if (SlotContext.TryGet(out context))
            {
                return context.Key;
            }

            Vector3 position = Vector3.zero;
            EntitySlot live = slot as EntitySlot;
            if (live != null)
            {
                position = live.transform.position;
            }
            else
            {
                EntitySlotData data = slot as EntitySlotData;
                if (data != null)
                {
                    position = data.localPosition;
                }
            }

            int allowedTypes = 0;
            for (int i = 0; i < 5; i++)
            {
                if (slot.IsTypeAllowed((EntitySlot.Type)i))
                {
                    allowedTypes |= 1 << i;
                }
            }
            return DeriveSlot(position, (int)slot.GetBiomeType(), allowedTypes, slot.GetDensity());
        }

        internal static ulong DeriveSlot(Vector3 worldPosition, int biome, int allowedTypes, float density)
        {
            ulong hash = Derive("entity-slot");
            hash = DeterministicHash.Add(hash, worldPosition);
            hash = DeterministicHash.Add(hash, biome);
            hash = DeterministicHash.Add(hash, allowedTypes);
            return DeterministicHash.Add(hash, density);
        }

        internal static ulong DeriveComponentEvent(Component component, string domain)
        {
            ulong baseHash = DeriveComponent(component, domain);

            string counterKey = domain + "|" + baseHash.ToString("x16", CultureInfo.InvariantCulture);
            int count;
            EventCounters.TryGetValue(counterKey, out count);
            EventCounters[counterKey] = count + 1;
            return DeterministicHash.Add(baseHash, count);
        }

        internal static ulong DeriveComponent(Component component, string domain)
        {
            ulong hash = Derive(domain);
            if (component == null)
            {
                return hash;
            }

            Creature creature = component as Creature;
            if (creature == null)
            {
                creature = component.GetComponentInParent<Creature>();
            }
            if (creature != null)
            {
                hash = DeterministicHash.Add(hash, GetCreatureOrigin(creature));
            }
            else
            {
                // Runtime UniqueIdentifier.Id values may be generated GUIDs. World position,
                // rotation, component type, and prefab/object name are stable across fresh worlds.
                hash = DeterministicHash.Add(hash, component.transform.position);
                hash = DeterministicHash.Add(hash, component.transform.rotation);
            }
            hash = DeterministicHash.Add(hash, component.GetType().FullName ?? component.GetType().Name);
            return DeterministicHash.Add(hash, component.gameObject.name ?? component.GetType().Name);
        }

        internal static void RegisterCreatureOrigin(Creature creature)
        {
            GetCreatureOrigin(creature);
        }

        internal static float NextCreatureAIValue(Component component)
        {
            ulong state = NextCreatureAISeed(component);
            return (uint)(state >> 40) / 16777216f;
        }

        internal static ulong NextCreatureAISeed(Component component)
        {
            int id = component.GetInstanceID();
            CreatureAIStreamRecord record;
            if (!CreatureAIStreams.TryGetValue(id, out record) || !ReferenceEquals(record.Component, component))
            {
                record = new CreatureAIStreamRecord
                {
                    Component = component,
                    State = DeriveComponent(component, "creature-ai-stream")
                };
                CreatureAIStreams[id] = record;
            }

            record.State = DeterministicHash.Mix(record.State);
            return record.State;
        }

        internal static ulong DeriveEcoTargetTie(IEcoTarget target)
        {
            ulong hash = Derive("eco-target-tie");
            hash = DeterministicHash.Add(hash, target.GetPosition());
            hash = DeterministicHash.Add(hash, target.GetName() ?? string.Empty);
            GameObject gameObject = target.GetGameObject();
            if (gameObject != null)
            {
                hash = DeterministicHash.Add(hash, gameObject.name ?? string.Empty);
                hash = DeterministicHash.Add(hash, (int)CraftData.GetTechType(gameObject));
            }
            return hash;
        }

        private static ulong GetCreatureOrigin(Creature creature)
        {
            int id = creature.GetInstanceID();
            CreatureOriginRecord record;
            if (CreatureOrigins.TryGetValue(id, out record) && ReferenceEquals(record.Creature, creature))
            {
                return record.Origin;
            }

            ulong origin = Derive("creature-origin");
            origin = DeterministicHash.Add(origin, creature.transform.position);
            origin = DeterministicHash.Add(origin, creature.transform.rotation);
            origin = DeterministicHash.Add(origin, creature.GetType().FullName ?? creature.GetType().Name);
            origin = DeterministicHash.Add(origin, creature.gameObject.name ?? creature.GetType().Name);
            origin = DeterministicHash.Add(origin, (int)CraftData.GetTechType(creature.gameObject));
            CreatureOrigins[id] = new CreatureOriginRecord { Creature = creature, Origin = origin };
            return origin;
        }

        private sealed class CreatureOriginRecord
        {
            internal Creature Creature;
            internal ulong Origin;
        }

        private sealed class CreatureAIStreamRecord
        {
            internal Component Component;
            internal ulong State;
        }

        internal static int DeriveIndex(Component component, string domain, int count)
        {
            if (count <= 0)
            {
                return 0;
            }

            ulong state = DeriveComponent(component, domain);
            int index = (int)(DeterministicHash.NextUnitFloat(ref state) * count);
            return index < count ? index : count - 1;
        }

        internal static Vector3 InsideUnitSphere(ulong seed)
        {
            ulong state = seed;
            for (int i = 0; i < 32; i++)
            {
                float x = NextSignedFloat(ref state);
                float y = NextSignedFloat(ref state);
                float z = NextSignedFloat(ref state);
                Vector3 value = new Vector3(x, y, z);
                if (value.sqrMagnitude <= 1f)
                {
                    return value;
                }
            }

            return Vector3.zero;
        }

        internal static Vector2 InsideUnitCircle(ulong seed)
        {
            ulong state = seed;
            for (int i = 0; i < 32; i++)
            {
                float x = NextSignedFloat(ref state);
                float y = NextSignedFloat(ref state);
                Vector2 value = new Vector2(x, y);
                if (value.sqrMagnitude <= 1f)
                {
                    return value;
                }
            }
            return Vector2.zero;
        }

        internal static string Describe()
        {
            return "Seed: " + seedText;
        }

        internal static void LogLifepodPosition(Vector3 position)
        {
            hasLifepodPosition = true;
            lifepodPosition = position;
            ModLog.Info(string.Format(CultureInfo.InvariantCulture,
                "Ruleset 8 seed '{0}' resolved lifepod to ({1:F3}, {2:F3}, {3:F3}).",
                seedText, position.x, position.y, position.z));
        }

        private static void ActivateGlobalStream()
        {
            active = true;
            UnityEngine.Random.InitState(DeterministicHash.ToUnitySeed(Derive("global-unity-random")));
        }

        private static float NextSignedFloat(ref ulong state)
        {
            state = DeterministicHash.Mix(state);
            uint value = (uint)(state >> 40);
            return value / 8388607.5f - 1f;
        }

        private static void WriteMetadata(string path)
        {
            string encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(seedText));
            string[] lines =
            {
                "format\t1",
                "build\t247",
                "ruleset\t" + RulesetVersion.ToString(CultureInfo.InvariantCulture),
                "seedTextBase64\t" + encoded,
                "seedValue\t" + seedValue.ToString("x16", CultureInfo.InvariantCulture),
                "algorithm\tlocation-keyed-fnv1a64-splitmix64",
                "fragmentReweighting\tnative-v1"
            };
            File.WriteAllLines(path, lines);
        }

        private static string GetMetadataFileName()
        {
            return string.Format(CultureInfo.InvariantCulture, "seed-{0:x16}-R-{1}.tsv", seedValue, RulesetVersion);
        }

        private static string FindMetadataPath(string root)
        {
            string[] current = Directory.GetFiles(root, MetadataPattern, SearchOption.TopDirectoryOnly);
            if (current.Length > 0)
            {
                Array.Sort(current, StringComparer.OrdinalIgnoreCase);
                return current[current.Length - 1];
            }

            string legacy = Path.Combine(root, LegacyMetadataFileName);
            return File.Exists(legacy) ? legacy : null;
        }

        private static bool TryReadMetadata(string path, out string text, out ulong value)
        {
            text = string.Empty;
            value = 0UL;
            try
            {
                string encoded = null;
                string valueText = null;
                string[] lines = File.ReadAllLines(path);
                for (int i = 0; i < lines.Length; i++)
                {
                    int separator = lines[i].IndexOf('\t');
                    if (separator <= 0)
                    {
                        separator = lines[i].IndexOf('=');
                    }
                    if (separator <= 0)
                    {
                        continue;
                    }

                    string key = lines[i].Substring(0, separator);
                    string item = lines[i].Substring(separator + 1);
                    if (key == "seedTextBase64") encoded = item;
                    if (key == "seedValue") valueText = item;
                }

                if (encoded == null || valueText == null)
                {
                    return false;
                }

                text = Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
                if (!ulong.TryParse(valueText, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value))
                {
                    return false;
                }

                return value == DeterministicHash.SeedFromText(text);
            }
            catch (Exception ex)
            {
                ModLog.Warn("Could not read seed metadata: " + ex.Message);
                return false;
            }
        }

        private static string ComputeSha256(string path)
        {
            using (SHA256 algorithm = SHA256.Create())
            using (FileStream stream = File.OpenRead(path))
            {
                byte[] bytes = algorithm.ComputeHash(stream);
                StringBuilder builder = new StringBuilder(bytes.Length * 2);
                for (int i = 0; i < bytes.Length; i++)
                {
                    builder.Append(bytes[i].ToString("x2", CultureInfo.InvariantCulture));
                }

                return builder.ToString();
            }
        }
    }
}

