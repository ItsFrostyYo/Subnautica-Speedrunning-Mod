using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using SubnauticaSpeedrunningMod.Runtime.Practice;

namespace SubnauticaSpeedrunningMod.Runtime.Ui
{
    internal static class ModOptionsPanelRuntime
    {
        private const string PracticeTabLabel = "Practice";
        private const string PracticeChoiceRowObjectName = "ModPracticeHotbarChoice";
        private const string PracticePreviewImageObjectName = "ModPracticeHotbarPreview";
        private const string AccountTabLabel = "Account";
        private const string ModSettingsTabLabel = "Ranked Settings";
        private const string FutureUpdatePlaceholderText = "Coming in a Future Update";
        private static readonly Dictionary<string, Sprite> HotbarPreviewSprites = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);

        private static int _lastSeenOptionsPanelId;
        private static bool _loggedForCurrentPanel;

        public static void Install()
        {
        }

        public static void RefreshLivePanel()
        {
            if (uGUI_MainMenu.main == null)
            {
                return;
            }

            uGUI_OptionsPanel panel = UnityEngine.Object.FindObjectOfType<uGUI_OptionsPanel>();
            if (panel == null || !panel.isActiveAndEnabled || !panel.gameObject.activeInHierarchy)
            {
                return;
            }

            PatchPanel(panel);
        }

        internal static bool IsLivePanelVisible()
        {
            if (uGUI_MainMenu.main == null)
            {
                return false;
            }

            uGUI_OptionsPanel panel = UnityEngine.Object.FindObjectOfType<uGUI_OptionsPanel>();
            return panel != null && panel.isActiveAndEnabled && panel.gameObject.activeInHierarchy;
        }

        internal static void PatchPanel(uGUI_OptionsPanel panel)
        {
            if (panel == null || uGUI_MainMenu.main == null)
            {
                return;
            }

            int panelId = panel.GetInstanceID();
            if (_lastSeenOptionsPanelId != panelId)
            {
                _lastSeenOptionsPanelId = panelId;
                _loggedForCurrentPanel = false;
            }

            bool removedAccountTab = RemoveCustomTab(panel, AccountTabLabel);
            bool removedRankedSettingsTab = RemoveCustomTab(panel, ModSettingsTabLabel);
            if ((removedAccountTab || removedRankedSettingsTab || !_loggedForCurrentPanel) &&
                !HasTab(panel, AccountTabLabel) &&
                !HasTab(panel, ModSettingsTabLabel))
            {
                ModLog.Info("Removed Account and Ranked Settings tabs from the main menu options panel.");
                _loggedForCurrentPanel = true;
            }

            bool practiceTabAdded;
            int practiceTabIndex = EnsureTab(panel, PracticeTabLabel, out practiceTabAdded);
            EnsurePracticeTabContent(panel, practiceTabIndex);

            if (panel.tabsContainer != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(panel.tabsContainer);
            }

            if (panel.panesContainer != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(panel.panesContainer);
            }

            SyncVisiblePane(panel);
        }

        private static bool RemoveCustomTab(uGUI_OptionsPanel panel, string label)
        {
            int tabIndex = GetTabIndex(panel, label);
            if (tabIndex < 0)
            {
                return false;
            }

            if (panel.tabsContainer != null && tabIndex < panel.tabsContainer.childCount)
            {
                Transform tabTransform = panel.tabsContainer.GetChild(tabIndex);
                if (tabTransform != null)
                {
                    UnityEngine.Object.Destroy(tabTransform.gameObject);
                }
            }

            if (panel.panesContainer != null && tabIndex < panel.panesContainer.childCount)
            {
                Transform paneTransform = panel.panesContainer.GetChild(tabIndex);
                if (paneTransform != null)
                {
                    UnityEngine.Object.Destroy(paneTransform.gameObject);
                }
            }

            return true;
        }

        private static void EnsurePracticeTabContent(uGUI_OptionsPanel panel, int tabIndex)
        {
            if (panel == null || panel.panesContainer == null || tabIndex < 0 || tabIndex >= panel.panesContainer.childCount)
            {
                return;
            }

            Transform paneTransform = panel.panesContainer.GetChild(tabIndex);
            if (paneTransform == null)
            {
                return;
            }

            Transform contentTransform = paneTransform.Find("Content") ?? paneTransform;
            if (contentTransform == null)
            {
                return;
            }

            GameObject row = FindOrCreatePracticeChoiceRow(panel, tabIndex, contentTransform);
            if (row == null)
            {
                return;
            }

            ConfigurePracticeChoiceRow(row);
        }

