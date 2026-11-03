using System;
using System.Collections.Generic;
using CloudGarden.Buildings;
using CloudGarden.Core;
using CloudGarden.Economy;

namespace CloudGarden.Trading
{
    public sealed class TradeOrder
    {
        public int Id;
        public string Merchant;
        public bool Special;
        public readonly List<ResourceAmount> Wants = new List<ResourceAmount>();
        public double RewardCoins;
        public ResourceAmount Bonus;
    }

    public sealed class TradeState
    {
        public readonly List<TradeOrder> Orders = new List<TradeOrder>();
        public uint Seed = 2463534242u;
        public int NextId = 1;
        public int Completed;
        public int Rerolls = 2;
        public const int MaxRerolls = 3;

        public uint NextRandom()
        {
            uint x = Seed == 0 ? 2463534242u : Seed;
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
            Seed = x;
            return x;
        }

        /// <summary>Uniform double in [0,1).</summary>
        public double NextDouble()
        {
            return (NextRandom() & 0xFFFFFF) / (double)0x1000000;
        }
    }

    /// <summary>Air-port merchants: generates delivery orders adapted to the player's progression.</summary>
    public static class TradeSystem
    {
        static readonly string[] merchants =
        {
            "Capitana Brisa", "Don Cúmulo", "Mercader Alba", "Dama Nimbo",
            "Tío Cirro", "Mercader Aurora", "Capitán Estratos", "Madame Bruma"
        };

        // Base amounts per resource at the start of the game (scaled with progression).
        static readonly double[] baseAmount = { 0, 120, 60, 40, 60, 25, 12, 4, 12, 25, 14, 10 };

        public static void Refill(GameState s)
        {
            if (!s.Mods.HasPort) return;
            int guard = 0;
            while (s.Trade.Orders.Count < s.Mods.TradeSlots && guard++ < 10)
                s.Trade.Orders.Add(Generate(s));
        }

        static double ProgressionScale(GameState s)
        {
            double earned = s.Res.CoinsEarnedLifetime;
            return Math.Max(1.0, Math.Sqrt(earned / 2000.0));
        }

        public static TradeOrder Generate(GameState s)
        {
            TradeState ts = s.Trade;
            TradeOrder o = new TradeOrder();
            o.Id = ts.NextId++;
            o.Merchant = merchants[(int)(ts.NextRandom() % (uint)merchants.Length)];
            o.Special = (o.Id % 4) == 0;

            // Candidate resources: reachable in unlocked regions and actually produced by an owned building.
            List<ResourceType> candidates = new List<ResourceType>();
            bool[] produced = new bool[ResourceInfo.Count];
            for (int i = 0; i < s.Buildings.Count; i++)
            {
                BuildingDef d = s.Buildings[i].Def;
                for (int k = 0; k < d.Outputs.Count; k++) produced[(int)d.Outputs[k].Type] = true;
            }
            int maxRegion = s.HighestUnlockedRegion();
            for (int r = 1; r < ResourceInfo.Count; r++)
            {
                ResourceType t = (ResourceType)r;
                if (ResourceInfo.FirstRegion(t) > maxRegion) continue;
                if (produced[r] || t == ResourceType.Dew) candidates.Add(t);
            }
            if (candidates.Count == 0) candidates.Add(ResourceType.Dew);

            double scale = ProgressionScale(s);
            int kinds = o.Special && candidates.Count >= 2 ? 2 + (candidates.Count >= 4 ? (int)(ts.NextRandom() % 2u) : 0) : 1;
            double valueSum = 0;
            for (int n = 0; n < kinds; n++)
            {
                int idx = (int)(ts.NextRandom() % (uint)candidates.Count);
                ResourceType t = candidates[idx];
                candidates.RemoveAt(idx);
                double amt = baseAmount[(int)t] * scale * (0.7 + 0.6 * ts.NextDouble());
                if (kinds > 1) amt *= 0.6;
                double cap = s.Res.Capacity[(int)t] * 0.8;
                if (amt > cap) amt = cap;
                amt = Math.Max(1.0, Math.Floor(amt));
                o.Wants.Add(new ResourceAmount(t, amt));
                valueSum += amt * ResourceInfo.Value(t);
                if (candidates.Count == 0) break;
            }

            double mult = 1.5 * (0.95 + 0.3 * ts.NextDouble());
            if (o.Special) mult *= 1.4;
            o.RewardCoins = Math.Floor(valueSum * mult);

            // Bonus reward: the most valuable resource available so far.
            ResourceType best = ResourceType.Dew;
            for (int r = 1; r < ResourceInfo.Count; r++)
            {
                ResourceType t = (ResourceType)r;
                if (ResourceInfo.FirstRegion(t) <= maxRegion && produced[r] && ResourceInfo.Value(t) > ResourceInfo.Value(best)) best = t;
            }
            double bonusAmt = Math.Max(1.0, Math.Floor(0.15 * o.RewardCoins / ResourceInfo.Value(best)));
            o.Bonus = new ResourceAmount(best, bonusAmt);
            return o;
        }

        public static TradeOrder Find(GameState s, int id)
        {
            for (int i = 0; i < s.Trade.Orders.Count; i++)
                if (s.Trade.Orders[i].Id == id) return s.Trade.Orders[i];
            return null;
        }

        public static bool CanComplete(GameState s, TradeOrder o)
        {
            return o != null && s.Res.CanAfford(o.Wants);
        }

        public static double FinalReward(GameState s, TradeOrder o)
        {
            return Math.Floor(o.RewardCoins * (1.0 + s.Mods.TradeBonus));
        }

        public static bool Complete(GameState s, int orderId)
        {
            TradeOrder o = Find(s, orderId);
            if (o == null || !s.Mods.HasPort) return false;
            if (!s.Res.TrySpend(o.Wants, 1.0)) return false;
            for (int i = 0; i < o.Wants.Count; i++)
                s.Res.TotalConsumed[(int)o.Wants[i].Type] += o.Wants[i].Amount;

            s.Res.AddCoins(FinalReward(s, o));
            s.Res.Produce(o.Bonus.Type, o.Bonus.Amount, true);
            s.Trade.Orders.Remove(o);
            s.Trade.Completed++;
            s.Stats.TradesCompleted++;
            if (s.Trade.Completed % 2 == 0 && s.Trade.Rerolls < TradeState.MaxRerolls) s.Trade.Rerolls++;
            Refill(s);
            s.RaiseChanged();
            return true;
        }

        /// <summary>Replaces an order using one of the limited rerolls.</summary>
        public static bool Reroll(GameState s, int orderId)
        {
            TradeOrder o = Find(s, orderId);
            if (o == null || s.Trade.Rerolls <= 0) return false;
            int idx = s.Trade.Orders.IndexOf(o);
            s.Trade.Rerolls--;
            s.Trade.Orders[idx] = Generate(s);
            s.RaiseChanged();
            return true;
        }
    }
}
