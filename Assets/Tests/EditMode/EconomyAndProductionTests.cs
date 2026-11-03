using CloudGarden.Buildings;
using CloudGarden.Core;
using CloudGarden.Economy;
using NUnit.Framework;
using static CloudGarden.Tests.TestHelpers;

namespace CloudGarden.Tests
{
    public class EconomyTests
    {
        [Test]
        public void NewGame_StartsWithCoinsAndOnlyFirstRegion()
        {
            GameState s = new GameState();
            Assert.AreEqual(GameState.StartingCoins, s.Res.Get(ResourceType.Coins), 1e-9);
            Assert.IsTrue(s.RegionUnlocked[0]);
            Assert.IsFalse(s.RegionUnlocked[1]);
            Assert.AreEqual(0, s.Buildings.Count);
        }

        [Test]
        public void Spend_NeverMakesResourcesNegative()
        {
            ResourceStore r = new ResourceStore();
            r.AddCoins(10);
            ResourceAmount[] cost = { new ResourceAmount(ResourceType.Coins, 25) };
            Assert.IsFalse(r.TrySpend(cost, 1.0));
            Assert.AreEqual(10, r.Get(ResourceType.Coins), 1e-9);
            Assert.IsTrue(r.TrySpendCoins(10));
            Assert.AreEqual(0, r.Get(ResourceType.Coins), 1e-9);
            Assert.IsFalse(r.TrySpendCoins(0.5));
            r.Consume(ResourceType.Water, 999);
            Assert.AreEqual(0, r.Get(ResourceType.Water), 1e-9);
            Assert.IsTrue(r.IsValid());
        }

        [Test]
        public void Produce_IsLimitedByCapacity_AndCountsWaste()
        {
            ResourceStore r = new ResourceStore();
            double accepted = r.Produce(ResourceType.Water, 500, true);
            Assert.AreEqual(ResourceStore.BaseCapacity, accepted, 1e-9);
            Assert.AreEqual(ResourceStore.BaseCapacity, r.Get(ResourceType.Water), 1e-9);
            Assert.AreEqual(300, r.TotalWasted[(int)ResourceType.Water], 1e-9);
        }

        [Test]
        public void Sanitize_FixesNaNAndNegativeValues()
        {
            ResourceStore r = new ResourceStore();
            r.Amount[2] = double.NaN;
            r.Amount[3] = -50;
            r.Amount[4] = 99999;
            r.Sanitize();
            Assert.IsTrue(r.IsValid());
            Assert.AreEqual(0, r.Amount[2]);
            Assert.AreEqual(0, r.Amount[3]);
            Assert.AreEqual(r.Capacity[4], r.Amount[4]);
        }

        [Test]
        public void ReducingCapacity_ClampsStoredAmount()
        {
            ResourceStore r = new ResourceStore();
            r.SetCapacity(ResourceType.Pollen, 1000);
            r.Produce(ResourceType.Pollen, 800, false);
            r.SetCapacity(ResourceType.Pollen, 300);
            Assert.AreEqual(300, r.Get(ResourceType.Pollen), 1e-9);
        }

        [Test]
        public void Coins_HaveUnlimitedCapacity()
        {
            ResourceStore r = new ResourceStore();
            r.AddCoins(1e15);
            Assert.AreEqual(1e15, r.Get(ResourceType.Coins), 1);
        }

        [Test]
        public void NumberFormat_IsCompactAndSafe()
        {
            Assert.AreEqual("0", NumberFormat.Compact(0));
            Assert.AreEqual("999", NumberFormat.Compact(999));
            Assert.AreEqual("1.50K", NumberFormat.Compact(1500));
            Assert.AreEqual("2.00M", NumberFormat.Compact(2e6));
            Assert.AreEqual("0", NumberFormat.Compact(double.NaN));
            Assert.AreEqual("inf", NumberFormat.Compact(double.PositiveInfinity));
        }

        [Test]
        public void Collect_AddsDew_AndRespectsCapacity()
        {
            GameState s = new GameState();
            Assert.AreEqual(1, s.Collect(), 1e-9);
            Assert.AreEqual(1, s.Res.Get(ResourceType.Dew), 1e-9);
            s.Res.Amount[(int)ResourceType.Dew] = 200;
            Assert.AreEqual(0, s.Collect(), 1e-9);
            Assert.AreEqual(200, s.Res.Get(ResourceType.Dew), 1e-9);
        }

        [Test]
        public void ManualSell_ConvertsResourceToCoins()
        {
            GameState s = new GameState();
            s.Res.Amount[(int)ResourceType.Water] = 10;
            double before = s.Res.Get(ResourceType.Coins);
            double coins = s.SellAll(ResourceType.Water);
            Assert.AreEqual(10 * ResourceInfo.Value(ResourceType.Water), coins, 1e-9);
            Assert.AreEqual(before + coins, s.Res.Get(ResourceType.Coins), 1e-9);
            Assert.AreEqual(0, s.Res.Get(ResourceType.Water), 1e-9);
        }