        private static GameObject FindOrCreatePracticeChoiceRow(uGUI_OptionsPanel panel, int tabIndex, Transform contentTransform)
        {
            Transform existingTransform = contentTransform.Find(PracticeChoiceRowObjectName);
            if (existingTransform != null)
            {
                return existingTransform.gameObject;
            }

            string[] options = BuildPracticeHotbarOptionLabels();
            uGUI_Choice choice = panel.AddChoiceOption(
                tabIndex,
                "Hotbar Layout",
                options,
                ModPracticeHotbarOptions.GetSelectedLayoutIndex(),
                null);

            if (choice == null)
            {
                return null;
            }

            GameObject row = GetChoiceRowRoot(contentTransform, choice.transform);
            if (row == null)
            {
                return null;
            }

            row.name = PracticeChoiceRowObjectName;
            return row;
        }

        private static void ConfigurePracticeChoiceRow(GameObject row)
        {
            if (row == null)
            {
                return;
            }

            uGUI_Choice choice = row.GetComponentInChildren<uGUI_Choice>(true);
            if (choice == null)
            {
                return;
            }

            Text labelText = FindChoiceLabelText(row, choice.currentText);
            if (labelText != null)
            {
                labelText.text = "Hotbar Layout";
                labelText.resizeTextForBestFit = false;
                labelText.horizontalOverflow = HorizontalWrapMode.Overflow;
                TranslationLiveUpdate labelTranslation = labelText.GetComponent<TranslationLiveUpdate>();
                if (labelTranslation != null)
                {
                    UnityEngine.Object.Destroy(labelTranslation);
                }

                uGUI_Text labelLocalizer = labelText.GetComponent<uGUI_Text>();
                if (labelLocalizer != null)
                {
                    UnityEngine.Object.Destroy(labelLocalizer);
                }
            }

            if (choice.currentText != null)
            {
                choice.currentText.enabled = true;
                choice.currentText.color = new Color(1f, 1f, 1f, 0f);
                choice.currentText.raycastTarget = false;
            }

            int selectedIndex = ModPracticeHotbarOptions.GetSelectedLayoutIndex();
            string[] options = BuildPracticeHotbarOptionLabels();
            choice.SetOptions(options);
            choice.onValueChanged = new uGUI_Choice.ChoiceEvent();
            choice.onValueChanged.AddListener(delegate(int value)
            {
                ModPracticeHotbarOptions.SetSelectedLayoutIndex(value);
                RefreshPracticeChoicePreview(choice, value);
            });
            choice.value = selectedIndex;
            RefreshPracticeChoicePreview(choice, selectedIndex);
        }

        private static void RefreshPracticeChoicePreview(uGUI_Choice choice, int layoutIndex)
        {
            if (choice == null || choice.currentText == null)
            {
                return;
            }

            Image previewImage = EnsurePracticePreviewImage(choice);
            if (previewImage == null)
            {
                return;
            }

            string previewPath;
            if (!ModPracticeSaveCatalog.TryGetHotbarLayoutPreviewPath(layoutIndex, out previewPath))
            {
                previewImage.enabled = false;
                return;
            }

            Sprite sprite = LoadPreviewSprite(previewPath);
            previewImage.sprite = sprite;
            previewImage.enabled = sprite != null;
        }

        private static Image EnsurePracticePreviewImage(uGUI_Choice choice)
        {
            RectTransform choiceRectTransform = choice.transform as RectTransform;
            if (choiceRectTransform == null)
            {
                return null;
            }

            Transform existingTransform = choiceRectTransform.Find(PracticePreviewImageObjectName);
            Image previewImage = existingTransform != null ? existingTransform.GetComponent<Image>() : null;
            if (previewImage == null)
            {
                GameObject previewObject = new GameObject(PracticePreviewImageObjectName, typeof(RectTransform), typeof(Image));
                previewObject.transform.SetParent(choiceRectTransform, false);
                previewImage = previewObject.GetComponent<Image>();
            }

            RectTransform rectTransform = previewImage.rectTransform;
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.offsetMin = new Vector2(34f, 2f);
            rectTransform.offsetMax = new Vector2(-34f, -2f);
            rectTransform.localScale = Vector3.one;

            previewImage.preserveAspect = true;
            previewImage.raycastTarget = false;
            previewImage.color = Color.white;
            previewImage.transform.SetSiblingIndex(0);

            return previewImage;
        }

        private static Sprite LoadPreviewSprite(string previewPath)
        {
            if (string.IsNullOrEmpty(previewPath) || !File.Exists(previewPath))
            {
                return null;
            }

            Sprite sprite;
            if (HotbarPreviewSprites.TryGetValue(previewPath, out sprite))
            {
                return sprite;
            }

            byte[] imageBytes = File.ReadAllBytes(previewPath);
            Texture2D texture = new Texture2D(2, 2, TextureFormat.ARGB32, false);
            if (!texture.LoadImage(imageBytes))
            {
                UnityEngine.Object.Destroy(texture);
                return null;
            }

            texture.name = Path.GetFileNameWithoutExtension(previewPath);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;

            sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                100f);

