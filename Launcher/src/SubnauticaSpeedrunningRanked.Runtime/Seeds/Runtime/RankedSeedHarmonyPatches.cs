using System.Reflection;
using UWE;
using UnityEngine;

namespace SubnauticaSpeedrunningRanked.Runtime.Seeds
{
    internal static class RankedSeedHarmonyPatches
    {
        private static FieldInfo _vfxSchoolFishMeshRendererField;
        private static MethodInfo _stalkerLoseToothMethod;
        private static MethodInfo _craftDataGetTechTypeMethod;
        private static MethodInfo _hardnessGetMethod;

        private static bool BlockFishSchoolPrefix(object __instance)
        {
            if (!ShouldBlockFishSchools())
            {
                return true;
            }

            TryDisableSchoolObject(__instance, "VFXSchoolFish");
            return false;
        }

        private static bool BlockFishSchoolManagerAddPrefix(VFXSchoolFish school)
        {
            if (!ShouldBlockFishSchools())
            {
                return true;
            }

            TryDisableSchoolObject(school, "VFXSchoolFishManager.AddSchool");
            return false;
        }

        private static bool BlockSimpleSchoolPrefix(School __instance)
        {
            if (!ShouldBlockFishSchools())
            {
                return true;
            }

            TryDisableSchoolObject(__instance, "School");
            return false;
        }

        private static bool CheckLoseToothPrefix(Stalker __instance, GameObject target)
        {
            try
            {
                RankedSeedRuntimeProfile profile = RankedSeedRuntimeHost.GetProfile();
                if (profile == null || __instance == null || !RankedSeedRuntimeHost.IsSupportedGameplayMode())
                {
                    return true;
                }

                bool salvageTarget = IsMetalSalvageTarget(target);
                if (!profile.StalkerBitesDropTeeth && !salvageTarget)
                {
                    return true;
                }

                float hardness;
                if (salvageTarget && !profile.FixBadMetal && TryGetTargetHardness(target, out hardness) && hardness <= 0f)
                {
                    return true;
                }

                if (salvageTarget && !profile.FixBadMetal && LooksLikeLegacyNoToothVariant(target))
                {
                    return true;
                }

                float percent = Mathf.Clamp(profile.StalkerToothDropProbability, 0f, 1000f);
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

                if (bonusChance > 0f && (bonusChance >= 100f || UnityEngine.Random.value <= bonusChance / 100f))
                {
                    TryInvokeLoseTooth(__instance);
                }

                return false;
            }
            catch (System.Exception ex)
            {
                RankedLog.Warn("Seed stalker tooth hook fell back to vanilla logic: " + ex.Message);
                return true;
            }
        }

        private static void LootDistributionInitializePrefix(System.Collections.Generic.Dictionary<string, LootDistributionData.SrcData> src)
        {
            RankedSeedRuntimeProfile profile = RankedSeedRuntimeHost.GetProfile();
            if (profile == null || src == null || !RankedSeedRuntimeHost.IsSupportedGameplayMode())
            {
                return;
            }

            try
            {
                int touchedEntries = 0;
                foreach (System.Collections.Generic.KeyValuePair<string, LootDistributionData.SrcData> entry in src)
                {
                    LootDistributionData.SrcData sourceData = entry.Value;
                    if (sourceData == null || sourceData.distribution == null || sourceData.distribution.Count == 0)
                    {
                        continue;
                    }

                    WorldEntityInfo info;
                    if (!WorldEntityDatabase.TryGetInfo(entry.Key, out info))
                    {
                        continue;
                    }

                    bool isCreatureSlot = info.slotType == EntitySlot.Type.Creature;
                    for (int i = 0; i < sourceData.distribution.Count; i++)
                    {
                        LootDistributionData.BiomeData biomeData = sourceData.distribution[i];
                        if (biomeData == null || biomeData.probability <= 0f)
                        {
                            continue;
                        }

                        float multiplier = profile.GetEntityProbabilityMultiplier(
                            info.techType,
                            biomeData.biome,
                            isCreatureSlot,
                            RankedSeedRuntimeHost.IsSurvivalLikeMode());
                        if (Mathf.Approximately(multiplier, 1f))
                        {
                            continue;
                        }

                        biomeData.probability = Mathf.Max(0f, biomeData.probability * multiplier);
                        touchedEntries++;
                    }
                }

                if (touchedEntries > 0)
                {
                    RankedLog.Info("Applied always-on loot distribution rules to " + touchedEntries + " biome distribution entries.");
                }
            }
            catch (System.Exception ex)
            {
                RankedLog.Warn("Loot distribution initialization hook fell back to vanilla data: " + ex.Message);
            }
        }

