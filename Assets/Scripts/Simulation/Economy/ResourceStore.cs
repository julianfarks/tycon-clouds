using System;
using System.Collections.Generic;

namespace CloudGarden.Economy
{
    /// <summary>
    /// Holds amounts and capacities of every resource. Guarantees that no amount ever becomes
    /// negative, NaN or larger than its capacity.
    /// </summary>
    public sealed class ResourceStore
    {
        public const double Unlimited = 1e18;
        public const double BaseCapacity = 200;

        public readonly double[] Amount = new double[ResourceInfo.Count];
        public readonly double[] Capacity = new double[ResourceInfo.Count];
        public readonly double[] TotalProduced = new double[ResourceInfo.Count];
        public readonly double[] TotalConsumed = new double[ResourceInfo.Count];
        public readonly double[] TotalWasted = new double[ResourceInfo.Count];

        /// <summary>Coins earned/spent since the beginning of the current run (reset on rebirth).</summary>
        public double CoinsEarnedRun;
        public double CoinsEarnedLifetime;
        public double CoinsSpentLifetime;

        public ResourceStore()
        {
            ResetCapacities();
        }

        public void ResetCapacities()
        {
            for (int i = 0; i < Capacity.Length; i++)
                Capacity[i] = i == 0 ? Unlimited : BaseCapacity;
        }

        public double Get(ResourceType t) { return Amount[(int)t]; }
        public double Cap(ResourceType t) { return Capacity[(int)t]; }
        public double Space(ResourceType t)
        {
            double s = Capacity[(int)t] - Amount[(int)t];
            return s > 0 ? s : 0;
        }

        public void SetCapacity(ResourceType t, double cap)
        {
            if (t == ResourceType.Coins) { Capacity[0] = Unlimited; return; }
            if (double.IsNaN(cap) || cap < 0) cap = 0;
            Capacity[(int)t] = cap;
            if (Amount[(int)t] > cap) Amount[(int)t] = cap;
        }

        public bool CanAfford(IList<ResourceAmount> cost, double multiplier)
        {
            if (cost == null) return true;
            for (int i = 0; i < cost.Count; i++)
            {
                double need = cost[i].Amount * multiplier;
                if (need > Amount[(int)cost[i].Type] + 1e-9) return false;
            }
            return true;
        }

        public bool CanAfford(IList<ResourceAmount> cost) { return CanAfford(cost, 1.0); }

        public bool CanAffordCoins(double coins) { return coins <= Amount[0] + 1e-9; }

        public bool TrySpend(IList<ResourceAmount> cost, double multiplier)
        {
            if (!CanAfford(cost, multiplier)) return false;
            for (int i = 0; i < cost.Count; i++)
                Spend(cost[i].Type, cost[i].Amount * multiplier);
            return true;
        }

        public bool TrySpendCoins(double coins)
        {
            if (!CanAffordCoins(coins)) return false;
            Spend(ResourceType.Coins, coins);
            return true;
        }

        void Spend(ResourceType t, double amount)
        {
            if (amount <= 0 || double.IsNaN(amount)) return;
            int i = (int)t;
            double taken = Math.Min(amount, Amount[i]);
            Amount[i] -= taken;
            if (Amount[i] < 0) Amount[i] = 0;
            if (i == 0) CoinsSpentLifetime += taken;
        }

        /// <summary>Consume input for production (counts as consumption, never below 0).</summary>
        public void Consume(ResourceType t, double amount)
        {
            if (amount <= 0 || double.IsNaN(amount)) return;
            int i = (int)t;
            double taken = Math.Min(amount, Amount[i]);
            Amount[i] -= taken;
            TotalConsumed[i] += taken;
        }

        /// <summary>
        /// Adds production, limited by capacity. Returns the accepted amount. Overflow is counted as waste
        /// when countWaste is true.
        /// </summary>
        public double Produce(ResourceType t, double amount, bool countWaste)
        {
            if (amount <= 0 || double.IsNaN(amount) || double.IsInfinity(amount)) return 0;
            int i = (int)t;
            double space = Capacity[i] - Amount[i];
            if (space < 0) space = 0;
            double accepted = Math.Min(space, amount);
            Amount[i] += accepted;
            TotalProduced[i] += accepted;
            if (countWaste && accepted < amount) TotalWasted[i] += amount - accepted;
            return accepted;
        }

        public void AddCoins(double coins)
        {
            if (coins <= 0 || double.IsNaN(coins) || double.IsInfinity(coins)) return;
            Amount[0] += coins;
            TotalProduced[0] += coins;
            CoinsEarnedRun += coins;
            CoinsEarnedLifetime += coins;
        }

        public void Sanitize()
        {
            for (int i = 0; i < Amount.Length; i++)
            {
                if (double.IsNaN(Amount[i]) || Amount[i] < 0) Amount[i] = 0;
                if (double.IsNaN(Capacity[i]) || Capacity[i] < 0) Capacity[i] = i == 0 ? Unlimited : BaseCapacity;
                if (Amount[i] > Capacity[i]) Amount[i] = Capacity[i];
                if (double.IsNaN(TotalProduced[i]) || TotalProduced[i] < 0) TotalProduced[i] = 0;
                if (double.IsNaN(TotalConsumed[i]) || TotalConsumed[i] < 0) TotalConsumed[i] = 0;
                if (double.IsNaN(TotalWasted[i]) || TotalWasted[i] < 0) TotalWasted[i] = 0;
            }
            if (double.IsNaN(CoinsEarnedRun) || CoinsEarnedRun < 0) CoinsEarnedRun = 0;
            if (double.IsNaN(CoinsEarnedLifetime) || CoinsEarnedLifetime < 0) CoinsEarnedLifetime = 0;
            if (double.IsNaN(CoinsSpentLifetime) || CoinsSpentLifetime < 0) CoinsSpentLifetime = 0;
        }

        public bool IsValid()
        {
            for (int i = 0; i < Amount.Length; i++)
            {
                if (double.IsNaN(Amount[i]) || Amount[i] < 0) return false;
                if (Amount[i] > Capacity[i] + 1e-6) return false;
            }
            return true;
        }
    }
}
