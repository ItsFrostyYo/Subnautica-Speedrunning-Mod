using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SubnauticaSpeedrunningRanked.Runtime.Ui
{
    internal static class RankedMainMenuRuntimeHost
    {
        private const string RankedClientWatermark = "Speedrunning Ranked Client [Sep-2018 61056]";
        private const int WatermarkFontSize = 18;
        private const int QueueButtonFontSize = 34;
        private const int PanelTitleFontSize = 18;
        private const int DisabledQueueButtonFontSize = 19;
        private const float ComingSoonRowAlpha = 0.42f;
        private const float MenuPatchRetryIntervalSeconds = 0.25f;
        private const float WatermarkScanIntervalSeconds = 2f;
        private const float PersistentWatermarkRetryIntervalSeconds = 2f;
        private const string FallbackWatermarkRootName = "SubnauticaSpeedrunningRanked.FallbackWatermark";

        private static bool _installed;
        private static int _currentMenuInstanceId;
        private static bool _menuSummaryLogged;
        private static bool _menuSceneActive;
        private static bool _homePanelSuppressed;
        private static bool _leftMenuPatched;
        private static bool _rankedQueuePatched;
        private static float _nextMenuPatchAttemptAt;
        private static float _nextWatermarkScanAt;
        private static float _nextPersistentWatermarkRetryAt;
        private static RankedUiRuntimeBehaviour _sceneRuntimeBehaviour;
        private static RankedUiRuntimeBehaviour _persistentRuntimeBehaviour;
        private static GameObject _fallbackWatermarkRoot;
        private static Text _fallbackWatermarkText;

        public static void Install(RuntimeContext context)
        {
            if (_installed)
            {
                return;
            }

            SceneManager.sceneLoaded += OnSceneLoaded;
            _installed = true;
            RankedLog.Info("Ranked UI host installed.");
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            _sceneRuntimeBehaviour = null;
            TryAttachPersistentRuntimeBehaviour();
            AttachSceneRuntimeBehaviour(scene);
            _fallbackWatermarkRoot = null;
            _fallbackWatermarkText = null;
            _nextWatermarkScanAt = 0f;
            _nextPersistentWatermarkRetryAt = 0f;
            _menuSceneActive = string.Equals(scene.name, "XMenu", StringComparison.Ordinal);

            if (_menuSceneActive)
            {
                ResetMenuState();
                _nextMenuPatchAttemptAt = 0f;
                RankedLog.Info("Ranked UI detected XMenu scene load.");
            }
        }

        private static void OnRuntimeUpdate()
        {
            TryAttachPersistentRuntimeBehaviour();
            RankedOverlayRuntime.SetWatermark(RankedClientWatermark, true);

            if (ShouldRefreshWatermarkPresentation() && Time.unscaledTime >= _nextWatermarkScanAt)
            {
                RefreshWatermarkPresentation();
                _nextWatermarkScanAt = Time.unscaledTime + WatermarkScanIntervalSeconds;
            }

            if (!_menuSceneActive)
            {
                return;
            }

            if (Time.unscaledTime >= _nextMenuPatchAttemptAt)
            {
                TrySuppressMainMenuHomePanel();
                _nextMenuPatchAttemptAt = Time.unscaledTime + MenuPatchRetryIntervalSeconds;
            }
        }

        private static bool ShouldRefreshWatermarkPresentation()
        {
            if (_menuSceneActive)
            {
                return true;
            }

            return Player.main == null || !string.Equals(SceneManager.GetActiveScene().name, "Main", StringComparison.OrdinalIgnoreCase);
        }

        private static void TrySuppressMainMenuHomePanel()
        {
            if (uGUI_MainMenu.main == null || MainMenuRightSide.main == null)
            {
                return;
            }

            uGUI_MainMenu menu = uGUI_MainMenu.main;
            MainMenuRightSide rightSide = MainMenuRightSide.main;
            GameObject homeGroup = rightSide.homeGroup;
            if (homeGroup == null)
            {
                if (!_menuSummaryLogged)
                {
                    RankedLog.Warn("Ranked UI could not find MainMenuRightSide.homeGroup.");
                    _menuSummaryLogged = true;
                }

                return;
            }

            int menuInstanceId = menu.GetInstanceID();
            if (_currentMenuInstanceId != menuInstanceId)
            {
                ResetMenuState();
                _currentMenuInstanceId = menuInstanceId;
            }

            if (!_menuSummaryLogged)
            {
                LogMainMenuHomeSummary(homeGroup);
                _menuSummaryLogged = true;
            }
            
            EnsureRankedMenuPatched(menu, rightSide);

            if (homeGroup.activeSelf || !_homePanelSuppressed)
            {
                homeGroup.SetActive(false);
                HideAllRightSideGroupsExceptHome(rightSide);
                _homePanelSuppressed = true;
                RestorePrimaryMenuNavigation(menu);
                RankedLog.Info("Suppressed main menu right-side home panel.");
            }
        }

        private static void RestorePrimaryMenuNavigation(uGUI_MainMenu menu)
        {
            if (menu.primaryOptions == null)
            {
                return;
            }

            menu.ShowPrimaryOptions(true);
            if (GamepadInputModule.current != null)
            {
                GamepadInputModule.current.SetCurrentGrid(menu.primaryOptions);
            }

            menu.primaryOptions.DeselectItem();
            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(null);
            }
        }

        private static void LogMainMenuHomeSummary(GameObject homeGroup)
        {
            int storeButtons = CountStoreButtons(homeGroup);
            int newsletterWidgets = CountNewsletterWidgets(homeGroup);
            int comingSoonMatches = CountTextsContaining(homeGroup, "coming soon");
            int readMoreMatches = CountTextsContaining(homeGroup, "read more");
            RankedLog.Info(
                "Main menu home group detected: name='" + homeGroup.name +
                "', storeButtons=" + storeButtons +
                ", newsletterWidgets=" + newsletterWidgets +
                ", comingSoonTextMatches=" + comingSoonMatches +
                ", readMoreTextMatches=" + readMoreMatches + ".");
        }

        private static int CountStoreButtons(GameObject root)
        {
            int count = 0;
            Button[] buttons = root.GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                Button button = buttons[i];
                if (button == null)
                {
                    continue;
                }

                if (ButtonCallsMethod(button, "OnButtonStore") || HasText(button.gameObject, "Subnautica Store"))
                {
                    count++;
                }
            }

            return count;
        }

        private static int CountNewsletterWidgets(GameObject root)
        {
            int count = 0;
            count += root.GetComponentsInChildren<MainMenuEmailHandler>(true).Length;
            count += root.GetComponentsInChildren<MainMenuEmailCollector>(true).Length;
            count += root.GetComponentsInChildren<EmailBox>(true).Length;
            return count;
        }

        private static int CountTextsContaining(GameObject root, string value)
        {
            int count = 0;
            Text[] texts = root.GetComponentsInChildren<Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                Text text = texts[i];
                if (text == null || string.IsNullOrEmpty(text.text))
                {
                    continue;
                }

                if (text.text.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    count++;
                }
            }

            return count;
        }

        private static bool ButtonCallsMethod(Button button, string methodName)
        {
            if (button == null || button.onClick == null)
            {
                return false;
            }

            int eventCount = button.onClick.GetPersistentEventCount();
            for (int i = 0; i < eventCount; i++)
            {
                string currentMethod = button.onClick.GetPersistentMethodName(i);
                if (string.Equals(currentMethod, methodName, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasText(GameObject root, string value)
        {
            Text[] texts = root.GetComponentsInChildren<Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                Text text = texts[i];
                if (text == null || string.IsNullOrEmpty(text.text))
                {
                    continue;
                }

                if (text.text.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static void ResetMenuState()
        {
            _currentMenuInstanceId = 0;
            _menuSummaryLogged = false;
            _homePanelSuppressed = false;
            _leftMenuPatched = false;
            _rankedQueuePatched = false;
        }

        private static void EnsureRankedMenuPatched(uGUI_MainMenu menu, MainMenuRightSide rightSide)
        {
            if (!_leftMenuPatched)
            {
                PatchPrimaryOptions(menu, rightSide);
                _leftMenuPatched = true;
            }
            else
            {
                SyncPrimaryOptionLabels(menu);
            }

            if (!_rankedQueuePatched)
            {
                EnsureRankedQueueGroup(rightSide);
                _rankedQueuePatched = true;
            }
            else
            {
                SyncRankedQueueLabels(rightSide);
            }

            SyncSingleplayerPanelTitle(rightSide);
        }

        private static void PatchPrimaryOptions(uGUI_MainMenu menu, MainMenuRightSide rightSide)
        {
            if (menu.primaryOptions == null)
            {
                return;
            }

            Transform playTransform = FindDescendantByName(menu.primaryOptions.transform, "ButtonPlay");
            Button playButton = playTransform != null ? playTransform.GetComponent<Button>() : null;
            if (playButton == null)
            {
                RankedLog.Warn("Ranked UI could not find ButtonPlay.");
                return;
            }

            Transform menuButtons = playButton.transform.parent;
            SetButtonLabel(playButton.gameObject, "Singleplayer");

            Transform rankedTransform = menuButtons.Find("ButtonRanked");
            if (rankedTransform == null)
            {
                GameObject rankedObject = UnityEngine.Object.Instantiate(playButton.gameObject, menuButtons, false);
                rankedObject.name = "ButtonRanked";
                rankedObject.transform.SetSiblingIndex(playButton.transform.GetSiblingIndex());
                SetButtonLabel(rankedObject, "Ranked");

                Button rankedButton = rankedObject.GetComponent<Button>();
                if (rankedButton != null)
                {
                    rankedButton.onClick = new Button.ButtonClickedEvent();
                    rankedButton.onClick.AddListener(new UnityAction(delegate
                    {
                        OpenRankedQueue(menu, rightSide, rankedButton);
                    }));
                }

                ResetMainMenuButtonVisualState(rankedObject);
                RankedLog.Info("Inserted Ranked button above Singleplayer.");
            }
            else
            {
                SetButtonLabel(rankedTransform.gameObject, "Ranked");
            }
        }

        private static void EnsureRankedQueueGroup(MainMenuRightSide rightSide)
        {
            GameObject rankedGroup = FindGroup(rightSide, "RankedQueue");
            if (rankedGroup == null)
            {
                GameObject savedGamesGroup = FindGroup(rightSide, "SavedGames");
                if (savedGamesGroup == null)
                {
                    RankedLog.Warn("Ranked UI could not find SavedGames group to clone.");
                    return;
                }

                rankedGroup = UnityEngine.Object.Instantiate(savedGamesGroup, savedGamesGroup.transform.parent, false);
                rankedGroup.name = "RankedQueue";
                rankedGroup.transform.SetSiblingIndex(savedGamesGroup.transform.GetSiblingIndex() + 1);
                rankedGroup.SetActive(false);
                rightSide.groups.Add(rankedGroup);
            }

            DisableSavedGameBehaviors(rankedGroup);
            ConfigureRankedQueueGroup(rankedGroup);
        }

        private static void ConfigureRankedQueueGroup(GameObject rankedGroup)
        {
            Transform content = rankedGroup.transform.Find("Scroll View/Viewport/SavedGameAreaContent");
            if (content == null)
            {
                RankedLog.Warn("Ranked UI could not find SavedGameAreaContent in RankedQueue.");
                return;
            }

            Transform template = content.Find("NewGame");
            if (template == null)
            {
                RankedLog.Warn("Ranked UI could not find NewGame template row in RankedQueue.");
                return;
            }

            List<GameObject> rowsToDestroy = new List<GameObject>();
            int childCount = content.childCount;
            for (int i = 0; i < childCount; i++)
            {
                Transform child = content.GetChild(i);
                if (child != template)
                {
                    rowsToDestroy.Add(child.gameObject);
                }
            }

            for (int i = 0; i < rowsToDestroy.Count; i++)
            {
                rowsToDestroy[i].SetActive(false);
                UnityEngine.Object.Destroy(rowsToDestroy[i]);
            }

            QueueRowDefinition[] rows = new QueueRowDefinition[]
            {
                new QueueRowDefinition("Queue Survival", isInteractable: true, QueueButtonFontSize),
                new QueueRowDefinition("Queue Creative", isInteractable: true, QueueButtonFontSize),
                new QueueRowDefinition("Hardcore\nComing in Future Update", isInteractable: false, DisabledQueueButtonFontSize)
            };

            template.gameObject.SetActive(true);
            template.name = "NewGame";
            template.SetSiblingIndex(0);
            ConfigureQueueRow(template.gameObject, rows[0]);

            for (int i = 1; i < rows.Length; i++)
            {
                GameObject row = UnityEngine.Object.Instantiate(template.gameObject, content, false);
                row.name = "NewGame";
                row.transform.SetSiblingIndex(i);
                ConfigureQueueRow(row, rows[i]);
            }

            SetPanelTitle(rankedGroup, "Queue Ranked Matches");
            RankedLog.Info("Configured RankedQueue group with " + rows.Length + " queue buttons.");
        }

        private static void ConfigureQueueRow(GameObject row, QueueRowDefinition definition)
        {
            if (row == null)
            {
                return;
            }

            Button button = row.GetComponentInChildren<Button>(true);
            if (button == null)
            {
                RankedLog.Warn("Ranked UI could not find queue button for label " + definition.Label + ".");
                return;
            }

            SetButtonLabel(button.gameObject, definition.Label);
            SetButtonLabelFontSize(button.gameObject, definition.FontSize);
            button.onClick = new Button.ButtonClickedEvent();
            if (definition.IsInteractable)
            {
                button.onClick.AddListener(new UnityAction(delegate
                {
                    RankedLog.Info(definition.Label + " pressed.");
                }));
            }

            button.interactable = definition.IsInteractable;
            ResetMainMenuButtonVisualState(row);
            ApplyQueueRowPresentation(row, definition);
        }

        private static void DisableSavedGameBehaviors(GameObject rankedGroup)
        {
            if (rankedGroup == null)
            {
                return;
            }

            MainMenuLoadPanel[] loadPanels = rankedGroup.GetComponentsInChildren<MainMenuLoadPanel>(true);
            for (int i = 0; i < loadPanels.Length; i++)
            {
                MainMenuLoadPanel loadPanel = loadPanels[i];
                if (loadPanel != null)
                {
                    loadPanel.enabled = false;
                    UnityEngine.Object.Destroy(loadPanel);
                }
            }

            MainMenuLoadScroll[] loadScrolls = rankedGroup.GetComponentsInChildren<MainMenuLoadScroll>(true);
            for (int i = 0; i < loadScrolls.Length; i++)
            {
                MainMenuLoadScroll loadScroll = loadScrolls[i];
                if (loadScroll != null)
                {
                    loadScroll.enabled = false;
                    UnityEngine.Object.Destroy(loadScroll);
                }
            }
        }

        private static GameObject FindGroup(MainMenuRightSide rightSide, string name)
        {
            for (int i = 0; i < rightSide.groups.Count; i++)
            {
                GameObject group = rightSide.groups[i];
                if (group != null && string.Equals(group.name, name, StringComparison.Ordinal))
                {
                    return group;
                }
            }

            return null;
        }

        private static Transform FindDescendantByName(Transform root, string name)
        {
            if (root == null)
            {
                return null;
            }

            if (string.Equals(root.name, name, StringComparison.Ordinal))
            {
                return root;
            }

            int childCount = root.childCount;
            for (int i = 0; i < childCount; i++)
            {
                Transform result = FindDescendantByName(root.GetChild(i), name);
                if (result != null)
                {
                    return result;
                }
            }

            return null;
        }

        private static void OpenRankedQueue(uGUI_MainMenu menu, MainMenuRightSide rightSide, Button rankedButton)
        {
            if (rightSide == null)
            {
                return;
            }

            rightSide.OpenGroup("RankedQueue");
            GameObject rankedGroup = FindGroup(rightSide, "RankedQueue");
            if (rankedGroup == null)
            {
                return;
            }

            uGUI_INavigableIconGrid grid = rankedGroup.GetComponentInChildren<uGUI_INavigableIconGrid>();
            if (grid != null)
            {
                grid.DeselectItem();
            }

            if (menu != null && menu.primaryOptions != null && rankedButton != null)
            {
                menu.primaryOptions.SelectItem(rankedButton);
            }

            if (EventSystem.current != null && rankedButton != null)
            {
                EventSystem.current.SetSelectedGameObject(rankedButton.gameObject);
            }
        }

        private static void SetButtonLabel(GameObject root, string textValue)
        {
            if (root == null)
            {
                return;
            }

            uGUI_Text[] localizers = root.GetComponentsInChildren<uGUI_Text>(true);
            for (int i = 0; i < localizers.Length; i++)
            {
                uGUI_Text localizer = localizers[i];
                if (localizer != null)
                {
                    localizer.SetText(textValue, false);
                    return;
                }
            }

            Text[] texts = root.GetComponentsInChildren<Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                Text text = texts[i];
                if (text != null)
                {
                    text.text = textValue;
                    return;
                }
            }
        }

        private static void SyncPrimaryOptionLabels(uGUI_MainMenu menu)
        {
            if (menu == null || menu.primaryOptions == null)
            {
                return;
            }

            Transform playTransform = FindDescendantByName(menu.primaryOptions.transform, "ButtonPlay");
            if (playTransform != null)
            {
                SetButtonLabel(playTransform.gameObject, "Singleplayer");
            }

            Transform rankedTransform = FindDescendantByName(menu.primaryOptions.transform, "ButtonRanked");
            if (rankedTransform != null)
            {
                SetButtonLabel(rankedTransform.gameObject, "Ranked");
            }
        }

        private static void SyncRankedQueueLabels(MainMenuRightSide rightSide)
        {
            GameObject rankedGroup = FindGroup(rightSide, "RankedQueue");
            if (rankedGroup == null)
            {
                return;
            }

            Transform content = rankedGroup.transform.Find("Scroll View/Viewport/SavedGameAreaContent");
            if (content == null)
            {
                return;
            }

            QueueRowDefinition[] rows = new QueueRowDefinition[]
            {
                new QueueRowDefinition("Queue Survival", isInteractable: true, QueueButtonFontSize),
                new QueueRowDefinition("Queue Creative", isInteractable: true, QueueButtonFontSize),
                new QueueRowDefinition("Hardcore\nComing in Future Update", isInteractable: false, DisabledQueueButtonFontSize)
            };

            int rowCount = Math.Min(rows.Length, content.childCount);
            for (int i = 0; i < rowCount; i++)
            {
                GameObject row = content.GetChild(i).gameObject;
                Button button = row.GetComponentInChildren<Button>(true);
                if (button != null)
                {
                    SetButtonLabel(button.gameObject, rows[i].Label);
                    SetButtonLabelFontSize(button.gameObject, rows[i].FontSize);
                    button.interactable = rows[i].IsInteractable;
                }

                ApplyQueueRowPresentation(row, rows[i]);
            }

            SetPanelTitle(rankedGroup, "Queue Ranked Matches");
        }

        private static void SuppressBuiltInWatermarks()
        {
            MainMenuChangeset[] changesets = UnityEngine.Object.FindObjectsOfType<MainMenuChangeset>();
            for (int i = 0; i < changesets.Length; i++)
            {
                MainMenuChangeset changeset = changesets[i];
                if (changeset != null && changeset.gameObject.activeSelf)
                {
                    changeset.gameObject.SetActive(false);
                }
            }
        }

        private static void RefreshWatermarkPresentation()
        {
            uGUI_BuildWatermark[] watermarks = UnityEngine.Object.FindObjectsOfType<uGUI_BuildWatermark>();
            for (int i = 0; i < watermarks.Length; i++)
            {
                uGUI_BuildWatermark watermark = watermarks[i];
                if (watermark != null && watermark.gameObject.activeSelf)
                {
                    watermark.gameObject.SetActive(false);
                }
            }

            SuppressBuiltInWatermarks();
        }

        private static void SyncSingleplayerPanelTitle(MainMenuRightSide rightSide)
        {
            if (rightSide == null)
            {
                return;
            }

            GameObject savedGamesGroup = FindGroup(rightSide, "SavedGames");
            if (savedGamesGroup != null)
            {
                SetPanelTitle(savedGamesGroup, "Play Singleplayer");
            }
        }

        private static void SetPanelTitle(GameObject panelGroup, string title)
        {
            if (panelGroup == null)
            {
                return;
            }

            EnsureCustomPanelTitle(panelGroup, title);
            HideOriginalPanelTitles(panelGroup);
        }

        private static void SetButtonLabelFontSize(GameObject root, int fontSize)
        {
            if (root == null)
            {
                return;
            }

            Text[] texts = root.GetComponentsInChildren<Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                Text text = texts[i];
                if (text == null)
                {
                    continue;
                }

                text.fontSize = fontSize;
            }
        }

        private static void ApplyQueueRowPresentation(GameObject row, QueueRowDefinition definition)
        {
            if (row == null)
            {
                return;
            }

            SetAlpha(row, definition.IsInteractable ? 1f : ComingSoonRowAlpha);

            Text[] texts = row.GetComponentsInChildren<Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                Text text = texts[i];
                if (text == null)
                {
                    continue;
                }

                text.fontSize = definition.FontSize;
                text.color = definition.IsInteractable ? Color.white : new Color(0.9f, 0.9f, 0.9f, 0.95f);
                text.alignment = TextAnchor.MiddleCenter;
            }
        }

        private static void EnsureCustomPanelTitle(GameObject panelGroup, string title)
        {
            Transform existingTransform = panelGroup.transform.Find("RankedCustomPanelTitle");
            Text titleText = existingTransform != null ? existingTransform.GetComponent<Text>() : null;
            if (titleText == null)
            {
                Text template = panelGroup.GetComponentInChildren<Text>(true);
                if (template == null)
                {
                    return;
                }

                GameObject titleObject = UnityEngine.Object.Instantiate(template.gameObject, panelGroup.transform, false);
                titleObject.name = "RankedCustomPanelTitle";

                uGUI_Text localizer = titleObject.GetComponent<uGUI_Text>();
                if (localizer != null)
                {
                    UnityEngine.Object.Destroy(localizer);
                }

                titleText = titleObject.GetComponent<Text>();
                if (titleText == null)
                {
                    return;
                }
            }

            RectTransform rectTransform = titleText.rectTransform;
            rectTransform.SetParent(panelGroup.transform, false);
            rectTransform.anchorMin = new Vector2(0f, 1f);
            rectTransform.anchorMax = new Vector2(1f, 1f);
            rectTransform.pivot = new Vector2(0f, 1f);
            rectTransform.anchoredPosition = new Vector2(24f, -18f);
            rectTransform.sizeDelta = new Vector2(-48f, 46f);
            rectTransform.localScale = Vector3.one;

            titleText.text = title;
            titleText.fontSize = PanelTitleFontSize;
            titleText.alignment = TextAnchor.UpperLeft;
            titleText.horizontalOverflow = HorizontalWrapMode.Overflow;
            titleText.verticalOverflow = VerticalWrapMode.Truncate;
            titleText.raycastTarget = false;
            titleText.color = Color.white;
        }

        private static void HideOriginalPanelTitles(GameObject panelGroup)
        {
            Text[] texts = panelGroup.GetComponentsInChildren<Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                Text text = texts[i];
                if (text == null || string.Equals(text.gameObject.name, "RankedCustomPanelTitle", StringComparison.Ordinal))
                {
                    continue;
                }

                if (string.Equals(text.text, "Play", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(text.text, "Play Singleplayer", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(text.text, "Queue Ranked Matches", StringComparison.OrdinalIgnoreCase))
                {
                    text.gameObject.SetActive(false);
                }
            }
        }

        private static void HideAllRightSideGroupsExceptHome(MainMenuRightSide rightSide)
        {
            if (rightSide == null)
            {
                return;
            }

            for (int i = 0; i < rightSide.groups.Count; i++)
            {
                GameObject group = rightSide.groups[i];
                if (group == null || group == rightSide.homeGroup)
                {
                    continue;
                }

                group.SetActive(false);
            }
        }

        private static void SetAlpha(GameObject root, float alpha)
        {
            if (root == null)
            {
                return;
            }

            Graphic[] graphics = root.GetComponentsInChildren<Graphic>(true);
            for (int i = 0; i < graphics.Length; i++)
            {
                Graphic graphic = graphics[i];
                if (graphic == null)
                {
                    continue;
                }

                Color color = graphic.color;
                color.a = alpha;
                graphic.color = color;
            }
        }

        private static void AttachSceneRuntimeBehaviour(Scene scene)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                GameObject root = roots[i];
                if (root == null)
                {
                    continue;
                }

                _sceneRuntimeBehaviour = root.GetComponent<RankedUiRuntimeBehaviour>();
                if (_sceneRuntimeBehaviour == null)
                {
                    _sceneRuntimeBehaviour = root.AddComponent<RankedUiRuntimeBehaviour>();
                }

                if (_sceneRuntimeBehaviour != null)
                {
                    RankedLog.Info("Attached ranked UI runtime behaviour to scene root '" + root.name + "'.");
                    return;
                }
            }
        }

        private static void TryAttachPersistentRuntimeBehaviour()
        {
            if (_persistentRuntimeBehaviour != null)
            {
                return;
            }

            if (uGUI.main == null)
            {
                return;
            }

            GameObject root = uGUI.main.gameObject;
            _persistentRuntimeBehaviour = root.GetComponent<RankedUiRuntimeBehaviour>();
            if (_persistentRuntimeBehaviour == null)
            {
                _persistentRuntimeBehaviour = root.AddComponent<RankedUiRuntimeBehaviour>();
            }

            if (_persistentRuntimeBehaviour != null)
            {
                RankedLog.Info("Attached ranked UI runtime behaviour to persistent uGUI root.");
            }
        }

        private static void EnsurePersistentWatermark()
        {
            if (_fallbackWatermarkText != null)
            {
                return;
            }

            if (Time.unscaledTime < _nextPersistentWatermarkRetryAt)
            {
                return;
            }

            _nextPersistentWatermarkRetryAt = Time.unscaledTime + PersistentWatermarkRetryIntervalSeconds;

            Canvas canvas = RankedOverlayUiUtility.GetOrCreatePersistentOverlayCanvas();
            if (canvas == null)
            {
                return;
            }

            RankedLog.Info("Preparing watermark overlay on canvas '" + canvas.name + "'.");

            Transform existingRoot = canvas.transform.Find(FallbackWatermarkRootName);
            GameObject root = existingRoot != null ? existingRoot.gameObject : null;
            if (root == null)
            {
                root = new GameObject(FallbackWatermarkRootName, typeof(RectTransform));
            }

            root.transform.SetParent(canvas.transform, false);

            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(0f, 0f);
            rootRect.anchorMax = new Vector2(1f, 1f);
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;
            root.transform.SetAsLastSibling();

            Transform existingTextTransform = root.transform.Find("WatermarkText");
            GameObject textObject = existingTextTransform != null ? existingTextTransform.gameObject : null;
            if (textObject == null)
            {
                textObject = new GameObject("WatermarkText", typeof(RectTransform));
            }

            textObject.transform.SetParent(root.transform, false);

            Text text = textObject.GetComponent<Text>();
            if (text == null)
            {
                text = textObject.AddComponent<Text>();
            }

            RankedOverlayUiUtility.ApplyTemplate(
                text,
                RankedOverlayUiUtility.FindTemplateText(),
                WatermarkFontSize,
                TextAnchor.UpperRight);

            RectTransform rectTransform = text.rectTransform;
            rectTransform.anchorMin = new Vector2(1f, 1f);
            rectTransform.anchorMax = new Vector2(1f, 1f);
            rectTransform.pivot = new Vector2(1f, 1f);
            rectTransform.anchoredPosition = new Vector2(-12f, -12f);
            rectTransform.sizeDelta = new Vector2(560f, 24f);

            _fallbackWatermarkRoot = root;
            _fallbackWatermarkText = text;
            RefreshPersistentWatermark();
            RankedLog.Info("Created fallback watermark overlay under '" + canvas.name + "'.");
        }

        private static Font ResolveWatermarkFont()
        {
            uGUI_BuildWatermark watermark = UnityEngine.Object.FindObjectOfType<uGUI_BuildWatermark>();
            if (watermark != null)
            {
                Text text = watermark.GetComponent<Text>();
                if (text != null && text.font != null)
                {
                    return text.font;
                }
            }

            MainMenuChangeset changeset = UnityEngine.Object.FindObjectOfType<MainMenuChangeset>();
            if (changeset != null)
            {
                Text text = changeset.GetComponent<Text>();
                if (text != null && text.font != null)
                {
                    return text.font;
                }
            }

            return Resources.GetBuiltinResource(typeof(Font), "Arial.ttf") as Font;
        }

        private static void RefreshPersistentWatermark()
        {
            if (_fallbackWatermarkText == null)
            {
                return;
            }

            _fallbackWatermarkText.text = RankedClientWatermark;
            _fallbackWatermarkText.fontSize = WatermarkFontSize;

            if (_fallbackWatermarkRoot != null && !_fallbackWatermarkRoot.activeSelf)
            {
                _fallbackWatermarkRoot.SetActive(true);
            }
        }

        private static void ResetMainMenuButtonVisualState(GameObject root)
        {
            if (root == null)
            {
                return;
            }

            MainMenuOffsetAnimator[] animators = root.GetComponentsInChildren<MainMenuOffsetAnimator>(true);
            for (int i = 0; i < animators.Length; i++)
            {
                MainMenuOffsetAnimator animator = animators[i];
                if (animator == null || animator.target == null)
                {
                    continue;
                }

                animator.OnPointerExit(null);
                animator.target.localPosition -= animator.offset;
            }

            uGUI_BasicColorSwap[] colorSwaps = root.GetComponentsInChildren<uGUI_BasicColorSwap>(true);
            for (int i = 0; i < colorSwaps.Length; i++)
            {
                uGUI_BasicColorSwap colorSwap = colorSwaps[i];
                if (colorSwap != null)
                {
                    colorSwap.makeTextWhite();
                }
            }
        }

        private struct QueueRowDefinition
        {
            public QueueRowDefinition(string label, bool isInteractable, int fontSize)
            {
                Label = label;
                IsInteractable = isInteractable;
                FontSize = fontSize;
            }

            public string Label { get; private set; }

            public bool IsInteractable { get; private set; }

            public int FontSize { get; private set; }
        }

        private sealed class RankedUiRuntimeBehaviour : MonoBehaviour
        {
            private void Update()
            {
                if (_persistentRuntimeBehaviour == null || ReferenceEquals(_persistentRuntimeBehaviour, this))
                {
                    OnRuntimeUpdate();
                    return;
                }

                TryAttachPersistentRuntimeBehaviour();
            }
        }
    }
}
