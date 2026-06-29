using System;
using UnityEngine;
using UnityEngine.UI;

namespace SubnauticaSpeedrunningMod.Runtime.Ui
{
    internal static class ModOptionsPanelRuntime
    {
        private const string AccountTabLabel = "Account";
        private const string ModSettingsTabLabel = "Ranked Settings";
        private const string FutureUpdatePlaceholderText = "Coming in a Future Update";
        private const string AccountPlaceholderObjectName = "ModAccountPlaceholder";
        private const string ModSettingsPlaceholderObjectName = "ModSettingsPlaceholder";

        private static int _lastSeenOptionsPanelId;
        private static bool _loggedForCurrentPanel;

        public static void Install()
        {
            // The stable menu path patches the options panel live while it is open.
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

        internal static void PatchPanel(uGUI_OptionsPanel panel)
        {
            if (panel == null)
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
                ModLog.Info("Temporarily removed Account and Ranked Settings tabs from the main menu options panel.");
                _loggedForCurrentPanel = true;
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
