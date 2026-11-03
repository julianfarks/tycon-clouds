using System;
using CloudGarden.Buildings;
using CloudGarden.Construction;
using CloudGarden.Economy;
using CloudGarden.Islands;
using CloudGarden.Production;
using CloudGarden.Progression;
using CloudGarden.Research;

namespace CloudGarden.Core
{
    public enum PlaceResult
    {
        Ok = 0,
        UnknownBuilding,
        Locked,
        RegionLocked,
        OutOfBounds,
        Overlap,
        CannotAfford,
        MaxCount
    }

    /// <summary>Player actions. Every action validates its rules and keeps the model consistent.</summary>
    public sealed partial class GameState
    {
        public const double RefundFraction = 0.6;

        // ================= BUILDINGS =================

        public static void Footprint(BuildingDef d, int rotation, out int w, out int h)
        {
            if ((rotation & 1) == 0) { w = d.Width; h = d.Depth; }
            else { w = d.Depth; h = d.Width; }
        }

        public bool IsUnlocked(BuildingDef d)
        {
            return LockReason(d).Length == 0;
        }

        /// <summary>Empty string when the building is available.</summary>
        public string LockReason(BuildingDef d)
        {
            if (d == null) return "Edificio desconocido";
            if (d.RequiredPrestigeUpgrade >= 0 && !Prestige.Upgrades[d.RequiredPrestigeUpgrade])
                return "Mejora de Renacimiento: " + PrestigeState.UpgradeNames[d.RequiredPrestigeUpgrade];
            if (!RegionUnlocked[d.MinRegion])
                return "Requiere: " + RegionDatabase.Get(d.MinRegion).Name;
            if (!string.IsNullOrEmpty(d.RequiredResearch) && !Research.Contains(d.RequiredResearch))
            {
                ResearchDef r = ResearchDatabase.Get(d.RequiredResearch);
                return "Investigar: " + (r != null ? r.Name : d.RequiredResearch);
            }
            return "";
        }

        public double BuildCoinCost(BuildingDef d)
        {
            return d.BaseCost * Math.Pow(d.CostGrowth, CountOf(d.Id)) * (1.0 - Mods.BuildDiscount);
        }

        public double BuildMaterialMultiplier(BuildingDef d)
        {
            return Math.Pow(1.08, CountOf(d.Id));
        }

        public bool CanAffordBuild(BuildingDef d)
        {
            return Res.CanAffordCoins(BuildCoinCost(d)) && Res.CanAfford(d.Materials, BuildMaterialMultiplier(d));
        }

        /// <summary>Checks everything about a placement except the cost.</summary>
        public PlaceResult CheckPlacement(BuildingDef d, int region, int x, int y, int rotation, int ignoreUid)
        {
            if (d == null) return PlaceResult.UnknownBuilding;
            if (region < 0 || region >= RegionDatabase.Count || !RegionUnlocked[region]) return PlaceResult.RegionLocked;
            if (!IsUnlocked(d)) return PlaceResult.Locked;
            if (ignoreUid == 0 && d.MaxCount > 0 && CountOf(d.Id) >= d.MaxCount) return PlaceResult.MaxCount;
            int w, h;
            Footprint(d, rotation, out w, out h);
            IslandGrid g = Grids[region];
            for (int i = 0; i < w; i++)
                for (int j = 0; j < h; j++)
                    if (!g.InBounds(x + i, y + j)) return PlaceResult.OutOfBounds;
            if (!g.CanPlace(x, y, w, h, ignoreUid)) return PlaceResult.Overlap;
            return PlaceResult.Ok;
        }

        public PlaceResult TryBuild(string defId, int region, int x, int y, int rotation, out BuildingInstance created)
        {
            created = null;
            BuildingDef d = BuildingDatabase.Get(defId);
            PlaceResult check = CheckPlacement(d, region, x, y, rotation, 0);
            if (check != PlaceResult.Ok) return check;

            double coins = BuildCoinCost(d);
            double matMul = BuildMaterialMultiplier(d);
            if (!Res.CanAffordCoins(coins) || !Res.CanAfford(d.Materials, matMul)) return PlaceResult.CannotAfford;

            Res.TrySpendCoins(coins);
            Res.TrySpend(d.Materials, matMul);

            BuildingInstance b = new BuildingInstance();
            b.Uid = NextUid++;
            b.DefId = d.Id;
            b.Def = d;
            b.Region = region;
            b.X = x;
            b.Y = y;
            b.Rotation = rotation & 3;
            b.Level = 1;
            b.PaidCoins = coins;
            AddToWorld(b);

            Stats.BuildingsBuilt++;
            Stats.EverBuilt.Add(d.Id);
            created = b;
            ModsDirty = true;
            OrderDirty = true;
            RecomputeMods();
            RaiseBuildingAdded(b);
            RaiseChanged();
            return PlaceResult.Ok;
        }

