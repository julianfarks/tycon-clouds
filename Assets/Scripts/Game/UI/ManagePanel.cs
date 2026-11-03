using System;
using System.Text;
using CloudGarden.Buildings;
using CloudGarden.Core;
using CloudGarden.Economy;
using CloudGarden.Optimization;
using CloudGarden.Progression;
using UnityEngine;
using UnityEngine.UI;

namespace CloudGarden.UI
{
    /// <summary>Statistics, automatic sales, settings and the Renacimiento Celestial (prestige).</summary>
    public sealed class ManagePanel : PanelBase
    {
        static int tab;
        public Action Refresh;

        public ManagePanel(UiContext ctx) : base(ctx) { }
        public override string Title { get { return "Gestión"; } }

        public static void SelectTab(int t) { tab = t; }

        public override void Build(RectTransform content)
        {
            string[] names = { "Estadísticas", "Ventas", "Ajustes", "Renacer" };
            RectTransform tabs = PanelKit.TabRow(content);
            for (int i = 0; i < names.Length; i++)
            {
                int idx = i;
                PanelKit.TabButton(tabs, names[i], i == tab, () => { tab = idx; if (Refresh != null) Refresh(); });
            }
            switch (tab)
            {
                case 0: BuildStats(content); break;
                case 1: BuildSales(content); break;
                case 2: BuildSettings(content); break;
                default: BuildPrestige(content); break;
            }
        }

        // ------------------------------------------------------------------ stats
        void BuildStats(RectTransform content)
        {
            GameState s = Ctx.State;
            GameStats st = s.Stats;

            PanelKit.Header(content, "Campaña");
            PanelKit.Entry camp = new PanelKit.Entry();
            camp.Title = "Progreso de campaña: " + Mathf.RoundToInt((float)(Campaign.Progress(s) * 100)) + "%";
            camp.Progress = (float)Campaign.Progress(s);
            StringBuilder ms = new StringBuilder();
            for (int i = 0; i < Campaign.Milestones.Length; i++)
            {
                Milestone m = Campaign.Milestones[i];
                bool done = st.Milestones.Contains(m.Id);
                ms.AppendLine(PanelKit.Colored(done ? "[OK] " : "[  ] ", done ? UIKit.Good : UIKit.TextDim) + (done ? m.Name : PanelKit.Colored(m.Name, UIKit.TextDim)));
            }
            camp.Info = ms.ToString();
            PanelKit.AddEntry(content, camp);

            PanelKit.Header(content, "Economía");
            double stored = 0, wasted = 0, producedTotal = 0;
            double perMinProd = 0, perMinCons = 0;
            for (int r = 1; r < ResourceInfo.Count; r++)
            {
                stored += s.Res.Amount[r];
                wasted += s.Res.TotalWasted[r];
                producedTotal += s.Res.TotalProduced[r];
                perMinProd += st.ProdRate[r] * 60.0;
                perMinCons += st.ConsRate[r] * 60.0;
            }
            StringBuilder eco = new StringBuilder();
            eco.AppendLine("Ganancias totales: " + PanelKit.Colored(NumberFormat.Compact(s.Res.CoinsEarnedLifetime), UIKit.Gold));
            eco.AppendLine("Gastos totales: " + NumberFormat.Compact(s.Res.CoinsSpentLifetime));
            eco.AppendLine("Ingresos por minuto: " + PanelKit.Colored(NumberFormat.Compact(st.CoinRate * 60.0), UIKit.Gold));
            eco.AppendLine("Producción total (unidades): " + NumberFormat.Compact(producedTotal));
            eco.AppendLine("Producción por minuto: " + NumberFormat.Compact(perMinProd));
            eco.AppendLine("Consumo por minuto: " + NumberFormat.Compact(perMinCons));
            eco.AppendLine("Recursos almacenados: " + NumberFormat.Compact(stored));
            eco.AppendLine("Recursos desperdiciados: " + PanelKit.Colored(NumberFormat.Compact(wasted), wasted > 0 ? UIKit.Warn : UIKit.TextMain));
            eco.AppendLine("Edificios construidos: " + st.BuildingsBuilt + "  (activos: " + s.Buildings.Count + ")");
            eco.AppendLine("Mejoras realizadas: " + st.BuildingsUpgraded);
            eco.AppendLine("Pedidos completados: " + st.TradesCompleted);
            eco.AppendLine("Tiempo de juego: " + NumberFormat.Duration(st.PlaySeconds));
            eco.Append("Tiempo producido sin conexión: " + NumberFormat.Duration(st.OfflineSecondsTotal));
            PanelKit.Entry ee = new PanelKit.Entry();
            ee.Info = eco.ToString();
            PanelKit.AddEntry(content, ee);

            PanelKit.Header(content, "Por recurso");
            for (int r = 1; r < ResourceInfo.Count; r++)
            {
                ResourceType t = (ResourceType)r;
                if (s.Res.TotalProduced[r] <= 0 && s.Res.Amount[r] <= 0) continue;
                PanelKit.Entry e = new PanelKit.Entry();
                e.Icon = IconFactory.Resource(t);
                e.Title = ResourceInfo.Name(t);
                e.Info = "Almacén " + NumberFormat.Compact(s.Res.Amount[r]) + " / " + NumberFormat.Compact(s.Res.Capacity[r])
                    + "\nProducido " + NumberFormat.Compact(s.Res.TotalProduced[r]) + "   Consumido " + NumberFormat.Compact(s.Res.TotalConsumed[r])
                    + "\n" + PanelKit.Colored("+" + NumberFormat.Compact(st.ProdRate[r] * 60) + "/min", UIKit.Good) + "   "
                    + PanelKit.Colored("-" + NumberFormat.Compact(st.ConsRate[r] * 60) + "/min", UIKit.Warn)
                    + "   Desperdicio " + NumberFormat.Compact(s.Res.TotalWasted[r]);
                e.Progress = s.Res.Capacity[r] > 0 ? (float)(s.Res.Amount[r] / s.Res.Capacity[r]) : 0f;
                PanelKit.AddEntry(content, e);
            }
        }

