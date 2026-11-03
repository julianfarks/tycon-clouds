using System;
using CloudGarden.Core;
using CloudGarden.Economy;

namespace CloudGarden.Progression
{
    public sealed class Milestone
    {
        public readonly string Id;
        public readonly string Name;
        public readonly Func<GameState, bool> Check;

        public Milestone(string id, string name, Func<GameState, bool> check)
        {
            Id = id;
            Name = name;
            Check = check;
        }
    }

    /// <summary>Campaign progress is measured with 20 milestones that stay achieved forever.</summary>
    public static class Campaign
    {
        public static readonly Milestone[] Milestones =
        {
            new Milestone("m01", "Construye tu primer Recolector de Rocío", s => s.Stats.EverBuilt.Contains("dew_collector")),
            new Milestone("m02", "Abre el Mercado Flotante", s => s.Stats.EverBuilt.Contains("market")),
            new Milestone("m03", "Destila tu primera agua", s => s.Stats.EverBuilt.Contains("distiller")),
            new Milestone("m04", "Cultiva en un Invernadero", s => s.Stats.EverBuilt.Contains("greenhouse")),
            new Milestone("m05", "Completa 5 investigaciones", s => s.Research.Count >= 5),
            new Milestone("m06", "Desbloquea la Isla Tropical", s => s.RegionUnlocked[1]),
            new Milestone("m07", "Planta un Huerto Tropical", s => s.Stats.EverBuilt.Contains("tropical_orchard")),
            new Milestone("m08", "Completa tu primer pedido comercial", s => s.Stats.TradesCompleted >= 1),
            new Milestone("m09", "Desbloquea la Isla Nevada", s => s.RegionUnlocked[2]),
            new Milestone("m10", "Fabrica tu primer hielo", s => s.Res.TotalProduced[(int)ResourceType.Ice] > 0),
            new Milestone("m11", "Obtén 100 cristales celestes", s => s.Res.TotalProduced[(int)ResourceType.Crystals] >= 100),
            new Milestone("m12", "Construye el Observatorio", s => s.Stats.EverBuilt.Contains("observatory")),
            new Milestone("m13", "Desbloquea la Isla Tormentosa", s => s.RegionUnlocked[3]),
            new Milestone("m14", "Genera 100 de energía atmosférica", s => s.Res.TotalProduced[(int)ResourceType.Energy] >= 100),
            new Milestone("m15", "Lleva un edificio al nivel 10", s => s.Stats.MaxLevelReached >= 10),
            new Milestone("m16", "Desbloquea la Isla Celestial", s => s.RegionUnlocked[4]),
            new Milestone("m17", "Destila tu primera esencia celestial", s => s.Res.TotalProduced[(int)ResourceType.Essence] >= 1),
            new Milestone("m18", "Enciende el Núcleo Climático", s => s.Stats.EverBuilt.Contains("climate_core")),
            new Milestone("m19", "Produce 500 de esencia celestial", s => s.Res.TotalProduced[(int)ResourceType.Essence] >= 500),
            new Milestone("m20", "Gana 1 billón de monedas en total", s => s.Res.CoinsEarnedLifetime >= 1e12)
        };

        public static int Total { get { return Milestones.Length; } }

        public static int Completed(GameState s)
        {
            int n = 0;
            for (int i = 0; i < Milestones.Length; i++) if (s.Stats.Milestones.Contains(Milestones[i].Id)) n++;
            return n;
        }

        /// <summary>0..1</summary>
        public static double Progress(GameState s)
        {
            return Completed(s) / (double)Milestones.Length;
        }

        /// <summary>Evaluates milestones; returns the number newly achieved.</summary>
        public static int Update(GameState s)
        {
            int added = 0;
            for (int i = 0; i < Milestones.Length; i++)
            {
                Milestone m = Milestones[i];
                if (s.Stats.Milestones.Contains(m.Id)) continue;
                if (m.Check(s))
                {
                    s.Stats.Milestones.Add(m.Id);
                    s.RaiseToast("Hito: " + m.Name);
                    added++;
                }
            }
            if (!s.Prestige.CampaignCompleted && Completed(s) >= Milestones.Length)
            {
                s.Prestige.CampaignCompleted = true;
                s.RaiseToast("¡Campaña completada! Desbloqueado el Renacimiento Celestial.");
                added++;
            }
            if (added > 0) s.RaiseChanged();
            return added;
        }
    }
}
