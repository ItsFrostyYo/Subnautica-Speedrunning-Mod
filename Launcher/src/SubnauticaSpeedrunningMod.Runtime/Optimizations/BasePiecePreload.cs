using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace SubnauticaSpeedrunningMod.Runtime.Optimizations
{
    [HarmonyPatch(typeof(Base), "RegisterPiecesAsync")]
    internal static class BasePiecePreload
    {
        private static readonly FieldInfo NativeBundle = AccessTools.Field(typeof(ActualAssetBundle), "assetBundle");
        internal static Dictionary<string, GameObject> ReadyAssets;
        internal static int CacheHits;
        internal static int Warmups;
        private static void Postfix(ref IEnumerator __result)
        {
            BaseSessionCache.MarkWorldUse();
            if (ModPreloadBaseRuntime.Enabled && NativeBundle != null) __result = WarmThenRegister(__result);
        }

        private static IEnumerator WarmThenRegister(IEnumerator original)
        {
            // Use the game's bundle owner and balance only our extra reference.
            // Its original per-piece requests and PieceDef assignments run intact.
            var bundleRequest = AssetBundleManager.LoadBundleAsync("basegeneratorpieces");
            var watch = Stopwatch.StartNew();
            try
            {
                yield return bundleRequest;
                var actual = bundleRequest.assetBundle as ActualAssetBundle;
                var bundle = actual != null ? NativeBundle.GetValue(actual) as AssetBundle : null;
                if (bundle != null)
                {
                    Dictionary<string, GameObject> cached;
                    if (BaseSessionCache.TryReuse(bundle, out cached))
                    {
                        ReadyAssets = cached; CacheHits = 0;
                        while (original.MoveNext()) yield return original.Current;
                        UnityEngine.Debug.Log("Speedrunning Mod base assets: session cache hit, native registration " + watch.Elapsed.TotalSeconds.ToString("0.000") + " s; already-loaded requests reused " + CacheHits + ".");
                        yield break;
                    }
                    AssetBundleRequest warmup = null;
                    try { warmup = bundle.LoadAllAssetsAsync<GameObject>(); Warmups++; }
                    catch (Exception ex) { UnityEngine.Debug.LogWarning("Speedrunning Mod: base prewarm unavailable; using native requests. " + ex.Message); }
                    if (warmup != null)
                    {
                        yield return warmup;
                        // Keep native asset references alive until registration is
                        // complete. Loading prefabs never instantiates a base.
                        var assets = warmup.allAssets;
                        ReadyAssets = BuildCache(bundle.GetAllAssetNames(), assets);
                        CacheHits = 0;
                        double warmSeconds = watch.Elapsed.TotalSeconds;
                        watch.Reset(); watch.Start();
                        while (original.MoveNext()) yield return original.Current;
                        BaseSessionCache.Capture(bundle, ReadyAssets);
                        UnityEngine.Debug.Log("Speedrunning Mod base assets: warmup " + warmSeconds.ToString("0.000") + " s, native registration " + watch.Elapsed.TotalSeconds.ToString("0.000") + " s, already-loaded requests reused " + CacheHits + ".");
                        GC.KeepAlive(assets);
                        yield break;
                    }
                }
                while (original.MoveNext()) yield return original.Current;
            }
            finally
            {
                ReadyAssets = null;
                var disposable = original as IDisposable;
                if (disposable != null) disposable.Dispose();
                AssetBundleManager.UnloadBundle("basegeneratorpieces");
            }
        }

        private static Dictionary<string, GameObject> BuildCache(string[] names, UnityEngine.Object[] assets)
        {
            var byName = new Dictionary<string, GameObject>(StringComparer.OrdinalIgnoreCase);
            var ambiguous = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var asset in assets)
            {
                var prefab = asset as GameObject;
                if (prefab == null) continue;
                if (byName.ContainsKey(prefab.name)) ambiguous.Add(prefab.name);
                else byName.Add(prefab.name, prefab);
            }
            var result = new Dictionary<string, GameObject>(StringComparer.OrdinalIgnoreCase);
            foreach (string path in names)
            {
                string name = System.IO.Path.GetFileNameWithoutExtension(path);
                GameObject prefab;
                if (!ambiguous.Contains(name) && byName.TryGetValue(name, out prefab)) result[path] = prefab;
            }
            return result;
        }

        internal sealed class CachedBundleRequest : IAssetBundleWrapperCreateRequest
        {
            private readonly IAssetBundleWrapperCreateRequest original;
            public CachedBundleRequest(IAssetBundleWrapperCreateRequest original) { this.original = original; }
            public bool isDone { get { return original.isDone; } }
            public object Current { get { return original.Current; } }
            public bool MoveNext() { return original.MoveNext(); }
            public void Reset() { original.Reset(); }
            public IAssetBundleWrapper assetBundle { get { return new CachedBundle(original.assetBundle); } }
        }

        private sealed class CachedBundle : IAssetBundleWrapper
        {
            private readonly IAssetBundleWrapper original;
            internal CachedBundle(IAssetBundleWrapper original) { this.original = original; }
            public string name { get { return original.name; } }
            public void Unload(bool unloadAllLoadedObjects) { original.Unload(unloadAllLoadedObjects); }
            public bool Contains(string path) { return original.Contains(path); }
            public IAssetBundleWrapperRequest LoadAssetAsync<T>(string path) where T : UnityEngine.Object
            {
                GameObject prefab;
                if (typeof(T) == typeof(GameObject) && ReadyAssets != null && ReadyAssets.TryGetValue(path, out prefab) && prefab != null)
                { CacheHits++; return new ReadyAssetRequest(prefab); }
                return original.LoadAssetAsync<T>(path);
            }
        }

        private sealed class ReadyAssetRequest : IAssetBundleWrapperRequest
        {
            private readonly GameObject prefab;
            internal ReadyAssetRequest(GameObject prefab) { this.prefab = prefab; }
            public UnityEngine.Object asset { get { return prefab; } }
            public object Current { get { return null; } }
            public bool MoveNext() { return false; }
            public void Reset() { }
        }
    }

    [HarmonyPatch(typeof(AssetBundleManager), "LoadBundleAsync")]
    internal static class BasePieceCachedRequests
    {
        private static void Postfix(string bundleName, ref IAssetBundleWrapperCreateRequest __result)
        {
            if (bundleName == "basegeneratorpieces" && ModPreloadBaseRuntime.Enabled && BasePiecePreload.ReadyAssets != null && __result != null)
                __result = new BasePiecePreload.CachedBundleRequest(__result);
        }
    }
}

