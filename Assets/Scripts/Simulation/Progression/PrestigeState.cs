using CloudGarden.Economy;
using R = CloudGarden.Economy.ResourceType;

namespace CloudGarden.Progression
{
    public sealed class ChallengeDef
    {
        public string Name;
        public ResourceType Resource;
        public double Target;
        public double Seconds;
        public int RewardStars;
    }

    /// <summary>Renacimiento Celestial: permanent stars, upgrades and industrial challenges.</summary>
    public sealed class PrestigeState
    {
        public const int UpgradeCount = 7;
        public const int ChallengeCount = 3;

        public int Stars;            // spendable
        public int StarsEarnedTotal; // permanent multiplier source
        public int Rebirths;
        public bool CampaignCompleted;
        public readonly bool[] Upgrades = new bool[UpgradeCount];

        public int ActiveChallenge = -1;
        public double ChallengeStartProduced;
        public double ChallengeTimeLeft;
        public readonly bool[] ChallengeDone = new bool[ChallengeCount];

        public static readonly string[] UpgradeNames =
        {
            "Cosecha estelar", "Almacén infinito", "Inicio rápido", "Descuento celestial",
            "Descanso prolongado", "Colector Dorado", "Apariencia dorada"
        };

        public static readonly string[] UpgradeDescriptions =
        {
            "+25% de producción global.",
            "+50% de almacenamiento.",
            "Empiezas cada Renacimiento con 10.000 monedas.",
            "-10% al coste de construcción.",
            "+4 horas de producción sin conexión.",
            "Desbloquea la máquina especial Colector Dorado.",
            "Los edificios de nivel 10 lucen detalles dorados."
        };

        public static readonly int[] UpgradeCosts = { 3, 5, 4, 6, 8, 10, 2 };

        public static readonly ChallengeDef[] Challenges =
        {
            new ChallengeDef { Name = "Maratón del rocío", Resource = R.Water, Target = 50000, Seconds = 20 * 60, RewardStars = 2 },
            new ChallengeDef { Name = "Fiebre del polen", Resource = R.Pollen, Target = 100000, Seconds = 30 * 60, RewardStars = 3 },
            new ChallengeDef { Name = "Tormenta perfecta", Resource = R.Energy, Target = 20000, Seconds = 40 * 60, RewardStars = 5 }
        };

        public double ChallengeProgress(ResourceStore res)
        {
            if (ActiveChallenge < 0) return 0;
            double produced = res.TotalProduced[(int)Challenges[ActiveChallenge].Resource] - ChallengeStartProduced;
            return produced < 0 ? 0 : produced;
        }
    }
}
