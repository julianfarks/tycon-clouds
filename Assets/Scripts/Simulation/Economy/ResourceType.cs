using System;

namespace CloudGarden.Economy
{
    /// <summary>All resources of the game. Coins is always index 0.</summary>
    public enum ResourceType
    {
        Coins = 0,
        Dew = 1,
        Water = 2,
        Pollen = 3,
        Wind = 4,
        Clouds = 5,
        Crystals = 6,
        Essence = 7,
        Goods = 8,
        Fruit = 9,
        Ice = 10,
        Energy = 11
    }

    [Serializable]
    public struct ResourceAmount
    {
        public ResourceType Type;
        public double Amount;

        public ResourceAmount(ResourceType type, double amount)
        {
            Type = type;
            Amount = amount;
        }
    }

    /// <summary>Static metadata for each resource (names, market value, color, region).</summary>
    public static class ResourceInfo
    {
        public const int Count = 12;

        static readonly string[] names =
        {
            "Monedas", "Rocio", "Agua", "Polen", "Viento", "Nubes",
            "Cristales celestes", "Esencia celestial", "Productos",
            "Fruta tropical", "Hielo", "Energia atmosferica"
        };

        static readonly string[] shortNames =
        {
            "Monedas", "Rocio", "Agua", "Polen", "Viento", "Nubes",
            "Cristal", "Esencia", "Producto", "Fruta", "Hielo", "Energia"
        };

        // Commercial value in coins per unit.
        static readonly double[] values = { 1, 1, 4, 6, 3, 10, 30, 400, 55, 18, 40, 70 };

        // RGB colors (0xRRGGBB).
        static readonly uint[] colors =
        {
            0xF7C948, 0x8FD3F4, 0x3F8FEA, 0xF29BC8, 0xA8E6D8, 0xF4F8FF,
            0x8B73F2, 0xFFC857, 0xE48B4F, 0xFF7F50, 0xBDEBFF, 0xFFE04A
        };

        // First region (0-based) in which the resource can be obtained.
        static readonly int[] firstRegion = { 0, 0, 0, 0, 0, 0, 2, 4, 0, 1, 2, 3 };

        public static string Name(ResourceType t) { return names[(int)t]; }
        public static string ShortName(ResourceType t) { return shortNames[(int)t]; }
        public static double Value(ResourceType t) { return values[(int)t]; }
        public static uint Color(ResourceType t) { return colors[(int)t]; }
        public static int FirstRegion(ResourceType t) { return firstRegion[(int)t]; }
        public static bool IsStorable(ResourceType t) { return t != ResourceType.Coins; }
    }
}
