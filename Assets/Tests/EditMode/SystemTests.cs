using System;
using System.IO;
using System.Collections.Generic;
using CloudGarden.Buildings;
using CloudGarden.Core;
using CloudGarden.Economy;
using CloudGarden.Islands;
using CloudGarden.OfflineProduction;
using CloudGarden.Progression;
using CloudGarden.Research;
using CloudGarden.SaveSystem;
using CloudGarden.Trading;
using NUnit.Framework;
using static CloudGarden.Tests.TestHelpers;

namespace CloudGarden.Tests
{
    public class ConstructionTests
    {
        [Test]
        public void Build_SpendsCoins_AndCostGrowsWithCopies()
        {
            GameState s = new GameState();
            double c1 = s.BuildCoinCost(BuildingDatabase.Get("dew_collector"));
            Assert.AreEqual(15, c1, 1e-9);
            Build(s, "dew_collector", 0, 0);
            Assert.AreEqual(0, s.Res.Get(ResourceType.Coins), 1e-9);
            double c2 = s.BuildCoinCost(BuildingDatabase.Get("dew_collector"));
            Assert.AreEqual(15 * BuildingDatabase.Get("dew_collector").CostGrowth, c2, 1e-9);
            BuildingInstance b;
            Assert.AreEqual(PlaceResult.CannotAfford, s.TryBuild("dew_collector", 0, 2, 0, 0, out b));
            Assert.IsNull(b);
            Assert.AreEqual(1, s.Buildings.Count);
        }

        [Test]
        public void Build_RequiresResearchAndRegion()
        {
            GameState s = Rich();
            BuildingInstance b;
            Assert.AreEqual(PlaceResult.Locked, s.TryBuild("distiller", 0, 0, 0, 0, out b));
            GrantResearch(s, "distillation");
            Assert.AreEqual(PlaceResult.Ok, s.TryBuild("distiller", 0, 0, 0, 0, out b));
            GrantResearch(s, "tropical_farming");
            Assert.AreEqual(PlaceResult.Locked, s.TryBuild("tropical_orchard", 0, 2, 0, 0, out b));
            Assert.AreEqual(PlaceResult.RegionLocked, s.TryBuild("dew_collector", 1, 0, 0, 0, out b));
        }

        [Test]
        public void Build_DetectsOverlap_AndBounds()
        {
            GameState s = Rich();
            Build(s, "dew_collector", 0, 0); // tiles (0..1, 0..1)
            BuildingInstance b;
            Assert.AreEqual(PlaceResult.Overlap, s.TryBuild("dew_collector", 0, 1, 1, 0, out b));
            Assert.AreEqual(PlaceResult.Overlap, s.TryBuild("dew_collector", 0, 0, 0, 0, out b));
            Assert.AreEqual(PlaceResult.Ok, s.TryBuild("dew_collector", 0, 2, 0, 0, out b));
            Assert.AreEqual(PlaceResult.OutOfBounds, s.TryBuild("dew_collector", 0, -5, 0, 0, out b));
            Assert.AreEqual(PlaceResult.OutOfBounds, s.TryBuild("dew_collector", 0, 3, 3, 0, out b));
            Assert.AreEqual(PlaceResult.Ok, s.TryBuild("dew_collector", 0, 2, 2, 0, out b));
            Assert.AreEqual(PlaceResult.Ok, s.TryBuild("dew_collector", 0, -4, -4, 0, out b));
        }

        [Test]
        public void Rotation_SwapsFootprint()
        {
            GameState s = Rich();
            GrantResearch(s, "distillation", "botany");
            // Greenhouse is 3x2: rotated it becomes 2x3.
            BuildingInstance g = Build(s, "greenhouse", 0, 0, 0, 0);
            Assert.AreEqual(3, g.FootprintWidth);
            Assert.AreEqual(2, g.FootprintDepth);
            BuildingInstance b;
            Assert.AreEqual(PlaceResult.Ok, s.TryBuild("dew_collector", 0, 0, 2, 0, out b));  // right above (y=2)
            Assert.AreEqual(PlaceResult.Overlap, s.TryBuild("dew_collector", 0, 2, 1, 0, out b)); // overlaps x=2,y=1
            Assert.AreEqual(PlaceResult.Ok, s.TryMove(g.Uid, 0, -4, -4, 1));
            Assert.AreEqual(2, g.FootprintWidth);
            Assert.AreEqual(3, g.FootprintDepth);
            // (-4..-3, -4..-2) occupied, (-2,-4) free.
            Assert.AreEqual(PlaceResult.Overlap, s.TryBuild("dew_collector", 0, -3, -3, 0, out b));
            Assert.AreEqual(PlaceResult.Ok, s.TryBuild("dew_collector", 0, -2, -4, 0, out b));
        }

        [Test]
        public void Move_ChecksCollisions_AndFreesOldTiles()
        {
            GameState s = Rich();
            BuildingInstance a = Build(s, "dew_collector", 0, 0);
            BuildingInstance c = Build(s, "dew_collector", 2, 0);
            Assert.AreEqual(PlaceResult.Overlap, s.TryMove(a.Uid, 0, 2, 0, 0));
            Assert.AreEqual(0, a.X);
            Assert.AreEqual(PlaceResult.Ok, s.TryMove(a.Uid, 0, 0, 2, 0));
            Assert.AreEqual(PlaceResult.Ok, s.TryMove(c.Uid, 0, 0, 0, 0)); // old tiles of "a" are free
            Assert.AreEqual(a.Uid, s.Grids[0].OccupantAt(0, 2));
            Assert.AreEqual(c.Uid, s.Grids[0].OccupantAt(1, 1));
        }

