#if STUDIO_UGUI
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if STUDIO_TMP
using TMPro;
#endif

namespace Studio.EditorTools
{
    // uGUI building blocks for builders (HUD, menus, popups). Layout only: behaviour lives in the game's UI asmdef.
    public static class StudioUiUtil
    {
        private const string InputSystemModule = "UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem";

        // Scale With Screen Size; match 0 = width (portrait phones), 1 = height (landscape), 0.5 = both.
        public static UnityEngine.Canvas Canvas(string name, Vector2 referenceResolution, float match = 0.5f, int sortingOrder = 0)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = LayerMask.NameToLayer("UI");
            var canvas = go.AddComponent<UnityEngine.Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = referenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = match;
            go.AddComponent<GraphicRaycaster>();
            EnsureEventSystem();
            return canvas;
        }

        // One per scene. Input System module when the package is in, else the legacy one (it throws under "Input System only").
        public static EventSystem EnsureEventSystem()
        {
            var existing = SceneManager.GetActiveScene().GetRootGameObjects()
                .Select(r => r.GetComponentInChildren<EventSystem>(true)).FirstOrDefault(e => e != null);
            if (existing != null) return existing;
            var go = new GameObject("EventSystem", typeof(EventSystem));
            var module = Type.GetType(InputSystemModule);
            if (module != null) go.AddComponent(module);
            else go.AddComponent<StandaloneInputModule>();
            return go.GetComponent<EventSystem>();
        }

        // Full-stretch child; the game's SafeArea component (ui.md § Safe area) resizes it at runtime for notches.
        public static RectTransform SafeArea(Component canvas) => Panel(canvas.transform, "SafeArea", Vector2.zero, Vector2.one);

        public static RectTransform Panel(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Color? color = null)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            if (color.HasValue)
            {
                var image = go.AddComponent<Image>();
                image.sprite = DefaultSprite;
                image.type = Image.Type.Sliced;
                image.color = color.Value;
            }

            return rt;
        }

        // Fixed-size element anchored at a point (0,1 = top-left … 0.5,0.5 = centre); pivot follows the anchor.
        public static RectTransform Place(RectTransform rt, Vector2 anchor, Vector2 size, Vector2 offset)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
            rt.sizeDelta = size;
            rt.anchoredPosition = offset;
            return rt;
        }

        public static Graphic Label(Transform parent, string name, string text, float size, TextAnchor align = TextAnchor.MiddleCenter)
        {
            var rt = Panel(parent, name, Vector2.zero, Vector2.one);
#if STUDIO_TMP
            EnsureTmpEssentials();
            var tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.alignment = ToTmp(align);
            tmp.raycastTarget = false;
            return tmp;
#else
            var legacy = rt.gameObject.AddComponent<Text>();
            legacy.text = text;
            legacy.fontSize = Mathf.RoundToInt(size);
            legacy.alignment = align;
            legacy.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            legacy.raycastTarget = false;
            return legacy;
#endif
        }

        public static UnityEngine.UI.Button Button(Transform parent, string name, string label, Vector2 size, Color color)
        {
            var rt = Panel(parent, name, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), color);
            rt.sizeDelta = size;
            var button = rt.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = rt.GetComponent<Image>();
            Label(rt, "Label", label, size.y * 0.45f);
            return button;
        }

        // Horizontal fill (health, timer, progress): set Image.fillAmount 0-1 from the game's UI code.
        public static Image Fill(Transform parent, string name, Color back, Color fill)
        {
            var bg = Panel(parent, name, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), back);
            var bar = Panel(bg, "Fill", Vector2.zero, Vector2.one, fill).GetComponent<Image>();
            bar.type = Image.Type.Filled;
            bar.fillMethod = Image.FillMethod.Horizontal;
            bar.fillAmount = 1f;
            return bar;
        }

        private static Sprite DefaultSprite => AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

#if STUDIO_TMP
        // Without TMP Essentials, TMP text has no font and Unity opens an import window mid-build.
        private static void EnsureTmpEssentials()
        {
            if (Resources.Load<TMP_Settings>("TMP Settings") != null) return;
            var package = new[] { "Packages/com.unity.ugui/Package Resources/TMP Essential Resources.unitypackage",
                    "Packages/com.unity.textmeshpro/Package Resources/TMP Essential Resources.unitypackage" }
                .FirstOrDefault(p => File.Exists(Path.GetFullPath(p)));
            if (package == null)
            {
                Debug.LogError("[StudioUiUtil] TMP Essential Resources not found: Window > TextMeshPro > Import TMP Essential Resources");
                return;
            }

            AssetDatabase.ImportPackage(package, false);
            AssetDatabase.Refresh();
        }

        private static TextAlignmentOptions ToTmp(TextAnchor a)
        {
            switch (a)
            {
                case TextAnchor.UpperLeft: return TextAlignmentOptions.TopLeft;
                case TextAnchor.UpperCenter: return TextAlignmentOptions.Top;
                case TextAnchor.UpperRight: return TextAlignmentOptions.TopRight;
                case TextAnchor.MiddleLeft: return TextAlignmentOptions.Left;
                case TextAnchor.MiddleRight: return TextAlignmentOptions.Right;
                case TextAnchor.LowerLeft: return TextAlignmentOptions.BottomLeft;
                case TextAnchor.LowerCenter: return TextAlignmentOptions.Bottom;
                case TextAnchor.LowerRight: return TextAlignmentOptions.BottomRight;
                default: return TextAlignmentOptions.Center;
            }
        }
#endif
    }
}
#endif
