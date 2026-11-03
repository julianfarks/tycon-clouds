using System.Collections.Generic;
using CloudGarden.Economy;

namespace CloudGarden.Research
{
    public enum ResearchKind
    {
        Unlock = 0,        // unlocks buildings that reference this research
        Production,        // + global production (Value = fraction)
        Storage,           // + storage (fraction)
        Offline,           // sets offline cap hours (Value = hours)
        UpgradeDiscount,   // - upgrade cost (fraction)
        BuildDiscount,     // - build cost (fraction)
        TradeBonus,        // + trade rewards (fraction)
        ClickBonus,        // + manual collection (fraction)
        SellBonus,         // + sell price (fraction)
        RegionGate,        // required to unlock Region[Value]
        Special
    }

    public sealed class ResearchDef
    {
        public readonly string Id;
        public readonly string Name;
        public readonly string Description;
        public readonly double CostCoins;
        public readonly ResearchKind Kind;
        public readonly double Value;

        public readonly List<ResourceAmount> Materials = new List<ResourceAmount>();
        public string[] Prerequisites = new string[0];
        public string RequiredBuilding = "";
        public bool RequiresRebirth;

        public ResearchDef(string id, string name, string description, double costCoins, ResearchKind kind, double value)
        {
            Id = id;
            Name = name;
            Description = description;
            CostCoins = costCoins;
            Kind = kind;
            Value = value;
        }

        public ResearchDef Pre(params string[] ids) { Prerequisites = ids; return this; }
        public ResearchDef Bld(string buildingId) { RequiredBuilding = buildingId; return this; }
        public ResearchDef Mat(ResourceType t, double a) { Materials.Add(new ResourceAmount(t, a)); return this; }
        public ResearchDef Rebirth() { RequiresRebirth = true; return this; }
    }
}
