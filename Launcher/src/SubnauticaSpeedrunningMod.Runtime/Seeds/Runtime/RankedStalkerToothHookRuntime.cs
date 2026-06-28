using System;
using System.Globalization;
using System.Reflection;
using HarmonyLib;

namespace SubnauticaSpeedrunningMod.Runtime.Seeds
{
    internal static class ModStalkerToothHookRuntime
    {
        private const string HarmonyId = "subnautica.speedrunning.ranked.stalker.tooth";

        private static readonly object Gate = new object();
        private static readonly Random FallbackRandom = new Random();

        private static bool _installAttempted;
        private static bool _installed;
        private static bool _available = true;
        private static bool _initFailedLogged;
        private static Harmony _harmony;
        private static MethodInfo _loseToothMethod;
        private static MethodInfo _craftDataGetTechTypeMethod;
        private static PropertyInfo _unityRandomValueProperty;
        private static MethodInfo _hardnessGetMethod;

        public static bool EnsureInstalled()
        {
            if (_installAttempted)
            {
                return _installed && _available;
            }

            _installAttempted = true;

            try
            {
                Type stalkerType = ResolveType("Stalker, Assembly-CSharp");
                if (stalkerType == null)
                {
                    _available = false;
                    ModLog.Warn("Stalker tooth hook unavailable; Stalker type was not found.");
                    return false;
                }

                MethodInfo checkLoseTooth = FindMethod(stalkerType, "CheckLoseTooth", 1);
                _loseToothMethod = FindMethod(stalkerType, "LoseTooth", 0);
                if (checkLoseTooth == null || _loseToothMethod == null)
                {
                    _available = false;
                    ModLog.Warn("Stalker tooth hook unavailable; CheckLoseTooth/LoseTooth were not found.");
                    return false;
                }

                Type randomType = ResolveType("UnityEngine.Random, UnityEngine");
                _unityRandomValueProperty = randomType == null
                    ? null
                    : randomType.GetProperty("value", BindingFlags.Public | BindingFlags.Static);

                Type craftDataType = ResolveType("CraftData, Assembly-CSharp");
                if (craftDataType != null)
                {
                    MethodInfo[] methods = craftDataType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                    for (int i = 0; i < methods.Length; i++)
                    {
                        MethodInfo method = methods[i];
                        if (!string.Equals(method.Name, "GetTechType", StringComparison.Ordinal))
                        {
                            continue;
                        }

                        ParameterInfo[] parameters = method.GetParameters();
                        if (parameters != null && parameters.Length == 1)
                        {
                            _craftDataGetTechTypeMethod = method;
                            break;
                        }
                    }
                }

                Type hardnessMixinType = ResolveType("HardnessMixin, Assembly-CSharp");
                if (hardnessMixinType != null)
                {
                    MethodInfo[] methods = hardnessMixinType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                    for (int i = 0; i < methods.Length; i++)
                    {
                        MethodInfo method = methods[i];
                        if (!string.Equals(method.Name, "GetHardness", StringComparison.Ordinal))
                        {
                            continue;
                        }

                        ParameterInfo[] parameters = method.GetParameters();
                        if (parameters != null && parameters.Length == 1)
                        {
                            _hardnessGetMethod = method;
                            break;
                        }
                    }
                }

                MethodInfo prefix = typeof(ModStalkerToothHookRuntime).GetMethod(nameof(CheckLoseToothPrefix), BindingFlags.Static | BindingFlags.NonPublic);
                if (prefix == null)
                {
                    _available = false;
                    return false;
                }

                _harmony = new Harmony(HarmonyId);
                _harmony.Patch(checkLoseTooth, prefix: new HarmonyMethod(prefix));
                _installed = true;
                ModLog.Info("Installed stalker tooth handling hook.");
                return true;
            }
            catch (Exception ex)
            {
                _available = false;
                if (!_initFailedLogged)
                {
                    _initFailedLogged = true;
                    ModLog.Warn("Failed to initialize stalker tooth hook: " + ex.Message);
                }

                return false;
            }
        }

        private static bool CheckLoseToothPrefix(object __instance, object target)
        {
            try
            {
                ModSeedRuntimeProfile profile = ModSeedRuntimeHost.GetProfile();
                if (__instance == null || profile == null || !ModSeedRuntimeHost.IsSupportedGameplayMode())
                {
                    return true;
                }

                bool salvageTarget = IsMetalSalvageTarget(target);
                if (!profile.StalkerBitesDropTeeth && !salvageTarget)
                {
                    return true;
                }

                if (salvageTarget && !profile.FixBadMetal && TryGetTargetHardness(target, out float hardness) && hardness <= 0f)
                {
                    return true;
                }

                if (salvageTarget && !profile.FixBadMetal && LooksLikeLegacyNoToothVariant(target))
                {
                    return true;
                }

                float percent = ClampPercent(profile.StalkerToothDropProbability);
                if (percent <= 0f)
                {
                    return false;
                }

                int guaranteedDrops = (int)(percent / 100f);
                float bonusChance = percent - guaranteedDrops * 100f;

                for (int i = 0; i < guaranteedDrops; i++)
                {
                    TryInvokeLoseTooth(__instance);
                }

                if (bonusChance > 0f && (bonusChance >= 100f || NextRandom01() <= bonusChance / 100f))
                {
                    TryInvokeLoseTooth(__instance);
                }

                return false;
            }
            catch
            {
                return true;
            }
        }