        [Test]
        public void Market_AutoSellsFlaggedResources_AndKeepsReserve()
        {
            GameState s = Rich();
            Build(s, "market", 0, 0);
            s.Res.Amount[(int)ResourceType.Dew] = 150;
            s.ReserveStep[(int)ResourceType.Dew] = 2; // keep 50% of the 200 capacity
            double before = s.Res.Get(ResourceType.Coins);
            s.Tick(1);
            Assert.AreEqual(100, s.Res.Get(ResourceType.Dew), 1e-6);
            Assert.AreEqual(before + 50, s.Res.Get(ResourceType.Coins), 1e-6);
        }

        [Test]
        public void Market_DoesNothingWhenNotBuilt()
        {
            GameState s = new GameState();
            s.Res.Amount[(int)ResourceType.Dew] = 100;
            s.Tick(5);
            Assert.AreEqual(100, s.Res.Get(ResourceType.Dew), 1e-9);
        }
    }

    public class ProductionTests
    {
        [Test]
        public void Collector_ProducesHalfDewPerSecond()
        {
            GameState s = new GameState();
            Build(s, "dew_collector", 0, 0);
            RunSeconds(s, 100);
            Assert.AreEqual(50, s.Res.Get(ResourceType.Dew), 0.01);
        }

        [Test]
        public void Production_IsIndependentOfStepSize()
        {
            GameState a = new GameState();
            GameState b = new GameState();
            Build(a, "dew_collector", 0, 0);
            Build(b, "dew_collector", 0, 0);
            RunSeconds(a, 60, 1.0);
            RunSeconds(b, 60, 0.1);
            Assert.AreEqual(a.Res.Get(ResourceType.Dew), b.Res.Get(ResourceType.Dew), 0.01);
        }

        [Test]
        public void Upgrade_IncreasesProduction()
        {
            GameState s = Rich();
            BuildingInstance b = Build(s, "dew_collector", 0, 0);
            double rate1 = Production.ProductionSystem.NominalOutputPerSecond(s, b.Def, 1, ResourceType.Dew);
            Assert.IsTrue(s.TryUpgrade(b.Uid));
            Assert.AreEqual(2, b.Level);
            double rate2 = Production.ProductionSystem.NominalOutputPerSecond(s, b.Def, 2, ResourceType.Dew);
            Assert.Greater(rate2, rate1);

            double before = s.Res.Get(ResourceType.Dew);
            s.Tick(10);
            Assert.AreEqual(before + rate2 * 10, s.Res.Get(ResourceType.Dew), 0.01);
        }

        [Test]
        public void Distiller_ConsumesDew_AndProducesWater()
        {
            GameState s = Rich();
            GrantResearch(s, "distillation");
            Build(s, "distiller", 0, 0);
            s.Res.Amount[(int)ResourceType.Dew] = 100;
            s.Tick(10); // 4 cycles: -8 dew, +4 water
            Assert.AreEqual(92, s.Res.Get(ResourceType.Dew), 1e-6);
            Assert.AreEqual(4, s.Res.Get(ResourceType.Water), 1e-6);
            Assert.AreEqual(8, s.Res.TotalConsumed[(int)ResourceType.Dew], 1e-6);
        }

        [Test]
        public void Machine_StopsWithoutInputs_AndNeverGoesNegative()
        {
            GameState s = Rich();
            GrantResearch(s, "distillation");
            BuildingInstance d = Build(s, "distiller", 0, 0);
            s.Res.Amount[(int)ResourceType.Dew] = 1;
            s.Tick(10);
            Assert.AreEqual(0, s.Res.Get(ResourceType.Dew), 1e-9);
            Assert.AreEqual(0.5, s.Res.Get(ResourceType.Water), 1e-6);
            Assert.AreEqual(BuildingStatus.MissingInput, d.Status);
            s.Tick(10);
            Assert.AreEqual(0.5, s.Res.Get(ResourceType.Water), 1e-6);
            Assert.IsTrue(s.Res.IsValid());
        }

        [Test]
        public void FullStorage_StopsProduction_AndCountsWaste()
        {
            GameState s = Rich();
            BuildingInstance last = null;
            int n = 0;
            for (int x = -4; x <= 2 && n < 10; x += 2)
                for (int y = -4; y <= 2 && n < 10; y += 2, n++)
                    last = Build(s, "dew_collector", x, y);
            RunSeconds(s, 100); // 10 collectors * 50 = 500 > 200
            Assert.AreEqual(200, s.Res.Get(ResourceType.Dew), 1e-6);
            Assert.Greater(s.Res.TotalWasted[(int)ResourceType.Dew], 0);
            Assert.AreEqual(BuildingStatus.StorageFull, last.Status);
        }

