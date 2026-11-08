using System;
using System.Collections.Generic;
using System.IO;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using SubnauticaSpeedrunningMod.Runtime.Practice;

namespace SubnauticaSpeedrunningMod.Runtime.Ui
{
    internal static class ModOptionsPanelRuntime
    {
        private static readonly Dictionary<string, Sprite> PreviewSprites = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);
        private static bool scheduled, installed;
        private static uGUI_OptionsPanel livePanel;

        public static void Install()
        {
            Optimizations.ModPreloadBaseRuntime.Install();
            if (scheduled) return;
            scheduled = true;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            livePanel = null;
            if (scene.name != "XMenu" || installed) return;
            new Harmony("subnautica.speedrunning.practice.options").CreateClassProcessor(typeof(PracticeOptionsBuildPatch)).Patch();
            installed = true;
        }

        internal static bool IsLivePanelVisible()
        {
            return uGUI_MainMenu.main != null && livePanel != null && livePanel.isActiveAndEnabled && livePanel.gameObject.activeInHierarchy;
        }

        internal static void PatchPanel(uGUI_OptionsPanel panel)
        {
            if (panel == null || uGUI_MainMenu.main == null || !IsMainMenuOptionsPanel(panel)) return;
            livePanel = panel;
            // Native builders own toggle/pane navigation and visual state. No per-frame restyling.
            int tab = panel.AddTab("Practice");
            AddImageChoice(panel, tab, "Hotbar Layout", BuildHotbarLabels(), ModPracticeHotbarOptions.GetSelectedLayoutIndex(),
                "ModPracticeHotbarPreview", ModPracticeHotbarOptions.SetSelectedLayoutIndex, ModPracticeSaveCatalog.TryGetHotbarLayoutPreviewPath);
            AddImageChoice(panel, tab, "Health", BuildHealthLabels(), ModPracticeHealthOptions.GetSelectedHealthIndex(),
                "ModPracticeHealthPreview", ModPracticeHealthOptions.SetSelectedHealthIndex, ModPracticeSaveCatalog.TryGetHealthPreviewPath);
            string[] times = new string[ModPracticeTimeOfDayOptions.Count];
            for (int i = 0; i < times.Length; i++) times[i] = ModPracticeTimeOfDayOptions.GetDisplayName(i);
            panel.AddChoiceOption(tab, "Time of Day", times, ModPracticeTimeOfDayOptions.GetSelectedIndex(), ModPracticeTimeOfDayOptions.SetSelectedIndex);
            ModLog.Info("Built native Practice options tab.");
        }

        private static bool IsMainMenuOptionsPanel(uGUI_OptionsPanel panel)
        {
            for (Transform current = panel.transform; current != null; current = current.parent)
                if (current.name == "Options") return true;
            return false;
        }

        private static void AddImageChoice(uGUI_OptionsPanel panel, int tab, string label, string[] options, int selected,
            string imageName, Action<int> save, TryGetPreviewPathDelegate resolve)
        {
            Image preview = null;
            uGUI_Choice choice = null;
            choice = panel.AddChoiceOption(tab, label, options, selected, delegate(int value)
            {
                save(value);
                RefreshPreview(choice, preview, resolve, value);
            });
            // Follow the native value rectangle without moving native arrows or hit areas.
            var imageObject = new GameObject(imageName, typeof(RectTransform), typeof(Image));
            imageObject.transform.SetParent(choice.currentText.transform, false);
            preview = imageObject.GetComponent<Image>();
            var rect = preview.rectTransform;
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
            preview.preserveAspect = true;
            preview.raycastTarget = false;
            RefreshPreview(choice, preview, resolve, selected);
        }

        private static void RefreshPreview(uGUI_Choice choice, Image preview, TryGetPreviewPathDelegate resolve, int index)
        {
            if (choice == null || preview == null) return;
            string path;
            Sprite sprite = resolve(index, out path) ? LoadPreviewSprite(path) : null;
            preview.sprite = sprite; preview.enabled = sprite != null;
            choice.currentText.enabled = sprite == null;
        }

        private static Sprite LoadPreviewSprite(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
            Sprite sprite;
            if (PreviewSprites.TryGetValue(path, out sprite) && sprite != null) return sprite;
            var texture = new Texture2D(2, 2, TextureFormat.ARGB32, false);
            if (!texture.LoadImage(File.ReadAllBytes(path))) { UnityEngine.Object.Destroy(texture); return null; }
            texture.name = Path.GetFileNameWithoutExtension(path);
            texture.wrapMode = TextureWrapMode.Clamp; texture.filterMode = FilterMode.Bilinear;
            sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(.5f, .5f), 100f);
            PreviewSprites[path] = sprite;
            return sprite;
        }

        private static string[] BuildHotbarLabels()
        {
            var labels = new string[ModPracticeHotbarOptions.LayoutCountValue];
            for (int i = 0; i < labels.Length; i++) labels[i] = ModPracticeHotbarOptions.GetDisplayName(i);
            return labels;
        }
        private static string[] BuildHealthLabels()
        {
            var labels = new string[ModPracticeHealthOptions.Count];
            for (int i = 0; i < labels.Length; i++) labels[i] = ModPracticeHealthOptions.GetDisplayName(i);
            return labels;
        }
        private delegate bool TryGetPreviewPathDelegate(int selectedIndex, out string previewPath);
    }

    [HarmonyPatch(typeof(uGUI_OptionsPanel), "AddTabs")]
    internal static class PracticeOptionsBuildPatch
    {
        private static void Postfix(uGUI_OptionsPanel __instance) { ModOptionsPanelRuntime.PatchPanel(__instance); }
    }
}
