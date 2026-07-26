using UnityEngine;
using UnityEngine.UI;

namespace Rill.UI
{
    /// <summary>
    /// The whole interface is built in code from primitives: no prefabs, no atlases, no fonts to
    /// license. RILL's UI ghosts in on touch and gets out of the way, so there is very little of
    /// it to build — every frame is composed to survive a social feed with no HUD in it.
    /// </summary>
    public static class UIFactory
    {
        static Font _font;

        public static Font Font
        {
            get
            {
                if (_font == null)
                {
                    _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                    if (_font == null) _font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                }
                return _font;
            }
        }

        public static readonly Color Ink = new Color(0.98f, 0.97f, 0.94f, 0.96f);
        public static readonly Color InkDim = new Color(0.98f, 0.97f, 0.94f, 0.62f);
        public static readonly Color Panel = new Color(0.08f, 0.09f, 0.11f, 0.72f);
        public static readonly Color Accent = new Color(0.55f, 0.84f, 0.93f, 1f);

        public static Canvas CreateCanvas(string name, int sortOrder = 0)
        {
            var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortOrder;

            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            return canvas;
        }

        public static RectTransform Rect(GameObject go)
        {
            var rt = go.GetComponent<RectTransform>();
            if (rt == null) rt = go.AddComponent<RectTransform>();
            return rt;
        }

        public static Text MakeText(Transform parent, string name, string content, int size,
                                    TextAnchor anchor, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<Text>();
            t.font = Font;
            t.text = content;
            t.fontSize = size;
            t.alignment = anchor;
            t.color = color;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        public static Image MakePanel(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = color;
            return img;
        }

        /// <summary>
        /// A vertical gradient, built from stacked translucent bands rather than a generated
        /// texture. A flat slab over the mountain reads as a grey filter and flattens the
        /// silhouette; a gradient that is clear at the top and dense at the bottom keeps the
        /// terrain readable while still giving text something to sit on.
        ///
        /// Bands rather than a Texture2D on purpose: no sprite assets, nothing to import, and it
        /// stays inside the "everything is built in code" rule the project holds to.
        /// </summary>
        public static GameObject MakeGradient(Transform parent, string name, Color top, Color bottom, int bands = 10)
        {
            var holder = new GameObject(name, typeof(RectTransform));
            holder.transform.SetParent(parent, false);
            var hrt = Rect(holder);
            hrt.anchorMin = Vector2.zero; hrt.anchorMax = Vector2.one;
            hrt.offsetMin = Vector2.zero; hrt.offsetMax = Vector2.zero;

            for (int i = 0; i < bands; i++)
            {
                float t0 = i / (float)bands;
                float t1 = (i + 1) / (float)bands;
                var band = MakePanel(holder.transform, name + i, Color.Lerp(bottom, top, (t0 + t1) * 0.5f));
                var rt = Rect(band.gameObject);
                rt.anchorMin = new Vector2(0f, t0);
                rt.anchorMax = new Vector2(1f, t1);
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
                band.raycastTarget = false;
            }
            return holder;
        }

        public static Button MakeButton(Transform parent, string name, string label, int size = 30)
        {
            Text ignored;
            return MakeButton(parent, name, label, size, out ignored);
        }

        /// <summary>
        /// As above, but hands back the label so a caller can re-label the button later. A menu
        /// that destroys and rebuilds its own buttons to change their text is a menu that loses a
        /// click, so the three mountain rows are built once and re-labelled.
        /// </summary>
        public static Button MakeButton(Transform parent, string name, string label, int size, out Text labelText)
        {
            var img = MakePanel(parent, name, new Color(0.14f, 0.16f, 0.19f, 0.78f));
            var btn = img.gameObject.AddComponent<Button>();
            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
            colors.pressedColor = new Color(0.75f, 0.85f, 0.9f, 1f);
            colors.fadeDuration = 0.08f;
            btn.colors = colors;

            var text = MakeText(img.transform, "Label", label, size, TextAnchor.MiddleCenter, Ink);
            labelText = text;
            var rt = Rect(text.gameObject);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return btn;
        }

        public static void Anchor(GameObject go, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            var rt = Rect(go);
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
        }

        /// <summary>Places a fixed-size element relative to an anchor point.</summary>
        public static void Place(GameObject go, Vector2 anchor, Vector2 pivot, Vector2 anchoredPos, Vector2 size)
        {
            var rt = Rect(go);
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;
        }

        public static CanvasGroup Group(GameObject go)
        {
            var g = go.GetComponent<CanvasGroup>();
            if (g == null) g = go.AddComponent<CanvasGroup>();
            return g;
        }
    }
}