        // ------------------------------------------------------------------ sales
        void BuildSales(RectTransform content)
        {
            GameState s = Ctx.State;
            PanelKit.Header(content, "Venta de recursos");
            if (!s.Mods.HasMarket)
                PanelKit.Note(content, PanelKit.Colored("Construye el Mercado Flotante para vender automáticamente. Mientras tanto puedes vender a mano.", UIKit.Warn));
            else
                PanelKit.Note(content, "El Mercado vende cada tick lo que supere la reserva elegida. Precio x" + s.Mods.SellMult.ToString("0.00") + ".");

            for (int r = 1; r < ResourceInfo.Count; r++)
            {
                ResourceType t = (ResourceType)r;
                if (!(s.Res.TotalProduced[r] > 0 || s.Res.Amount[r] > 0 || r <= 3)) continue;
                int idx = r;
                PanelKit.Entry e = new PanelKit.Entry();
                e.Icon = IconFactory.Resource(t);
                e.Title = ResourceInfo.Name(t) + "  <color=#FFC857>" + NumberFormat.Compact(ResourceInfo.Value(t) * s.Mods.SellMult) + " c/u</color>";
                e.Subtitle = "En almacén: " + NumberFormat.Compact(s.Res.Amount[r]) + " / " + NumberFormat.Compact(s.Res.Capacity[r]);
                bool auto = s.AutoSell[r];
                int reserve = Mathf.RoundToInt((float)GameState.ReserveFractions[s.ReserveStep[r]] * 100f);
                e.Info = "Venta automática: " + PanelKit.Colored(auto ? "ACTIVA" : "apagada", auto ? UIKit.Good : UIKit.TextDim)
                    + "   Reserva: " + reserve + "%";
                e.Button = auto ? "Auto: SÍ" : "Auto: NO";
                e.ButtonColor = auto ? UIKit.Good : UIKit.Disabled;
                e.OnClick = () => s.SetAutoSell((ResourceType)idx, !s.AutoSell[idx]);
                e.Button2 = "Reserva " + reserve + "%";
                e.OnClick2 = () => s.CycleReserve((ResourceType)idx);
                PanelKit.AddEntry(content, e);

                Button sell = UIKit.MakeButton(content, "Vender todo el " + ResourceInfo.ShortName(t) + " (" + NumberFormat.Compact(s.Res.Amount[r] * ResourceInfo.Value(t) * s.Mods.SellMult) + ")", UIKit.CardBgLight, () => s.SellAll((ResourceType)idx), 24);
                UIKit.Fixed(sell.gameObject, -1, 62);
                sell.interactable = s.Res.Amount[r] > 0.5;
            }
        }

        // ------------------------------------------------------------------ settings
        void BuildSettings(RectTransform content)
        {
            GameState s = Ctx.State;
            PanelKit.Header(content, "Calidad gráfica");
            PanelKit.Note(content, "Perfil actual: " + QualityManager.Names[(int)QualityManager.Current] + ". Bajo ahorra batería; Ultra añade sombras, más nubes y vegetación.");
            RectTransform row = PanelKit.TabRow(content);
            for (int i = 0; i < 4; i++)
            {
                QualityProfile p = (QualityProfile)i;
                PanelKit.TabButton(row, QualityManager.Names[i], QualityManager.Current == p, () =>
                {
                    QualityManager.Apply(p, true);
                    if (Refresh != null) Refresh();
                });
            }

            PanelKit.Header(content, "Partida");
            Button save = UIKit.MakeButton(content, "Guardar ahora", UIKit.Good, () => { Ctx.SaveNow(); Ctx.Toast("Partida guardada"); }, 28);
            UIKit.Fixed(save.gameObject, -1, 80);
            PanelKit.Note(content, "El juego guarda automáticamente cada pocos segundos y al salir. Tus máquinas siguen produciendo hasta " + s.Mods.OfflineHours.ToString("0") + " h sin conexión.");
            Button reset = UIKit.MakeButton(content, "Reiniciar partida (borra todo)", UIKit.Bad, () =>
            {
                Ctx.Confirm("¿Seguro que quieres borrar TODA la partida? Esta acción no se puede deshacer.", () => Ctx.ResetGame());
            }, 26);
            UIKit.Fixed(reset.gameObject, -1, 80);
        }

