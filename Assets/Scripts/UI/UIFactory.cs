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

        /// <summary>
        /// A rounded-rectangle sprite, generated once and nine-sliced so it scales to any control.
        ///
        /// Everything here was a flat Image with a solid colour, which is why the interface read as
        /// placeholder rectangles dropped on the game rather than as part of it. Rounded corners, a
        /// lighter top edge and a soft outer falloff are most of the difference between a debug rect
        /// and a button, and all three are a texture — so they are generated in code like every
        /// other asset in this project.
        /// </summary>
        static Sprite _roundedSprite, _roundedSoftSprite;
        const int SpriteRadius = 28;
        const int SpritePad = 6;      // transparent margin so the edge has somewhere to fade into

        public static Sprite Rounded => _roundedSprite ?? (_roundedSprite = BuildRounded(false));
        public static Sprite RoundedSoft => _roundedSoftSprite ?? (_roundedSoftSprite = BuildRounded(true));

        static Sprite BuildRounded(bool soft)
        {
            int r = SpriteRadius, pad = SpritePad;
            int size = (r + pad) * 2 + 2;          // +2 so the nine-slice has a centre row/column
            var tex = new Texture2D(size, size, TextureFormat.ARGB32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;

            var px = new Color[size * size];
            float cx = (size - 1) * 0.5f, cy = (size - 1) * 0.5f;
            float half = (size - 1) * 0.5f - pad;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // Distance outside a rounded rect: the classic signed-distance form, which is
                    // what lets the corner radius stay a true circle at any control size.
                    float dx = Mathf.Abs(x - cx) - (half - r);
                    float dy = Mathf.Abs(y - cy) - (half - r);
                    float outside = Mathf.Sqrt(Mathf.Max(dx, 0f) * Mathf.Max(dx, 0f) +
                                               Mathf.Max(dy, 0f) * Mathf.Max(dy, 0f))
                                    + Mathf.Min(Mathf.Max(dx, dy), 0f) - r;

                    // One pixel of feather so the edge is not a staircase on a phone.
                    float a = Mathf.Clamp01(0.5f - outside);
                    if (soft) a *= a;   // a softer falloff for panels that should sit under text

                    // A lighter band along the top inside edge. Light comes from above in every
                    // real object anyone has ever held, and one row of it is the cheapest way to
                    // stop a rectangle looking printed on the screen.
                    float rim = Mathf.Clamp01(1f + outside) * Mathf.Clamp01((y - cy) / half);
                    float v = 1f + rim * 0.30f;

                    px[y * size + x] = new Color(v, v, v, a);
                }
            }
            tex.SetPixels(px);
            tex.Apply();

            int b = r + pad;
            return Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f),
                                 100f, 0u, SpriteMeshType.FullRect, new Vector4(b, b, b, b));
        }

        public static readonly Color Ink = new Color(0.98f, 0.97f, 0.94f, 0.96f);
        public static readonly Color InkDim = new Color(0.98f, 0.97f, 0.94f, 0.62f);
        public static readonly Color Panel = new Color(0.08f, 0.09f, 0.11f, 0.72f);
        public static readonly Color Accent = new Color(0.55f, 0.84f, 0.93f, 1f);
        /// <summary>Buttons sit slightly lighter than panels so they read as raised, not cut out.</summary>
        public static readonly Color ButtonFace = new Color(0.16f, 0.19f, 0.23f, 0.88f);

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
            return MakePanel(parent, name, color, true);
        }

        public static Image MakePanel(Transform parent, string name, Color color, bool rounded)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = color;
            if (rounded)
            {
                img.sprite = Rounded;
                img.type = Image.Type.Sliced;   // corners keep their radius at any control size
            }
            return img;
        }

        /// <summary>
        /// A square button carrying one glyph, for the corners. A back control does not need the
        /// word "Back" on it at 30 point — it needs to be reachable with a thumb, unmistakable, and
        /// small enough that it is not part of the picture.
        /// </summary>
        public static Button MakeIconButton(Transform parent, string name, string glyph, int size = 46)
        {
            var img = MakePanel(parent, name, ButtonFace);
            var btn = img.gameObject.AddComponent<Button>();
            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.18f, 1.18f, 1.18f, 1f);
            colors.pressedColor = new Color(0.72f, 0.86f, 0.92f, 1f);
            colors.fadeDuration = 0.08f;
            btn.colors = colors;
            btn.targetGraphic = img;

            var text = MakeText(img.transform, "Glyph", glyph, size, TextAnchor.MiddleCenter, Ink);
            var rt = Rect(text.gameObject);
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(0f, -4f); rt.offsetMax = new Vector2(0f, -4f);
            text.raycastTarget = false;
            return btn;
        }

        /// <summary>
        /// Insets a full-screen RectTransform to the device's safe area, so nothing lands under a
        /// notch or a home indicator. Corner controls are exactly what this protects: a back button
        /// pinned to the true corner of a modern phone is pinned under its camera cutout.
        /// </summary>
        public static void ApplySafeArea(RectTransform rt)
        {
            if (rt == null) return;
            var area = Screen.safeArea;
            float w = Screen.width, h = Screen.height;
            if (w <= 0f || h <= 0f) return;

            rt.anchorMin = new Vector2(area.x / w, area.y / h);
            rt.anchorMax = new Vector2((area.x + area.width) / w, (area.y + area.height) / h);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
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
        // 24 bands, not 10. At ten the wash behind the title reads as visible horizontal
        // stripes across the whole screen — clear in every captured frame of the main screen.
        public static GameObject MakeGradient(Transform parent, string name, Color top, Color bottom, int bands = 24)
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
                // Square, explicitly. A gradient is ten full-bleed bands stacked edge to edge, and
                // giving them rounded corners cuts nine visible notches down both sides of it.
                var band = MakePanel(holder.transform, name + i, Color.Lerp(bottom, top, (t0 + t1) * 0.5f), false);
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
