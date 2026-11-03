using System.Collections.Generic;
using CloudGarden.Economy;

namespace CloudGarden.Progression
{
    /// <summary>Statistics shown in the stats panel plus persistent progress flags.</summary>
    public sealed class GameStats
    {
        public int BuildingsBuilt;
        public int BuildingsUpgraded;
        public int TradesCompleted;
        public int ManualCollects;
        public int MaxLevelReached = 1;
        public double SoldCoins;
        public double OfflineSecondsTotal;
        public double PlaySeconds;

        public readonly HashSet<string> Milestones = new HashSet<string>();
        public readonly HashSet<string> EverBuilt = new HashSet<string>();

        // Runtime rates (per second), smoothed.
        public readonly double[] ProdRate = new double[ResourceInfo.Count];
        public readonly double[] ConsRate = new double[ResourceInfo.Count];
        public double CoinRate;

        // Accumulators for the rate window.
        public readonly double[] AccProd = new double[ResourceInfo.Count];
        public readonly double[] AccCons = new double[ResourceInfo.Count];
        public double AccCoins;
        public double RateTimer;

        public void Reset()
        {
            BuildingsBuilt = 0;
            BuildingsUpgraded = 0;
            TradesCompleted = 0;
            ManualCollects = 0;
            MaxLevelReached = 1;
            SoldCoins = 0;
            OfflineSecondsTotal = 0;
            PlaySeconds = 0;
            Milestones.Clear();
            EverBuilt.Clear();
            for (int i = 0; i < ProdRate.Length; i++)
            {
                ProdRate[i] = 0;
                ConsRate[i] = 0;
                AccProd[i] = 0;
                AccCons[i] = 0;
            }
            CoinRate = 0;
            AccCoins = 0;
            RateTimer = 0;
        }

        public void Sanitize()
        {
            if (BuildingsBuilt < 0) BuildingsBuilt = 0;
            if (BuildingsUpgraded < 0) BuildingsUpgraded = 0;
            if (TradesCompleted < 0) TradesCompleted = 0;
            if (ManualCollects < 0) ManualCollects = 0;
            if (MaxLevelReached < 1) MaxLevelReached = 1;
            if (double.IsNaN(SoldCoins) || SoldCoins < 0) SoldCoins = 0;
            if (double.IsNaN(OfflineSecondsTotal) || OfflineSecondsTotal < 0) OfflineSecondsTotal = 0;
            if (double.IsNaN(PlaySeconds) || PlaySeconds < 0) PlaySeconds = 0;
        }
    }
}
