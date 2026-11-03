using System;
using CloudGarden.Core;
using CloudGarden.Economy;

namespace CloudGarden.Progression
{
    /// <summary>Rules of the Renacimiento Celestial (prestige), star upgrades and industrial challenges.</summary>
    public static class PrestigeSystem
    {
        public static bool CanRebirth(GameState s)
        {
            return s.Prestige.CampaignCompleted;
        }

        public static int PendingStars(GameState s)
        {
            if (!s.Prestige.CampaignCompleted) return 0;
            int stars = (int)Math.Floor(Math.Sqrt(s.Res.CoinsEarnedRun / 1e10));
            return Math.Max(1, stars);
        }

        public static bool Rebirth(GameState s)
        {
            if (!CanRebirth(s)) return false;
            int stars = PendingStars(s);
            s.Prestige.Stars += stars;
            s.Prestige.StarsEarnedTotal += stars;
            s.Prestige.Rebirths++;
            s.Prestige.ActiveChallenge = -1;
            s.ResetForRebirth();
            s.RaiseToast("Renacimiento completado: +" + stars + " estrellas");
            return true;
        }

        public static bool BuyUpgrade(GameState s, int index)
        {
            if (index < 0 || index >= PrestigeState.UpgradeCount) return false;
            if (s.Prestige.Upgrades[index]) return false;
            int cost = PrestigeState.UpgradeCosts[index];
            if (s.Prestige.Stars < cost) return false;
            s.Prestige.Stars -= cost;
            s.Prestige.Upgrades[index] = true;
            s.ModsDirty = true;
            s.RecomputeMods();
            s.RaiseChanged();
            return true;
        }

        public static bool StartChallenge(GameState s, int index)
        {
            if (index < 0 || index >= PrestigeState.ChallengeCount) return false;
            if (!s.Prestige.CampaignCompleted && s.Prestige.Rebirths < 1) return false;
            if (s.Prestige.ActiveChallenge >= 0 || s.Prestige.ChallengeDone[index]) return false;
            ChallengeDef c = PrestigeState.Challenges[index];
            s.Prestige.ActiveChallenge = index;
            s.Prestige.ChallengeStartProduced = s.Res.TotalProduced[(int)c.Resource];
            s.Prestige.ChallengeTimeLeft = c.Seconds;
            s.RaiseChanged();
            return true;
        }

        public static void CancelChallenge(GameState s)
        {
            if (s.Prestige.ActiveChallenge < 0) return;
            s.Prestige.ActiveChallenge = -1;
            s.RaiseChanged();
        }

        public static void Tick(GameState s, double dt)
        {
            PrestigeState p = s.Prestige;
            if (p.ActiveChallenge < 0) return;
            ChallengeDef c = PrestigeState.Challenges[p.ActiveChallenge];
            if (p.ChallengeProgress(s.Res) >= c.Target)
            {
                p.ChallengeDone[p.ActiveChallenge] = true;
                p.Stars += c.RewardStars;
                p.StarsEarnedTotal += c.RewardStars;
                p.ActiveChallenge = -1;
                s.ModsDirty = true;
                s.RaiseToast("Desafío completado: " + c.Name + " (+" + c.RewardStars + " estrellas)");
                s.RaiseChanged();
                return;
            }
            p.ChallengeTimeLeft -= dt;
            if (p.ChallengeTimeLeft <= 0)
            {
                p.ActiveChallenge = -1;
                s.RaiseToast("Desafío fallido: " + c.Name);
                s.RaiseChanged();
            }
        }
    }
}
