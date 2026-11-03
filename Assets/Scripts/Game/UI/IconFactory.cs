using System;
using System.Collections.Generic;
using CloudGarden.Economy;
using CloudGarden.Islands;
using UnityEngine;

namespace CloudGarden.UI
{
    /// <summary>Procedural, consistent icon set (resources and menu buttons) drawn from simple shapes.</summary>
    public static class IconFactory
    {
        const int Size = 64;
        static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

        // ---- shape helpers (coordinates in [-1, 1], y up) ----
        static float Circle(float x, float y, float cx, float cy, float r)
        {
            float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
            return Mathf.Clamp01((r - d) * 24f + 0.5f);
        }

        static float Ring(float x, float y, float cx, float cy, float r, float w)
        {
            float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
            return Mathf.Clamp01((w * 0.5f - Mathf.Abs(d - r)) * 24f + 0.5f);
        }

        static float Bx(float x, float y, float cx, float cy, float hw, float hh)
        {
            float dx = Mathf.Abs(x - cx) - hw;
            float dy = Mathf.Abs(y - cy) - hh;
            float d = Mathf.Max(dx, dy);
            return Mathf.Clamp01(-d * 24f + 0.5f);
        }

        static float Segment(float x, float y, float ax, float ay, float bx, float by, float w)
        {
            float px = x - ax, py = y - ay, bxl = bx - ax, byl = by - ay;
            float t = Mathf.Clamp01((px * bxl + py * byl) / (bxl * bxl + byl * byl + 1e-6f));
            float dx = px - bxl * t, dy = py - byl * t;
            float d = Mathf.Sqrt(dx * dx + dy * dy);
            return Mathf.Clamp01((w * 0.5f - d) * 24f + 0.5f);
        }

        static float Triangle(float x, float y, float ax, float ay, float bx, float by, float cx, float cy)
        {
            float d1 = (x - bx) * (ay - by) - (ax - bx) * (y - by);
            float d2 = (x - cx) * (by - cy) - (bx - cx) * (y - cy);
            float d3 = (x - ax) * (cy - ay) - (cx - ax) * (y - ay);
            bool neg = d1 < 0 || d2 < 0 || d3 < 0;
            bool pos = d1 > 0 || d2 > 0 || d3 > 0;
            return (neg && pos) ? 0f : 1f;
        }

        static float Max(float a, float b) { return a > b ? a : b; }