        [Test]
        public void Remove_RefundsSixtyPercent_AndFreesSpace()
        {
            GameState s = Rich();
            BuildingInstance b = Build(s, "dew_collector", 0, 0);
            double before = s.Res.Get(ResourceType.Coins);
            Assert.IsTrue(s.TryRemove(b.Uid));
            Assert.AreEqual(before + 15 * GameState.RefundFraction, s.Res.Get(ResourceType.Coins), 1e-6);
            Assert.AreEqual(0, s.Buildings.Count);
            Assert.AreEqual(0, s.Grids[0].OccupiedTiles);
            Build(s, "dew_collector", 0, 0);
            Assert.IsFalse(s.TryRemove(9999));
        }

        [Test]
        public void MaxCount_LimitsUniqueBuildings()
        {
            GameState s = Rich();
            Build(s, "market", 0, 0);
            BuildingInstance b;
            Assert.AreEqual(PlaceResult.MaxCount, s.TryBuild("market", 0, 2, 0, 0, out b));
        }

        [Test]
        public void Upgrade_LevelsUpToTen_ThenStops_AndCostsGrow()
        {
            GameState s = Rich();
            BuildingInstance b = Build(s, "dew_collector", 0, 0);
            double last = 0;
            for (int lvl = 1; lvl < LevelMath.MaxLevel; lvl++)
            {
                double cost = s.UpgradeCost(b);
                Assert.Greater(cost, last);
                last = cost;
                Assert.IsTrue(s.TryUpgrade(b.Uid));
            }
            Assert.AreEqual(10, b.Level);
            Assert.IsFalse(s.TryUpgrade(b.Uid));
            Assert.AreEqual(10, s.Stats.MaxLevelReached);
        }

        [Test]
        public void Upgrade_NeedsEnoughCoins()
        {
            GameState s = new GameState();
            BuildingInstance b = Build(s, "dew_collector", 0, 0);
            Assert.IsFalse(s.TryUpgrade(b.Uid));
            Assert.AreEqual(1, b.Level);
            Assert.AreEqual(0, s.Res.Get(ResourceType.Coins), 1e-9);
        }

        [Test]
        public void LevelMath_IsMonotonic_AndLevelTenHasBonus()
        {
            for (int l = 1; l < LevelMath.MaxLevel; l++)
            {
                Assert.Greater(LevelMath.Speed(l + 1), LevelMath.Speed(l));
                Assert.Greater(LevelMath.Amount(l + 1), LevelMath.Amount(l));
                Assert.Greater(LevelMath.Capacity(l + 1), LevelMath.Capacity(l));
                Assert.Greater(LevelMath.UpgradeCost(100, l + 1), LevelMath.UpgradeCost(100, l));
            }
            Assert.Greater(LevelMath.Amount(10), LevelMath.Amount(9) * 1.3);
            Assert.AreEqual(3, LevelMath.VisualTier(10));
            Assert.AreEqual(0, LevelMath.VisualTier(1));
            Assert.AreEqual(1, LevelMath.VisualTier(4));
            Assert.AreEqual(2, LevelMath.VisualTier(7));
        }

        [Test]
        public void ExpandTerrain_GrowsTheGrid()
        {
            GameState s = Rich();
            Assert.AreEqual(-4, s.Grids[0].Min);
            double cost = s.ExpansionCost(0);
            Assert.IsTrue(s.TryExpand(0));
            Assert.AreEqual(-5, s.Grids[0].Min);
            Assert.AreEqual(4, s.Grids[0].Max);
            Assert.Greater(s.ExpansionCost(0), cost);
            BuildingInstance b;
            Assert.AreEqual(PlaceResult.Ok, s.TryBuild("dew_collector", 0, -5, -5, 0, out b));
            for (int i = 0; i < 10; i++) s.TryExpand(0);
            Assert.AreEqual(Construction.IslandGrid.MaxExpansion, s.Grids[0].Expansion);
            Assert.IsFalse(s.CanExpand(0));
        }

        [Test]
        public void BuildingStaysAfterExpansion_NoCoordinateShift()
        {
            GameState s = Rich();
            BuildingInstance b = Build(s, "dew_collector", 2, 2);
            s.TryExpand(0);
            Assert.AreEqual(b.Uid, s.Grids[0].OccupantAt(3, 3));
        }
    }

    public class ProgressionTests
    {
        [Test]
        public void Research_RequiresPrerequisites_AndCoins()
        {
            GameState s = new GameState();
            Assert.IsFalse(s.TryResearch("botany"));
            Assert.IsFalse(s.TryResearch("distillation")); // 15 coins < 60
            s.Res.Amount[0] = 1000;
            Assert.IsTrue(s.TryResearch("distillation"));
            Assert.AreEqual(940, s.Res.Get(ResourceType.Coins), 1e-9);
            Assert.IsFalse(s.TryResearch("distillation")); // already done
            Assert.IsTrue(s.TryResearch("botany"));
            Assert.IsTrue(s.Research.Contains("botany"));
        }

        [Test]
        public void Research_RequiresBuilding()
        {
            GameState s = Rich();
            GrantResearch(s, "distillation", "botany", "aerodynamics", "aviation");
            string reason;
            Assert.IsFalse(s.CanResearch(ResearchDatabase.Get("sky_navigation"), out reason)); // needs Puerto Aereo
            StringAssert.Contains("Construir", reason);
            Build(s, "air_port", 0, 0);
            Assert.IsTrue(s.TryResearch("sky_navigation"));
        }

        [Test]
        public void Research_EffectsApplyToModifiers()
        {
            GameState s = Rich();
            Assert.AreEqual(2, s.Mods.OfflineHours, 1e-9);
            GrantResearch(s, "distillation", "botany", "offline_1");
            Assert.AreEqual(4, s.Mods.OfflineHours, 1e-9);
            GrantResearch(s, "offline_2", "offline_3");
            Assert.AreEqual(8, s.Mods.OfflineHours, 1e-9);
            GrantResearch(s, "manual_harvest");
            Assert.AreEqual(2, s.Mods.ClickPower, 1e-9);
            GrantResearch(s, "market_trading");
            Assert.AreEqual(1.1, s.Mods.SellMult, 1e-9);
            GrantResearch(s, "logistics");
            Assert.AreEqual(1.1, s.Mods.StorageMult, 1e-9);
            Assert.AreEqual(220, s.Res.Cap(ResourceType.Pollen), 1e-9);
        }

