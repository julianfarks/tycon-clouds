using System;
using CloudGarden.Buildings;
using CloudGarden.Core;
using CloudGarden.Economy;
using CloudGarden.Progression;

namespace CloudGarden.Production
{
    /// <summary>
    /// Central production simulation. One pass over all producers per tick, in dependency (tier) order.
    /// Each machine runs the fraction of its cycle that its inputs and the storage space allow, so the
    /// result does not depend on the step size and production stops when storage is full.
    /// </summary>
    public static class ProductionSystem
    {
        public static void Tick(GameState s, double dt)
        {
            if (dt <= 0 || double.IsNaN(dt) || double.IsInfinity(dt)) return;
            if (s.ModsDirty) s.RecomputeMods();

            Modifiers m = s.Mods;
            ResourceStore res = s.Res;
            GameStats stats = s.Stats;
            var order = s.ProductionOrder;

            for (int i = 0; i < order.Count; i++)
            {
                BuildingInstance b = order[i];
                BuildingDef d = b.Def;
                int level = b.Level;

                double speed = LevelMath.Speed(level);
                if (d.Category == BuildingCategory.Processing || d.Category == BuildingCategory.Industry)
                    speed *= 1.0 + m.ProcessingSpeed;

                double cycles = dt * speed / d.CycleSeconds;
                double amount = LevelMath.Amount(level);
                double inMul = amount * (1.0 - m.InputSave);
                double outMul = amount * LevelMath.Efficiency(level) * m.GlobalProd;

                double scaleIn = 1.0;
                for (int k = 0; k < d.Inputs.Count; k++)
                {
                    double need = d.Inputs[k].Amount * inMul * cycles;
                    if (need <= 1e-12) continue;
                    double have = res.Amount[(int)d.Inputs[k].Type];
                    double f = have / need;
                    if (f < scaleIn) scaleIn = f;
                }
                if (scaleIn < 0) scaleIn = 0;

                double scaleOut = 1.0;
                for (int k = 0; k < d.Outputs.Count; k++)
                {
                    ResourceType ot = d.Outputs[k].Type;
                    if (ot == ResourceType.Coins) continue;
                    double prod = d.Outputs[k].Amount * outMul * cycles;
                    if (prod <= 1e-12) continue;
                    double space = res.Capacity[(int)ot] - res.Amount[(int)ot];
                    if (space < 0) space = 0;
                    double f = space / prod;
                    if (f < scaleOut) scaleOut = f;
                }
                if (scaleOut < 0) scaleOut = 0;

                double scale = Math.Min(scaleIn, scaleOut);
                if (scale < 0) scale = 0;
                if (scale > 1) scale = 1;

                if (scale >= 0.999) b.Status = BuildingStatus.Running;
                else if (scaleOut < scaleIn) b.Status = BuildingStatus.StorageFull;
                else b.Status = BuildingStatus.MissingInput;
                b.Activity = scale;

                if (scale > 0)
                {
                    for (int k = 0; k < d.Inputs.Count; k++)
                    {
                        double used = d.Inputs[k].Amount * inMul * cycles * scale;
                        int ti = (int)d.Inputs[k].Type;
                        double before = res.Amount[ti];
                        res.Consume(d.Inputs[k].Type, used);
                        stats.AccCons[ti] += before - res.Amount[ti];
                    }
                    for (int k = 0; k < d.Outputs.Count; k++)
                    {
                        ResourceType ot = d.Outputs[k].Type;
                        double made = d.Outputs[k].Amount * outMul * cycles * scale;
                        if (ot == ResourceType.Coins)
                        {
                            res.AddCoins(made);
                            stats.AccCoins += made;
                        }
                        else
                        {
                            double accepted = res.Produce(ot, made, false);
                            stats.AccProd[(int)ot] += accepted;
                        }
                    }
                }

                // Blocked production because the storage was full counts as wasted.
                if (scaleOut < scaleIn && scaleOut < 1.0)
                {
                    double lost = Math.Min(scaleIn, 1.0) - scaleOut;
                    for (int k = 0; k < d.Outputs.Count; k++)
                    {
                        ResourceType ot = d.Outputs[k].Type;
                        if (ot == ResourceType.Coins) continue;
                        res.TotalWasted[(int)ot] += d.Outputs[k].Amount * outMul * cycles * lost;
                    }
                }
            }

            if (m.HasMarket) AutoSell(s);

            stats.RateTimer += dt;
            if (stats.RateTimer >= 1.0)
            {
                double t = stats.RateTimer;
                for (int r = 0; r < ResourceInfo.Count; r++)
                {
                    double p = stats.AccProd[r] / t;
                    double c = stats.AccCons[r] / t;
                    stats.ProdRate[r] = stats.ProdRate[r] * 0.5 + p * 0.5;
                    stats.ConsRate[r] = stats.ConsRate[r] * 0.5 + c * 0.5;
                    stats.AccProd[r] = 0;
                    stats.AccCons[r] = 0;
                }
                stats.CoinRate = stats.CoinRate * 0.5 + (stats.AccCoins / t) * 0.5;
                stats.AccCoins = 0;
                stats.RateTimer = 0;
            }
        }

