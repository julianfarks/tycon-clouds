using System;
using System.Collections.Generic;
using System.Text;
using CloudGarden.Buildings;
using CloudGarden.Core;
using CloudGarden.Economy;
using CloudGarden.Islands;
using CloudGarden.Research;
using CloudGarden.Trading;
using UnityEngine;
using UnityEngine.UI;

namespace CloudGarden.UI
{
    // ============================================================ CONSTRUCCION
    public sealed class BuildPanel : PanelBase
    {
        static int category;
        public Action Refresh;

        public BuildPanel(UiContext ctx) : base(ctx) { }
        public override string Title { get { return "Construcción"; } }

        public override void Build(RectTransform content)
        {
            GameState s = Ctx.State;
            string[] names = { "Recolección", "Proceso", "Agricultura", "Industria", "Servicios" };
            RectTransform tabs = PanelKit.TabRow(content);
            for (int i = 0; i < names.Length; i++)
            {
                int idx = i;
                PanelKit.TabButton(tabs, names[i], i == category, () => { category = idx; if (Refresh != null) Refresh(); });
            }

            List<BuildingDef> available = new List<BuildingDef>();
            List<BuildingDef> locked = new List<BuildingDef>();
            for (int i = 0; i < BuildingDatabase.Count; i++)
            {
                BuildingDef d = BuildingDatabase.All[i];
                if ((int)d.Category != category) continue;
                if (s.IsUnlocked(d)) available.Add(d); else locked.Add(d);
            }

            for (int i = 0; i < available.Count; i++) AddBuildingEntry(content, available[i], true);
            for (int i = 0; i < locked.Count; i++) AddBuildingEntry(content, locked[i], false);
        }

        void AddBuildingEntry(RectTransform content, BuildingDef d, bool unlocked)
        {
            GameState s = Ctx.State;
            int owned = s.CountOf(d.Id);
            bool maxed = d.MaxCount > 0 && owned >= d.MaxCount;
            Color tint;
            PanelKit.Entry e = new PanelKit.Entry();
            e.Icon = BuildingInfo.IconFor(d, out tint);
            e.IconTint = unlocked ? tint : new Color(0.5f, 0.5f, 0.55f, 0.6f);
            e.Title = d.Name + (owned > 0 ? "  <color=#A9B0D0>x" + owned + "</color>" : "");
            e.Subtitle = d.Description;
            StringBuilder info = new StringBuilder();
            if (!unlocked) info.AppendLine(PanelKit.Colored("Bloqueado - " + s.LockReason(d), UIKit.Warn));
            string io = PanelKit.IoText(d);
            if (io.Length > 0) info.AppendLine(io);
            else info.AppendLine(BuildingInfo.RoleText(s, d, 1));
            info.Append("Coste: " + PanelKit.CostText(s, s.BuildCoinCost(d), d.Materials, s.BuildMaterialMultiplier(d)));
            info.Append(PanelKit.Colored("   Tamaño " + d.Width + "x" + d.Depth, UIKit.TextDim));
            e.Info = info.ToString();
            e.Button = maxed ? "Máximo" : (unlocked ? "Construir" : "Bloqueado");
            e.ButtonEnabled = unlocked && !maxed;
            e.ButtonColor = UIKit.Good;
            BuildingDef dd = d;
            e.OnClick = () =>
            {
                Ctx.ClosePanel();
                Ctx.Placement.BeginPlace(dd.Id);
            };
            PanelKit.AddEntry(content, e);
        }
    }

    // ============================================================ MEJORAS
    public sealed class UpgradePanel : PanelBase
    {
        public UpgradePanel(UiContext ctx) : base(ctx) { }
        public override string Title { get { return "Mejoras"; } }

