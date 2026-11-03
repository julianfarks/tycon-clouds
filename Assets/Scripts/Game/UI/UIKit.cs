using System;
using CloudGarden.Islands;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CloudGarden.UI
{
    /// <summary>Helpers to build the mobile interface from code (no external assets).</summary>
    public static class UIKit
    {
        public static readonly Color PanelBg = new Color(0.09f, 0.11f, 0.20f, 0.94f);
        public static readonly Color BarBg = new Color(0.07f, 0.09f, 0.17f, 0.90f);
        public static readonly Color CardBg = new Color(1f, 1f, 1f, 0.09f);
        public static readonly Color CardBgLight = new Color(1f, 1f, 1f, 0.16f);
        public static readonly Color Accent = ColorUtil.Hex(0x7C6CF2);
        public static readonly Color Good = ColorUtil.Hex(0x3FCB86);
        public static readonly Color Warn = ColorUtil.Hex(0xFFB84A);
        public static readonly Color Bad = ColorUtil.Hex(0xFF5A6E);
        public static readonly Color Disabled = new Color(0.32f, 0.34f, 0.44f, 1f);
        public static readonly Color TextMain = ColorUtil.Hex(0xF4F6FF);
        public static readonly Color TextDim = ColorUtil.Hex(0xA9B0D0);
        public static readonly Color Gold = ColorUtil.Hex(0xFFC857);

        static Font font;
        static Sprite rounded;
        static Sprite circle;

        public static Font DefaultFont
        {
            get
            {
                if (font != null) return font;
                try { font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch (Exception) { font = null; }
                if (font == null) font = Font.CreateDynamicFontFromOSFont("Arial", 18);
                return font;
            }
        }

        public static Sprite Rounded
        {
            get
            {
                if (rounded != null) return rounded;
                const int size = 64;
                const float radius = 22f;
                Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
                Color[] px = new Color[size * size];
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float dx = Mathf.Max(radius - x - 0.5f, 0f, x + 0.5f - (size - radius));
                        float dy = Mathf.Max(radius - y - 0.5f, 0f, y + 0.5f - (size - radius));
                        float dist = Mathf.Sqrt(dx * dx + dy * dy);
                        float a = Mathf.Clamp01(radius - dist + 0.5f);
                        px[y * size + x] = new Color(1f, 1f, 1f, a);
                    }
                }
                tex.SetPixels(px);
                tex.filterMode = FilterMode.Bilinear;
                tex.Apply(false, true);
                rounded = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(24, 24, 24, 24));
                return rounded;
            }
        }

        public static Sprite Circle
        {
            get
            {
                if (circle != null) return circle;
                const int size = 64;
                Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
                Color[] px = new Color[size * size];
                float r = size * 0.5f;
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float d = Mathf.Sqrt((x + 0.5f - r) * (x + 0.5f - r) + (y + 0.5f - r) * (y + 0.5f - r));
                        px[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(r - d));
                    }
                }
                tex.SetPixels(px);
                tex.filterMode = FilterMode.Bilinear;
                tex.Apply(false, true);
                circle = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
                return circle;
            }
        }

        // ------------------------------------------------------------------ creation

        public static RectTransform NewRect(string name, Transform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
        }

        public static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        public static void Place(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
        }

        /// <summary>Anchored box: anchor point + pivot + fixed size.</summary>
        public static void Box(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
        }

        public static Image Panel(Transform parent, string name, Color color)
        {
            RectTransform rt = NewRect(name, parent);
            Image img = rt.gameObject.AddComponent<Image>();
            img.sprite = Rounded;
            img.type = Image.Type.Sliced;
            img.color = color;
            return img;
        }

        public static Image Plain(Transform parent, string name, Color color)
        {
            RectTransform rt = NewRect(name, parent);
            Image img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            return img;
        }

        public static Text Label(Transform parent, string text, int size, Color color, TextAnchor anchor)
        {
            return Label(parent, text, size, color, anchor, FontStyle.Normal);
        }

        public static Text Label(Transform parent, string text, int size, Color color, TextAnchor anchor, FontStyle style)
        {
            RectTransform rt = NewRect("Text", parent);
            Text t = rt.gameObject.AddComponent<Text>();
            t.font = DefaultFont;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = anchor;
            t.fontStyle = style;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.supportRichText = true;
            t.raycastTarget = false;
            return t;
        }

        public static Image Icon(Transform parent, Sprite sprite, float size)
        {
            RectTransform rt = NewRect("Icon", parent);
            Image img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.preserveAspect = true;
            img.raycastTarget = false;
            LayoutElement le = rt.gameObject.AddComponent<LayoutElement>();
            le.minWidth = size;
            le.minHeight = size;
            le.preferredWidth = size;
            le.preferredHeight = size;
            return img;
        }

        public static Button MakeButton(Transform parent, string label, Color bg, Action onClick, int fontSize)
        {
            Image img = Panel(parent, "Button", bg);
            Button btn = img.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            ColorBlock cb = btn.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(1.1f, 1.1f, 1.1f, 1f);
            cb.pressedColor = new Color(0.75f, 0.75f, 0.8f, 1f);
            cb.selectedColor = Color.white;
            cb.disabledColor = new Color(0.6f, 0.6f, 0.6f, 0.7f);
            cb.fadeDuration = 0.06f;
            btn.colors = cb;
            if (onClick != null) btn.onClick.AddListener(() => onClick());
            Text t = Label(img.transform, label, fontSize, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            Stretch(t.rectTransform);
            t.rectTransform.offsetMin = new Vector2(8, 4);
            t.rectTransform.offsetMax = new Vector2(-8, -4);
            t.verticalOverflow = VerticalWrapMode.Truncate;
            return btn;
        }

        public static void SetButtonLabel(Button b, string text)
        {
            Text t = b.GetComponentInChildren<Text>();
            if (t != null) t.text = text;
        }

        public static LayoutElement Fixed(GameObject go, float width, float height)
        {
            LayoutElement le = go.GetComponent<LayoutElement>();
            if (le == null) le = go.AddComponent<LayoutElement>();
            if (width >= 0) { le.minWidth = width; le.preferredWidth = width; }
            if (height >= 0) { le.minHeight = height; le.preferredHeight = height; }
            return le;
        }

        public static LayoutElement Flex(GameObject go, float flexWidth)
        {
            LayoutElement le = go.GetComponent<LayoutElement>();
            if (le == null) le = go.AddComponent<LayoutElement>();
            le.flexibleWidth = flexWidth;
            return le;
        }

        public static VerticalLayoutGroup VStack(GameObject go, float spacing, int padding)
        {
            VerticalLayoutGroup v = go.AddComponent<VerticalLayoutGroup>();
            v.spacing = spacing;
            v.padding = new RectOffset(padding, padding, padding, padding);
            v.childControlWidth = true;
            v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;
            return v;
        }

        public static HorizontalLayoutGroup HStack(GameObject go, float spacing, int padding)
        {
            HorizontalLayoutGroup h = go.AddComponent<HorizontalLayoutGroup>();
            h.spacing = spacing;
            h.padding = new RectOffset(padding, padding, padding, padding);
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandWidth = false;
            h.childForceExpandHeight = true;
            h.childAlignment = TextAnchor.MiddleLeft;
            return h;
        }

        /// <summary>Vertical scroll view. Returns the content rect (a vertical stack that grows with its children).</summary>
        public static ScrollRect ScrollView(Transform parent, out RectTransform content)
        {
            RectTransform root = NewRect("Scroll", parent);
            ScrollRect sr = root.gameObject.AddComponent<ScrollRect>();
            Image bg = root.gameObject.AddComponent<Image>();
            bg.color = new Color(0, 0, 0, 0.001f);

            RectTransform viewport = NewRect("Viewport", root);
            Stretch(viewport);
            viewport.gameObject.AddComponent<RectMask2D>();

            content = NewRect("Content", viewport);
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(0.5f, 1);
            content.offsetMin = new Vector2(0, 0);
            content.offsetMax = new Vector2(0, 0);
            VerticalLayoutGroup v = content.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = 10;
            v.padding = new RectOffset(6, 6, 6, 14);
            v.childControlWidth = true;
            v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;
            ContentSizeFitter fit = content.gameObject.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            sr.viewport = viewport;
            sr.content = content;
            sr.horizontal = false;
            sr.vertical = true;
            sr.movementType = ScrollRect.MovementType.Clamped;
            sr.scrollSensitivity = 30f;
            sr.inertia = true;
            sr.decelerationRate = 0.12f;
            return sr;
        }

        public static void Clear(Transform t)
        {
            for (int i = t.childCount - 1; i >= 0; i--)
            {
                GameObject child = t.GetChild(i).gameObject;
                child.SetActive(false);
                UnityEngine.Object.Destroy(child);
            }
        }

        public static GameObject Card(Transform parent, float height)
        {
            Image img = Panel(parent, "Card", CardBg);
            Fixed(img.gameObject, -1, height);
            return img.gameObject;
        }

        public static Slider MakeBar(Transform parent, Color fill)
        {
            RectTransform root = NewRect("Bar", parent);
            Image bg = root.gameObject.AddComponent<Image>();
            bg.sprite = Rounded;
            bg.type = Image.Type.Sliced;
            bg.color = new Color(0, 0, 0, 0.35f);
            RectTransform fillRt = NewRect("Fill", root);
            Image fi = fillRt.gameObject.AddComponent<Image>();
            fi.sprite = Rounded;
            fi.type = Image.Type.Sliced;
            fi.color = fill;
            fillRt.anchorMin = new Vector2(0, 0);
            fillRt.anchorMax = new Vector2(1, 1);
            fillRt.offsetMin = Vector2.zero;
            fillRt.offsetMax = Vector2.zero;
            Slider s = root.gameObject.AddComponent<Slider>();
            s.fillRect = fillRt;
            s.interactable = false;
            s.transition = Selectable.Transition.None;
            s.direction = Slider.Direction.LeftToRight;
            s.minValue = 0;
            s.maxValue = 1;
            s.value = 0;
            return s;
        }
    }
}
