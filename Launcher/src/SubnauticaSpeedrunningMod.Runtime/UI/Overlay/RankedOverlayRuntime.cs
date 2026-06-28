using UnityEngine;
using UnityEngine.UI;

namespace SubnauticaSpeedrunningMod.Runtime.Ui
{
    internal static class ModOverlayRuntime
    {
        private const string OverlayRootName = "SubnauticaSpeedrunningMod.PersistentOverlay";
        private static OverlayBehaviour _behaviour;
        private static string _watermarkText = string.Empty;
        private static bool _watermarkVisible = true;
        private static string _timerText = "0.000";
        private static bool _timerVisible;
        private static string _runTitleText = string.Empty;
        private static string _runSubtitleText = string.Empty;
        private static bool _runStatusVisible;
        private static Color _runTitleColor = new Color(1f, 0.76f, 0.36f, 1f);
        private static Font _font;
        private static FontStyle _fontStyle = FontStyle.Normal;
        private static Color _textColor = Color.white;
        private static float _nextTemplateRefreshAt;
        private static GUIStyle _topRightStyle;
        private static GUIStyle _runTitleStyle;
        private static GUIStyle _runSubtitleStyle;

        public static void EnsureInstalled()
        {
            if (_behaviour != null)
            {
                return;
            }

            GameObject root = GameObject.Find(OverlayRootName);
            if (root == null)
            {
                root = new GameObject(OverlayRootName);
                Object.DontDestroyOnLoad(root);
            }

            _behaviour = root.GetComponent<OverlayBehaviour>();
            if (_behaviour == null)
            {
                _behaviour = root.AddComponent<OverlayBehaviour>();
            }
        }

        public static void SetWatermark(string text, bool visible)
        {
            EnsureInstalled();
            _watermarkText = text ?? string.Empty;
            _watermarkVisible = visible;
        }

        public static void SetTimer(string text, bool visible)
        {
            EnsureInstalled();
            _timerText = text ?? "0.000";
            _timerVisible = visible;
        }

        public static void SetRunStatus(string title, string subtitle, Color titleColor, bool visible)
        {
            EnsureInstalled();
            _runTitleText = title ?? string.Empty;
            _runSubtitleText = subtitle ?? string.Empty;
            _runTitleColor = titleColor;
            _runStatusVisible = visible;
        }

        private static void RefreshTemplateStyle()
        {
            if (Time.unscaledTime < _nextTemplateRefreshAt)
            {
                return;
            }

            _nextTemplateRefreshAt = Time.unscaledTime + 1f;

            Text template = ModOverlayUiUtility.FindTemplateText();
            if (template == null)
            {
                return;
            }

            if (template.font != null)
            {
                _font = template.font;
            }

            _fontStyle = template.fontStyle;
            _textColor = template.color;
            _topRightStyle = null;
            _runTitleStyle = null;
            _runSubtitleStyle = null;
        }

        private static void Draw()
        {
            RefreshTemplateStyle();

            if (_watermarkVisible && !string.IsNullOrEmpty(_watermarkText))
            {
                DrawTopRightLabel(_watermarkText, 18, 12f, _textColor);
            }

            if (_timerVisible && !string.IsNullOrEmpty(_timerText))
            {
                DrawTopRightLabel(_timerText, 42, 40f, _textColor);
            }

            if (_runStatusVisible && !string.IsNullOrEmpty(_runTitleText))
            {
                DrawRunStatus();
            }
        }

        private static void DrawTopRightLabel(string text, int fontSize, float top, Color color)
        {
            GUIStyle style = GetTopRightStyle(fontSize, color);

            Vector2 size = style.CalcSize(new GUIContent(text));
            Rect rect = new Rect(Screen.width - size.x - 12f, top, size.x + 2f, size.y + 4f);
            GUI.Label(rect, text, style);
        }

        private static void DrawRunStatus()
        {
            GUIStyle titleStyle = GetRunTitleStyle();
            GUIStyle subtitleStyle = GetRunSubtitleStyle();

            float left = 12f;
            float top = 12f;
            float width = 520f;
            float titleHeight = titleStyle.CalcHeight(new GUIContent(_runTitleText), width);
            GUI.Label(new Rect(left, top, width, titleHeight + 4f), _runTitleText, titleStyle);

            if (string.IsNullOrEmpty(_runSubtitleText))
            {
                return;
            }

            float subtitleTop = top + titleHeight + 2f;
            float subtitleHeight = subtitleStyle.CalcHeight(new GUIContent(_runSubtitleText), width);
            GUI.Label(new Rect(left, subtitleTop, width, subtitleHeight + 4f), _runSubtitleText, subtitleStyle);
        }

        private static GUIStyle GetTopRightStyle(int fontSize, Color color)
        {
            if (_topRightStyle == null)
            {
                _topRightStyle = CreateBaseStyle(TextAnchor.UpperRight);
            }

            _topRightStyle.fontSize = fontSize;
            _topRightStyle.normal.textColor = color;
            return _topRightStyle;
        }

        private static GUIStyle GetRunTitleStyle()
        {
            if (_runTitleStyle == null)
            {
                _runTitleStyle = CreateBaseStyle(TextAnchor.UpperLeft);
                _runTitleStyle.wordWrap = true;
                _runTitleStyle.fontSize = 28;
            }

            _runTitleStyle.normal.textColor = _runTitleColor;
            return _runTitleStyle;
        }

        private static GUIStyle GetRunSubtitleStyle()
        {
            if (_runSubtitleStyle == null)
            {
                _runSubtitleStyle = CreateBaseStyle(TextAnchor.UpperLeft);
                _runSubtitleStyle.wordWrap = true;
                _runSubtitleStyle.fontSize = 24;
            }

            _runSubtitleStyle.normal.textColor = _textColor;
            return _runSubtitleStyle;
        }

        private static GUIStyle CreateBaseStyle(TextAnchor alignment)
        {
            GUIStyle style = new GUIStyle(GUI.skin.label);
            style.alignment = alignment;
            style.fontStyle = _fontStyle;
            style.richText = false;
            style.clipping = TextClipping.Overflow;
            style.wordWrap = false;

            if (_font != null)
            {
                style.font = _font;
            }

            return style;
        }

        private sealed class OverlayBehaviour : MonoBehaviour
        {
            private void Awake()
            {
                Object.DontDestroyOnLoad(base.gameObject);
            }

            private void OnGUI()
            {
                GUI.depth = -10000;
                Draw();
            }
        }
    }
}