        [Test]
        public void ResearchCenter_ReducesResearchCost()
        {
            GameState s = Rich();
            ResearchDef r = ResearchDatabase.Get("distillation");
            Assert.AreEqual(60, s.ResearchCoinCost(r), 1e-9);
            BuildingInstance rc = Build(s, "research_center", 0, 0);
            Assert.AreEqual(60 * 0.97, s.ResearchCoinCost(r), 1e-9);
        }

        [Test]
        public void GlobalUpgrades_IncreaseProduction_UntilMaxLevel()
        {
            GameState s = Rich();
            Assert.IsTrue(s.TryBuyGlobalUpgrade(GlobalUpgrades.Production));
            Assert.AreEqual(1.08, s.Mods.GlobalProd, 1e-9);
            for (int i = 0; i < 100; i++) s.TryBuyGlobalUpgrade(GlobalUpgrades.ManualHarvest);
            Assert.AreEqual(GlobalUpgrades.MaxLevel(GlobalUpgrades.ManualHarvest), s.GlobalUpgradeLevels[GlobalUpgrades.ManualHarvest]);
        }

        [Test]
        public void Region_UnlockNeedsResearchResourcesAndPreviousRegion()
        {
            GameState s = Rich();
            string reason;
            Assert.IsFalse(s.CanUnlockRegion(1, out reason));
            GrantResearch(s, "sky_navigation");
            Assert.IsFalse(s.CanUnlockRegion(1, out reason)); // no materials
            s.Res.Amount[(int)ResourceType.Water] = 150;
            s.Res.Amount[(int)ResourceType.Pollen] = 100;
            Assert.IsFalse(s.CanUnlockRegion(2, out reason)); // previous region still locked
            Assert.IsTrue(s.TryUnlockRegion(1));
            Assert.IsTrue(s.RegionUnlocked[1]);
            Assert.AreEqual(0, s.Res.Get(ResourceType.Water), 1e-9);
            Assert.AreEqual(0, s.Res.Get(ResourceType.Pollen), 1e-9);
            Assert.AreEqual(1e12 - RegionDatabase.Get(1).UnlockCoins, s.Res.Get(ResourceType.Coins), 1);
            Assert.IsFalse(s.TryUnlockRegion(1));
        }

        [Test]
        public void Region_BuildingsCanBePlacedOnUnlockedIslands()
        {
            GameState s = Rich();
            GrantResearch(s, "sky_navigation", "tropical_farming");
            s.Res.Amount[(int)ResourceType.Water] = 150;
            s.Res.Amount[(int)ResourceType.Pollen] = 100;
            s.TryUnlockRegion(1);
            BuildingInstance b = Build(s, "tropical_orchard", 0, 0, 1);
            Assert.AreEqual(1, b.Region);
            Assert.AreEqual(b.Uid, s.Grids[1].OccupantAt(0, 0));
            Assert.AreEqual(0, s.Grids[0].OccupantAt(0, 0));
            // Same tile on another island does not collide.
            Build(s, "dew_collector", 0, 0, 0);
        }

        [Test]
        public void Region_ExclusiveBuildingsNeedTheirRegion()
        {
            GameState s = Rich();
            GrantResearch(s, "crystal_mining");
            BuildingInstance b;
            Assert.AreEqual(PlaceResult.Locked, s.TryBuild("crystal_extractor", 0, 0, 0, 0, out b));
        }

        [Test]
        public void Campaign_TracksMilestones()
        {
            GameState s = Rich();
            Assert.AreEqual(0, Campaign.Completed(s));
            Build(s, "dew_collector", 0, 0);
            Campaign.Update(s);
            Assert.AreEqual(1, Campaign.Completed(s));
            Assert.Greater(Campaign.Progress(s), 0);
            Assert.AreEqual(20, Campaign.Total);
            Assert.IsFalse(s.Prestige.CampaignCompleted);
        }

        [Test]
        public void Prestige_RequiresCampaign_ThenResetsRunButKeepsStars()
        {
            GameState s = Rich();
            Build(s, "dew_collector", 0, 0);
            Assert.IsFalse(PrestigeSystem.Rebirth(s));

            s.Prestige.CampaignCompleted = true;
            s.Res.CoinsEarnedRun = 8e8;
            Assert.AreEqual(2, PrestigeSystem.PendingStars(s));
            GrantResearch(s, "distillation");
            Assert.IsTrue(PrestigeSystem.Rebirth(s));

            Assert.AreEqual(2, s.Prestige.Stars);
            Assert.AreEqual(1, s.Prestige.Rebirths);
            Assert.AreEqual(0, s.Buildings.Count);
            Assert.AreEqual(0, s.Research.Count);
            Assert.IsFalse(s.RegionUnlocked[1]);
            Assert.AreEqual(GameState.StartingCoins, s.Res.Get(ResourceType.Coins), 1e-9);
            Assert.AreEqual(1.10, s.Mods.GlobalProd, 1e-9); // 2 stars * 5%
        }

        [Test]
        public void Prestige_UpgradesCostStars_AndApply()
        {
            GameState s = new GameState();
            s.Prestige.Stars = 10;
            Assert.IsTrue(PrestigeSystem.BuyUpgrade(s, 0));
            Assert.AreEqual(7, s.Prestige.Stars);
            Assert.IsFalse(PrestigeSystem.BuyUpgrade(s, 0));
            Assert.AreEqual(1.25, s.Mods.GlobalProd, 1e-9);
            Assert.IsTrue(PrestigeSystem.BuyUpgrade(s, 2)); // start with 10k coins
            s.Prestige.CampaignCompleted = true;
            PrestigeSystem.Rebirth(s);
            Assert.AreEqual(GameState.StartingCoins + 10000, s.Res.Get(ResourceType.Coins), 1e-6);
        }

