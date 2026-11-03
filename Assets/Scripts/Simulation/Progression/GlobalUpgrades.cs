using System;

namespace CloudGarden.Progression
{
    /// <summary>Global, coin-only upgrades with many levels.</summary>
    public static class GlobalUpgrades
    {
        public const int Count = 4;
        public const int Production = 0;
        public const int Storage = 1;
        public const int Efficiency = 2;
        public const int ManualHarvest = 3;

        static readonly string[] names = { "Producción global", "Almacenamiento", "Eficiencia de materiales", "Cosecha manual" };
        static readonly string[] descriptions =
        {
            "+8% de producción por nivel.",
            "+12% de capacidad de almacenes por nivel.",
            "-2,5% de materiales consumidos por nivel.",
            "+50% de rocío al tocar por nivel."
        };
        static readonly int[] maxLevels = { 25, 20, 16, 10 };
        static readonly double[] baseCosts = { 5000, 3000, 20000, 200 };
        static readonly double[] growth = { 1.9, 1.8, 2.0, 1.7 };

        public static string Name(int i) { return names[i]; }
        public static string Description(int i) { return descriptions[i]; }
        public static int MaxLevel(int i) { return maxLevels[i]; }

        public static double Cost(int i, int currentLevel)
        {
            return baseCosts[i] * Math.Pow(growth[i], currentLevel);
        }
    }
}
