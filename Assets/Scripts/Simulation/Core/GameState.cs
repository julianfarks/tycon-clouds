using System;
using System.Collections.Generic;
using CloudGarden.Buildings;
using CloudGarden.Construction;
using CloudGarden.Economy;
using CloudGarden.Islands;
using CloudGarden.Production;
using CloudGarden.Progression;
using CloudGarden.Research;
using CloudGarden.Trading;

namespace CloudGarden.Core
{
    /// <summary>
    /// The whole game model. It is plain C# (no engine dependency) so every rule can be unit tested.
    /// Presentation layers listen to the events and never modify the model directly.
    /// </summary>
    public sealed partial class GameState
    {
        public static readonly double[] ReserveFractions = { 0.0, 0.25, 0.5, 0.75 };
        public const double StartingCoins = 15;

        public readonly ResourceStore Res = new ResourceStore();
        public readonly List<BuildingInstance> Buildings = new List<BuildingInstance>();
        public readonly List<BuildingInstance> ProductionOrder = new List<BuildingInstance>();
        public readonly IslandGrid[] Grids = new IslandGrid[RegionDatabase.Count];
        public readonly bool[] RegionUnlocked = new bool[RegionDatabase.Count];
        public readonly HashSet<string> Research = new HashSet<string>();
        public readonly int[] GlobalUpgradeLevels = new int[GlobalUpgrades.Count];
        public readonly bool[] AutoSell = new bool[ResourceInfo.Count];
        public readonly int[] ReserveStep = new int[ResourceInfo.Count];
        public readonly GameStats Stats = new GameStats();
        public readonly Modifiers Mods = new Modifiers();
        public TradeState Trade = new TradeState();
        public PrestigeState Prestige = new PrestigeState();

        public int NextUid = 1;
        public long LastSaveUtcTicks;
        public long MaxSeenUtcTicks;
        public bool ModsDirty = true;
        public bool OrderDirty = true;
        public int Version;

        double secondTimer;

        // ---- events ----
        public event Action StateChanged;
        public event Action<BuildingInstance> BuildingAdded;
        public event Action<BuildingInstance> BuildingRemoved;
        public event Action<BuildingInstance> BuildingChanged;
        public event Action WorldReset;
        public event Action<string> Toast;

        public GameState()
        {
            for (int i = 0; i < Grids.Length; i++) Grids[i] = new IslandGrid();
            ResetToNewGame();
        }

        void ResetToNewGame()
        {
            Buildings.Clear();
            ProductionOrder.Clear();
            Research.Clear();
            for (int i = 0; i < Grids.Length; i++)
            {
                Grids[i].Clear();
                Grids[i].SetExpansion(0);
                RegionUnlocked[i] = i == 0;
            }
            for (int i = 0; i < GlobalUpgradeLevels.Length; i++) GlobalUpgradeLevels[i] = 0;
            for (int i = 0; i < ResourceInfo.Count; i++) { AutoSell[i] = false; ReserveStep[i] = 0; }
            AutoSell[(int)ResourceType.Dew] = true;
            for (int i = 0; i < ResourceInfo.Count; i++) Res.Amount[i] = 0;
            Res.Amount[0] = StartingCoins;
            Trade = new TradeState();
            NextUid = 1;
            ModsDirty = true;
            OrderDirty = true;
            RecomputeMods();
        }

        // ---- notifications ----
        public void RaiseChanged()
        {
            Version++;
            if (StateChanged != null) StateChanged();
        }

        public void RaiseToast(string text)
        {
            if (Toast != null) Toast(text);
        }

        void RaiseBuildingAdded(BuildingInstance b) { if (BuildingAdded != null) BuildingAdded(b); }
        void RaiseBuildingRemoved(BuildingInstance b) { if (BuildingRemoved != null) BuildingRemoved(b); }
        void RaiseBuildingChanged(BuildingInstance b) { if (BuildingChanged != null) BuildingChanged(b); }
        void RaiseWorldReset() { if (WorldReset != null) WorldReset(); }

        // ---- modifiers & ordering ----
        public void RecomputeMods()
        {
            if (OrderDirty)
            {
                ProductionOrder.Clear();
                for (int i = 0; i < Buildings.Count; i++)
                {
                    BuildingInstance b = Buildings[i];
                    if (b.Def != null && b.Def.IsProducer) ProductionOrder.Add(b);
                    else b.Status = BuildingStatus.Passive;
                }
                ProductionOrder.Sort(CompareTier);
                OrderDirty = false;
            }
            Modifiers.Compute(this, Mods);
            for (int r = 1; r < ResourceInfo.Count; r++)
                Res.SetCapacity((ResourceType)r, Mods.Capacity[r]);
            ModsDirty = false;
        }

        static int CompareTier(BuildingInstance a, BuildingInstance b)
        {
            int c = a.Def.Tier.CompareTo(b.Def.Tier);
            return c != 0 ? c : a.Uid.CompareTo(b.Uid);
        }

        /// <summary>Advances the simulation by dt seconds.</summary>
        public void Tick(double dt)
        {
            if (dt <= 0 || double.IsNaN(dt) || double.IsInfinity(dt)) return;
            Stats.PlaySeconds += dt;
            ProductionSystem.Tick(this, dt);
            PrestigeSystem.Tick(this, dt);

            secondTimer += dt;
            if (secondTimer >= 1.0)
            {
                secondTimer = 0;
                Campaign.Update(this);
                TradeSystem.Refill(this);
            }
        }

        // ---- queries ----
        public int HighestUnlockedRegion()
        {
            int h = 0;
            for (int i = 0; i < RegionUnlocked.Length; i++) if (RegionUnlocked[i]) h = i;
            return h;
        }

        public int CountOf(string defId)
        {
            int n = 0;
            for (int i = 0; i < Buildings.Count; i++) if (Buildings[i].DefId == defId) n++;
            return n;
        }

        public BuildingInstance FindBuilding(int uid)
        {
            for (int i = 0; i < Buildings.Count; i++) if (Buildings[i].Uid == uid) return Buildings[i];
            return null;
        }

        public bool HasBuilding(string defId)
        {
            return CountOf(defId) > 0;
        }

        public bool HasResearch(string id)
        {
            return string.IsNullOrEmpty(id) || Research.Contains(id);
        }
    }
}
