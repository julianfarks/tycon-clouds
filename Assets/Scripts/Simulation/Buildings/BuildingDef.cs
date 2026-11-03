using System.Collections.Generic;
using CloudGarden.Economy;

namespace CloudGarden.Buildings
{
    public enum BuildingCategory
    {
        Collection = 0,
        Processing = 1,
        Agriculture = 2,
        Industry = 3,
        Infrastructure = 4
    }

    public enum BuildingRole
    {
        Producer = 0,
        Storage,
        Market,
        Port,
        ResearchCenter,
        Observatory,
        CommTower,
        ClimateCore,
        IndustrialCenter
    }

    public enum BuildingStatus
    {
        Idle = 0,
        Running,
        MissingInput,
        StorageFull,
        Passive
    }

    /// <summary>Static, data-driven definition of a building type.</summary>
    public sealed class BuildingDef
    {
        public readonly string Id;
        public readonly string Name;
        public readonly BuildingCategory Category;
        public readonly int Width;
        public readonly int Depth;
        public readonly double BaseCost;

        public string Description = "";
        public double CostGrowth = 1.12;
        public int MinRegion;
        public string RequiredResearch = "";
        public int RequiredPrestigeUpgrade = -1;
        public int Tier;
        public double CycleSeconds = 1;
        public BuildingRole Role = BuildingRole.Producer;
        public double RoleValue;
        public int MaxCount;
        public string Visual = "box";
        public uint ColorA = 0xFFFFFF;
        public uint ColorB = 0xCCCCCC;

        public readonly List<ResourceAmount> Materials = new List<ResourceAmount>();
        public readonly List<ResourceAmount> Inputs = new List<ResourceAmount>();
        public readonly List<ResourceAmount> Outputs = new List<ResourceAmount>();

        public BuildingDef(string id, string name, BuildingCategory category, int width, int depth, double baseCost)
        {
            Id = id;
            Name = name;
            Category = category;
            Width = width;
            Depth = depth;
            BaseCost = baseCost;
        }

        public bool IsProducer { get { return Role == BuildingRole.Producer; } }

        // ---- fluent configuration helpers ----
        public BuildingDef Desc(string d) { Description = d; return this; }
        public BuildingDef Growth(double g) { CostGrowth = g; return this; }
        public BuildingDef Reg(int minRegion, string research) { MinRegion = minRegion; RequiredResearch = research ?? ""; return this; }
        public BuildingDef Tr(int tier) { Tier = tier; return this; }
        public BuildingDef Mat(ResourceType t, double a) { Materials.Add(new ResourceAmount(t, a)); return this; }
        public BuildingDef In(ResourceType t, double a) { Inputs.Add(new ResourceAmount(t, a)); return this; }
        public BuildingDef Out(ResourceType t, double a) { Outputs.Add(new ResourceAmount(t, a)); return this; }
        public BuildingDef Cycle(double s) { CycleSeconds = s; return this; }
        public BuildingDef AsRole(BuildingRole r, double value) { Role = r; RoleValue = value; return this; }
        public BuildingDef Max(int n) { MaxCount = n; return this; }
        public BuildingDef Vis(string kind, uint a, uint b) { Visual = kind; ColorA = a; ColorB = b; return this; }
        public BuildingDef Prestige(int upgradeIndex) { RequiredPrestigeUpgrade = upgradeIndex; return this; }
    }
}
