using System.Collections.Generic;
using UnityEngine;

namespace CloudGarden.Islands
{
    /// <summary>
    /// Procedural "hand painted" textures. Everything is generated at runtime so the game does not depend
    /// on external assets: tileable value noise plus soft brush dabs.
    /// </summary>
    public static class TextureFactory
    {
        static readonly Dictionary<string, Texture2D> cache = new Dictionary<string, Texture2D>();

        static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                int h = x * 374761393 + y * 668265263 + seed * 144665;
                h = (h ^ (h >> 13)) * 1274126177;
                h ^= h >> 16;
                return (h & 0xFFFF) / 65535f;
            }
        }

        static float Smooth(float t) { return t * t * (3f - 2f * t); }

        /// <summary>Tileable value noise with the given lattice period.</summary>
        static float Noise(float x, float y, int period, int seed)
        {
            int x0 = Mathf.FloorToInt(x);
            int y0 = Mathf.FloorToInt(y);
            float fx = Smooth(x - x0);
            float fy = Smooth(y - y0);
            int xa = Wrap(x0, period), xb = Wrap(x0 + 1, period);
            int ya = Wrap(y0, period), yb = Wrap(y0 + 1, period);
            float a = Hash(xa, ya, seed);
            float b = Hash(xb, ya, seed);
            float c = Hash(xa, yb, seed);
            float d = Hash(xb, yb, seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        static int Wrap(int v, int period)
        {
            int m = v % period;
            return m < 0 ? m + period : m;
        }

        static Texture2D Finish(Texture2D t)
        {
            t.wrapMode = TextureWrapMode.Repeat;
            t.filterMode = FilterMode.Bilinear;
            t.anisoLevel = 1;
            t.Apply(true, true);
            return t;
        }

        /// <summary>Painted texture mixing colors a and b. scale = noise cells across the texture.</summary>
        public static Texture2D Painted(string key, Color a, Color b, int size, int cells, int seed, int dabs)
        {
            Texture2D cached;
            if (cache.TryGetValue(key, out cached) && cached != null) return cached;

            Color[] px = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = x / (float)size * cells;
                    float v = y / (float)size * cells;
                    float n = Noise(u, v, cells, seed) * 0.6f
                            + Noise(u * 2f, v * 2f, cells * 2, seed + 7) * 0.3f
                            + Noise(u * 4f, v * 4f, cells * 4, seed + 13) * 0.1f;
                    px[y * size + x] = Color.Lerp(a, b, Mathf.Clamp01((n - 0.2f) * 1.5f));
                }
            }

            // Soft brush dabs for a painted look (wrapped so the texture still tiles).
            System.Random rnd = new System.Random(seed * 31 + 5);
            for (int i = 0; i < dabs; i++)
            {
                int cx = rnd.Next(size);
                int cy = rnd.Next(size);
                int radius = 2 + rnd.Next(Mathf.Max(3, size / 14));
                Color dab = Color.Lerp(a, b, (float)rnd.NextDouble());
                dab = Color.Lerp(dab, rnd.Next(2) == 0 ? Color.white : Color.black, 0.07f);
                for (int dy = -radius; dy <= radius; dy++)
                {
                    for (int dx = -radius; dx <= radius; dx++)
                    {
                        float dist = Mathf.Sqrt(dx * dx + dy * dy) / radius;
                        if (dist > 1f) continue;
                        float alpha = (1f - dist) * 0.35f;
                        int px2 = Wrap(cx + dx, size);
                        int py2 = Wrap(cy + dy, size);
                        px[py2 * size + px2] = Color.Lerp(px[py2 * size + px2], dab, alpha);
                    }
                }
            }

            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, true);
            tex.SetPixels(px);
            cache[key] = Finish(tex);
            return tex;
        }

        /// <summary>Neutral light-gray painted texture multiplied by material colors to add brush variation.</summary>
        public static Texture2D PaintedNeutral()
        {
            return Painted("neutral", new Color(0.80f, 0.80f, 0.80f), new Color(1f, 1f, 1f), 64, 4, 3, 60);
        }

        /// <summary>Vertical gradient (1 pixel wide) for the sky backdrop.</summary>
        public static Texture2D Gradient(Color top, Color bottom, int height, Texture2D reuse)
        {
            Texture2D t = reuse;
            if (t == null)
            {
                t = new Texture2D(1, height, TextureFormat.RGBA32, false);
                t.wrapMode = TextureWrapMode.Clamp;
                t.filterMode = FilterMode.Bilinear;
            }
            Color[] px = new Color[height];
            for (int y = 0; y < height; y++)
            {
                float f = y / (float)(height - 1);
                px[y] = Color.Lerp(bottom, top, f * f * (3f - 2f * f));
            }
            t.SetPixels(px);
            t.Apply(false);
            return t;
        }

        /// <summary>Soft white blob used for water highlights.</summary>
        public static Texture2D WaterRipples()
        {
            Texture2D cached;
            if (cache.TryGetValue("water", out cached) && cached != null) return cached;
            int size = 64;
            Color[] px = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = x / (float)size * 4f;
                    float v = y / (float)size * 4f;
                    float n = Noise(u, v, 4, 91) * 0.7f + Noise(u * 2f, v * 2f, 8, 17) * 0.3f;
                    float wave = Mathf.Sin((x / (float)size) * Mathf.PI * 4f + n * 4f) * 0.5f + 0.5f;
                    float light = Mathf.Clamp01(wave * 0.35f + n * 0.5f);
                    px[y * size + x] = new Color(0.55f + light * 0.45f, 0.75f + light * 0.25f, 1f, 0.55f + light * 0.3f);
                }
            }
            Texture2D t = new Texture2D(size, size, TextureFormat.RGBA32, true);
            t.SetPixels(px);
            cache["water"] = Finish(t);
            return t;
        }
    }
}
