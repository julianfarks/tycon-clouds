using System;
using System.Collections.Generic;
using CloudGarden.Buildings;
using CloudGarden.Construction;
using CloudGarden.Core;
using CloudGarden.Economy;
using CloudGarden.Islands;
using CloudGarden.Progression;
using CloudGarden.Research;
using CloudGarden.Trading;

namespace CloudGarden.SaveSystem
{
    /// <summary>Converts the game model to/from DTOs and validates untrusted data.</summary>
    public static class SaveMapper
    {
        // ---------------- model -> data ----------------
        public static SaveData ToData(GameState s, long nowUtcTicks)
        {
            SaveData d = new SaveData();
            d.Version = SaveData.CurrentVersion;
            d.SavedUtcTicks = nowUtcTicks;
            d.MaxSeenUtcTicks = Math.Max(s.MaxSeenUtcTicks, nowUtcTicks);
            d.NextUid = s.NextUid;

            d.Amounts = (double[])s.Res.Amount.Clone();
            d.Produced = (double[])s.Res.TotalProduced.Clone();
            d.Consumed = (double[])s.Res.TotalConsumed.Clone();
            d.Wasted = (double[])s.Res.TotalWasted.Clone();
            d.CoinsEarnedRun = s.Res.CoinsEarnedRun;
            d.CoinsEarnedLifetime = s.Res.CoinsEarnedLifetime;
            d.CoinsSpentLifetime = s.Res.CoinsSpentLifetime;

            for (int i = 0; i < s.Buildings.Count; i++)
            {
                BuildingInstance b = s.Buildings[i];
                BuildingSave bs = new BuildingSave();
                bs.Uid = b.Uid; bs.DefId = b.DefId; bs.Region = b.Region; bs.X = b.X; bs.Y = b.Y;
                bs.Rotation = b.Rotation; bs.Level = b.Level; bs.PaidCoins = b.PaidCoins;
                d.Buildings.Add(bs);
            }

            d.RegionUnlocked = (bool[])s.RegionUnlocked.Clone();
            d.RegionExpansion = new int[RegionDatabase.Count];
            for (int i = 0; i < RegionDatabase.Count; i++) d.RegionExpansion[i] = s.Grids[i].Expansion;

            foreach (string r in s.Research) d.Research.Add(r);
            d.GlobalUpgrades = (int[])s.GlobalUpgradeLevels.Clone();
            d.AutoSell = (bool[])s.AutoSell.Clone();
            d.ReserveStep = (int[])s.ReserveStep.Clone();
            foreach (string m in s.Stats.Milestones) d.Milestones.Add(m);
            foreach (string e in s.Stats.EverBuilt) d.EverBuilt.Add(e);

            GameStats st = s.Stats;
            d.BuildingsBuilt = st.BuildingsBuilt;
            d.BuildingsUpgraded = st.BuildingsUpgraded;
            d.TradesCompleted = st.TradesCompleted;
            d.ManualCollects = st.ManualCollects;
            d.MaxLevelReached = st.MaxLevelReached;
            d.SoldCoins = st.SoldCoins;
            d.OfflineSecondsTotal = st.OfflineSecondsTotal;
            d.PlaySeconds = st.PlaySeconds;

            for (int i = 0; i < s.Trade.Orders.Count; i++)
            {
                TradeOrder o = s.Trade.Orders[i];
                OrderSave os = new OrderSave();
                os.Id = o.Id; os.Merchant = o.Merchant; os.Special = o.Special;
                os.WantTypes = new int[o.Wants.Count];
                os.WantAmounts = new double[o.Wants.Count];
                for (int k = 0; k < o.Wants.Count; k++)
                {
                    os.WantTypes[k] = (int)o.Wants[k].Type;
                    os.WantAmounts[k] = o.Wants[k].Amount;
                }
                os.RewardCoins = o.RewardCoins;
                os.BonusType = (int)o.Bonus.Type;
                os.BonusAmount = o.Bonus.Amount;
                d.Orders.Add(os);
            }
            d.TradeSeed = s.Trade.Seed;
            d.TradeNextId = s.Trade.NextId;
            d.TradeCompleted = s.Trade.Completed;
            d.TradeRerolls = s.Trade.Rerolls;

            PrestigeState p = s.Prestige;
            d.Stars = p.Stars;
            d.StarsEarnedTotal = p.StarsEarnedTotal;
            d.Rebirths = p.Rebirths;
            d.CampaignCompleted = p.CampaignCompleted;
            d.PrestigeUpgrades = (bool[])p.Upgrades.Clone();
            d.ActiveChallenge = p.ActiveChallenge;
            d.ChallengeStartProduced = p.ChallengeStartProduced;
            d.ChallengeTimeLeft = p.ChallengeTimeLeft;
            d.ChallengeDone = (bool[])p.ChallengeDone.Clone();
            return d;
        }