            HotbarPreviewSprites[previewPath] = sprite;
            return sprite;
        }

        private static Text FindChoiceLabelText(GameObject row, Text currentValueText)
        {
            if (row == null)
            {
                return null;
            }

            Text[] texts = row.GetComponentsInChildren<Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                Text text = texts[i];
                if (text != null && !ReferenceEquals(text, currentValueText))
                {
                    return text;
                }
            }

            return null;
        }

        private static string[] BuildPracticeHotbarOptionLabels()
        {
            string[] options = new string[ModPracticeHotbarOptions.LayoutCountValue];
            for (int i = 0; i < options.Length; i++)
            {
                options[i] = ModPracticeHotbarOptions.GetDisplayName(i);
            }

            return options;
        }

        private static GameObject GetChoiceRowRoot(Transform contentTransform, Transform choiceTransform)
        {
            if (contentTransform == null || choiceTransform == null)
            {
                return null;
            }

            Transform current = choiceTransform;
            while (current != null && current.parent != null && current.parent != contentTransform)
            {
                current = current.parent;
            }

            if (current != null && current.parent == contentTransform)
            {
                return current.gameObject;
            }

            return null;
        }

        private static int EnsureTab(uGUI_OptionsPanel panel, string label, out bool wasAdded)
        {
            wasAdded = false;
            if (panel == null)
            {
                return -1;
            }

            int existingTabIndex = GetTabIndex(panel, label);
            if (existingTabIndex >= 0)
            {
                ConfigureTabLabel(panel, existingTabIndex, label);
                return existingTabIndex;
            }

            int tabIndex = panel.AddTab(label);
            ConfigureTabLabel(panel, tabIndex, label);
            wasAdded = true;
            return tabIndex;
        }

        private static bool HasTab(uGUI_OptionsPanel panel, string label)
        {
            return GetTabIndex(panel, label) >= 0;
        }

        private static int GetTabIndex(uGUI_OptionsPanel panel, string label)
        {
            if (panel == null || panel.tabsContainer == null || string.IsNullOrEmpty(label))
            {
                return -1;
            }

            for (int i = 0; i < panel.tabsContainer.childCount; i++)
            {
                Transform child = panel.tabsContainer.GetChild(i);
                if (child == null)
                {
                    continue;
                }

                Text labelText = child.GetComponentInChildren<Text>(true);
                if (labelText != null &&
                    string.Equals((labelText.text ?? string.Empty).Trim(), label, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }

            return -1;
        }

        private static void ConfigureTabLabel(uGUI_OptionsPanel panel, int tabIndex, string label)
        {
            if (panel == null || panel.tabsContainer == null || tabIndex < 0 || tabIndex >= panel.tabsContainer.childCount)
            {
                return;
            }

            Transform tabTransform = panel.tabsContainer.GetChild(tabIndex);
            if (tabTransform == null)
            {
                return;
            }

            TranslationLiveUpdate translation = tabTransform.GetComponentInChildren<TranslationLiveUpdate>(true);
            if (translation != null)
            {
                UnityEngine.Object.Destroy(translation);
            }

            Text labelText = tabTransform.GetComponentInChildren<Text>(true);
            if (labelText == null)
            {
                return;
            }

            labelText.text = label;
            RemoveLocalizationComponents(tabTransform.gameObject);
            CopyTextStyle(FindTemplateTabLabel(panel, tabIndex), labelText);
            labelText.resizeTextForBestFit = false;
            labelText.horizontalOverflow = HorizontalWrapMode.Overflow;
            labelText.verticalOverflow = VerticalWrapMode.Truncate;
            labelText.alignment = TextAnchor.MiddleCenter;

            Toggle toggle = tabTransform.GetComponentInChildren<Toggle>(true);
            if (toggle != null)
            {
                toggle.group = panel.tabsContainer.GetComponentInChildren<ToggleGroup>();
            }
        }

        private static bool EnsurePlaceholderContent(uGUI_OptionsPanel panel, int tabIndex, string placeholderObjectName)
        {
            if (panel == null || panel.panesContainer == null || tabIndex < 0 || tabIndex >= panel.panesContainer.childCount)
            {
                return false;
            }

            Transform paneTransform = panel.panesContainer.GetChild(tabIndex);
            if (paneTransform == null)
            {
                return false;
            }

            Transform contentTransform = paneTransform.Find("Content") ?? paneTransform;
            Transform existingTransform = contentTransform.Find(placeholderObjectName);
            if (existingTransform != null)
            {
                Text existingText = existingTransform.GetComponent<Text>();
                if (existingText != null)
                {
                    existingText.text = FutureUpdatePlaceholderText;
                }

                return false;
            }

            Text templateText = FindTemplateContentText(panel);
            if (templateText == null)
            {
                return false;
            }

            GameObject placeholderObject = new GameObject(placeholderObjectName, typeof(RectTransform));
            placeholderObject.transform.SetParent(contentTransform, false);
            RemoveLocalizationComponents(placeholderObject);

            Text placeholderText = placeholderObject.AddComponent<Text>();
            CopyTextStyle(templateText, placeholderText);
            placeholderText.text = FutureUpdatePlaceholderText;
            placeholderText.alignment = TextAnchor.UpperCenter;
            placeholderText.horizontalOverflow = HorizontalWrapMode.Wrap;
            placeholderText.verticalOverflow = VerticalWrapMode.Overflow;
            placeholderText.resizeTextForBestFit = false;
            placeholderText.raycastTarget = false;

            RectTransform rectTransform = placeholderText.rectTransform;
            rectTransform.anchorMin = new Vector2(0f, 1f);
            rectTransform.anchorMax = new Vector2(1f, 1f);
            rectTransform.pivot = new Vector2(0.5f, 1f);
            rectTransform.anchoredPosition = new Vector2(0f, -20f);
            rectTransform.sizeDelta = new Vector2(-48f, 80f);
            rectTransform.localScale = Vector3.one;

            return true;
        }

        private static void SyncVisiblePane(uGUI_OptionsPanel panel)
        {
            if (panel == null || panel.tabsContainer == null || panel.panesContainer == null)
            {
                return;
            }

            int selectedIndex = -1;
            int tabCount = Math.Min(panel.tabsContainer.childCount, panel.panesContainer.childCount);
            for (int i = 0; i < tabCount; i++)
            {
                Toggle toggle = panel.tabsContainer.GetChild(i).GetComponentInChildren<Toggle>(true);
                if (toggle != null && toggle.isOn)
                {
                    selectedIndex = i;
                    break;
                }
            }

            if (selectedIndex < 0)
            {
                return;
            }

            for (int i = 0; i < tabCount; i++)
            {
                Transform pane = panel.panesContainer.GetChild(i);
                if (pane != null)
                {
                    pane.gameObject.SetActive(i == selectedIndex);
                }
            }
        }

        private static void RemoveLocalizationComponents(GameObject gameObject)
        {
            if (gameObject == null)
            {
                return;
            }

            TranslationLiveUpdate[] translations = gameObject.GetComponentsInChildren<TranslationLiveUpdate>(true);
            for (int i = 0; i < translations.Length; i++)
            {
                if (translations[i] != null)
                {
                    UnityEngine.Object.Destroy(translations[i]);
                }
            }

            uGUI_Text[] guiTexts = gameObject.GetComponentsInChildren<uGUI_Text>(true);
            for (int i = 0; i < guiTexts.Length; i++)
            {
                if (guiTexts[i] != null)
                {
                    UnityEngine.Object.Destroy(guiTexts[i]);
                }
            }
        }

        private static Text FindTemplateTabLabel(uGUI_OptionsPanel panel, int excludedTabIndex)
        {
            if (panel == null || panel.tabsContainer == null)
            {
                return null;
            }

            for (int i = 0; i < panel.tabsContainer.childCount; i++)
            {
                if (i == excludedTabIndex)
                {
                    continue;
                }

                Text template = panel.tabsContainer.GetChild(i).GetComponentInChildren<Text>(true);
                if (template != null)
                {
                    return template;
                }
            }

            return null;
        }

        private static Text FindTemplateContentText(uGUI_OptionsPanel panel)
        {
            if (panel == null || panel.panesContainer == null)
            {
                return null;
            }

            for (int i = 0; i < panel.panesContainer.childCount; i++)
            {
                Text[] texts = panel.panesContainer.GetChild(i).GetComponentsInChildren<Text>(true);
                for (int j = 0; j < texts.Length; j++)
                {
                    if (texts[j] != null)
                    {
                        return texts[j];
                    }
                }
            }

            return null;
        }

        private static void CopyTextStyle(Text source, Text destination)
        {
            if (destination == null || source == null)
            {
                return;
            }

            destination.font = source.font;
            destination.fontStyle = source.fontStyle;
            destination.fontSize = source.fontSize;
            destination.lineSpacing = source.lineSpacing;
            destination.supportRichText = source.supportRichText;
            destination.alignByGeometry = source.alignByGeometry;
            destination.alignment = source.alignment;
            destination.color = source.color;
            destination.material = source.material;
            destination.resizeTextForBestFit = source.resizeTextForBestFit;
            destination.horizontalOverflow = source.horizontalOverflow;
            destination.verticalOverflow = source.verticalOverflow;
            destination.raycastTarget = false;
        }
    }
}
