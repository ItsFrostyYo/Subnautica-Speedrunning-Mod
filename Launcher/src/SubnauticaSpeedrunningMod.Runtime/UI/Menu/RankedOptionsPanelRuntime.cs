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
        private const string PracticeSettingsRootName = "ModPracticeSettingsRoot";
        private const string HotbarChoiceRowObjectName = "ModPracticeHotbarChoice";
        private const string HotbarPreviewImageObjectName = "ModPracticeHotbarPreview";
        private const string HealthChoiceRowObjectName = "ModPracticeHealthChoice";
        private const string HealthPreviewImageObjectName = "ModPracticeHealthPreview";
        private const string TimeOfDayChoiceRowObjectName = "ModPracticeTimeOfDayChoice";
        private const string TimeOfDayPreviewImageObjectName = "ModPracticeTimeOfDayPreview";
        private const string TimeOfDayValueTextObjectName = "ModPracticeTimeOfDayValue";
        private const string AccountTabLabel = "Account";
        private const string ModSettingsTabLabel = "Ranked Settings";

        private static readonly Dictionary<string, Sprite> PreviewSprites = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);

        private static int _lastSeenOptionsPanelId;
        private static bool _loggedForCurrentPanel;
        private static bool _structureInitializedForCurrentPanel;

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
            if (panel == null || !panel.isActiveAndEnabled || !panel.gameObject.activeInHierarchy || !IsMainMenuOptionsPanel(panel))
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
            return panel != null && panel.isActiveAndEnabled && panel.gameObject.activeInHierarchy && IsMainMenuOptionsPanel(panel);
        }

        private static bool IsMainMenuOptionsPanel(uGUI_OptionsPanel panel)
        {
            Transform current = panel != null ? panel.transform : null;
            while (current != null)
            {
                if (string.Equals(current.name, "Options", StringComparison.Ordinal))
                {
                    return true;
                }

                current = current.parent;
            }

            return false;
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
                _structureInitializedForCurrentPanel = false;
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
            if (practiceTabIndex < 0)
            {
                _structureInitializedForCurrentPanel = false;
                return;
            }

            if (!_structureInitializedForCurrentPanel)
            {
                EnsurePracticeTabContent(panel, practiceTabIndex, initializeStructure: true);
                _structureInitializedForCurrentPanel = true;
            }
            else
            {
                EnsurePracticeTabContent(panel, practiceTabIndex, initializeStructure: false);
            }

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

        private static void EnsurePracticeTabContent(uGUI_OptionsPanel panel, int tabIndex, bool initializeStructure)
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

            GameObject rootObject = EnsurePracticeSettingsRoot(contentTransform, initializeStructure);
            if (rootObject == null)
            {
                return;
            }

            GameObject hotbarRow = EnsureSettingRow(rootObject.transform, panel.choiceOptionPrefab, HotbarChoiceRowObjectName, 0);
            GameObject healthRow = EnsureSettingRow(rootObject.transform, panel.choiceOptionPrefab, HealthChoiceRowObjectName, 1);
            GameObject timeOfDayRow = EnsureSettingRow(rootObject.transform, panel.choiceOptionPrefab, TimeOfDayChoiceRowObjectName, 2);

            ConfigureImageChoiceRow(
                hotbarRow,
                "Hotbar Layout",
                BuildHotbarOptionLabels(),
                ModPracticeHotbarOptions.GetSelectedLayoutIndex(),
                HotbarPreviewImageObjectName,
                delegate(int value)
                {
                    ModPracticeHotbarOptions.SetSelectedLayoutIndex(value);
                },
                ModPracticeSaveCatalog.TryGetHotbarLayoutPreviewPath);

            ConfigureImageChoiceRow(
                healthRow,
                "Health",
                BuildHealthOptionLabels(),
                ModPracticeHealthOptions.GetSelectedHealthIndex(),
                HealthPreviewImageObjectName,
                delegate(int value)
                {
                    ModPracticeHealthOptions.SetSelectedHealthIndex(value);
                },
                ModPracticeSaveCatalog.TryGetHealthPreviewPath);

            ConfigureTextChoiceRow(
                timeOfDayRow,
                "Time of Day",
                BuildTimeOfDayOptionLabels(),
                ModPracticeTimeOfDayOptions.GetSelectedIndex(),
                delegate(int value)
                {
                    ModPracticeTimeOfDayOptions.SetSelectedIndex(value);
                });
        }

        private static GameObject EnsurePracticeSettingsRoot(Transform contentTransform, bool initializeStructure)
        {
            if (contentTransform == null)
            {
                return null;
            }

            Transform existingTransform = contentTransform.Find(PracticeSettingsRootName);
            GameObject rootObject = existingTransform != null ? existingTransform.gameObject : null;
            if (rootObject == null)
            {
                rootObject = new GameObject(PracticeSettingsRootName, typeof(RectTransform));
                rootObject.transform.SetParent(contentTransform, false);
            }

            if (initializeStructure)
            {
                DestroyForeignPracticeChildren(rootObject.transform);
            }

            RectTransform rectTransform = rootObject.GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                rectTransform.anchorMin = new Vector2(0f, 0f);
                rectTransform.anchorMax = new Vector2(1f, 1f);
                rectTransform.pivot = new Vector2(0.5f, 0.5f);
                rectTransform.offsetMin = new Vector2(12f, 12f);
                rectTransform.offsetMax = new Vector2(-12f, -12f);
                rectTransform.localScale = Vector3.one;
            }

            rootObject.transform.SetAsLastSibling();
            return rootObject;
        }

        private static void DestroyForeignPracticeChildren(Transform rootTransform)
        {
            if (rootTransform == null)
            {
                return;
            }

            List<GameObject> toDestroy = new List<GameObject>();
            for (int i = 0; i < rootTransform.childCount; i++)
            {
                Transform child = rootTransform.GetChild(i);
                if (child == null)
                {
                    continue;
                }

                string childName = child.gameObject.name ?? string.Empty;
                if (childName.StartsWith("ModPractice", StringComparison.Ordinal))
                {
                    toDestroy.Add(child.gameObject);
                }
            }

            for (int i = 0; i < toDestroy.Count; i++)
            {
                UnityEngine.Object.Destroy(toDestroy[i]);
            }
        }

        private static GameObject EnsureSettingRow(Transform rootTransform, GameObject choiceOptionPrefab, string rowObjectName, int rowIndex)
        {
            if (rootTransform == null || choiceOptionPrefab == null || string.IsNullOrEmpty(rowObjectName))
            {
                return null;
            }

            Transform existingTransform = rootTransform.Find(rowObjectName);
            GameObject rowObject = existingTransform != null ? existingTransform.gameObject : null;
            if (rowObject == null)
            {
                rowObject = UnityEngine.Object.Instantiate(choiceOptionPrefab);
                rowObject.name = rowObjectName;
                rowObject.transform.SetParent(rootTransform, false);
                PrepareManualRow(rowObject);
            }

            RectTransform rowRect = rowObject.transform as RectTransform;
            if (rowRect != null)
            {
                const float rowHeight = 102f;
                const float rowSpacing = 10f;
                const float topPadding = 14f;

                rowRect.anchorMin = new Vector2(0f, 1f);
                rowRect.anchorMax = new Vector2(1f, 1f);
                rowRect.pivot = new Vector2(0.5f, 1f);
                rowRect.anchoredPosition = new Vector2(0f, -(topPadding + ((rowHeight + rowSpacing) * rowIndex)));
                rowRect.sizeDelta = new Vector2(0f, rowHeight);
                rowRect.localScale = Vector3.one;
            }

            rowObject.SetActive(true);
            rowObject.transform.SetSiblingIndex(rowIndex);
            return rowObject;
        }

        private static void PrepareManualRow(GameObject rowObject)
        {
            if (rowObject == null)
            {
                return;
            }

            LayoutElement layoutElement = rowObject.GetComponent<LayoutElement>();
            if (layoutElement != null)
            {
                UnityEngine.Object.Destroy(layoutElement);
            }

            uGUI_Choice choice = rowObject.GetComponentInChildren<uGUI_Choice>(true);
            if (choice != null)
            {
                RectTransform choiceRect = choice.transform as RectTransform;
                if (choiceRect != null)
                {
                    choiceRect.anchorMin = new Vector2(0f, 0f);
                    choiceRect.anchorMax = new Vector2(1f, 1f);
                    choiceRect.pivot = new Vector2(0.5f, 0.5f);
                    choiceRect.offsetMin = new Vector2(320f, 10f);
                    choiceRect.offsetMax = new Vector2(-34f, -10f);
                    choiceRect.localScale = Vector3.one;
                }

                ConfigureChoiceWidgetLayout(choice);
                choice.onValueChanged = new uGUI_Choice.ChoiceEvent();
            }

            TranslationLiveUpdate[] translations = rowObject.GetComponentsInChildren<TranslationLiveUpdate>(true);
            for (int i = 0; i < translations.Length; i++)
            {
                TranslationLiveUpdate translation = translations[i];
                if (translation == null || translation.textComponent == null)
                {
                    continue;
                }

                translation.translationKey = translation.textComponent.text;
            }
        }

        private static void ConfigureImageChoiceRow(
            GameObject rowObject,
            string label,
            string[] options,
            int selectedIndex,
            string previewObjectName,
            Action<int> onValueChanged,
            TryGetPreviewPathDelegate previewResolver)
        {
            if (rowObject == null)
            {
                return;
            }

            uGUI_Choice choice = rowObject.GetComponentInChildren<uGUI_Choice>(true);
            if (choice == null)
            {
                return;
            }

            ConfigureRowLabel(rowObject, choice, label);
            ConfigureChoiceValueText(choice, fontSize: 26, alignment: TextAnchor.MiddleCenter);

            choice.SetOptions(options);
            choice.onValueChanged = new uGUI_Choice.ChoiceEvent();
            choice.onValueChanged.AddListener(delegate(int value)
            {
                if (onValueChanged != null)
                {
                    onValueChanged(value);
                }

                RefreshChoicePreview(choice, previewObjectName, previewResolver, value, showPreview: true);
            });

            int clampedIndex = ClampChoiceIndex(selectedIndex, options);
            choice.value = clampedIndex;
            RefreshChoicePreview(choice, previewObjectName, previewResolver, clampedIndex, showPreview: true);
        }

        private static void ConfigureTextChoiceRow(
            GameObject rowObject,
            string label,
            string[] options,
            int selectedIndex,
            Action<int> onValueChanged)
        {
            if (rowObject == null)
            {
                return;
            }

            uGUI_Choice choice = rowObject.GetComponentInChildren<uGUI_Choice>(true);
            if (choice == null)
            {
                return;
            }

            ConfigureRowLabel(rowObject, choice, label);
            ConfigureChoiceValueText(choice, fontSize: 26, alignment: TextAnchor.MiddleCenter);
            Text valueText = EnsureStandaloneChoiceValueText(rowObject, choice, TimeOfDayValueTextObjectName);

            choice.SetOptions(options);
            choice.onValueChanged = new uGUI_Choice.ChoiceEvent();
            choice.onValueChanged.AddListener(delegate(int value)
            {
                if (onValueChanged != null)
                {
                    onValueChanged(value);
                }

                ApplyStandaloneTextChoiceDisplay(choice, valueText, options, value);
            });

            int clampedIndex = ClampChoiceIndex(selectedIndex, options);
            choice.value = clampedIndex;
            RefreshChoicePreview(choice, TimeOfDayPreviewImageObjectName, null, clampedIndex, showPreview: false);
            ApplyStandaloneTextChoiceDisplay(choice, valueText, options, clampedIndex);
        }

        private static void ConfigureRowLabel(GameObject rowObject, uGUI_Choice choice, string label)
        {
            if (rowObject == null || choice == null)
            {
                return;
            }

            Text labelText = FindRowLabelText(rowObject, choice);
            if (labelText != null)
            {
                labelText.text = label;
                labelText.resizeTextForBestFit = false;
                labelText.horizontalOverflow = HorizontalWrapMode.Overflow;
                labelText.verticalOverflow = VerticalWrapMode.Truncate;
                labelText.fontSize = 28;
                labelText.alignment = TextAnchor.MiddleLeft;

                TranslationLiveUpdate translation = labelText.GetComponent<TranslationLiveUpdate>();
                if (translation != null)
                {
                    translation.translationKey = label;
                }
            }
        }

        private static Text FindRowLabelText(GameObject rowObject, uGUI_Choice choice)
        {
            if (rowObject == null || choice == null)
            {
                return null;
            }

            Text[] texts = rowObject.GetComponentsInChildren<Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                Text text = texts[i];
                if (text == null || text == choice.currentText)
                {
                    continue;
                }

                if (text.transform.IsChildOf(choice.transform))
                {
                    continue;
                }

                return text;
            }

            return null;
        }

        private static void RefreshChoicePreview(
            uGUI_Choice choice,
            string previewObjectName,
            TryGetPreviewPathDelegate previewResolver,
            int selectedIndex,
            bool showPreview)
        {
            if (choice == null || choice.currentText == null)
            {
                return;
            }

            RemoveOtherPreviewImages(choice, previewObjectName);

            if (!showPreview)
            {
                RemoveChoicePreview(choice, previewObjectName);
                Color visibleTextColor = choice.currentText.color;
                visibleTextColor.a = 0f;
                choice.currentText.color = visibleTextColor;
                choice.currentText.enabled = true;
                choice.currentText.raycastTarget = false;
                return;
            }

            Image previewImage = EnsureChoicePreviewImage(choice, previewObjectName);
            if (previewImage == null)
            {
                return;
            }

            string previewPath;
            Sprite sprite = null;
            if (previewResolver != null && previewResolver(selectedIndex, out previewPath))
            {
                sprite = LoadPreviewSprite(previewPath);
            }

            previewImage.sprite = sprite;
            previewImage.enabled = sprite != null;

            Color currentTextColor = choice.currentText.color;
            currentTextColor.a = sprite != null ? 0f : 1f;
            choice.currentText.color = currentTextColor;
            choice.currentText.enabled = true;
            choice.currentText.raycastTarget = false;
        }

        private static Image EnsureChoicePreviewImage(uGUI_Choice choice, string previewObjectName)
        {
            RectTransform choiceRectTransform = choice.transform as RectTransform;
            if (choiceRectTransform == null)
            {
                return null;
            }

            Transform existingTransform = choiceRectTransform.Find(previewObjectName);
            Image previewImage = existingTransform != null ? existingTransform.GetComponent<Image>() : null;
            if (previewImage == null)
            {
                GameObject previewObject = new GameObject(previewObjectName, typeof(RectTransform), typeof(Image));
                previewObject.transform.SetParent(choiceRectTransform, false);
                previewImage = previewObject.GetComponent<Image>();
            }

            RectTransform rectTransform = previewImage.rectTransform;
            bool isHotbarPreview = string.Equals(previewObjectName, HotbarPreviewImageObjectName, StringComparison.Ordinal);

            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = new Vector2(0f, 0f);
            rectTransform.sizeDelta = isHotbarPreview ? new Vector2(300f, 74f) : new Vector2(112f, 74f);
            rectTransform.localScale = Vector3.one;

            previewImage.preserveAspect = true;
            previewImage.raycastTarget = false;
            previewImage.color = Color.white;
            previewImage.transform.SetSiblingIndex(0);
            return previewImage;
        }

        private static void ConfigureChoiceWidgetLayout(uGUI_Choice choice)
        {
            if (choice == null)
            {
                return;
            }

            ConfigureChoiceValueText(choice, fontSize: 26, alignment: TextAnchor.MiddleCenter);

            if (choice.previousButton != null)
            {
                RectTransform previousRect = choice.previousButton.transform as RectTransform;
                if (previousRect != null)
                {
                    previousRect.anchorMin = new Vector2(0f, 0.5f);
                    previousRect.anchorMax = new Vector2(0f, 0.5f);
                    previousRect.pivot = new Vector2(0f, 0.5f);
                    previousRect.anchoredPosition = new Vector2(0f, 0f);
                    previousRect.sizeDelta = new Vector2(58f, 72f);
                    previousRect.localScale = Vector3.one;
                }
            }

            if (choice.nextButton != null)
            {
                RectTransform nextRect = choice.nextButton.transform as RectTransform;
                if (nextRect != null)
                {
                    nextRect.anchorMin = new Vector2(1f, 0.5f);
                    nextRect.anchorMax = new Vector2(1f, 0.5f);
                    nextRect.pivot = new Vector2(1f, 0.5f);
                    nextRect.anchoredPosition = new Vector2(0f, 0f);
                    nextRect.sizeDelta = new Vector2(58f, 72f);
                    nextRect.localScale = Vector3.one;
                }
            }
        }

        private static void ConfigureChoiceValueText(uGUI_Choice choice, int fontSize, TextAnchor alignment)
        {
            if (choice == null || choice.currentText == null)
            {
                return;
            }

            RectTransform currentTextRect = choice.currentText.transform as RectTransform;
            if (currentTextRect != null)
            {
                currentTextRect.anchorMin = new Vector2(0f, 0f);
                currentTextRect.anchorMax = new Vector2(1f, 1f);
                currentTextRect.pivot = new Vector2(0.5f, 0.5f);
                currentTextRect.offsetMin = new Vector2(76f, 0f);
                currentTextRect.offsetMax = new Vector2(-76f, 0f);
                currentTextRect.localScale = Vector3.one;
            }

            choice.currentText.fontSize = fontSize;
            choice.currentText.alignment = alignment;
            choice.currentText.resizeTextForBestFit = false;
            choice.currentText.horizontalOverflow = HorizontalWrapMode.Overflow;
            choice.currentText.verticalOverflow = VerticalWrapMode.Truncate;
            choice.currentText.raycastTarget = false;
        }

        private static void ApplyStandaloneTextChoiceDisplay(uGUI_Choice choice, Text valueText, string[] options, int selectedIndex)
        {
            if (choice == null || options == null || options.Length == 0)
            {
                return;
            }

            int clampedIndex = ClampChoiceIndex(selectedIndex, options);
            string displayValue = options[clampedIndex];

            if (choice.currentText != null)
            {
                choice.currentText.text = displayValue;
                Color hiddenColor = choice.currentText.color;
                hiddenColor.a = 0f;
                choice.currentText.color = hiddenColor;
                choice.currentText.enabled = true;
            }

            if (valueText != null)
            {
                valueText.text = displayValue;
                Color visibleColor = valueText.color;
                visibleColor.a = 1f;
                valueText.color = visibleColor;
                valueText.enabled = true;
                valueText.gameObject.SetActive(true);
                valueText.transform.SetAsLastSibling();
            }
        }

        private static Text EnsureStandaloneChoiceValueText(GameObject rowObject, uGUI_Choice choice, string objectName)
        {
            if (rowObject == null || choice == null || choice.currentText == null || string.IsNullOrEmpty(objectName))
            {
                return null;
            }

            Transform existingTransform = rowObject.transform.Find(objectName);
            Text valueText = existingTransform != null ? existingTransform.GetComponent<Text>() : null;
            if (valueText == null)
            {
                GameObject valueObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
                valueObject.transform.SetParent(rowObject.transform, false);
                valueText = valueObject.GetComponent<Text>();
            }

            RectTransform valueRect = valueText.transform as RectTransform;
            if (valueRect != null)
            {
                valueRect.anchorMin = new Vector2(0f, 0f);
                valueRect.anchorMax = new Vector2(1f, 1f);
                valueRect.pivot = new Vector2(0.5f, 0.5f);
                valueRect.offsetMin = new Vector2(396f, 0f);
                valueRect.offsetMax = new Vector2(-110f, 0f);
                valueRect.localScale = Vector3.one;
            }

            valueText.font = choice.currentText.font;
            valueText.material = choice.currentText.material;
            valueText.fontStyle = choice.currentText.fontStyle;
            valueText.fontSize = 26;
            valueText.alignment = TextAnchor.MiddleCenter;
            valueText.resizeTextForBestFit = false;
            valueText.horizontalOverflow = HorizontalWrapMode.Overflow;
            valueText.verticalOverflow = VerticalWrapMode.Truncate;
            valueText.supportRichText = choice.currentText.supportRichText;
            valueText.raycastTarget = false;
            valueText.enabled = true;
            valueText.gameObject.SetActive(true);
            Color valueColor = choice.currentText.color;
            valueColor.a = 1f;
            valueText.color = valueColor;
            valueText.transform.SetAsLastSibling();
            return valueText;
        }

        private static void RemoveChoicePreview(uGUI_Choice choice, string previewObjectName)
        {
            if (choice == null)
            {
                return;
            }

            RectTransform choiceRectTransform = choice.transform as RectTransform;
            if (choiceRectTransform == null)
            {
                return;
            }

            Transform existingTransform = choiceRectTransform.Find(previewObjectName);
            if (existingTransform != null)
            {
                UnityEngine.Object.Destroy(existingTransform.gameObject);
            }
        }

        private static void RemoveOtherPreviewImages(uGUI_Choice choice, string keepPreviewObjectName)
        {
            if (choice == null)
            {
                return;
            }

            RectTransform choiceRectTransform = choice.transform as RectTransform;
            if (choiceRectTransform == null)
            {
                return;
            }

            Image[] images = choiceRectTransform.GetComponentsInChildren<Image>(true);
            for (int i = 0; i < images.Length; i++)
            {
                Image image = images[i];
                if (image == null)
                {
                    continue;
                }

                string objectName = image.gameObject.name ?? string.Empty;
                if (!objectName.StartsWith("ModPractice", StringComparison.Ordinal))
                {
                    continue;
                }

                if (string.Equals(objectName, keepPreviewObjectName, StringComparison.Ordinal))
                {
                    continue;
                }

                UnityEngine.Object.Destroy(image.gameObject);
            }
        }

        private static Sprite LoadPreviewSprite(string previewPath)
        {
            if (string.IsNullOrEmpty(previewPath) || !File.Exists(previewPath))
            {
                return null;
            }

            Sprite sprite;
            if (PreviewSprites.TryGetValue(previewPath, out sprite))
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

            PreviewSprites[previewPath] = sprite;
            return sprite;
        }

        private static string[] BuildHotbarOptionLabels()
        {
            string[] options = new string[ModPracticeHotbarOptions.LayoutCountValue];
            for (int i = 0; i < options.Length; i++)
            {
                options[i] = ModPracticeHotbarOptions.GetDisplayName(i);
            }

            return options;
        }

        private static string[] BuildHealthOptionLabels()
        {
            string[] options = new string[ModPracticeHealthOptions.Count];
            for (int i = 0; i < options.Length; i++)
            {
                options[i] = ModPracticeHealthOptions.GetDisplayName(i);
            }

            return options;
        }

        private static string[] BuildTimeOfDayOptionLabels()
        {
            string[] options = new string[ModPracticeTimeOfDayOptions.Count];
            for (int i = 0; i < options.Length; i++)
            {
                options[i] = ModPracticeTimeOfDayOptions.GetDisplayName(i);
            }

            return options;
        }

        private static int ClampChoiceIndex(int selectedIndex, string[] options)
        {
            if (options == null || options.Length <= 0)
            {
                return 0;
            }

            if (selectedIndex < 0)
            {
                return 0;
            }

            if (selectedIndex >= options.Length)
            {
                return options.Length - 1;
            }

            return selectedIndex;
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

            Text labelText = tabTransform.GetComponentInChildren<Text>(true);
            if (labelText == null)
            {
                return;
            }

            labelText.text = label;
            TranslationLiveUpdate translation = labelText.GetComponent<TranslationLiveUpdate>();
            if (translation != null)
            {
                translation.translationKey = label;
            }

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

        private delegate bool TryGetPreviewPathDelegate(int selectedIndex, out string previewPath);
    }
}
