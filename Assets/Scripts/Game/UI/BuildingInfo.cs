using System.Text;
using CloudGarden.Buildings;
using CloudGarden.Core;
using CloudGarden.Economy;
using CloudGarden.Production;
using UnityEngine;

namespace CloudGarden.UI
{
    /// <summary>Text descriptions of what a building does at a given level.</summary>
    public static class BuildingInfo
    {
        public static string StatusText(BuildingInstance b)
        {
            switch (b.Status)
            {
                case BuildingStatus.Running: return PanelKit.Colored("Produciendo", UIKit.Good);
                case BuildingStatus.MissingInput: return PanelKit.Colored("Faltan materiales", UIKit.Warn);
                case BuildingStatus.StorageFull: return PanelKit.Colored("Almacén lleno", UIKit.Bad);
                case BuildingStatus.Passive: return PanelKit.Colored("Activo", UIKit.Good);
                default: return PanelKit.Colored("En espera", UIKit.TextDim);
            }
        }

        public static string RoleText(GameState s, BuildingDef d, int level)
        {
            switch (d.Role)
            {
                case BuildingRole.Storage:
                    return "+" + NumberFormat.Compact(d.RoleValue * LevelMath.Capacity(level)) + " de capacidad en cada recurso";
                case BuildingRole.Market:
                    return "Vende lo marcado en Gestión. Precio +" + Mathf.RoundToInt((float)(d.RoleValue * (level - 1) * 100)) + "%";
                case BuildingRole.Port:
                    return "Pedidos comerciales simultáneos: " + s.Mods.TradeSlots;
                case BuildingRole.CommTower:
                    return "Recompensas de pedidos +" + Mathf.RoundToInt((float)(d.RoleValue * level * 100)) + "%";
                case BuildingRole.ResearchCenter:
                    return "Investigaciones -" + Mathf.RoundToInt((float)(d.RoleValue * level * 100)) + "% de coste";
                case BuildingRole.Observatory:
                    return "Producción global +" + Mathf.RoundToInt((float)(d.RoleValue * level * 100)) + "%";
                case BuildingRole.ClimateCore:
                    return "Producción global x" + (1.0 + d.RoleValue * level).ToString("0.00");
                case BuildingRole.IndustrialCenter:
                    return "Fábricas y procesadoras +" + Mathf.RoundToInt((float)(d.RoleValue * level * 100)) + "% de velocidad";
                default:
                    return "";
            }
        }

        /// <summary>Per-second production/consumption lines at 100% activity.</summary>
        public static string RatesText(GameState s, BuildingDef d, int level)
        {
            if (!d.IsProducer) return RoleText(s, d, level);
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < d.Outputs.Count; i++)
            {
                ResourceType t = d.Outputs[i].Type;
                double rate = ProductionSystem.NominalOutputPerSecond(s, d, level, t);
                if (sb.Length > 0) sb.Append("   ");
                sb.Append(PanelKit.Colored("+" + NumberFormat.Compact(rate) + "/s " + ResourceInfo.ShortName(t), UIKit.Good));
            }
            for (int i = 0; i < d.Inputs.Count; i++)
            {
                ResourceType t = d.Inputs[i].Type;
                double rate = ProductionSystem.NominalInputPerSecond(s, d, level, t);
                if (sb.Length > 0) sb.Append("   ");
                sb.Append(PanelKit.Colored("-" + NumberFormat.Compact(rate) + "/s " + ResourceInfo.ShortName(t), UIKit.Warn));
            }
            return sb.ToString();
        }

        public static Sprite IconFor(BuildingDef d, out Color tint)
        {
            tint = Color.white;
            if (d.Outputs.Count > 0) return IconFactory.Resource(d.Outputs[0].Type);
            tint = Tinted(d.ColorA);
            switch (d.Role)
            {
                case BuildingRole.Market: tint = Color.white; return IconFactory.Resource(ResourceType.Coins);
                case BuildingRole.Storage: tint = Color.white; return IconFactory.Resource(ResourceType.Goods);
                case BuildingRole.Port:
                case BuildingRole.CommTower: return IconFactory.Menu(IconFactory.MenuTrade);
                case BuildingRole.ResearchCenter:
                case BuildingRole.Observatory: return IconFactory.Menu(IconFactory.MenuResearch);
                default: return IconFactory.Menu(IconFactory.MenuManage);
            }
        }

        static Color Tinted(uint rgb)
        {
            return CloudGarden.Islands.ColorUtil.Lighten(CloudGarden.Islands.ColorUtil.Hex(rgb), 0.25f);
        }
    }
}