        public override void Build(RectTransform content)
        {
            GameState s = Ctx.State;
            PanelKit.Header(content, "Mejoras globales");
            for (int i = 0; i < Progression.GlobalUpgrades.Count; i++)
            {
                int idx = i;
                int level = s.GlobalUpgradeLevels[i];
                int max = Progression.GlobalUpgrades.MaxLevel(i);
                bool maxed = level >= max;
                PanelKit.Entry e = new PanelKit.Entry();
                e.Title = Progression.GlobalUpgrades.Name(i) + "  <color=#A9B0D0>Nv." + level + "/" + max + "</color>";
                e.Subtitle = Progression.GlobalUpgrades.Description(i);
                e.Progress = level / (float)max;
                double cost = s.GlobalUpgradeCost(i);
                e.Info = maxed ? PanelKit.Colored("Nivel máximo", UIKit.Good) : "Coste: " + PanelKit.Colored(NumberFormat.Compact(cost) + " monedas", s.Res.CanAffordCoins(cost) ? UIKit.Gold : UIKit.Bad);
                e.Button = maxed ? "Máximo" : "Mejorar";
                e.ButtonEnabled = !maxed && s.Res.CanAffordCoins(cost);
                e.ButtonColor = UIKit.Good;
                e.OnClick = () => s.TryBuyGlobalUpgrade(idx);
                PanelKit.AddEntry(content, e);
            }

            PanelKit.Header(content, "Tus edificios");
            if (s.Buildings.Count == 0)
            {
                PanelKit.Note(content, "Aún no tienes edificios. Construye uno desde el menú Construir.");
                return;
            }

            List<BuildingInstance> list = new List<BuildingInstance>(s.Buildings);
            list.Sort((a, b) =>
            {
                int c = a.Def.Category.CompareTo(b.Def.Category);
                if (c != 0) return c;
                c = string.CompareOrdinal(a.DefId, b.DefId);
                return c != 0 ? c : a.Level.CompareTo(b.Level);
            });

            for (int i = 0; i < list.Count; i++)
            {
                BuildingInstance b = list[i];
                int uid = b.Uid;
                Color tint;
                PanelKit.Entry e = new PanelKit.Entry();
                e.Icon = BuildingInfo.IconFor(b.Def, out tint);
                e.IconTint = tint;
                e.Title = b.Def.Name + "  <color=#A9B0D0>Nv." + b.Level + "/" + LevelMath.MaxLevel + "</color>";
                e.Subtitle = RegionDatabase.Get(b.Region).Name + "   " + BuildingInfo.StatusText(b);
                e.Info = BuildingInfo.RatesText(s, b.Def, b.Level);
                if (b.Level < LevelMath.MaxLevel)
                {
                    string next = BuildingInfo.RatesText(s, b.Def, b.Level + 1);
                    e.Info += "\n" + PanelKit.Colored("Siguiente: ", UIKit.TextDim) + next;
                }
                e.Progress = b.Level / (float)LevelMath.MaxLevel;
                if (b.Level >= LevelMath.MaxLevel)
                {
                    e.Button = "Nivel máx.";
                    e.ButtonEnabled = false;
                }
                else
                {
                    double cost = s.UpgradeCost(b);
                    bool can = s.Res.CanAffordCoins(cost);
                    e.Button = "Mejorar\n" + NumberFormat.Compact(cost);
                    e.ButtonEnabled = can;
                    e.ButtonColor = UIKit.Good;
                    e.OnClick = () => s.TryUpgrade(uid);
                }
                e.Button2 = "Ver";
                e.OnClick2 = () =>
                {
                    BuildingInstance bi = s.FindBuilding(uid);
                    if (bi == null) return;
                    Ctx.ClosePanel();
                    Ctx.Camera.FocusOn(Ctx.World.Islands[bi.Region].TileCenter(bi.X + bi.FootprintWidth / 2, bi.Y + bi.FootprintDepth / 2), 7f);
                    Ctx.Selection.Select(uid);
                };
                PanelKit.AddEntry(content, e);
            }
        }
    }

    // ============================================================ INVESTIGACION
    public sealed class ResearchPanel : PanelBase
    {
        public ResearchPanel(UiContext ctx) : base(ctx) { }
        public override string Title { get { return "Investigación"; } }