        private static bool TryGetTargetHardness(object target, out float hardness)
        {
            hardness = 0f;
            MethodInfo method = _hardnessGetMethod;
            if (method == null || target == null)
            {
                return false;
            }

            try
            {
                object value = method.Invoke(null, new[] { target });
                if (value == null)
                {
                    return false;
                }

                hardness = Convert.ToSingle(value, CultureInfo.InvariantCulture);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool LooksLikeLegacyNoToothVariant(object target)
        {
            if (target == null)
            {
                return false;
            }

            string token = NormalizeToken(target.ToString() ?? string.Empty);
            if (token.Length == 0)
            {
                return false;
            }

            return token.Contains("lshape") ||
                   token.Contains("lshaped") ||
                   token.Contains("shape_l") ||
                   token.EndsWith("_l", StringComparison.Ordinal) ||
                   token.Contains("scrapmetal_l");
        }

        private static void TryInvokeLoseTooth(object instance)
        {
            MethodInfo method = _loseToothMethod;
            if (method == null || instance == null)
            {
                return;
            }

            try
            {
                method.Invoke(instance, new object[0]);
            }
            catch
            {
            }
        }

        private static bool IsMetalSalvageTarget(object target)
        {
            if (target == null)
            {
                return false;
            }

            string techTypeName = string.Empty;
            int techTypeId = int.MinValue;
            TryResolveTargetTechType(target, ref techTypeName, ref techTypeId);
            if (techTypeId == 2)
            {
                return true;
            }

            return string.Equals(NormalizeToken(techTypeName), "scrapmetal", StringComparison.Ordinal);
        }

        private static void TryResolveTargetTechType(object target, ref string techTypeName, ref int techTypeId)
        {
            MethodInfo resolver = _craftDataGetTechTypeMethod;
            if (resolver == null || target == null)
            {
                return;
            }

            try
            {
                object value = resolver.Invoke(null, new[] { target });
                if (value == null)
                {
                    return;
                }

                techTypeName = value.ToString() ?? string.Empty;
                try
                {
                    techTypeId = Convert.ToInt32(value, CultureInfo.InvariantCulture);
                }
                catch
                {
                    techTypeId = int.MinValue;
                }
            }
            catch
            {
            }
        }

        private static float NextRandom01()
        {
            PropertyInfo unityRandomValue = _unityRandomValueProperty;
            if (unityRandomValue != null)
            {
                try
                {
                    object value = unityRandomValue.GetValue(null, null);
                    if (value != null)
                    {
                        float parsed = Convert.ToSingle(value, CultureInfo.InvariantCulture);
                        if (parsed < 0f)
                        {
                            return 0f;
                        }

                        if (parsed > 1f)
                        {
                            return 1f;
                        }

                        return parsed;
                    }
                }
                catch
                {
                }
            }

            lock (Gate)
            {
                return (float)FallbackRandom.NextDouble();
            }
        }

        private static float ClampPercent(float percent)
        {
            if (percent < 0f)
            {
                return 0f;
            }

            if (percent > 1000f)
            {
                return 1000f;
            }

            return percent;
        }

        private static MethodInfo FindMethod(Type type, string name, int parameterCount)
        {
            if (type == null || string.IsNullOrEmpty(name))
            {
                return null;
            }

            Type current = type;
            while (current != null)
            {
                MethodInfo[] methods = current.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                for (int i = 0; i < methods.Length; i++)
                {
                    MethodInfo method = methods[i];
                    if (!string.Equals(method.Name, name, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    ParameterInfo[] parameters = method.GetParameters();
                    if (parameters == null || parameters.Length == parameterCount)
                    {
                        return method;
                    }
                }

                current = current.BaseType;
            }

            return null;
        }

        private static Type ResolveType(string typeName)
        {
            if (string.IsNullOrEmpty(typeName))
            {
                return null;
            }

            Type type = Type.GetType(typeName, false);
            if (type != null)
            {
                return type;
            }

            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                Assembly assembly = assemblies[i];
                if (assembly == null)
                {
                    continue;
                }

                type = assembly.GetType(typeName, false);
                if (type != null)
                {
                    return type;
                }
            }

            return null;
        }

        private static string NormalizeToken(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            System.Text.StringBuilder buffer = new System.Text.StringBuilder(value.Length);
            for (int i = 0; i < value.Length; i++)
            {
                char character = value[i];
                if (char.IsLetterOrDigit(character))
                {
                    buffer.Append(char.ToLowerInvariant(character));
                }
                else if (character == '_' || character == '-')
                {
                    buffer.Append(character);
                }
            }

            return buffer.ToString();
        }
    }
}