        /// <summary>Inserts an already validated building (used by build and by save loading).</summary>
        public void AddToWorld(BuildingInstance b)
        {
            int w, h;
            Footprint(b.Def, b.Rotation, out w, out h);
            Grids[b.Region].Place(b.Uid, b.X, b.Y, w, h);
            Buildings.Add(b);
            if (b.Uid >= NextUid) NextUid = b.Uid + 1;
            OrderDirty = true;
            ModsDirty = true;
        }

        public double UpgradeCost(BuildingInstance b)
        {
            if (b.Level >= LevelMath.MaxLevel) return 0;
            return LevelMath.UpgradeCost(b.Def.BaseCost, b.Level) * (1.0 - Mods.UpgradeDiscount);
        }

        public bool TryUpgrade(int uid)
        {
            BuildingInstance b = FindBuilding(uid);
            if (b == null || b.Level >= LevelMath.MaxLevel) return false;
            double cost = UpgradeCost(b);
            if (!Res.TrySpendCoins(cost)) return false;
            b.Level++;
            b.PaidCoins += cost;
            Stats.BuildingsUpgraded++;
            if (b.Level > Stats.MaxLevelReached) Stats.MaxLevelReached = b.Level;
            ModsDirty = true;
            RecomputeMods();
            RaiseBuildingChanged(b);
            RaiseChanged();
            return true;
        }

        public PlaceResult TryMove(int uid, int region, int x, int y, int rotation)
        {
            BuildingInstance b = FindBuilding(uid);
            if (b == null) return PlaceResult.UnknownBuilding;
            PlaceResult check = CheckPlacement(b.Def, region, x, y, rotation, uid);
            if (check != PlaceResult.Ok) return check;

            int w, h;
            Footprint(b.Def, b.Rotation, out w, out h);
            Grids[b.Region].Remove(b.Uid, b.X, b.Y, w, h);
            b.Region = region;
            b.X = x;
            b.Y = y;
            b.Rotation = rotation & 3;
            Footprint(b.Def, b.Rotation, out w, out h);
            Grids[b.Region].Place(b.Uid, b.X, b.Y, w, h);
            RaiseBuildingChanged(b);
            RaiseChanged();
            return PlaceResult.Ok;
        }

        /// <summary>Removes a building and refunds 60% of what was invested in it.</summary>
        public bool TryRemove(int uid)
        {
            BuildingInstance b = FindBuilding(uid);
            if (b == null) return false;
            int w, h;
            Footprint(b.Def, b.Rotation, out w, out h);
            Grids[b.Region].Remove(b.Uid, b.X, b.Y, w, h);
            Buildings.Remove(b);

            Res.Amount[0] += b.PaidCoins * RefundFraction;
            for (int i = 0; i < b.Def.Materials.Count; i++)
            {
                ResourceAmount m = b.Def.Materials[i];
                Res.Produce(m.Type, m.Amount * RefundFraction, false);
            }

            OrderDirty = true;
            ModsDirty = true;
            RecomputeMods();
            RaiseBuildingRemoved(b);
            RaiseChanged();
            return true;
        }

        // ================= REGIONS =================

        public bool CanUnlockRegion(int index, out string reason)
        {
            reason = "";
            if (index <= 0 || index >= RegionDatabase.Count) { reason = "Región inválida"; return false; }
            if (RegionUnlocked[index]) { reason = "Ya desbloqueada"; return false; }
            if (!RegionUnlocked[index - 1]) { reason = "Desbloquea antes: " + RegionDatabase.Get(index - 1).Name; return false; }
            RegionDef r = RegionDatabase.Get(index);
            if (!HasResearch(r.RequiredResearch))
            {
                ResearchDef rd = ResearchDatabase.Get(r.RequiredResearch);
                reason = "Investigar: " + (rd != null ? rd.Name : r.RequiredResearch);
                return false;
            }
            if (!Res.CanAffordCoins(r.UnlockCoins) || !Res.CanAfford(r.UnlockMaterials)) { reason = "Recursos insuficientes"; return false; }
            return true;
        }

        public bool TryUnlockRegion(int index)
        {
            string reason;
            if (!CanUnlockRegion(index, out reason)) return false;
            RegionDef r = RegionDatabase.Get(index);
            Res.TrySpendCoins(r.UnlockCoins);
            Res.TrySpend(r.UnlockMaterials, 1.0);
            RegionUnlocked[index] = true;
            Grids[index].SetExpansion(0);
            ModsDirty = true;
            RecomputeMods();
            RaiseToast("¡Nueva región desbloqueada: " + r.Name + "!");
            RaiseChanged();
            return true;
        }

        public double ExpansionCost(int region)
        {
            return RegionDatabase.ExpansionCost(region, Grids[region].Expansion);
        }

        public bool CanExpand(int region)
        {
            return region >= 0 && region < RegionDatabase.Count && RegionUnlocked[region]
                && Grids[region].Expansion < IslandGrid.MaxExpansion;
        }

        public bool TryExpand(int region)
        {
            if (!CanExpand(region)) return false;
            if (!Res.TrySpendCoins(ExpansionCost(region))) return false;
            Grids[region].SetExpansion(Grids[region].Expansion + 1);
            RaiseChanged();
            return true;
        }