        public override void Build(RectTransform content)
        {
            GameState s = Ctx.State;
            List<ResearchDef> ready = new List<ResearchDef>();
            List<ResearchDef> locked = new List<ResearchDef>();
            List<ResearchDef> done = new List<ResearchDef>();
            for (int i = 0; i < ResearchDatabase.Count; i++)
            {
                ResearchDef r = ResearchDatabase.All[i];
                string reason;
                if (s.Research.Contains(r.Id)) done.Add(r);
                else if (s.CanResearch(r, out reason)) ready.Add(r);
                else locked.Add(r);
            }

            PanelKit.Header(content, "Disponibles (" + ready.Count + ")");
            if (ready.Count == 0) PanelKit.Note(content, "No hay investigaciones disponibles ahora mismo.");
            for (int i = 0; i < ready.Count; i++) AddEntry(content, ready[i], 0);

            if (locked.Count > 0)
            {
                PanelKit.Header(content, "Bloqueadas (" + locked.Count + ")");
                for (int i = 0; i < locked.Count; i++) AddEntry(content, locked[i], 1);
            }
            if (done.Count > 0)
            {
                PanelKit.Header(content, "Completadas (" + done.Count + "/" + ResearchDatabase.Count + ")");
                for (int i = 0; i < done.Count; i++) AddEntry(content, done[i], 2);
            }
        }

        void AddEntry(RectTransform content, ResearchDef r, int state)
        {
            GameState s = Ctx.State;
            PanelKit.Entry e = new PanelKit.Entry();
            e.Title = r.Name;
            e.Subtitle = r.Description;
            string reason;
            bool can = state == 0;
            if (state == 2)
            {
                e.Info = PanelKit.Colored("Completada", UIKit.Good);
                e.Tint = new Color(0.2f, 0.5f, 0.35f, 0.18f);
            }
            else
            {
                s.CanResearch(r, out reason);
                StringBuilder sb = new StringBuilder();
                if (state == 1) sb.AppendLine(PanelKit.Colored(reason, UIKit.Warn));
                sb.Append("Coste: " + PanelKit.CostText(s, s.ResearchCoinCost(r), r.Materials, 1.0));
                e.Info = sb.ToString();
                e.Button = can ? "Investigar" : "Bloqueada";
                e.ButtonEnabled = can && s.CanAffordResearch(r);
                e.ButtonColor = UIKit.Accent;
                string id = r.Id;
                e.OnClick = () => s.TryResearch(id);
            }
            PanelKit.AddEntry(content, e);
        }
    }

    // ============================================================ COMERCIO
    public sealed class TradePanel : PanelBase
    {
        public TradePanel(UiContext ctx) : base(ctx) { }
        public override string Title { get { return "Comercio"; } }

        public override void Build(RectTransform content)
        {
            GameState s = Ctx.State;
            if (!s.Mods.HasPort)
            {
                PanelKit.Header(content, "Puerto Aéreo");
                BuildingDef port = BuildingDatabase.Get("air_port");
                PanelKit.Note(content, "Construye un Puerto Aéreo para atraer comerciantes con pedidos de entrega bien pagados.");
                if (!s.IsUnlocked(port)) PanelKit.Note(content, PanelKit.Colored(s.LockReason(port), UIKit.Warn));
                else
                {
                    Button b = UIKit.MakeButton(content, "Construir Puerto Aéreo", UIKit.Good, () => { Ctx.ClosePanel(); Ctx.Placement.BeginPlace("air_port"); }, 28);
                    UIKit.Fixed(b.gameObject, -1, 84);
                }
                return;
            }

            PanelKit.Header(content, "Pedidos de comerciantes");
            PanelKit.Note(content, "Cambios gratuitos disponibles: " + s.Trade.Rerolls + "/" + TradeState.MaxRerolls + "   (+1 cada 2 pedidos)");
            if (s.Trade.Orders.Count == 0) PanelKit.Note(content, "Los comerciantes llegarán enseguida...");

            for (int i = 0; i < s.Trade.Orders.Count; i++)
            {
                TradeOrder o = s.Trade.Orders[i];
                int id = o.Id;
                StringBuilder sb = new StringBuilder();
                for (int k = 0; k < o.Wants.Count; k++)
                {
                    ResourceAmount w = o.Wants[k];
                    double have = s.Res.Get(w.Type);
                    bool ok = have + 1e-9 >= w.Amount;
                    sb.AppendLine(PanelKit.Colored(NumberFormat.Compact(have) + " / " + NumberFormat.Compact(w.Amount), ok ? UIKit.Good : UIKit.Bad) + "  " + ResourceInfo.Name(w.Type));
                }
                sb.Append("Recompensa: " + PanelKit.Colored(NumberFormat.Compact(TradeSystem.FinalReward(s, o)) + " monedas", UIKit.Gold)
                    + "  +  " + NumberFormat.Compact(o.Bonus.Amount) + " " + ResourceInfo.ShortName(o.Bonus.Type));

                PanelKit.Entry e = new PanelKit.Entry();
                e.Icon = IconFactory.Resource(o.Wants[0].Type);
                e.Title = o.Merchant + (o.Special ? "  <color=#FFC857>(Pedido especial)</color>" : "");
                e.Info = sb.ToString();
                e.Button = "Entregar";
                e.ButtonEnabled = TradeSystem.CanComplete(s, o);
                e.ButtonColor = UIKit.Good;
                e.OnClick = () => TradeSystem.Complete(s, id);
                e.Button2 = s.Trade.Rerolls > 0 ? "Cambiar pedido" : "";
                e.OnClick2 = () => TradeSystem.Reroll(s, id);
                if (o.Special) e.Tint = new Color(1f, 0.78f, 0.34f, 0.12f);
                PanelKit.AddEntry(content, e);
            }
        }
    }