        // ---- glyphs: coverage of the white symbol inside the badge ----
        static float ResourceGlyph(ResourceType t, float x, float y)
        {
            switch (t)
            {
                case ResourceType.Coins:
                    return Max(Ring(x, y, 0, 0, 0.42f, 0.16f), Bx(x, y, 0, 0, 0.07f, 0.26f));
                case ResourceType.Dew:
                case ResourceType.Water:
                    {
                        float body = Circle(x, y, 0, -0.14f, 0.36f);
                        float tip = Triangle(x, y, 0, 0.58f, -0.34f, 0.0f, 0.34f, 0.0f);
                        float g = Max(body, tip);
                        if (t == ResourceType.Water) g = Max(g * 0.55f, g * Bx(x, y, 0, -0.1f, 0.5f, 0.08f));
                        return g;
                    }
                case ResourceType.Pollen:
                    {
                        float g = Circle(x, y, 0, 0, 0.14f);
                        for (int i = 0; i < 5; i++)
                        {
                            float a = i * 72f * Mathf.Deg2Rad;
                            g = Max(g, Circle(x, y, Mathf.Cos(a) * 0.36f, Mathf.Sin(a) * 0.36f, 0.19f));
                        }
                        return g;
                    }
                case ResourceType.Wind:
                    {
                        float g = 0;
                        for (int i = -1; i <= 1; i++)
                        {
                            float yy = i * 0.3f;
                            float wave = Mathf.Sin(x * 5f + i) * 0.05f;
                            g = Max(g, Bx(x, y - wave, 0.0f + (i == 0 ? 0.05f : 0f), yy, 0.5f - Mathf.Abs(i) * 0.1f, 0.055f));
                        }
                        return g;
                    }
                case ResourceType.Clouds:
                    return Max(Max(Circle(x, y, -0.25f, -0.1f, 0.25f), Circle(x, y, 0.2f, -0.12f, 0.27f)),
                        Max(Circle(x, y, 0f, 0.12f, 0.3f), Bx(x, y, 0, -0.26f, 0.5f, 0.12f)));
                case ResourceType.Crystals:
                    return Max(Triangle(x, y, 0, 0.62f, -0.4f, 0.0f, 0.4f, 0.0f), Triangle(x, y, 0, -0.62f, -0.4f, 0.0f, 0.4f, 0.0f));
                case ResourceType.Essence:
                    {
                        float ang = Mathf.Atan2(y, x);
                        float r = Mathf.Sqrt(x * x + y * y);
                        float lim = 0.2f + 0.34f * Mathf.Pow(Mathf.Abs(Mathf.Cos(2f * ang - Mathf.PI * 0.5f)), 4f);
                        return Mathf.Clamp01((lim - r) * 24f + 0.5f);
                    }
                case ResourceType.Goods:
                    return Max(Mathf.Min(Bx(x, y, 0, 0, 0.42f, 0.36f), 1f - Bx(x, y, 0, 0, 0.36f, 0.3f)),
                        Max(Bx(x, y, 0, 0, 0.07f, 0.36f), Bx(x, y, 0, 0, 0.42f, 0.07f)));
                case ResourceType.Fruit:
                    return Max(Circle(x, y, 0, -0.1f, 0.4f), Max(Bx(x, y, 0.03f, 0.38f, 0.04f, 0.14f), Circle(x, y, 0.2f, 0.4f, 0.12f)));
                case ResourceType.Ice:
                    {
                        float g = 0;
                        for (int i = 0; i < 3; i++)
                        {
                            float a = i * 60f * Mathf.Deg2Rad;
                            g = Max(g, Segment(x, y, -Mathf.Cos(a) * 0.5f, -Mathf.Sin(a) * 0.5f, Mathf.Cos(a) * 0.5f, Mathf.Sin(a) * 0.5f, 0.11f));
                        }
                        return Max(g, Circle(x, y, 0, 0, 0.12f));
                    }
                case ResourceType.Energy:
                    return Max(Triangle(x, y, 0.1f, 0.62f, -0.34f, -0.04f, 0.1f, -0.04f), Triangle(x, y, -0.1f, 0.04f, 0.34f, 0.04f, -0.1f, -0.62f));
                default:
                    return 0;
            }
        }

        public static Sprite Resource(ResourceType type)
        {
            string key = "res" + (int)type;
            Sprite s;
            if (cache.TryGetValue(key, out s) && s != null) return s;
            Color c = ColorUtil.Hex(ResourceInfo.Color(type));
            Color dark = ColorUtil.Darken(c, 0.35f);
            bool glyphDark = type == ResourceType.Clouds || type == ResourceType.Ice || type == ResourceType.Wind;
            s = Render(key, (x, y) =>
            {
                float badge = Circle(x, y, 0, 0, 0.95f);
                if (badge <= 0f) return new Color(0, 0, 0, 0);
                Color baseCol = Color.Lerp(c, dark, Mathf.Clamp01((y * -0.5f + 0.5f) * 0.5f));
                float g = ResourceGlyph(type, x * 1.12f, y * 1.12f);
                Color glyph = glyphDark ? ColorUtil.Darken(c, 0.5f) : Color.white;
                Color result = Color.Lerp(baseCol, glyph, g);
                float rim = Ring(x, y, 0, 0, 0.9f, 0.1f);
                result = Color.Lerp(result, ColorUtil.Lighten(c, 0.55f), rim * 0.7f);
                result.a = badge;
                return result;
            });
            return s;
        }