        [Test]
        public void RebirthResearch_IsLockedUntilFirstRebirth()
        {
            GameState s = Rich();
            string reason;
            Assert.IsFalse(s.CanResearch(ResearchDatabase.Get("star_harvest"), out reason));
            s.Prestige.Rebirths = 1;
            Assert.IsTrue(s.CanResearch(ResearchDatabase.Get("star_harvest"), out reason));
        }

        [Test]
        public void Challenge_CompletesWhenTargetIsProduced()
        {
            GameState s = new GameState();
            s.Prestige.Rebirths = 1;
            Assert.IsTrue(PrestigeSystem.StartChallenge(s, 0));
            s.Res.TotalProduced[(int)ResourceType.Water] += 50000;
            s.Tick(1);
            Assert.AreEqual(-1, s.Prestige.ActiveChallenge);
            Assert.IsTrue(s.Prestige.ChallengeDone[0]);
            Assert.AreEqual(2, s.Prestige.Stars);
        }

        [Test]
        public void Challenge_FailsWhenTimeRunsOut()
        {
            GameState s = new GameState();
            s.Prestige.Rebirths = 1;
            PrestigeSystem.StartChallenge(s, 0);
            s.Tick(60 * 21);
            Assert.AreEqual(-1, s.Prestige.ActiveChallenge);
            Assert.IsFalse(s.Prestige.ChallengeDone[0]);
        }
    }

    public class TradeTests
    {
        [Test]
        public void Port_GeneratesOrders_AndCompletingThemPaysCoins()
        {
            GameState s = Rich();
            GrantResearch(s, "distillation", "botany", "aerodynamics", "aviation");
            Assert.AreEqual(0, s.Trade.Orders.Count);
            Build(s, "air_port", 0, 0);
            s.Tick(1.1);
            Assert.AreEqual(s.Mods.TradeSlots, s.Trade.Orders.Count);
            Assert.Greater(s.Trade.Orders.Count, 0);

            TradeOrder o = s.Trade.Orders[0];
            Assert.IsFalse(TradeSystem.CanComplete(s, o));
            Assert.IsFalse(TradeSystem.Complete(s, o.Id));

            for (int i = 0; i < o.Wants.Count; i++) s.Res.Amount[(int)o.Wants[i].Type] = o.Wants[i].Amount;
            double before = s.Res.Get(ResourceType.Coins);
            double reward = TradeSystem.FinalReward(s, o);
            Assert.Greater(reward, 0);
            Assert.IsTrue(TradeSystem.Complete(s, o.Id));
            Assert.AreEqual(before + reward, s.Res.Get(ResourceType.Coins), 1e-6);
            Assert.AreEqual(s.Mods.TradeSlots, s.Trade.Orders.Count);
            Assert.AreEqual(1, s.Stats.TradesCompleted);
            Assert.IsTrue(s.Res.IsValid());
        }

        [Test]
        public void Orders_AreAlwaysReachableWithStorageCapacity()
        {
            GameState s = Rich();
            GrantResearch(s, "aviation", "aerodynamics", "distillation", "botany");
            Build(s, "air_port", 0, 0);
            for (int n = 0; n < 200; n++)
            {
                TradeOrder o = TradeSystem.Generate(s);
                for (int i = 0; i < o.Wants.Count; i++)
                    Assert.LessOrEqual(o.Wants[i].Amount, s.Res.Cap(o.Wants[i].Type) + 1e-9);
                Assert.Greater(o.RewardCoins, 0);
            }
        }

        [Test]
        public void Orders_ScaleWithProgression()
        {
            GameState s = Rich();
            GrantResearch(s, "aviation", "aerodynamics", "distillation", "botany");
            Build(s, "air_port", 0, 0);
            Build(s, "warehouse", -4, -4);
            s.Res.CoinsEarnedLifetime = 0;
            double small = 0;
            for (int n = 0; n < 50; n++) small += TradeSystem.Generate(s).RewardCoins;
            s.Res.CoinsEarnedLifetime = 5e7;
            double big = 0;
            for (int n = 0; n < 50; n++) big += TradeSystem.Generate(s).RewardCoins;
            Assert.Greater(big, small);
        }

        [Test]
        public void Reroll_ReplacesOrder_AndConsumesCharge()
        {
            GameState s = Rich();
            GrantResearch(s, "aviation", "aerodynamics");
            Build(s, "air_port", 0, 0);
            s.Tick(1.1);
            int id = s.Trade.Orders[0].Id;
            int charges = s.Trade.Rerolls;
            Assert.IsTrue(TradeSystem.Reroll(s, id));
            Assert.AreEqual(charges - 1, s.Trade.Rerolls);
            Assert.IsNull(TradeSystem.Find(s, id));
        }
    }

    public class OfflineTests
    {
        const long TicksPerSecond = 10000000L;

        [Test]
        public void Offline_ProducesAtNominalRate()
        {
            GameState s = Rich();
            for (int i = 0; i < 4; i++) Build(s, "dew_collector", -4 + i * 2, 0);
            OfflineReport r = OfflineSimulator.Simulate(s, 60);
            Assert.AreEqual(60, r.SimulatedSeconds, 1e-9);
            Assert.AreEqual(120, s.Res.Get(ResourceType.Dew), 0.05);
            Assert.AreEqual(120, r.Delta[(int)ResourceType.Dew], 0.05);
        }

