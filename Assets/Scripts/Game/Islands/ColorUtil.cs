using UnityEngine;

namespace CloudGarden.Islands
{
    public static class ColorUtil
    {
        public static Color Hex(uint rgb)
        {
            return Hex(rgb, 1f);
        }

        public static Color Hex(uint rgb, float alpha)
        {
            float r = ((rgb >> 16) & 0xFF) / 255f;
            float g = ((rgb >> 8) & 0xFF) / 255f;
            float b = (rgb & 0xFF) / 255f;
            return new Color(r, g, b, alpha);
        }

        public static Color Shade(Color c, float factor)
        {
            return new Color(Mathf.Clamp01(c.r * factor), Mathf.Clamp01(c.g * factor), Mathf.Clamp01(c.b * factor), c.a);
        }

        public static Color WithAlpha(Color c, float a)
        {
            return new Color(c.r, c.g, c.b, a);
        }

        public static Color Lighten(Color c, float t)
        {
            return Color.Lerp(c, Color.white, t);
        }

        public static Color Darken(Color c, float t)
        {
            return Color.Lerp(c, new Color(0.08f, 0.06f, 0.12f, c.a), t);
        }
    }
}
