using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace SubnauticaSpeedrunningMod.Runtime.Seeds
{
    internal static class ModFishSchoolHookRuntime
    {
        private const string HarmonyId = "subnautica.speedrunning.ranked.fishschools";

        private static bool _installAttempted;
        private static bool _installed;
        private static bool _available = true;
        private static Harmony _harmony;

        public static bool EnsureInstalled()
        {
            if (_installAttempted)
            {
                return _installed && _available;
            }

            _installAttempted = true;

            try
            {
                Type vfxSchoolFishType = ResolveType("VFXSchoolFish, Assembly-CSharp");
                Type vfxSchoolFishManagerType = ResolveType("VFXSchoolFishManager, Assembly-CSharp");
                Type schoolType = ResolveType("School, Assembly-CSharp");
                if (vfxSchoolFishType == null || vfxSchoolFishManagerType == null)
                {
                    _available = false;
                    ModLog.Warn("Fish school hook unavailable; school runtime types were not found.");
                    return false;
                }

                MethodInfo awake = FindMethod(vfxSchoolFishType, "Awake");
                MethodInfo onEnable = FindMethod(vfxSchoolFishType, "OnEnable");
                MethodInfo addSchool = FindMethod(vfxSchoolFishManagerType, "AddSchool", vfxSchoolFishType);
                MethodInfo schoolStart = FindMethod(schoolType, "Start");
                MethodInfo blockSchoolPrefix = typeof(ModFishSchoolHookRuntime).GetMethod(nameof(BlockSchoolPrefix), BindingFlags.Static | BindingFlags.NonPublic);
                MethodInfo blockManagerAddPrefix = typeof(ModFishSchoolHookRuntime).GetMethod(nameof(BlockManagerAddPrefix), BindingFlags.Static | BindingFlags.NonPublic);
                MethodInfo blockSimpleSchoolPrefix = typeof(ModFishSchoolHookRuntime).GetMethod(nameof(BlockSimpleSchoolPrefix), BindingFlags.Static | BindingFlags.NonPublic);
                if (onEnable == null || addSchool == null || blockSchoolPrefix == null || blockManagerAddPrefix == null)
                {
                    _available = false;
                    ModLog.Warn("Fish school hook unavailable; required school methods could not be resolved.");
                    return false;
                }

                _harmony = new Harmony(HarmonyId);
                if (awake != null)
                {
                    _harmony.Patch(awake, prefix: new HarmonyMethod(blockSchoolPrefix));
                }

                _harmony.Patch(onEnable, prefix: new HarmonyMethod(blockSchoolPrefix));
                _harmony.Patch(addSchool, prefix: new HarmonyMethod(blockManagerAddPrefix));
                if (schoolStart != null && blockSimpleSchoolPrefix != null)
                {
                    _harmony.Patch(schoolStart, prefix: new HarmonyMethod(blockSimpleSchoolPrefix));
                }

                _installed = true;
                ModLog.Info("Installed fish school suppression hooks.");
                return true;
            }
            catch (Exception ex)
            {
                _available = false;
                ModLog.Warn("Failed to install fish school suppression hooks: " + ex.Message);
                return false;
            }
        }

        private static bool BlockSchoolPrefix(object __instance)
        {
            if (!IsEnabled())
            {
                return true;
            }

            TryDisableSchoolObject(__instance, "VFXSchoolFish");
            return false;
        }

        private static bool BlockManagerAddPrefix(object school)
        {
            if (!IsEnabled())
            {
                return true;
            }

            TryDisableSchoolObject(school, "VFXSchoolFishManager.AddSchool");
            return false;
        }

        private static bool BlockSimpleSchoolPrefix(object __instance)
        {
            if (!IsEnabled())
            {
                return true;
            }

            TryDisableSchoolObject(__instance, "School");
            return false;
        }

        private static void TryDisableSchoolObject(object school, string source)
        {
            if (school == null)
            {
                return;
            }

            try
            {
                GameObject gameObject = null;
                if (school is GameObject)
                {
                    gameObject = (GameObject)school;
                }
                else if (school is Component)
                {
                    gameObject = ((Component)school).gameObject;
                }

                if (gameObject == null)
                {
                    return;
                }

                Renderer renderer = gameObject.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.enabled = false;
                }

                gameObject.SetActive(false);
            }
            catch (Exception ex)
            {
                ModLog.Warn("Failed to disable fish school object from " + source + ": " + ex.Message);
            }
        }

        private static bool IsEnabled()
        {
            // Keep fish schools suppressed across every session type for now, including vanilla play.
            return true;
        }

        private static Type ResolveType(string typeName)
        {
            return Type.GetType(typeName, false);
        }

        private static MethodInfo FindMethod(Type type, string name, params Type[] parameterTypes)
        {
            if (type == null || string.IsNullOrEmpty(name))
            {
                return null;
            }

            BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            return parameterTypes == null || parameterTypes.Length == 0
                ? type.GetMethod(name, flags)
                : type.GetMethod(name, flags, null, parameterTypes, null);
        }
    }
}