        [Test]
        public void Offline_IsCappedToTwoHoursByDefault_AndEightWithResearch()
        {
            GameState s = Rich();
            Build(s, "dew_collector", 0, 0);
            OfflineReport r = OfflineSimulator.Simulate(s, 10 * 3600);
            Assert.IsTrue(r.Capped);
            Assert.AreEqual(2 * 3600, r.SimulatedSeconds, 1e-9);

            GrantResearch(s, "distillation", "botany", "offline_1");
            Assert.AreEqual(4 * 3600, OfflineSimulator.Simulate(s, 10 * 3600).SimulatedSeconds, 1e-9);
            GrantResearch(s, "offline_2", "offline_3");
            Assert.AreEqual(8 * 3600, OfflineSimulator.Simulate(s, 10 * 3600).SimulatedSeconds, 1e-9);
        }

        [Test]
        public void Offline_RespectsStorageLimits()
        {
            GameState s = Rich();
            for (int i = 0; i < 4; i++) Build(s, "dew_collector", -4 + i * 2, 0);
            OfflineSimulator.Simulate(s, 3600);
            Assert.AreEqual(200, s.Res.Get(ResourceType.Dew), 1e-6);
            Assert.Greater(s.Res.TotalWasted[(int)ResourceType.Dew], 0);
        }

        [Test]
        public void Offline_DoesNotProduceWithoutMaterials()
        {
            GameState s = Rich();
            GrantResearch(s, "distillation");
            Build(s, "distiller", 0, 0);
            OfflineReport r = OfflineSimulator.Simulate(s, 3600);
            Assert.AreEqual(0, s.Res.Get(ResourceType.Water), 1e-9);
            Assert.IsFalse(r.HasGains);
        }

        [Test]
        public void Offline_ChainsWorkAndNeverGoNegative()
        {
            GameState s = Rich();
            GrantResearch(s, "distillation", "botany");
            for (int i = 0; i < 6; i++) Build(s, "dew_collector", -4 + (i % 3) * 2, -4 + (i / 3) * 2);
            Build(s, "distiller", 2, -4);
            Build(s, "greenhouse", -4, 1);
            OfflineSimulator.Simulate(s, 2 * 3600);
            Assert.IsTrue(s.Res.IsValid());
            Assert.Greater(s.Res.TotalProduced[(int)ResourceType.Pollen], 100);
        }

        [Test]
        public void Offline_ClockRewind_GrantsNothing()
        {
            long t0 = 1000 * TicksPerSecond;
            bool tamper;
            Assert.AreEqual(3600, OfflineSimulator.ComputeElapsed(t0 + 3600 * TicksPerSecond, t0, t0, out tamper), 1e-6);
            Assert.IsFalse(tamper);
            // Clock moved back below the last time we saw.
            Assert.AreEqual(0, OfflineSimulator.ComputeElapsed(t0 - 100 * TicksPerSecond, t0, t0, out tamper), 1e-9);
            Assert.IsTrue(tamper);
            // New game (no previous save): nothing.
            Assert.AreEqual(0, OfflineSimulator.ComputeElapsed(t0, 0, 0, out tamper), 1e-9);
        }

        [Test]
        public void Offline_Apply_CannotBeClaimedTwice()
        {
            GameState s = Rich();
            Build(s, "dew_collector", 0, 0);
            long t0 = 5000 * TicksPerSecond;
            s.LastSaveUtcTicks = t0;
            s.MaxSeenUtcTicks = t0;
            long now = t0 + 100 * TicksPerSecond;
            OfflineReport first = OfflineSimulator.Apply(s, now);
            OfflineReport second = OfflineSimulator.Apply(s, now);
            Assert.AreEqual(100, first.SimulatedSeconds, 1e-6);
            Assert.AreEqual(0, second.SimulatedSeconds, 1e-6);
            // Rewinding the clock and moving it forward again does not repeat the reward either.
            OfflineReport rewound = OfflineSimulator.Apply(s, t0);
            Assert.IsTrue(rewound.ClockTamperDetected);
            OfflineReport again = OfflineSimulator.Apply(s, now + 10 * TicksPerSecond);
            Assert.AreEqual(10, again.SimulatedSeconds, 1e-6);
        }
    }

    public class SaveTests
    {
        static double Tol(double v) { return Math.Max(1e-6, Math.Abs(v) * 1e-9); }

        static GameState BuildSampleGame()
        {
            GameState s = Rich();
            GrantResearch(s, "distillation", "botany", "aerodynamics", "aviation", "efficient_tools");
            Build(s, "dew_collector", 0, 0);
            BuildingInstance d = Build(s, "distiller", 2, 0);
            s.TryUpgrade(d.Uid);
            s.TryUpgrade(d.Uid);
            Build(s, "greenhouse", -4, 1, 0, 1);
            Build(s, "air_port", -4, -4);
            s.TryExpand(0);
            s.TryBuyGlobalUpgrade(GlobalUpgrades.Storage);
            s.Tick(2.5);
            s.Res.Amount[(int)ResourceType.Dew] = 123.5;
            s.Res.Amount[(int)ResourceType.Water] = 77;
            s.AutoSell[(int)ResourceType.Water] = true;
            s.ReserveStep[(int)ResourceType.Water] = 2;
            s.Prestige.Stars = 3;
            s.Prestige.Upgrades[4] = true;
            s.ModsDirty = true;
            s.RecomputeMods();
            return s;
        }