        // ================= RESEARCH =================

        public double ResearchCoinCost(ResearchDef r)
        {
            return r.CostCoins * (1.0 - Mods.ResearchDiscount);
        }

        public bool CanResearch(ResearchDef r, out string reason)
        {
            reason = "";
            if (r == null) { reason = "Desconocida"; return false; }
            if (Research.Contains(r.Id)) { reason = "Completada"; return false; }
            if (r.RequiresRebirth && Prestige.Rebirths < 1) { reason = "Requiere Renacimiento"; return false; }
            for (int i = 0; i < r.Prerequisites.Length; i++)
            {
                if (!Research.Contains(r.Prerequisites[i]))
                {
                    ResearchDef p = ResearchDatabase.Get(r.Prerequisites[i]);
                    reason = "Antes: " + (p != null ? p.Name : r.Prerequisites[i]);
                    return false;
                }
            }
            if (!string.IsNullOrEmpty(r.RequiredBuilding) && !HasBuilding(r.RequiredBuilding))
            {
                BuildingDef b = BuildingDatabase.Get(r.RequiredBuilding);
                reason = "Construir: " + (b != null ? b.Name : r.RequiredBuilding);
                return false;
            }
            return true;
        }

        public bool CanAffordResearch(ResearchDef r)
        {
            return Res.CanAffordCoins(ResearchCoinCost(r)) && Res.CanAfford(r.Materials);
        }

        public bool TryResearch(string id)
        {
            ResearchDef r = ResearchDatabase.Get(id);
            string reason;
            if (!CanResearch(r, out reason) || !CanAffordResearch(r)) return false;
            Res.TrySpendCoins(ResearchCoinCost(r));
            Res.TrySpend(r.Materials, 1.0);
            Research.Add(r.Id);
            ModsDirty = true;
            RecomputeMods();
            RaiseToast("Investigación completada: " + r.Name);
            RaiseChanged();
            return true;
        }

        // ================= GLOBAL UPGRADES =================

        public double GlobalUpgradeCost(int index)
        {
            return GlobalUpgrades.Cost(index, GlobalUpgradeLevels[index]);
        }

        public bool TryBuyGlobalUpgrade(int index)
        {
            if (index < 0 || index >= GlobalUpgrades.Count) return false;
            if (GlobalUpgradeLevels[index] >= GlobalUpgrades.MaxLevel(index)) return false;
            if (!Res.TrySpendCoins(GlobalUpgradeCost(index))) return false;
            GlobalUpgradeLevels[index]++;
            ModsDirty = true;
            RecomputeMods();
            RaiseChanged();
            return true;
        }

        // ================= MANUAL ACTIONS & SELLING =================

        /// <summary>Manual dew collection (tap). Returns the amount stored.</summary>
        public double Collect()
        {
            if (ModsDirty) RecomputeMods();
            double accepted = Res.Produce(ResourceType.Dew, Mods.ClickPower, true);
            Stats.AccProd[(int)ResourceType.Dew] += accepted;
            Stats.ManualCollects++;
            return accepted;
        }

        /// <summary>Manual sale of a resource (all units). Returns coins obtained.</summary>
        public double SellAll(ResourceType type)
        {
            double coins = ProductionSystem.SellAmount(this, type, Res.Amount[(int)type]);
            if (coins > 0) RaiseChanged();
            return coins;
        }

        public void SetAutoSell(ResourceType type, bool value)
        {
            if (type == ResourceType.Coins) return;
            AutoSell[(int)type] = value;
            RaiseChanged();
        }

        public void CycleReserve(ResourceType type)
        {
            if (type == ResourceType.Coins) return;
            int i = (int)type;
            ReserveStep[i] = (ReserveStep[i] + 1) % ReserveFractions.Length;
            RaiseChanged();
        }

        // ================= REBIRTH =================

        /// <summary>Resets the economic progress of the run. Called by PrestigeSystem.Rebirth.</summary>
        public void ResetForRebirth()
        {
            // Keep exclusive (rebirth) research, drop the rest.
            var keep = new System.Collections.Generic.List<string>();
            foreach (string id in Research)
            {
                ResearchDef r = ResearchDatabase.Get(id);
                if (r != null && r.RequiresRebirth) keep.Add(id);
            }
            ResetToNewGame();
            for (int i = 0; i < keep.Count; i++) Research.Add(keep[i]);
            Res.CoinsEarnedRun = 0;
            if (Prestige.Upgrades[2]) Res.Amount[0] += 10000;
            ModsDirty = true;
            RecomputeMods();
            RaiseWorldReset();
            RaiseChanged();
        }

        /// <summary>Used by the save loader after replacing the model data.</summary>
        public void NotifyWorldReset()
        {
            OrderDirty = true;
            ModsDirty = true;
            RecomputeMods();
            RaiseWorldReset();
            RaiseChanged();
        }
    }
}
