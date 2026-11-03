using System.Collections.Generic;
using CloudGarden.Economy;
using R = CloudGarden.Economy.ResourceType;

namespace CloudGarden.Islands
{
    public sealed class RegionDef
    {
        public int Index;
        public string Id;
        public string Name;
        public string Description;
        public string Exclusive;
        public double UnlockCoins;
        public readonly List<ResourceAmount> UnlockMaterials = new List<ResourceAmount>();
        public string RequiredResearch = "";
        public double ExpansionBaseCost;

        // Look (0xRRGGBB)
        public uint Ground;
        public uint Ground2;
        public uint Rock;
        public uint SkyTop;
        public uint SkyBottom;
        public uint Accent;
        public string Decor; // "meadow", "tropical", "snow", "storm", "celestial"

        // Island center in world space (units); one tile = one unit.
        public float WorldX;
        public float WorldZ;
    }

    public static class RegionDatabase
    {
        public const int Count = 5;
        static readonly RegionDef[] regions = new RegionDef[Count];

        public static RegionDef Get(int index) { return regions[index]; }

        static RegionDef Make(int index, string id, string name, string desc, string exclusive)
        {
            RegionDef r = new RegionDef();
            r.Index = index;
            r.Id = id;
            r.Name = name;
            r.Description = desc;
            r.Exclusive = exclusive;
            regions[index] = r;
            return r;
        }

        static RegionDatabase()
        {
            RegionDef r = Make(0, "dew_garden", "Jardín del Rocío", "Tu isla inicial. Rocío, agua, polen y viento.", "Rocío, Agua, Polen, Viento, Nubes");
            r.Ground = 0x9BE08A; r.Ground2 = 0x7FCB7A; r.Rock = 0x9A7B63; r.SkyTop = 0x8EC9FF; r.SkyBottom = 0xFFF1D6; r.Accent = 0xFFB3D1;
            r.Decor = "meadow"; r.WorldX = 0; r.WorldZ = 0; r.ExpansionBaseCost = 2000;

            r = Make(1, "tropical", "Isla Tropical", "Agricultura y recursos orgánicos: fruta tropical.", "Fruta tropical");
            r.Ground = 0x6FD6A0; r.Ground2 = 0xF3D9A4; r.Rock = 0xB58A5F; r.SkyTop = 0x7FE0D0; r.SkyBottom = 0xFFF3C9; r.Accent = 0xFF7F50;
            r.Decor = "tropical"; r.WorldX = 36; r.WorldZ = 12; r.ExpansionBaseCost = 80000;
            r.UnlockCoins = 15000; r.RequiredResearch = "sky_navigation";
            r.UnlockMaterials.Add(new ResourceAmount(R.Water, 150));
            r.UnlockMaterials.Add(new ResourceAmount(R.Pollen, 100));

            r = Make(2, "snow", "Isla Nevada", "Hielo y cristales celestes.", "Hielo, Cristales celestes");
            r.Ground = 0xF2FAFF; r.Ground2 = 0xD6EEFF; r.Rock = 0x8DB5D6; r.SkyTop = 0xB9D6FF; r.SkyBottom = 0xEAF4FF; r.Accent = 0x8B73F2;
            r.Decor = "snow"; r.WorldX = -6; r.WorldZ = 40; r.ExpansionBaseCost = 3000000;
            r.UnlockCoins = 600000; r.RequiredResearch = "polar_expedition";
            r.UnlockMaterials.Add(new ResourceAmount(R.Pollen, 1500));
            r.UnlockMaterials.Add(new ResourceAmount(R.Fruit, 800));
            r.UnlockMaterials.Add(new ResourceAmount(R.Goods, 300));

            r = Make(3, "storm", "Isla Tormentosa", "Energía atmosférica y máquinas industriales.", "Energía atmosférica");
            r.Ground = 0x7E9C86; r.Ground2 = 0x5F7A6B; r.Rock = 0x5A6482; r.SkyTop = 0x6C7AA8; r.SkyBottom = 0xC9C4E6; r.Accent = 0xFFE04A;
            r.Decor = "storm"; r.WorldX = -40; r.WorldZ = 8; r.ExpansionBaseCost = 120000000;
            r.UnlockCoins = 25000000; r.RequiredResearch = "storm_chasing";
            r.UnlockMaterials.Add(new ResourceAmount(R.Crystals, 2500));
            r.UnlockMaterials.Add(new ResourceAmount(R.Ice, 1500));
            r.UnlockMaterials.Add(new ResourceAmount(R.Goods, 2500));

            r = Make(4, "celestial", "Isla Celestial", "Esencia celestial y producción definitiva.", "Esencia celestial");
            r.Ground = 0xFFE7A8; r.Ground2 = 0xFFF4D0; r.Rock = 0xB9A3F2; r.SkyTop = 0xD8B8FF; r.SkyBottom = 0xFFE3F2; r.Accent = 0xFFC857;
            r.Decor = "celestial"; r.WorldX = 24; r.WorldZ = -36; r.ExpansionBaseCost = 5000000000;
            r.UnlockCoins = 1000000000; r.RequiredResearch = "celestial_ascension";
            r.UnlockMaterials.Add(new ResourceAmount(R.Energy, 8000));
            r.UnlockMaterials.Add(new ResourceAmount(R.Crystals, 6000));
            r.UnlockMaterials.Add(new ResourceAmount(R.Goods, 10000));
        }

        /// <summary>Coin cost of the next terrain expansion (0-based level being bought).</summary>
        public static double ExpansionCost(int region, int level)
        {
            return regions[region].ExpansionBaseCost * System.Math.Pow(3.2, level);
        }
    }
}