        [Test]
        public void SaveLoad_RoundTrip_PreservesTheWholeState()
        {
            GameState a = BuildSampleGame();
            SaveData data = SaveMapper.ToData(a, 123456789L);
            string text = SaveCodec.Encode(data);

            SaveData loaded;
            Assert.IsTrue(SaveCodec.TryDecode(text, out loaded));
            GameState b = new GameState();
            SaveMapper.Apply(loaded, b);

            Assert.AreEqual(a.Buildings.Count, b.Buildings.Count);
            for (int i = 0; i < a.Buildings.Count; i++)
            {
                BuildingInstance x = a.Buildings[i];
                BuildingInstance y = b.FindBuilding(x.Uid);
                Assert.IsNotNull(y);
                Assert.AreEqual(x.DefId, y.DefId);
                Assert.AreEqual(x.X, y.X);
                Assert.AreEqual(x.Y, y.Y);
                Assert.AreEqual(x.Rotation, y.Rotation);
                Assert.AreEqual(x.Level, y.Level);
                Assert.AreEqual(x.Region, y.Region);
            }
            for (int r = 0; r < ResourceInfo.Count; r++)
            {
                Assert.AreEqual(a.Res.Amount[r], b.Res.Amount[r], Tol(a.Res.Amount[r]), "resource " + r);
                Assert.AreEqual(a.Res.TotalProduced[r], b.Res.TotalProduced[r], Tol(a.Res.TotalProduced[r]));
            }
            Assert.AreEqual(a.Grids[0].Expansion, b.Grids[0].Expansion);
            CollectionAssert.AreEquivalent(a.Research, b.Research);
            Assert.AreEqual(a.GlobalUpgradeLevels[1], b.GlobalUpgradeLevels[1]);
            Assert.AreEqual(a.AutoSell[(int)ResourceType.Water], b.AutoSell[(int)ResourceType.Water]);
            Assert.AreEqual(a.ReserveStep[(int)ResourceType.Water], b.ReserveStep[(int)ResourceType.Water]);
            Assert.AreEqual(3, b.Prestige.Stars);
            Assert.IsTrue(b.Prestige.Upgrades[4]);
            Assert.AreEqual(a.Mods.OfflineHours, b.Mods.OfflineHours, 1e-9);
            Assert.AreEqual(a.Mods.GlobalProd, b.Mods.GlobalProd, 1e-9);
            Assert.AreEqual(123456789L, b.LastSaveUtcTicks);
            Assert.AreEqual(a.NextUid, b.NextUid);
            // The grid occupancy is rebuilt, so new overlapping buildings are still rejected.
            BuildingInstance tmp;
            Assert.AreEqual(PlaceResult.Overlap, b.TryBuild("dew_collector", 0, 0, 0, 0, out tmp));
        }

        [Test]
        public void SaveLoad_ContinuesProducingIdentically()
        {
            GameState a = BuildSampleGame();
            SaveData loaded;
            Assert.IsTrue(SaveCodec.TryDecode(SaveCodec.Encode(SaveMapper.ToData(a, 1)), out loaded));
            GameState b = new GameState();
            SaveMapper.Apply(loaded, b);
            a.Tick(30);
            b.Tick(30);
            for (int r = 1; r < ResourceInfo.Count; r++)
                Assert.AreEqual(a.Res.Amount[r], b.Res.Amount[r], Tol(a.Res.Amount[r]), "resource " + r);
        }

        [Test]
        public void Decode_RejectsTamperedOrTruncatedData()
        {
            string text = SaveCodec.Encode(SaveMapper.ToData(BuildSampleGame(), 1));
            SaveData d;
            Assert.IsTrue(SaveCodec.TryDecode(text, out d));
            Assert.IsFalse(SaveCodec.TryDecode(text.Substring(0, text.Length / 2), out d));
            Assert.IsFalse(SaveCodec.TryDecode(text.Replace("123.5", "999.5"), out d));
            Assert.IsFalse(SaveCodec.TryDecode("", out d));
            Assert.IsFalse(SaveCodec.TryDecode(null, out d));
            Assert.IsFalse(SaveCodec.TryDecode("{not json", out d));
        }

        [Test]
        public void Apply_RepairsInvalidData()
        {
            SaveData d = SaveMapper.ToData(new GameState(), 1);
            d.Amounts[(int)ResourceType.Water] = -500;
            d.Amounts[(int)ResourceType.Pollen] = double.NaN;
            d.Amounts[(int)ResourceType.Crystals] = 1e30;
            d.Buildings.Add(new BuildingSave { Uid = 1, DefId = "dew_collector", Region = 0, X = 0, Y = 0, Level = 99 });
            d.Buildings.Add(new BuildingSave { Uid = 2, DefId = "dew_collector", Region = 0, X = 1, Y = 1, Level = 1 }); // overlaps
            d.Buildings.Add(new BuildingSave { Uid = 3, DefId = "does_not_exist", Region = 0, X = 5, Y = 5, Level = 1 });
            d.Buildings.Add(new BuildingSave { Uid = 4, DefId = "dew_collector", Region = 3, X = 0, Y = 0, Level = 1 }); // locked region
            d.Buildings.Add(new BuildingSave { Uid = 5, DefId = "dew_collector", Region = 0, X = 100, Y = 0, Level = 1 }); // out of bounds
            d.Research.Add("fake_research");
            d.Research.Add("distillation");
            d.RegionUnlocked = new bool[] { false, false, true, true, false };

            GameState s = new GameState();
            SaveMapper.Apply(d, s);
            Assert.IsTrue(s.Res.IsValid());
            Assert.AreEqual(0, s.Res.Get(ResourceType.Water));
            Assert.AreEqual(0, s.Res.Get(ResourceType.Pollen));
            Assert.LessOrEqual(s.Res.Get(ResourceType.Crystals), s.Res.Cap(ResourceType.Crystals));
            Assert.AreEqual(1, s.Buildings.Count);
            Assert.AreEqual(10, s.Buildings[0].Level);
            Assert.IsTrue(s.Research.Contains("distillation"));
            Assert.IsFalse(s.Research.Contains("fake_research"));
            Assert.IsTrue(s.RegionUnlocked[0]);
            Assert.IsFalse(s.RegionUnlocked[1]);
            Assert.IsFalse(s.RegionUnlocked[2]); // cannot be unlocked while the previous region is locked
            Assert.IsFalse(s.RegionUnlocked[3]);
        }