    // ============================================================ EXPANSION
    public sealed class ExpandPanel : PanelBase
    {
        public ExpandPanel(UiContext ctx) : base(ctx) { }
        public override string Title { get { return "Expansión"; } }

        public override void Build(RectTransform content)
        {
            GameState s = Ctx.State;
            for (int i = 0; i < RegionDatabase.Count; i++)
            {
                RegionDef r = RegionDatabase.Get(i);
                int idx = i;
                PanelKit.Entry e = new PanelKit.Entry();
                e.Title = r.Name;
                e.Subtitle = r.Description + "\n" + PanelKit.Colored("Recursos: " + r.Exclusive, UIKit.TextDim);
                StringBuilder sb = new StringBuilder();

                if (s.RegionUnlocked[i])
                {
                    IslandGridInfo(s, i, sb, e);
                    e.Tint = new Color(0.2f, 0.5f, 0.35f, 0.14f);
                    e.Button2 = "Ir a la isla";
                    e.OnClick2 = () =>
                    {
                        Ctx.ClosePanel();
                        Ctx.Camera.FocusOn(Ctx.World.Islands[idx].transform.position, 9f);
                    };
                }
                else
                {
                    string reason;
                    bool can = s.CanUnlockRegion(i, out reason);
                    sb.AppendLine("Requisitos:");
                    if (i > 0 && !s.RegionUnlocked[i - 1]) sb.AppendLine(PanelKit.Colored("[ ] Desbloquear " + RegionDatabase.Get(i - 1).Name, UIKit.Bad));
                    ResearchDef rd = ResearchDatabase.Get(r.RequiredResearch);
                    bool researched = s.HasResearch(r.RequiredResearch);
                    sb.AppendLine(PanelKit.Colored((researched ? "[OK] " : "[ ] ") + "Investigación: " + (rd != null ? rd.Name : r.RequiredResearch), researched ? UIKit.Good : UIKit.Bad));
                    sb.Append(PanelKit.CostText(s, r.UnlockCoins, r.UnlockMaterials, 1.0));
                    e.Button = "Desbloquear";
                    e.ButtonEnabled = can;
                    e.ButtonColor = UIKit.Accent;
                    e.OnClick = () => s.TryUnlockRegion(idx);
                    e.Icon = null;
                }
                e.Info = sb.ToString();
                PanelKit.AddEntry(content, e);
            }
        }

        void IslandGridInfo(GameState s, int region, StringBuilder sb, PanelKit.Entry e)
        {
            Construction.IslandGrid g = s.Grids[region];
            sb.Append("Terreno: " + g.Size + "x" + g.Size + " casillas  (" + g.Expansion + "/" + Construction.IslandGrid.MaxExpansion + " ampliaciones)");
            if (s.CanExpand(region))
            {
                double cost = s.ExpansionCost(region);
                sb.Append("\nSiguiente: " + g.Size + " -> " + (g.Size + 2) + "   " + PanelKit.Colored(NumberFormat.Compact(cost) + " monedas", s.Res.CanAffordCoins(cost) ? UIKit.Gold : UIKit.Bad));
                e.Button = "Ampliar terreno";
                e.ButtonEnabled = s.Res.CanAffordCoins(cost);
                e.ButtonColor = UIKit.Good;
                e.OnClick = () => s.TryExpand(region);
            }
            else
            {
                sb.Append("\n" + PanelKit.Colored("Terreno al máximo", UIKit.Good));
            }
        }
    }
}