        // ---------------- validation ----------------

        static double Clean(double v)
        {
            if (double.IsNaN(v) || double.IsInfinity(v) || v < 0) return 0;
            return v;
        }

        static T[] Fit<T>(T[] source, int length)
        {
            T[] result = new T[length];
            if (source != null) Array.Copy(source, result, Math.Min(source.Length, length));
            return result;
        }

        /// <summary>
        /// Repairs/normalizes a loaded save. Returns false when the data is unusable (null or from a
        /// newer, unknown version).
        /// </summary>
        public static bool Validate(SaveData d)
        {
            if (d == null) return false;
            if (d.Version < 1 || d.Version > SaveData.CurrentVersion) return false;

            d.Amounts = Fit(d.Amounts, ResourceInfo.Count);
            d.Produced = Fit(d.Produced, ResourceInfo.Count);
            d.Consumed = Fit(d.Consumed, ResourceInfo.Count);
            d.Wasted = Fit(d.Wasted, ResourceInfo.Count);
            for (int i = 0; i < ResourceInfo.Count; i++)
            {
                d.Amounts[i] = Clean(d.Amounts[i]);
                d.Produced[i] = Clean(d.Produced[i]);
                d.Consumed[i] = Clean(d.Consumed[i]);
                d.Wasted[i] = Clean(d.Wasted[i]);
            }
            d.CoinsEarnedRun = Clean(d.CoinsEarnedRun);
            d.CoinsEarnedLifetime = Clean(d.CoinsEarnedLifetime);
            d.CoinsSpentLifetime = Clean(d.CoinsSpentLifetime);

            d.RegionUnlocked = Fit(d.RegionUnlocked, RegionDatabase.Count);
            d.RegionExpansion = Fit(d.RegionExpansion, RegionDatabase.Count);
            d.RegionUnlocked[0] = true;
            for (int i = 0; i < RegionDatabase.Count; i++)
            {
                if (d.RegionExpansion[i] < 0) d.RegionExpansion[i] = 0;
                if (d.RegionExpansion[i] > IslandGrid.MaxExpansion) d.RegionExpansion[i] = IslandGrid.MaxExpansion;
                if (i > 0 && d.RegionUnlocked[i] && !d.RegionUnlocked[i - 1]) d.RegionUnlocked[i] = false;
            }

            d.GlobalUpgrades = Fit(d.GlobalUpgrades, GlobalUpgrades.Count);
            for (int i = 0; i < GlobalUpgrades.Count; i++)
                d.GlobalUpgrades[i] = Math.Max(0, Math.Min(GlobalUpgrades.MaxLevel(i), d.GlobalUpgrades[i]));
            d.AutoSell = Fit(d.AutoSell, ResourceInfo.Count);
            d.ReserveStep = Fit(d.ReserveStep, ResourceInfo.Count);
            for (int i = 0; i < ResourceInfo.Count; i++)
                d.ReserveStep[i] = Math.Max(0, Math.Min(GameState.ReserveFractions.Length - 1, d.ReserveStep[i]));

            d.PrestigeUpgrades = Fit(d.PrestigeUpgrades, PrestigeState.UpgradeCount);
            d.ChallengeDone = Fit(d.ChallengeDone, PrestigeState.ChallengeCount);
            if (d.ActiveChallenge >= PrestigeState.ChallengeCount || d.ActiveChallenge < -1) d.ActiveChallenge = -1;
            d.Stars = Math.Max(0, d.Stars);
            d.StarsEarnedTotal = Math.Max(0, d.StarsEarnedTotal);
            d.Rebirths = Math.Max(0, d.Rebirths);
            d.ChallengeStartProduced = Clean(d.ChallengeStartProduced);
            d.ChallengeTimeLeft = Clean(d.ChallengeTimeLeft);

            if (d.Buildings == null) d.Buildings = new List<BuildingSave>();
            if (d.Research == null) d.Research = new List<string>();
            if (d.Milestones == null) d.Milestones = new List<string>();
            if (d.EverBuilt == null) d.EverBuilt = new List<string>();
            if (d.Orders == null) d.Orders = new List<OrderSave>();
            d.SoldCoins = Clean(d.SoldCoins);
            d.OfflineSecondsTotal = Clean(d.OfflineSecondsTotal);
            d.PlaySeconds = Clean(d.PlaySeconds);
            if (d.SavedUtcTicks < 0) d.SavedUtcTicks = 0;
            if (d.MaxSeenUtcTicks < d.SavedUtcTicks) d.MaxSeenUtcTicks = d.SavedUtcTicks;
            return true;
        }