        [Test]
        public void Validate_RejectsNewerVersions()
        {
            SaveData d = SaveMapper.ToData(new GameState(), 1);
            d.Version = SaveData.CurrentVersion + 5;
            Assert.IsFalse(SaveMapper.Validate(d));
            Assert.IsFalse(SaveMapper.Validate(null));
        }

        [Test]
        public void SaveManager_RotatesBackups_AndRecoversFromCorruption()
        {
            string dir = Path.Combine(Path.GetTempPath(), "cg_test_" + Guid.NewGuid().ToString("N"));
            try
            {
                SaveManager m = new SaveManager(dir, "save.json");
                GameState s = Rich();
                Build(s, "dew_collector", 0, 0);
                Assert.IsTrue(m.Save(SaveMapper.ToData(s, 1)));
                Build(s, "dew_collector", 2, 0);
                Assert.IsTrue(m.Save(SaveMapper.ToData(s, 2)));
                Build(s, "dew_collector", 4 - 6, 2);
                Assert.IsTrue(m.Save(SaveMapper.ToData(s, 3)));
                Assert.IsTrue(File.Exists(m.Backup1Path));
                Assert.IsTrue(File.Exists(m.Backup2Path));

                LoadSource src;
                SaveData d = m.Load(out src);
                Assert.AreEqual(LoadSource.Main, src);
                Assert.AreEqual(3, d.Buildings.Count);

                File.WriteAllText(m.MainPath, "garbage garbage");
                d = m.Load(out src);
                Assert.AreEqual(LoadSource.Backup1, src);
                Assert.AreEqual(2, d.Buildings.Count);

                File.WriteAllText(m.Backup1Path, "{");
                d = m.Load(out src);
                Assert.AreEqual(LoadSource.Backup2, src);
                Assert.AreEqual(1, d.Buildings.Count);

                File.WriteAllText(m.Backup2Path, "x");
                Assert.IsNull(m.Load(out src));
                Assert.AreEqual(LoadSource.None, src);
            }
            finally
            {
                if (Directory.Exists(dir)) Directory.Delete(dir, true);
            }
        }

        [Test]
        public void SaveManager_CorruptMainDoesNotOverwriteGoodBackups()
        {
            string dir = Path.Combine(Path.GetTempPath(), "cg_test_" + Guid.NewGuid().ToString("N"));
            try
            {
                SaveManager m = new SaveManager(dir, "save.json");
                GameState s = Rich();
                Build(s, "dew_collector", 0, 0);
                m.Save(SaveMapper.ToData(s, 1));
                m.Save(SaveMapper.ToData(s, 2));
                File.WriteAllText(m.MainPath, "corrupt");
                m.Save(SaveMapper.ToData(s, 3)); // must not rotate the corrupt file into the backups
                LoadSource src;
                Assert.IsNotNull(m.Load(out src));
                SaveData b1;
                Assert.IsTrue(SaveCodec.TryDecode(File.ReadAllText(m.Backup1Path), out b1));
            }
            finally
            {
                if (Directory.Exists(dir)) Directory.Delete(dir, true);
            }
        }
    }

    public class DataIntegrityTests
    {
        [Test]
        public void HasThirtyBaseBuildings_WithUniqueIds()
        {
            Assert.GreaterOrEqual(BuildingDatabase.Count, 30);
            HashSet<string> ids = new HashSet<string>();
            int nonPrestige = 0;
            foreach (BuildingDef d in BuildingDatabase.All)
            {
                Assert.IsTrue(ids.Add(d.Id), "duplicate " + d.Id);
                Assert.IsFalse(string.IsNullOrEmpty(d.Name));
                Assert.IsFalse(string.IsNullOrEmpty(d.Description), d.Id);
                Assert.Greater(d.BaseCost, 0, d.Id);
                Assert.Greater(d.Width * d.Depth, 0);
                Assert.Greater(d.CycleSeconds, 0);
                if (d.RequiredPrestigeUpgrade < 0) nonPrestige++;
                if (d.IsProducer) Assert.Greater(d.Outputs.Count, 0, d.Id + " has no output");
            }
            Assert.AreEqual(30, nonPrestige);
        }

        [Test]
        public void EveryBuildingCategoryHasTheRequiredBuildings()
        {
            string[] required =
            {
                "dew_collector", "wind_turbine", "cloud_catcher", "condenser", "crystal_extractor",
                "distiller", "pollen_processor", "refinery", "essence_lab", "crystallizer",
                "greenhouse", "pollen_garden", "sky_flowers", "tropical_orchard", "glow_garden",
                "packer", "goods_factory", "cloud_factory", "atmos_generator", "weather_factory",
                "warehouse", "big_warehouse", "air_port", "observatory", "research_center",
                "power_station", "comm_tower", "climate_core", "industrial_center"
            };
            foreach (string id in required) Assert.IsNotNull(BuildingDatabase.Get(id), id);
        }

        [Test]
        public void BuildingResearchReferences_Exist()
        {
            foreach (BuildingDef d in BuildingDatabase.All)
            {
                if (!string.IsNullOrEmpty(d.RequiredResearch))
                    Assert.IsNotNull(ResearchDatabase.Get(d.RequiredResearch), d.Id + " -> " + d.RequiredResearch);
                Assert.GreaterOrEqual(d.MinRegion, 0);
                Assert.Less(d.MinRegion, RegionDatabase.Count);
            }
        }

        [Test]
        public void ProductionTiers_ProduceInputsBeforeConsumingThem()
        {
            foreach (BuildingDef d in BuildingDatabase.All)
            {
                foreach (ResourceAmount input in d.Inputs)
                {
                    bool found = false;
                    foreach (BuildingDef p in BuildingDatabase.All)
                    {
                        if (!p.IsProducer || p.Tier >= d.Tier) continue;
                        foreach (ResourceAmount o in p.Outputs) if (o.Type == input.Type) found = true;
                    }
                    Assert.IsTrue(found, d.Id + " needs " + input.Type + " from a lower tier");
                }
            }
        }