        // ---- menu icons ----
        public const int MenuBuild = 0, MenuUpgrade = 1, MenuResearch = 2, MenuTrade = 3, MenuExpand = 4, MenuManage = 5, MenuClose = 6, MenuStats = 7;

        static float MenuGlyph(int id, float x, float y)
        {
            switch (id)
            {
                case MenuBuild:
                    return Max(Max(Bx(x, y, 0, -0.18f, 0.5f, 0.32f), Triangle(x, y, 0, 0.62f, -0.66f, 0.12f, 0.66f, 0.12f)), 0f)
                        * (1f - Bx(x, y, 0, -0.3f, 0.12f, 0.2f) * 0.85f);
                case MenuUpgrade:
                    return Max(Triangle(x, y, 0, 0.7f, -0.55f, 0.0f, 0.55f, 0.0f), Bx(x, y, 0, -0.3f, 0.2f, 0.38f));
                case MenuResearch:
                    return Max(Max(Bx(x, y, 0, 0.35f, 0.14f, 0.3f), Triangle(x, y, -0.5f, -0.55f, 0.5f, -0.55f, 0.0f, 0.15f)),
                        Bx(x, y, 0, 0.68f, 0.28f, 0.07f));
                case MenuTrade:
                    return Max(Triangle(x, y, 0.7f, 0.0f, -0.6f, 0.5f, -0.6f, -0.5f), Triangle(x, y, -0.6f, 0.0f, -0.1f, 0.0f, -0.6f, -0.25f) * 0f);
                case MenuExpand:
                    return Max(Max(Segment(x, y, -0.55f, -0.55f, 0.55f, 0.55f, 0.16f), Triangle(x, y, 0.7f, 0.7f, 0.15f, 0.7f, 0.7f, 0.15f)),
                        Triangle(x, y, -0.7f, -0.7f, -0.15f, -0.7f, -0.7f, -0.15f));
                case MenuManage:
                    {
                        float ang = Mathf.Atan2(y, x);
                        float r = Mathf.Sqrt(x * x + y * y);
                        float teeth = 0.52f + 0.12f * Mathf.Clamp01(Mathf.Sin(ang * 8f) * 3f + 0.5f);
                        float body = Mathf.Clamp01((teeth - r) * 24f + 0.5f);
                        return body * (1f - Circle(x, y, 0, 0, 0.22f));
                    }
                case MenuClose:
                    return Max(Segment(x, y, -0.5f, -0.5f, 0.5f, 0.5f, 0.18f), Segment(x, y, -0.5f, 0.5f, 0.5f, -0.5f, 0.18f));
                case MenuStats:
                    return Max(Bx(x, y, -0.45f, -0.25f, 0.14f, 0.35f), Max(Bx(x, y, 0, -0.05f, 0.14f, 0.55f), Bx(x, y, 0.45f, -0.35f, 0.14f, 0.25f)));
                default:
                    return 0;
            }
        }

        public static Sprite Menu(int id)
        {
            string key = "menu" + id;
            Sprite s;
            if (cache.TryGetValue(key, out s) && s != null) return s;
            s = Render(key, (x, y) =>
            {
                float g = Mathf.Clamp01(MenuGlyph(id, x * 1.15f, y * 1.15f));
                return new Color(1f, 1f, 1f, g);
            });
            return s;
        }

        static Sprite Render(string key, Func<float, float, Color> shader)
        {
            Texture2D tex = new Texture2D(Size, Size, TextureFormat.RGBA32, true);
            Color[] px = new Color[Size * Size];
            for (int j = 0; j < Size; j++)
            {
                for (int i = 0; i < Size; i++)
                {
                    float x = (i + 0.5f) / Size * 2f - 1f;
                    float y = (j + 0.5f) / Size * 2f - 1f;
                    px[j * Size + i] = shader(x, y);
                }
            }
            tex.SetPixels(px);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            tex.Apply(true, true);
            Sprite sp = Sprite.Create(tex, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), 100f);
            cache[key] = sp;
            return sp;
        }
    }
}
