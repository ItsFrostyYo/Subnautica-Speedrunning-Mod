using UnityEngine;
using UnityEngine.UI;

namespace SubnauticaSpeedrunningRanked.Runtime.Ui
{
    internal static class RankedOverlayUiUtility
    {
        private const string PersistentCanvasRootName = "SubnauticaSpeedrunningRanked.PersistentOverlayCanvas";
        private static Canvas _persistentCanvas;

        public static Canvas GetOrCreatePersistentOverlayCanvas()
        {
            if (_persistentCanvas != null)
            {
                return _persistentCanvas;
            }

            GameObject existingRoot = GameObject.Find(PersistentCanvasRootName);
            GameObject root = existingRoot;
            if (root == null)
            {
                root = new GameObject(
                    PersistentCanvasRootName,
                    typeof(RectTransform),
                    typeof(Canvas),
                    typeof(CanvasScaler),
                    typeof(GraphicRaycaster));
                Object.DontDestroyOnLoad(root);
            }

            Canvas canvas = root.GetComponent<Canvas>();
            if (canvas == null)
            {
                canvas = root.AddComponent<Canvas>();
            }

            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.pixelPerfect = false;
            canvas.sortingOrder = short.MaxValue;
            canvas.targetDisplay = 0;

            CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            if (scaler == null)
            {
                scaler = root.AddComponent<CanvasScaler>();
            }

            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            GraphicRaycaster raycaster = root.GetComponent<GraphicRaycaster>();
            if (raycaster == null)
            {
                raycaster = root.AddComponent<GraphicRaycaster>();
            }

            raycaster.enabled = false;

            RectTransform rectTransform = root.GetComponent<RectTransform>();
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;

            if (uGUI.main != null)
            {
                root.layer = uGUI.main.gameObject.layer;
            }

            _persistentCanvas = canvas;
            return _persistentCanvas;
        }

        public static Canvas FindPreferredCanvas()
        {
            if (uGUI.main != null && uGUI.main.screenCanvas != null)
            {
                return uGUI.main.screenCanvas;
            }

            Canvas[] canvases = Object.FindObjectsOfType<Canvas>();
            Canvas fallback = null;
            for (int i = 0; i < canvases.Length; i++)
            {
                Canvas canvas = canvases[i];
                if (canvas == null || !canvas.gameObject.activeInHierarchy)
                {
                    continue;
                }

                if (fallback == null)
                {
                    fallback = canvas;
                }

                if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
                {
                    return canvas;
                }
            }

            return fallback;
        }

        public static Text FindTemplateText()
        {
            uGUI_BuildWatermark[] watermarks = Object.FindObjectsOfType<uGUI_BuildWatermark>();
            for (int i = 0; i < watermarks.Length; i++)
            {
                uGUI_BuildWatermark watermark = watermarks[i];
                if (watermark == null)
                {
                    continue;
                }

                Text watermarkText = watermark.GetComponent<Text>();
                if (watermarkText != null)
                {
                    return watermarkText;
                }
            }

            MainMenuChangeset[] changesets = Object.FindObjectsOfType<MainMenuChangeset>();
            for (int i = 0; i < changesets.Length; i++)
            {
                MainMenuChangeset changeset = changesets[i];
                if (changeset == null)
                {
                    continue;
                }

                Text changesetText = changeset.GetComponent<Text>();
                if (changesetText != null)
                {
                    return changesetText;
                }
            }

            if (uGUI.main != null && uGUI.main.screenCanvas != null)
            {
                Text[] screenTexts = uGUI.main.screenCanvas.GetComponentsInChildren<Text>(true);
                for (int i = 0; i < screenTexts.Length; i++)
                {
                    Text screenText = screenTexts[i];
                    if (screenText != null)
                    {
                        return screenText;
                    }
                }
            }

            return Object.FindObjectOfType<Text>();
        }

        public static void ApplyTemplate(Text target, Text template, int fontSize, TextAnchor alignment)
        {
            if (target == null)
            {
                return;
            }

            if (template != null)
            {
                target.font = template.font;
                target.fontStyle = template.fontStyle;
                target.lineSpacing = template.lineSpacing;
                target.supportRichText = template.supportRichText;
                target.resizeTextForBestFit = template.resizeTextForBestFit;
                target.resizeTextMinSize = template.resizeTextMinSize;
                target.resizeTextMaxSize = template.resizeTextMaxSize;
                target.color = template.color;
                target.material = template.material;
            }
            else
            {
                target.font = Resources.GetBuiltinResource(typeof(Font), "Arial.ttf") as Font;
                target.fontStyle = FontStyle.Normal;
                target.lineSpacing = 1f;
                target.supportRichText = true;
                target.color = Color.white;
            }

            target.fontSize = fontSize;
            target.alignment = alignment;
            target.horizontalOverflow = HorizontalWrapMode.Overflow;
            target.verticalOverflow = VerticalWrapMode.Truncate;
            target.raycastTarget = false;

            ApplyEffects(target.gameObject, template);
        }

        private static void ApplyEffects(GameObject targetObject, Text template)
        {
            if (targetObject == null)
            {
                return;
            }

            Shadow existingShadow = targetObject.GetComponent<Shadow>();
            if (existingShadow != null)
            {
                Object.Destroy(existingShadow);
            }

            Outline existingOutline = targetObject.GetComponent<Outline>();
            if (existingOutline != null)
            {
                Object.Destroy(existingOutline);
            }

            if (template == null)
            {
                Shadow fallbackShadow = targetObject.AddComponent<Shadow>();
                fallbackShadow.effectColor = new Color(0f, 0f, 0f, 0.9f);
                fallbackShadow.effectDistance = new Vector2(2f, -2f);
                return;
            }

            Outline templateOutline = template.GetComponent<Outline>();
            if (templateOutline != null)
            {
                Outline outline = targetObject.AddComponent<Outline>();
                outline.effectColor = templateOutline.effectColor;
                outline.effectDistance = templateOutline.effectDistance;
                outline.useGraphicAlpha = templateOutline.useGraphicAlpha;
                return;
            }

            Shadow templateShadow = template.GetComponent<Shadow>();
            if (templateShadow != null)
            {
                Shadow shadow = targetObject.AddComponent<Shadow>();
                shadow.effectColor = templateShadow.effectColor;
                shadow.effectDistance = templateShadow.effectDistance;
                shadow.useGraphicAlpha = templateShadow.useGraphicAlpha;
                return;
            }

            Shadow defaultShadow = targetObject.AddComponent<Shadow>();
            defaultShadow.effectColor = new Color(0f, 0f, 0f, 0.9f);
            defaultShadow.effectDistance = new Vector2(2f, -2f);
        }
    }
}