        [Test]
        public void FullOutput_DoesNotConsumeInputs()
        {
            GameState s = Rich();
            GrantResearch(s, "distillation");
            Build(s, "distiller", 0, 0);
            s.Res.Amount[(int)ResourceType.Dew] = 100;
            s.Res.Amount[(int)ResourceType.Water] = 200; // output storage full
            s.Tick(10);
            Assert.AreEqual(100, s.Res.Get(ResourceType.Dew), 1e-9);
            Assert.AreEqual(200, s.Res.Get(ResourceType.Water), 1e-9);
        }

        [Test]
        public void Warehouse_IncreasesCapacityOfAllResources()
        {
            GameState s = Rich();
            Assert.AreEqual(200, s.Res.Cap(ResourceType.Water), 1e-9);
            BuildingInstance w = Build(s, "warehouse", 0, 0);
            Assert.AreEqual(500, s.Res.Cap(ResourceType.Water), 1e-9);
            Assert.AreEqual(500, s.Res.Cap(ResourceType.Crystals), 1e-9);
            s.TryUpgrade(w.Uid);
            Assert.Greater(s.Res.Cap(ResourceType.Water), 500);
            s.TryRemove(w.Uid);
            Assert.AreEqual(200, s.Res.Cap(ResourceType.Water), 1e-9);
        }

        [Test]
        public void RemovingWarehouse_ClampsStoredResources()
        {
            GameState s = Rich();
            BuildingInstance w = Build(s, "warehouse", 0, 0);
            s.Res.Amount[(int)ResourceType.Water] = 450;
            s.TryRemove(w.Uid);
            Assert.AreEqual(200, s.Res.Get(ResourceType.Water), 1e-9);
        }

        [Test]
        public void Chain_CollectorsDistillerGreenhouse_ProducesPollen()
        {
            GameState s = Rich();
            GrantResearch(s, "distillation", "botany");
            for (int i = 0; i < 6; i++) Build(s, "dew_collector", -4 + (i % 3) * 2, -4 + (i / 3) * 2);
            Build(s, "distiller", 2, -4);
            Build(s, "distiller", 2, -2);
            Build(s, "greenhouse", -4, 1);
            RunSeconds(s, 120);
            Assert.Greater(s.Res.Get(ResourceType.Water) + s.Res.TotalConsumed[(int)ResourceType.Water], 10);
            Assert.Greater(s.Res.Get(ResourceType.Pollen), 5);
            Assert.IsTrue(s.Res.IsValid());
        }

        [Test]
        public void GlobalProductionBonus_ScalesOutput()
        {
            GameState s = Rich();
            Build(s, "dew_collector", 0, 0);
            RunSeconds(s, 20);
            double baseDew = s.Res.Get(ResourceType.Dew);
            s.Res.Amount[(int)ResourceType.Dew] = 0;
            GrantResearch(s, "distillation", "efficient_tools"); // +10%
            Assert.AreEqual(1.10, s.Mods.GlobalProd, 1e-9);
            RunSeconds(s, 20);
            Assert.AreEqual(baseDew * 1.10, s.Res.Get(ResourceType.Dew), 0.01);
        }

        [Test]
        public void GoldenCollector_ProducesCoins_OnlyWithPrestigeUpgrade()
        {
            GameState s = Rich();
            BuildingInstance b;
            Assert.AreEqual(PlaceResult.Locked, s.TryBuild("golden_collector", 0, 0, 0, 0, out b));
            s.Prestige.Upgrades[5] = true;
            b = Build(s, "golden_collector", 0, 0);
            double before = s.Res.Get(ResourceType.Coins);
            s.Tick(10);
            Assert.AreEqual(before + 2000, s.Res.Get(ResourceType.Coins), 1e-6);
        }

        [Test]
        public void Simulation_Performance_EightHoursOffline_IsFast()
        {
            GameState s = Rich();
            GrantResearch(s, "distillation", "botany", "offline_1", "offline_2", "offline_3");
            int n = 0;
            for (int x = -4; x <= 2; x += 2)
                for (int y = -4; y <= 2; y += 2)
                {
                    string id = n % 3 == 0 ? "dew_collector" : (n % 3 == 1 ? "distiller" : "dew_collector");
                    Build(s, id, x, y);
                    n++;
                }
            System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
            OfflineProduction.OfflineReport r = OfflineProduction.OfflineSimulator.Simulate(s, 8 * 3600);
            sw.Stop();
            Assert.Less(sw.ElapsedMilliseconds, 3000);
            Assert.AreEqual(8 * 3600, r.SimulatedSeconds, 1e-6);
            Assert.IsTrue(s.Res.IsValid());
        }
    }
}
