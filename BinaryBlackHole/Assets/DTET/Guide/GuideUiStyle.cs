using UnityEngine;
using UnityEngine.UI;

namespace DTET.Guide
{
    /// <summary>
    /// Look of the guide UI. Same palette, font, rounded panels and white edge as PartnerLessonHud,
    /// so the guide pop-ups sit naturally next to the existing cards.
    /// </summary>
    public static class GuideUiStyle
    {
        public static readonly Color Accent = new Color(0.37f, 0.88f, 0.83f);
        public static readonly Color Warning = new Color(0.95f, 0.70f, 0.36f);
        public static readonly Color White = new Color(0.9f, 0.94f, 0.97f);
        public static readonly Color Muted = new Color(0.62f, 0.70f, 0.78f);
        public static readonly Color Panel = new Color(0.07f, 0.10f, 0.15f, 0.94f);
        public static readonly Color Edge = new Color(1f, 1f, 1f, 0.16f);

        private static Font font;
        private static Sprite rounded;

        public static Font Font
        {
            get
            {
                if (font == null)
                {
                    font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                }

                return font;
            }
        }

        public static Color KindColor(GuideKind kind)
        {
            switch (kind)
            {
                case GuideKind.Warning: return Warning;
                case GuideKind.Plain: return Muted;
                default: return Accent;
            }
        }

        // ---------- builders (all raycast-free: nothing here can be pointed at or clicked) ----------

        public static Canvas WorldCanvas(string name, Transform parent, Vector2 size, float scale)
        {
            GameObject root = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
            root.transform.SetParent(parent, false);

            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = Camera.main;

            CanvasGroup group = root.GetComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;
            group.alpha = 0f;

            RectTransform rect = root.GetComponent<RectTransform>();
            rect.sizeDelta = size;
            rect.localScale = Vector3.one * scale;
            canvas.enabled = false;
            return canvas;
        }

        /// <summary>Rounded box, positioned from the top-left corner (y is negative downwards), like the project HUD.</summary>
        public static RectTransform Box(Transform parent, string name, Vector2 position, Vector2 size, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            Place(go.GetComponent<RectTransform>(), position, size);

            Image image = go.GetComponent<Image>();
            image.sprite = RoundedSprite();
            image.type = Image.Type.Sliced;
            image.color = color;
            image.raycastTarget = false;
            return go.GetComponent<RectTransform>();
        }

        public static Text Label(Transform parent, string text, int fontSize, Color color, float x, float y, float width, float height,
            TextAnchor alignment = TextAnchor.MiddleLeft, bool bestFit = false)
        {
            GameObject go = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(parent, false);
            Place(go.GetComponent<RectTransform>(), new Vector2(x, -y), new Vector2(width, height));

            Text label = go.GetComponent<Text>();
            label.font = Font;
            label.text = text;
            label.fontSize = fontSize;
            label.color = color;
            label.alignment = alignment;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            label.raycastTarget = false;
            label.supportRichText = true;
            if (bestFit)
            {
                label.resizeTextForBestFit = true;
                label.resizeTextMinSize = Mathf.Max(10, fontSize / 2);
                label.resizeTextMaxSize = fontSize;
            }

            return label;
        }

        public static void Place(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        /// <summary>Rounded panel with the thin white edge. Returns the inner panel.</summary>
        public static RectTransform Card(Transform parent, string name, Vector2 size, Color fill)
        {
            RectTransform edge = Box(parent, name + " edge", Vector2.zero, size + Vector2.one * 4f, Edge);
            return Box(edge, name, new Vector2(2f, -2f), size, fill);
        }

        private static Sprite RoundedSprite()
        {
            if (rounded != null)
            {
                return rounded;
            }

            const int size = 32;
            const float radius = 12f;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.name = "Guide UI rounded corners";
            texture.wrapMode = TextureWrapMode.Clamp;

            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 nearest = new Vector2(
                        Mathf.Clamp(x + 0.5f, radius, size - radius),
                        Mathf.Clamp(y + 0.5f, radius, size - radius));
                    float distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), nearest);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(radius - distance + 0.5f));
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();
            rounded = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
            rounded.name = "Guide UI rounded sprite";
            return rounded;
        }
    }
}
