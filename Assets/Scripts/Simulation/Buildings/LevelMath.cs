using System;

namespace CloudGarden.Buildings
{
    /// <summary>Scaling rules for building levels (1..10).</summary>
    public static class LevelMath
    {
        public const int MaxLevel = 10;

        public static int Clamp(int level)
        {
            if (level < 1) return 1;
            if (level > MaxLevel) return MaxLevel;
            return level;
        }

        /// <summary>Cycles per second multiplier.</summary>
        public static double Speed(int level)
        {
            level = Clamp(level);
            return 1.0 + 0.10 * (level - 1);
        }

        /// <summary>Batch size multiplier (inputs and outputs). Level 10 grants a x1.5 bonus.</summary>
        public static double Amount(int level)
        {
            level = Clamp(level);
            double m = 1.0 + 0.30 * (level - 1);
            if (level >= MaxLevel) m *= 1.5;
            return m;
        }

        /// <summary>Output per unit of input multiplier.</summary>
        public static double Efficiency(int level)
        {
            level = Clamp(level);
            return 1.0 + 0.04 * (level - 1);
        }

        /// <summary>Capacity / role strength multiplier (storage, bonuses).</summary>
        public static double Capacity(int level)
        {
            level = Clamp(level);
            double m = 1.0 + 0.5 * (level - 1);
            if (level >= MaxLevel) m *= 1.5;
            return m;
        }

        /// <summary>Coin cost to go from fromLevel to fromLevel+1.</summary>
        public static double UpgradeCost(double baseCost, int fromLevel)
        {
            fromLevel = Clamp(fromLevel);
            return baseCost * 0.8 * Math.Pow(1.8, fromLevel);
        }

        /// <summary>Visual tier of the model: 0 (lvl 1-3), 1 (4-6), 2 (7-9), 3 (10).</summary>
        public static int VisualTier(int level)
        {
            level = Clamp(level);
            if (level >= MaxLevel) return 3;
            if (level >= 7) return 2;
            if (level >= 4) return 1;
            return 0;
        }
    }
}