        private static FieldInfo GetVfxSchoolFishMeshRendererField(System.Type instanceType)
        {
            if (_vfxSchoolFishMeshRendererField == null)
            {
                _vfxSchoolFishMeshRendererField = instanceType.GetField("meshRenderer", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            }

            return _vfxSchoolFishMeshRendererField;
        }

        private static MethodInfo GetStalkerLoseToothMethod(System.Type instanceType)
        {
            if (_stalkerLoseToothMethod == null)
            {
                _stalkerLoseToothMethod = instanceType.GetMethod("LoseTooth", BindingFlags.Instance | BindingFlags.NonPublic);
            }

            return _stalkerLoseToothMethod;
        }

        private static bool ShouldBlockFishSchools()
        {
            RankedSeedRuntimeProfile profile = RankedSeedRuntimeHost.GetProfile();
            return profile != null && RankedSeedRuntimeHost.IsSupportedGameplayMode() && profile.DisableFishSchools;
        }

        private static void TryDisableSchoolObject(object school, string source)
        {
            if (school == null)
            {
                return;
            }

            try
            {
                MeshRenderer meshRenderer = null;
                Component component = school as Component;
                if (component != null)
                {
                    FieldInfo meshRendererField = GetVfxSchoolFishMeshRendererField(component.GetType());
                    if (meshRendererField != null)
                    {
                        meshRenderer = meshRendererField.GetValue(component) as MeshRenderer;
                    }

                    if (meshRenderer == null)
                    {
                        meshRenderer = component.GetComponent<MeshRenderer>();
                    }
                }

                if (meshRenderer != null)
                {
                    meshRenderer.enabled = false;
                }

                Behaviour behaviour = school as Behaviour;
                if (behaviour != null)
                {
                    behaviour.enabled = false;
                }

                GameObject gameObject = null;
                if (school is GameObject)
                {
                    gameObject = (GameObject)school;
                }
                else if (component != null)
                {
                    gameObject = component.gameObject;
                }

                if (gameObject != null && gameObject.activeSelf)
                {
                    gameObject.SetActive(false);
                }
            }
            catch (System.Exception ex)
            {
                RankedLog.Warn("Failed to disable fish school object from " + source + ": " + ex.Message);
            }
        }

        private static void TryInvokeLoseTooth(Stalker stalker)
        {
            if (stalker == null)
            {
                return;
            }

            MethodInfo loseToothMethod = GetStalkerLoseToothMethod(stalker.GetType());
            if (loseToothMethod == null)
            {
                return;
            }

            loseToothMethod.Invoke(stalker, null);
        }

        private static bool TryGetTargetHardness(GameObject target, out float hardness)
        {
            hardness = 0f;
            if (target == null)
            {
                return false;
            }

            MethodInfo method = GetHardnessGetMethod();
            if (method == null)
            {
                return false;
            }

            try
            {
                object value = method.Invoke(null, new object[] { target });
                if (value == null)
                {
                    return false;
                }

                hardness = System.Convert.ToSingle(value, System.Globalization.CultureInfo.InvariantCulture);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool IsMetalSalvageTarget(GameObject target)
        {
            if (target == null)
            {
                return false;
            }

            TechType techType = TechType.None;
            MethodInfo resolver = GetCraftDataGetTechTypeMethod();
            if (resolver == null)
            {
                return false;
            }

            try
            {
                object value = resolver.Invoke(null, new object[] { target });
                if (value is TechType)
                {
                    techType = (TechType)value;
                }
            }
            catch
            {
                return false;
            }

            return techType == TechType.ScrapMetal;
        }

        private static MethodInfo GetCraftDataGetTechTypeMethod()
        {
            if (_craftDataGetTechTypeMethod == null)
            {
                MethodInfo[] methods = typeof(CraftData).GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                for (int i = 0; i < methods.Length; i++)
                {
                    MethodInfo method = methods[i];
                    if (!string.Equals(method.Name, "GetTechType", System.StringComparison.Ordinal))
                    {
                        continue;
                    }

                    ParameterInfo[] parameters = method.GetParameters();
                    if (parameters != null && parameters.Length == 1 && parameters[0].ParameterType == typeof(GameObject))
                    {
                        _craftDataGetTechTypeMethod = method;
                        break;
                    }
                }
            }

            return _craftDataGetTechTypeMethod;
        }

        private static MethodInfo GetHardnessGetMethod()
        {
            if (_hardnessGetMethod == null)
            {
                MethodInfo[] methods = typeof(HardnessMixin).GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                for (int i = 0; i < methods.Length; i++)
                {
                    MethodInfo method = methods[i];
                    if (!string.Equals(method.Name, "GetHardness", System.StringComparison.Ordinal))
                    {
                        continue;
                    }

                    ParameterInfo[] parameters = method.GetParameters();
                    if (parameters != null && parameters.Length == 1 && parameters[0].ParameterType == typeof(GameObject))
                    {
                        _hardnessGetMethod = method;
                        break;
                    }
                }
            }

            return _hardnessGetMethod;
        }

        private static bool LooksLikeLegacyNoToothVariant(GameObject target)
        {
            string token = NormalizeToken(target != null ? target.name : string.Empty);
            return token.Contains("lshape") ||
                   token.Contains("lshaped") ||
                   token.Contains("shape_l") ||
                   token.EndsWith("_l", System.StringComparison.Ordinal) ||
                   token.Contains("scrapmetal_l");
        }

        private static string NormalizeToken(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            System.Text.StringBuilder builder = new System.Text.StringBuilder(value.Length);
            for (int i = 0; i < value.Length; i++)
            {
                char character = value[i];
                if (char.IsLetterOrDigit(character))
                {
                    builder.Append(char.ToLowerInvariant(character));
                }
                else if (character == '_' || character == '-')
                {
                    builder.Append(character);
                }
            }

            return builder.ToString();
        }
    }
}
