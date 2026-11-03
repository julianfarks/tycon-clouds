using System;
using CloudGarden.Buildings;
using CloudGarden.Core;
using CloudGarden.Economy;
using CloudGarden.Progression;
using CloudGarden.Research;

namespace CloudGarden.Production
{
    /// <summary>
    /// Aggregated bonuses derived from research, buildings, global upgrades and prestige.
    /// Recomputed only when the structure of the game changes (not every tick).
    /// </summary>
    public sealed class Modifiers
    {
        public double GlobalProd = 1;
        public double StorageMult = 1;
        public double InputSave;
        public double ClickPower = 1;
        public double BuildDiscount;
        public double UpgradeDiscount;
        public double ResearchDiscount;
        public double TradeBonus;
        public double SellMult = 1;
        public double OfflineHours = 2;
        public double ProcessingSpeed;
        public bool HasMarket;
        public bool HasPort;
        public int TradeSlots;
        public readonly double[] Capacity = new double[ResourceInfo.Count];

        public const double BaseOfflineHours = 2;
        public const double MaxOfflineHours = 12;

        public static void Compute(GameState s, Modifiers m)
        {
            double prod = 0, storage = 0, build = 0, upgr = 0, trade = 0, sell = 0, click = 0;
            double offline = BaseOfflineHours;

            foreach (string id in s.Research)
            {
                ResearchDef r = ResearchDatabase.Get(id);
                if (r == null) continue;
                switch (r.Kind)
                {
                    case ResearchKind.Production: prod += r.Value; break;
                    case ResearchKind.Storage: storage += r.Value; break;
                    case ResearchKind.Offline: if (r.Value > offline) offline = r.Value; break;
                    case ResearchKind.UpgradeDiscount: upgr += r.Value; break;
                    case ResearchKind.BuildDiscount: build += r.Value; break;
                    case ResearchKind.TradeBonus: trade += r.Value; break;
                    case ResearchKind.ClickBonus: click += r.Value; break;
                    case ResearchKind.SellBonus: sell += r.Value; break;
                }
            }

            int[] gu = s.GlobalUpgradeLevels;
            prod += 0.08 * gu[GlobalUpgrades.Production];
            storage += 0.12 * gu[GlobalUpgrades.Storage];
            click += 0.5 * gu[GlobalUpgrades.ManualHarvest];

            PrestigeState p = s.Prestige;
            prod += 0.05 * p.StarsEarnedTotal;
            if (p.Upgrades[0]) prod += 0.25;
            if (p.Upgrades[1]) storage += 0.5;
            if (p.Upgrades[3]) build += 0.10;
            if (p.Upgrades[4]) offline += 4;

            double climate = 0;
            double observatory = 0;
            double flat = 0;
            double procSpeed = 0;
            double marketBonus = 0;
            bool market = false, port = false;
            int portLevel = 0, commLevel = 0, researchCenterLevel = 0;

            for (int i = 0; i < s.Buildings.Count; i++)
            {
                BuildingInstance b = s.Buildings[i];
                BuildingDef d = b.Def;
                if (d == null) continue;
                switch (d.Role)
                {
                    case BuildingRole.Storage: flat += d.RoleValue * LevelMath.Capacity(b.Level); break;
                    case BuildingRole.Market:
                        market = true;
                        marketBonus = Math.Max(marketBonus, d.RoleValue * (b.Level - 1));
                        break;
                    case BuildingRole.Port: port = true; portLevel = Math.Max(portLevel, b.Level); break;
                    case BuildingRole.CommTower: commLevel = Math.Max(commLevel, b.Level); trade += d.RoleValue * b.Level; break;
                    case BuildingRole.ResearchCenter: researchCenterLevel = Math.Max(researchCenterLevel, b.Level); break;
                    case BuildingRole.Observatory: observatory += d.RoleValue * b.Level; break;
                    case BuildingRole.ClimateCore: climate += d.RoleValue * b.Level; break;
                    case BuildingRole.IndustrialCenter: procSpeed += d.RoleValue * b.Level; break;
                }
            }

            m.GlobalProd = (1.0 + prod + observatory) * (1.0 + climate);
            m.StorageMult = 1.0 + storage;
            m.InputSave = Math.Min(0.5, 0.025 * gu[GlobalUpgrades.Efficiency]);
            m.ClickPower = 1.0 + click;
            m.BuildDiscount = Math.Min(0.6, build);
            m.UpgradeDiscount = Math.Min(0.6, upgr);
            m.ResearchDiscount = Math.Min(0.6, 0.03 * researchCenterLevel);
            m.TradeBonus = trade;
            m.SellMult = (1.0 + sell) * (1.0 + marketBonus);
            m.OfflineHours = Math.Min(MaxOfflineHours, offline);
            m.ProcessingSpeed = procSpeed;
            m.HasMarket = market;
            m.HasPort = port;
            m.TradeSlots = port ? Math.Min(6, 2 + portLevel / 3 + commLevel / 4) : 0;

            for (int r = 0; r < ResourceInfo.Count; r++)
            {
                if (r == 0) { m.Capacity[0] = ResourceStore.Unlimited; continue; }
                m.Capacity[r] = (ResourceStore.BaseCapacity + flat) * m.StorageMult;
            }
        }
    }
}
