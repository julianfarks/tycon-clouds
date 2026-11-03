using System;
using System.Collections.Generic;

namespace CloudGarden.SaveSystem
{
    // Plain serializable DTOs (compatible with Unity's JsonUtility: arrays and lists, no dictionaries).

    [Serializable]
    public class BuildingSave
    {
        public int Uid;
        public string DefId;
        public int Region;
        public int X;
        public int Y;
        public int Rotation;
        public int Level;
        public double PaidCoins;
    }

    [Serializable]
    public class OrderSave
    {
        public int Id;
        public string Merchant;
        public bool Special;
        public int[] WantTypes;
        public double[] WantAmounts;
        public double RewardCoins;
        public int BonusType;
        public double BonusAmount;
    }

    [Serializable]
    public class SaveData
    {
        public const int CurrentVersion = 1;

        public int Version = CurrentVersion;
        public long SavedUtcTicks;
        public long MaxSeenUtcTicks;
        public int NextUid = 1;

        // Resources
        public double[] Amounts;
        public double[] Produced;
        public double[] Consumed;
        public double[] Wasted;
        public double CoinsEarnedRun;
        public double CoinsEarnedLifetime;
        public double CoinsSpentLifetime;

        // World
        public List<BuildingSave> Buildings = new List<BuildingSave>();
        public bool[] RegionUnlocked;
        public int[] RegionExpansion;

        // Progress
        public List<string> Research = new List<string>();
        public int[] GlobalUpgrades;
        public bool[] AutoSell;
        public int[] ReserveStep;
        public List<string> Milestones = new List<string>();
        public List<string> EverBuilt = new List<string>();

        // Stats
        public int BuildingsBuilt;
        public int BuildingsUpgraded;
        public int TradesCompleted;
        public int ManualCollects;
        public int MaxLevelReached = 1;
        public double SoldCoins;
        public double OfflineSecondsTotal;
        public double PlaySeconds;

        // Trade
        public List<OrderSave> Orders = new List<OrderSave>();
        public uint TradeSeed;
        public int TradeNextId = 1;
        public int TradeCompleted;
        public int TradeRerolls;

        // Prestige
        public int Stars;
        public int StarsEarnedTotal;
        public int Rebirths;
        public bool CampaignCompleted;
        public bool[] PrestigeUpgrades;
        public int ActiveChallenge = -1;
        public double ChallengeStartProduced;
        public double ChallengeTimeLeft;
        public bool[] ChallengeDone;

        // Settings
        public int QualityLevel = -1;
    }

    /// <summary>Wrapper written to disk: payload JSON plus an integrity hash.</summary>
    [Serializable]
    public class SaveEnvelope
    {
        public int Version = SaveData.CurrentVersion;
        public string Payload;
        public string Hash;
    }
}
