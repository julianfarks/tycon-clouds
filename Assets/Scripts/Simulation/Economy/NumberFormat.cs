using System;
using System.Globalization;

namespace CloudGarden.Economy
{
    public static class NumberFormat
    {
        static readonly string[] suffixes = { "", "K", "M", "B", "T", "Qa", "Qi", "Sx", "Sp", "Oc" };

        /// <summary>Compact number: 1.23K, 4.5M... Never throws on NaN/Infinity.</summary>
        public static string Compact(double v)
        {
            if (double.IsNaN(v)) return "0";
            if (double.IsInfinity(v)) return "inf";
            bool neg = v < 0;
            double a = Math.Abs(v);
            string s;
            if (a < 1000)
            {
                if (a < 10 && Math.Abs(a - Math.Round(a)) > 0.005) s = a.ToString("0.0#", CultureInfo.InvariantCulture);
                else if (a < 100 && Math.Abs(a - Math.Round(a)) > 0.05) s = a.ToString("0.#", CultureInfo.InvariantCulture);
                else s = Math.Floor(a + 0.0001).ToString("0", CultureInfo.InvariantCulture);
            }
            else
            {
                int idx = 0;
                while (a >= 1000 && idx < suffixes.Length - 1)
                {
                    a /= 1000;
                    idx++;
                }
                s = (a < 10 ? a.ToString("0.00", CultureInfo.InvariantCulture)
                    : a < 100 ? a.ToString("0.0", CultureInfo.InvariantCulture)
                    : a.ToString("0", CultureInfo.InvariantCulture)) + suffixes[idx];
            }
            return neg ? "-" + s : s;
        }

        public static string Rate(double perSecond)
        {
            return Compact(perSecond) + "/s";
        }

        public static string Duration(double seconds)
        {
            if (seconds < 0 || double.IsNaN(seconds)) seconds = 0;
            int total = (int)Math.Min(seconds, 99 * 3600.0);
            int h = total / 3600;
            int m = (total % 3600) / 60;
            int s = total % 60;
            if (h > 0) return h + "h " + m.ToString("00") + "m";
            if (m > 0) return m + "m " + s.ToString("00") + "s";
            return s + "s";
        }
    }
}