        // ---------------- data -> model ----------------

        /// <summary>Loads validated data into the model (replacing its content). Overlapping or invalid buildings are dropped.</summary>
        public static void Apply(SaveData d, GameState s)
        {
            Validate(d);

            // Reset structural content.
            s.Buildings.Clear();
            s.ProductionOrder.Clear();
            s.Research.Clear();
            for (int i = 0; i < RegionDatabase.Count; i++)
            {
                s.Grids[i].Clear();
                s.Grids[i].SetExpansion(d.RegionExpansion[i]);
                s.RegionUnlocked[i] = d.RegionUnlocked[i];
            }

            for (int i = 0; i < ResourceInfo.Count; i++)
            {
                s.Res.Amount[i] = d.Amounts[i];
                s.Res.TotalProduced[i] = d.Produced[i];
                s.Res.TotalConsumed[i] = d.Consumed[i];
                s.Res.TotalWasted[i] = d.Wasted[i];
                s.AutoSell[i] = d.AutoSell[i];
                s.ReserveStep[i] = d.ReserveStep[i];
            }
            s.Res.CoinsEarnedRun = d.CoinsEarnedRun;
            s.Res.CoinsEarnedLifetime = d.CoinsEarnedLifetime;
            s.Res.CoinsSpentLifetime = d.CoinsSpentLifetime;

            for (int i = 0; i < d.Research.Count; i++)
                if (ResearchDatabase.Get(d.Research[i]) != null) s.Research.Add(d.Research[i]);
            for (int i = 0; i < GlobalUpgrades.Count; i++) s.GlobalUpgradeLevels[i] = d.GlobalUpgrades[i];

            GameStats st = s.Stats;
            st.Milestones.Clear();
            st.EverBuilt.Clear();
            for (int i = 0; i < d.Milestones.Count; i++) st.Milestones.Add(d.Milestones[i]);
            for (int i = 0; i < d.EverBuilt.Count; i++) if (BuildingDatabase.Get(d.EverBuilt[i]) != null) st.EverBuilt.Add(d.EverBuilt[i]);
            st.BuildingsBuilt = d.BuildingsBuilt;
            st.BuildingsUpgraded = d.BuildingsUpgraded;
            st.TradesCompleted = d.TradesCompleted;
            st.ManualCollects = d.ManualCollects;
            st.MaxLevelReached = Math.Max(1, d.MaxLevelReached);
            st.SoldCoins = d.SoldCoins;
            st.OfflineSecondsTotal = d.OfflineSecondsTotal;
            st.PlaySeconds = d.PlaySeconds;
            st.Sanitize();

            // Buildings (validate each one against the grid).
            int maxUid = 0;
            for (int i = 0; i < d.Buildings.Count; i++)
            {
                BuildingSave bs = d.Buildings[i];
                if (bs == null) continue;
                BuildingDef def = BuildingDatabase.Get(bs.DefId);
                if (def == null) continue;
                if (bs.Region < 0 || bs.Region >= RegionDatabase.Count || !s.RegionUnlocked[bs.Region]) continue;
                if (bs.Uid <= 0 || s.FindBuilding(bs.Uid) != null) continue;
                int rot = bs.Rotation & 3;
                int w, h;
                GameState.Footprint(def, rot, out w, out h);
                if (!s.Grids[bs.Region].CanPlace(bs.X, bs.Y, w, h, 0)) continue;

                BuildingInstance b = new BuildingInstance();
                b.Uid = bs.Uid; b.DefId = def.Id; b.Def = def; b.Region = bs.Region;
                b.X = bs.X; b.Y = bs.Y; b.Rotation = rot;
                b.Level = LevelMath.Clamp(bs.Level);
                b.PaidCoins = Clean(bs.PaidCoins);
                s.AddToWorld(b);
                if (b.Uid > maxUid) maxUid = b.Uid;
            }
            s.NextUid = Math.Max(Math.Max(d.NextUid, maxUid + 1), 1);

            // Trade
            s.Trade = new TradeState();
            s.Trade.Seed = d.TradeSeed == 0 ? 2463534242u : d.TradeSeed;
            s.Trade.NextId = Math.Max(1, d.TradeNextId);
            s.Trade.Completed = Math.Max(0, d.TradeCompleted);
            s.Trade.Rerolls = Math.Max(0, Math.Min(TradeState.MaxRerolls, d.TradeRerolls));
            for (int i = 0; i < d.Orders.Count; i++)
            {
                OrderSave os = d.Orders[i];
                if (os == null || os.WantTypes == null || os.WantAmounts == null) continue;
                TradeOrder o = new TradeOrder();
                o.Id = os.Id; o.Merchant = os.Merchant ?? "Mercader"; o.Special = os.Special;
                int n = Math.Min(os.WantTypes.Length, os.WantAmounts.Length);
                for (int k = 0; k < n; k++)
                {
                    if (os.WantTypes[k] <= 0 || os.WantTypes[k] >= ResourceInfo.Count) continue;
                    o.Wants.Add(new ResourceAmount((ResourceType)os.WantTypes[k], Math.Max(1, Clean(os.WantAmounts[k]))));
                }
                if (o.Wants.Count == 0) continue;
                o.RewardCoins = Clean(os.RewardCoins);
                int bt = os.BonusType > 0 && os.BonusType < ResourceInfo.Count ? os.BonusType : (int)ResourceType.Dew;
                o.Bonus = new ResourceAmount((ResourceType)bt, Math.Max(1, Clean(os.BonusAmount)));
                s.Trade.Orders.Add(o);
            }

            // Prestige
            PrestigeState p = new PrestigeState();
            p.Stars = d.Stars; p.StarsEarnedTotal = d.StarsEarnedTotal; p.Rebirths = d.Rebirths;
            p.CampaignCompleted = d.CampaignCompleted;
            for (int i = 0; i < PrestigeState.UpgradeCount; i++) p.Upgrades[i] = d.PrestigeUpgrades[i];
            for (int i = 0; i < PrestigeState.ChallengeCount; i++) p.ChallengeDone[i] = d.ChallengeDone[i];
            p.ActiveChallenge = d.ActiveChallenge;
            p.ChallengeStartProduced = d.ChallengeStartProduced;
            p.ChallengeTimeLeft = d.ChallengeTimeLeft;
            s.Prestige = p;

            s.LastSaveUtcTicks = d.SavedUtcTicks;
            s.MaxSeenUtcTicks = d.MaxSeenUtcTicks;

            s.OrderDirty = true;
            s.ModsDirty = true;
            s.RecomputeMods();
            s.Res.Sanitize();
            s.NotifyWorldReset();
        }
    }
}