        // ------------------------------------------------------------------ prestige
        void BuildPrestige(RectTransform content)
        {
            GameState s = Ctx.State;
            PrestigeState p = s.Prestige;
            PanelKit.Header(content, "Renacimiento Celestial");

            PanelKit.Entry head = new PanelKit.Entry();
            head.Title = "Estrellas: " + p.Stars + "   (totales ganadas: " + p.StarsEarnedTotal + ")";
            head.Subtitle = "Renacimientos: " + p.Rebirths + "   Bonificación permanente: +" + (p.StarsEarnedTotal * 5) + "% de producción";
            if (!PrestigeSystem.CanRebirth(s))
            {
                head.Info = PanelKit.Colored("Completa la campaña (" + Mathf.RoundToInt((float)(Campaign.Progress(s) * 100)) + "%) para desbloquear el Renacimiento.", UIKit.Warn);
            }
            else
            {
                int pending = PrestigeSystem.PendingStars(s);
                head.Info = "Si renaces ahora ganas " + PanelKit.Colored("+" + pending + " estrellas", UIKit.Gold)
                    + ".\nSe reinician monedas, recursos, edificios, regiones e investigaciones. Conservas estrellas, mejoras y estadísticas.";
                head.Button = "Renacer";
                head.ButtonColor = UIKit.Accent;
                head.OnClick = () => Ctx.Confirm("¿Renacer y ganar " + pending + " estrellas? Perderás tu progreso económico de esta partida.", () => PrestigeSystem.Rebirth(s));
            }
            PanelKit.AddEntry(content, head);

            PanelKit.Header(content, "Mejoras con estrellas");
            for (int i = 0; i < PrestigeState.UpgradeCount; i++)
            {
                int idx = i;
                bool owned = p.Upgrades[i];
                PanelKit.Entry e = new PanelKit.Entry();
                e.Title = PrestigeState.UpgradeNames[i] + (owned ? "  <color=#3FCB86>(comprada)</color>" : "");
                e.Subtitle = PrestigeState.UpgradeDescriptions[i];
                e.Info = owned ? "" : "Coste: " + PrestigeState.UpgradeCosts[i] + " estrellas";
                if (!owned)
                {
                    e.Button = "Comprar";
                    e.ButtonEnabled = p.Stars >= PrestigeState.UpgradeCosts[i];
                    e.ButtonColor = UIKit.Accent;
                    e.OnClick = () => PrestigeSystem.BuyUpgrade(s, idx);
                }
                PanelKit.AddEntry(content, e);
            }

            PanelKit.Header(content, "Desafíos industriales");
            PanelKit.Note(content, "Produce la cantidad indicada antes de que acabe el tiempo para ganar estrellas. Se desbloquean tras el primer Renacimiento o al completar la campaña.");
            for (int i = 0; i < PrestigeState.ChallengeCount; i++)
            {
                int idx = i;
                ChallengeDef c = PrestigeState.Challenges[i];
                PanelKit.Entry e = new PanelKit.Entry();
                e.Icon = IconFactory.Resource(c.Resource);
                e.Title = c.Name;
                e.Subtitle = "Produce " + NumberFormat.Compact(c.Target) + " de " + ResourceInfo.Name(c.Resource) + " en " + NumberFormat.Duration(c.Seconds)
                    + "   Recompensa: " + c.RewardStars + " estrellas";
                if (p.ChallengeDone[i])
                {
                    e.Info = PanelKit.Colored("Completado", UIKit.Good);
                }
                else if (p.ActiveChallenge == i)
                {
                    double prog = p.ChallengeProgress(s.Res);
                    e.Info = "En curso: " + NumberFormat.Compact(prog) + " / " + NumberFormat.Compact(c.Target) + "   Quedan " + NumberFormat.Duration(p.ChallengeTimeLeft);
                    e.Progress = (float)(prog / c.Target);
                    e.Button = "Cancelar";
                    e.ButtonColor = UIKit.Bad;
                    e.OnClick = () => PrestigeSystem.CancelChallenge(s);
                }
                else
                {
                    bool unlocked = p.CampaignCompleted || p.Rebirths > 0;
                    e.Button = "Empezar";
                    e.ButtonEnabled = unlocked && p.ActiveChallenge < 0;
                    e.ButtonColor = UIKit.Accent;
                    e.OnClick = () => PrestigeSystem.StartChallenge(s, idx);
                }
                PanelKit.AddEntry(content, e);
            }
        }
    }
}
