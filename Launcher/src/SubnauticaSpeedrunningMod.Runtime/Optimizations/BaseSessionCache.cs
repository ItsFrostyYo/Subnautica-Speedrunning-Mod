using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace SubnauticaSpeedrunningMod.Runtime.Optimizations
{
    // Process-local prefab assets only. Never retain a world's Base instances,
    // terrain, creatures, seed state, registrations, or save data.
    internal static class BaseSessionCache
    {
        internal const string BundleName = "basegeneratorpieces";
        private static AssetBundle retainedBundle;
        private static Dictionary<string, GameObject> retainedAssets;
        private static bool retainedAtMenu;
        private static readonly FieldInfo Bundles = AccessTools.Field(typeof(AssetBundleManager), "loadedBundles");
        private static readonly FieldInfo Native = AccessTools.Field(typeof(ActualAssetBundle), "assetBundle");
        internal static int Reuses { get; private set; }
        internal static int LastCleanupBundleCount { get; private set; }
        internal static int RetainedCount { get { return IsValid ? retainedAssets.Count : 0; } }
        internal static bool IsValid
        {
            get
            {
                if (retainedBundle == null || retainedAssets == null || retainedAssets.Count == 0) return false;
                foreach (var asset in retainedAssets.Values) if (asset == null) return false;
                return true;
            }
        }

        internal static void Capture(AssetBundle bundle, Dictionary<string, GameObject> assets)
        {
            if (bundle == null || assets == null || assets.Count == 0 || !ShouldKeep()) return;
            retainedBundle = bundle;
            retainedAssets = new Dictionary<string, GameObject>(assets, StringComparer.OrdinalIgnoreCase);
            Debug.Log("Speedrunning Mod: retained " + retainedAssets.Count + " base prefab assets in memory for this game session.");
        }

        internal static bool TryReuse(AssetBundle bundle, out Dictionary<string, GameObject> assets)
        {
            assets = null;
            if (!ShouldKeep() || !IsValid || bundle != retainedBundle) return false;
            assets = retainedAssets; Reuses++;
            return true;
        }

        private static bool ShouldKeep()
        {
            return ModPreloadBaseRuntime.Enabled;
        }

        internal static object DetachForCleanup()
        {
            if (!ShouldKeep() || !IsValid || Bundles == null || Native == null) return null;
            var entries = Bundles.GetValue(null) as IDictionary;
            if (entries == null || !entries.Contains(BundleName)) return null;
            var entry = entries[BundleName];
            var request = AccessTools.Field(entry.GetType(), "createRequest").GetValue(entry) as IAssetBundleWrapperCreateRequest;
            var wrapper = request != null ? request.assetBundle as ActualAssetBundle : null;
            if (wrapper == null || Native.GetValue(wrapper) as AssetBundle != retainedBundle) return null;
            // Native Deinitialize still unloads every other bundle and clears its
            // registry. Only our verified prefab bundle is temporarily excluded.
            entries.Remove(BundleName);
            return entry;
        }

        internal static void ReattachAfterCleanup(object entry)
        {
            if (entry == null || Bundles == null) return;
            var entries = Bundles.GetValue(null) as IDictionary;
            if (entries == null) return;
            // Previous world's native references are gone. One pin owns the cache;
            // the next world's original requests add their normal references.
            AccessTools.Field(entry.GetType(), "numReferences").SetValue(entry, 1);
            entries[BundleName] = entry;
            retainedAtMenu = true;
            LastCleanupBundleCount = entries.Count;
            Debug.Log("Speedrunning Mod: session base assets survived menu cleanup; bundle references reset to one cache pin.");
        }

        internal static void Release()
        {
            // Never force-destroy prefab assets that an active world may use.
            // Normal bundle cleanup is allowed again once our references are gone.
            retainedAssets = null; retainedBundle = null; retainedAtMenu = false; Reuses = 0; LastCleanupBundleCount = 0;
        }

        internal static void MarkWorldUse() { retainedAtMenu = false; }

        internal static void Disable()
        {
            // A retained menu-only pin can be dropped now. Unload(false) preserves
            // any remaining object references until normal unused-asset cleanup.
            // Never unload the bundle under an active world's base pieces.
            if (retainedAtMenu && uGUI_MainMenu.main != null && IsValid)
            {
                var entries = Bundles.GetValue(null) as IDictionary;
                var entry = entries != null && entries.Contains(BundleName) ? entries[BundleName] : null;
                if (entry != null)
                {
                    var request = AccessTools.Field(entry.GetType(), "createRequest").GetValue(entry) as IAssetBundleWrapperCreateRequest;
                    var wrapper = request != null ? request.assetBundle as ActualAssetBundle : null;
                    int references = (int)AccessTools.Field(entry.GetType(), "numReferences").GetValue(entry);
                    if (wrapper != null && Native.GetValue(wrapper) as AssetBundle == retainedBundle && references == 1)
                    {
                        wrapper.Unload(false);
                        entries.Remove(BundleName);
                    }
                }
            }
            Release();
        }
    }

    [HarmonyPatch(typeof(AssetBundleManager), "Deinitialize")]
    internal static class KeepBaseBundleDuringCleanup
    {
        private static void Prefix(out object __state) { __state = BaseSessionCache.DetachForCleanup(); }
        private static void Finalizer(object __state)
        {
            BaseSessionCache.ReattachAfterCleanup(__state);
            if (!ModPreloadBaseRuntime.Enabled) BaseSessionCache.Release();
        }
    }

    [HarmonyPatch(typeof(WaitScreen), "Add")]
    internal static class CachedBaseLoadingLabel
    {
        private static void Prefix(ref string name)
        {
            if (ModPreloadBaseRuntime.Enabled && BaseSessionCache.IsValid &&
                Language.main != null && name == Language.main.Get("PreloadingBase")) name = "Loading cached base assets";
        }
    }
}