        /// <summary>Sells the flagged resources above the reserve the player wants to keep.</summary>
        public static void AutoSell(GameState s)
        {
            ResourceStore res = s.Res;
            for (int r = 1; r < ResourceInfo.Count; r++)
            {
                if (!s.AutoSell[r]) continue;
                double reserve = res.Capacity[r] * GameState.ReserveFractions[s.ReserveStep[r]];
                double extra = res.Amount[r] - reserve;
                if (extra <= 0) continue;
                SellAmount(s, (ResourceType)r, extra);
            }
        }

        /// <summary>Sells units of a resource at market price. Returns the coins obtained.</summary>
        public static double SellAmount(GameState s, ResourceType type, double units)
        {
            if (type == ResourceType.Coins || units <= 0 || double.IsNaN(units)) return 0;
            int i = (int)type;
            double sold = Math.Min(units, s.Res.Amount[i]);
            if (sold <= 0) return 0;
            s.Res.Amount[i] -= sold;
            if (s.Res.Amount[i] < 0) s.Res.Amount[i] = 0;
            double coins = sold * ResourceInfo.Value(type) * s.Mods.SellMult;
            s.Res.AddCoins(coins);
            s.Stats.AccCoins += coins;
            s.Stats.SoldCoins += coins;
            return coins;
        }

        // ---- nominal rates for the UI (units per second when running at 100%) ----

        public static double NominalOutputPerSecond(GameState s, BuildingDef d, int level, ResourceType type)
        {
            double speed = LevelMath.Speed(level);
            if (d.Category == BuildingCategory.Processing || d.Category == BuildingCategory.Industry)
                speed *= 1.0 + s.Mods.ProcessingSpeed;
            double mul = LevelMath.Amount(level) * LevelMath.Efficiency(level) * s.Mods.GlobalProd;
            double sum = 0;
            for (int k = 0; k < d.Outputs.Count; k++)
                if (d.Outputs[k].Type == type) sum += d.Outputs[k].Amount * mul * speed / d.CycleSeconds;
            return sum;
        }

        public static double NominalInputPerSecond(GameState s, BuildingDef d, int level, ResourceType type)
        {
            double speed = LevelMath.Speed(level);
            if (d.Category == BuildingCategory.Processing || d.Category == BuildingCategory.Industry)
                speed *= 1.0 + s.Mods.ProcessingSpeed;
            double mul = LevelMath.Amount(level) * (1.0 - s.Mods.InputSave);
            double sum = 0;
            for (int k = 0; k < d.Inputs.Count; k++)
                if (d.Inputs[k].Type == type) sum += d.Inputs[k].Amount * mul * speed / d.CycleSeconds;
            return sum;
        }
    }
}