        [Test]
        public void EveryInputResourceCanBeProduced_AndIsProfitable()
        {
            // A processing step should never destroy value: output value >= input value.
            foreach (BuildingDef d in BuildingDatabase.All)
            {
                if (!d.IsProducer || d.Inputs.Count == 0) continue;
                double vin = 0, vout = 0;
                foreach (ResourceAmount a in d.Inputs) vin += a.Amount * ResourceInfo.Value(a.Type);
                foreach (ResourceAmount a in d.Outputs) vout += a.Amount * ResourceInfo.Value(a.Type);
                Assert.GreaterOrEqual(vout, vin, d.Id + " loses value");
            }
        }

        [Test]
        public void Research_PrerequisitesExist_AndFormNoCycles()
        {
            HashSet<string> seen = new HashSet<string>();
            foreach (ResearchDef r in ResearchDatabase.All)
            {
                Assert.IsTrue(seen.Add(r.Id), "duplicate " + r.Id);
                foreach (string p in r.Prerequisites)
                    Assert.IsTrue(seen.Contains(p), r.Id + " requires " + p + " which must be listed earlier");
                if (!string.IsNullOrEmpty(r.RequiredBuilding))
                    Assert.IsNotNull(BuildingDatabase.Get(r.RequiredBuilding), r.Id);
                Assert.GreaterOrEqual(r.CostCoins, 0);
            }
            Assert.GreaterOrEqual(ResearchDatabase.Count, 30);
        }

        [Test]
        public void EveryUnlockableBuilding_HasAnUnlockPath()
        {
            // Walk the whole tree: after researching everything, every normal building is available (given regions).
            GameState s = new GameState();
            foreach (ResearchDef r in ResearchDatabase.All) s.Research.Add(r.Id);
            for (int i = 0; i < RegionDatabase.Count; i++) s.RegionUnlocked[i] = true;
            foreach (BuildingDef d in BuildingDatabase.All)
            {
                if (d.RequiredPrestigeUpgrade >= 0) continue;
                Assert.IsTrue(s.IsUnlocked(d), d.Id + ": " + s.LockReason(d));
            }
        }

        [Test]
        public void Regions_AreCompleteAndOrdered()
        {
            Assert.AreEqual(5, RegionDatabase.Count);
            double lastCost = -1;
            for (int i = 0; i < RegionDatabase.Count; i++)
            {
                RegionDef r = RegionDatabase.Get(i);
                Assert.AreEqual(i, r.Index);
                Assert.IsFalse(string.IsNullOrEmpty(r.Name));
                Assert.Greater(r.UnlockCoins + (i == 0 ? 1 : 0), lastCost);
                lastCost = r.UnlockCoins;
                if (i > 0)
                {
                    Assert.IsNotNull(ResearchDatabase.Get(r.RequiredResearch), r.Id);
                    Assert.AreEqual(ResearchKind.RegionGate, ResearchDatabase.Get(r.RequiredResearch).Kind);
                    Assert.AreEqual(i, (int)ResearchDatabase.Get(r.RequiredResearch).Value);
                }
            }
        }

        [Test]
        public void Prestige_DataIsConsistent()
        {
            Assert.AreEqual(PrestigeState.UpgradeCount, PrestigeState.UpgradeNames.Length);
            Assert.AreEqual(PrestigeState.UpgradeCount, PrestigeState.UpgradeDescriptions.Length);
            Assert.AreEqual(PrestigeState.UpgradeCount, PrestigeState.UpgradeCosts.Length);
            Assert.AreEqual(PrestigeState.ChallengeCount, PrestigeState.Challenges.Length);
        }

        [Test]
        public void RandomActions_NeverBreakInvariants()
        {
            Random rnd = new Random(12345);
            GameState s = new GameState();
            s.Res.Amount[0] = 1e7;
            GrantResearch(s, "distillation", "botany", "aerodynamics", "condensation", "cloud_catching", "logistics", "aviation", "pollen_gardens");
            string[] ids = { "dew_collector", "wind_turbine", "distiller", "greenhouse", "warehouse", "condenser", "cloud_catcher", "market", "pollen_garden", "big_warehouse" };
            for (int step = 0; step < 3000; step++)
            {
                int action = rnd.Next(6);
                if (action == 0)
                {
                    BuildingInstance b;
                    s.TryBuild(ids[rnd.Next(ids.Length)], 0, rnd.Next(-6, 6), rnd.Next(-6, 6), rnd.Next(4), out b);
                }
                else if (action == 1 && s.Buildings.Count > 0) s.TryUpgrade(s.Buildings[rnd.Next(s.Buildings.Count)].Uid);
                else if (action == 2 && s.Buildings.Count > 0) s.TryMove(s.Buildings[rnd.Next(s.Buildings.Count)].Uid, 0, rnd.Next(-6, 6), rnd.Next(-6, 6), rnd.Next(4));
                else if (action == 3 && s.Buildings.Count > 3) s.TryRemove(s.Buildings[rnd.Next(s.Buildings.Count)].Uid);
                else if (action == 4) s.SellAll((ResourceType)rnd.Next(1, ResourceInfo.Count));
                else s.Tick(rnd.NextDouble() * 20);

                Assert.IsTrue(s.Res.IsValid(), "invalid store at step " + step);
                Assert.GreaterOrEqual(s.Res.Get(ResourceType.Coins), 0);
                // No overlaps: every tile of every building belongs to exactly its owner.
                if (step % 100 == 0)
                {
                    foreach (BuildingInstance b in s.Buildings)
                    {
                        int w, h;
                        GameState.Footprint(b.Def, b.Rotation, out w, out h);
                        for (int i = 0; i < w; i++)
                            for (int j = 0; j < h; j++)
                                Assert.AreEqual(b.Uid, s.Grids[b.Region].OccupantAt(b.X + i, b.Y + j));
                    }
                }
            }
        }
    }
}
