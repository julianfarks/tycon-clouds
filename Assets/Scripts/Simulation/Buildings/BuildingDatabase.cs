using System.Collections.Generic;
using CloudGarden.Economy;
using R = CloudGarden.Economy.ResourceType;

namespace CloudGarden.Buildings
{
    /// <summary>All building definitions (30 base buildings + 1 prestige machine).</summary>
    public static class BuildingDatabase
    {
        static readonly List<BuildingDef> all = new List<BuildingDef>();
        static readonly Dictionary<string, BuildingDef> byId = new Dictionary<string, BuildingDef>();

        public static IList<BuildingDef> All { get { return all; } }
        public static int Count { get { return all.Count; } }

        public static BuildingDef Get(string id)
        {
            BuildingDef d;
            return id != null && byId.TryGetValue(id, out d) ? d : null;
        }

        static BuildingDef Add(BuildingDef d)
        {
            all.Add(d);
            byId[d.Id] = d;
            return d;
        }

        /// <summary>Multiplier of (growth-1) for producer machines: owning many copies gets expensive quickly,
        /// which pushes the player to upgrade, expand and move on to the next tier.</summary>
        public const double ProducerGrowthScale = 2.5;

        static BuildingDatabase()
        {
            const BuildingCategory COL = BuildingCategory.Collection;
            const BuildingCategory PRO = BuildingCategory.Processing;
            const BuildingCategory AGR = BuildingCategory.Agriculture;
            const BuildingCategory IND = BuildingCategory.Industry;
            const BuildingCategory INF = BuildingCategory.Infrastructure;

            // ---------------- RECOLECCION ----------------
            Add(new BuildingDef("dew_collector", "Recolector de Rocío", COL, 2, 2, 15).Growth(1.10)
                .Desc("Atrapa gotas de rocío del aire. Tu primera maquina.")
                .Reg(0, "").Tr(0).Out(R.Dew, 1).Cycle(2).Vis("collector", 0x8FD3F4, 0xF4F8FF));

            Add(new BuildingDef("wind_turbine", "Turbina Eólica", COL, 2, 2, 650).Growth(1.12)
                .Desc("Capta corrientes de viento para producir viento utilizable.")
                .Reg(0, "aerodynamics").Tr(0).Out(R.Wind, 1).Cycle(2.5).Vis("turbine", 0xA8E6D8, 0xFFFFFF));

            Add(new BuildingDef("cloud_catcher", "Atrapanubes", COL, 2, 2, 2800).Growth(1.13)
                .Desc("Redes que condensan viento en nubes esponjosas.")
                .Reg(0, "cloud_catching").Tr(1).In(R.Wind, 2).Out(R.Clouds, 1).Cycle(4).Vis("cloudnet", 0xF4F8FF, 0xB8D8F8));

            Add(new BuildingDef("condenser", "Condensador", COL, 2, 2, 1200).Growth(1.12)
                .Desc("Convierte viento en grandes cantidades de rocío.")
                .Reg(0, "condensation").Tr(1).In(R.Wind, 1).Out(R.Dew, 6).Cycle(3).Vis("condenser", 0x6FB7E8, 0xD9ECFA));

            Add(new BuildingDef("crystal_extractor", "Extractor de Cristales", COL, 2, 2, 120000).Growth(1.15)
                .Desc("Perfora vetas de cristal celeste en la isla nevada.")
                .Reg(2, "crystal_mining").Tr(0).Mat(R.Water, 300).Out(R.Crystals, 1).Cycle(6).Vis("drill", 0x8B73F2, 0xC9BFFF));

            // ---------------- PROCESAMIENTO ----------------
            Add(new BuildingDef("distiller", "Destiladora", PRO, 2, 2, 140).Growth(1.12)
                .Desc("Destila rocío y lo convierte en agua pura.")
                .Reg(0, "distillation").Tr(2).In(R.Dew, 2).Out(R.Water, 1).Cycle(2.5).Vis("still", 0xD9893D, 0xF2C98A));

            Add(new BuildingDef("pollen_processor", "Procesadora de Polen", PRO, 2, 2, 9000).Growth(1.14)
                .Desc("Muele polen y agua para crear productos listos para vender.")
                .Reg(0, "pollen_processing").Tr(6).In(R.Pollen, 3).In(R.Water, 1).Out(R.Goods, 1).Cycle(5).Vis("mill", 0xF29BC8, 0xFFE3F0));

            Add(new BuildingDef("refinery", "Refinadora", PRO, 2, 2, 150000).Growth(1.14)
                .Desc("Refina cristales y agua en productos de alto valor.")
                .Reg(2, "refining").Tr(6).Mat(R.Water, 400).In(R.Crystals, 1).In(R.Water, 2).Out(R.Goods, 3).Cycle(5).Vis("refinery", 0x7C6CF2, 0xC9BFFF));

            Add(new BuildingDef("essence_lab", "Laboratorio de Esencia", PRO, 3, 3, 6000000).Growth(1.16)
                .Desc("Combina cristales, polen, nubes y energía para obtener esencia celestial.")
                .Reg(4, "essence_theory").Tr(7).Mat(R.Crystals, 500)
                .In(R.Crystals, 2).In(R.Pollen, 3).In(R.Clouds, 2).In(R.Energy, 1).Out(R.Essence, 1).Cycle(8).Vis("lab", 0xFFC857, 0xFFF0C4));

            Add(new BuildingDef("crystallizer", "Cristalizadora", PRO, 2, 2, 60000).Growth(1.14)
                .Desc("Congela agua y viento en hielo cristalino.")
                .Reg(2, "cryo").Tr(3).In(R.Water, 2).In(R.Wind, 1).Out(R.Ice, 1).Cycle(4).Vis("crystal", 0xBDEBFF, 0xFFFFFF));

            // ---------------- AGRICULTURA ----------------
            Add(new BuildingDef("greenhouse", "Invernadero", AGR, 3, 2, 480).Growth(1.12)
                .Desc("Cultiva flores con agua y produce polen.")
                .Reg(0, "botany").Tr(3).In(R.Water, 1).Out(R.Pollen, 2).Cycle(3.5).Vis("greenhouse", 0xBFF0E4, 0x7BD88F));

            Add(new BuildingDef("pollen_garden", "Jardín de Polen", AGR, 3, 3, 14000).Growth(1.13)
                .Desc("Un gran jardín que produce polen en abundancia.")
                .Reg(0, "pollen_gardens").Tr(3).In(R.Water, 3).Out(R.Pollen, 10).Cycle(5).Vis("garden", 0xF29BC8, 0x7BD88F));

            Add(new BuildingDef("sky_flowers", "Cultivo de Flores Celestiales", AGR, 3, 2, 36000).Growth(1.13)
                .Desc("Flores que florecen en el viento y regalan polen.")
                .Reg(1, "celestial_flora").Tr(3).In(R.Water, 2).In(R.Wind, 1).Out(R.Pollen, 12).Cycle(5).Vis("flowers", 0xB48CF2, 0xFFD6F0));

            Add(new BuildingDef("tropical_orchard", "Huerto Tropical", AGR, 3, 3, 22000).Growth(1.13)
                .Desc("Árboles frutales tropicales que dan fruta dulce.")
                .Reg(1, "tropical_farming").Tr(3).In(R.Water, 3).Out(R.Fruit, 5).Cycle(5).Vis("orchard", 0xFF7F50, 0x4FB36B));

            Add(new BuildingDef("glow_garden", "Jardín de Plantas Luminosas", AGR, 3, 2, 160000).Growth(1.14)
                .Desc("Plantas bioluminiscentes que cristalizan su luz.")
                .Reg(2, "bioluminescence").Tr(4).In(R.Water, 3).In(R.Pollen, 2).Out(R.Crystals, 2).Cycle(6).Vis("glow", 0x5BE3C8, 0x7C6CF2));

            // ---------------- INDUSTRIA ----------------
            Add(new BuildingDef("packer", "Empaquetadora", IND, 2, 2, 30000).Growth(1.14)
                .Desc("Empaqueta fruta y polen en productos.")
                .Reg(1, "packaging").Tr(6).In(R.Fruit, 2).In(R.Pollen, 1).Out(R.Goods, 2).Cycle(4).Vis("packer", 0xE48B4F, 0xFFD9B0));

            Add(new BuildingDef("goods_factory", "Fábrica de Productos", IND, 3, 3, 200000).Growth(1.15)
                .Desc("Línea de montaje que fabrica productos en masa.")
                .Reg(1, "mass_production").Tr(6).In(R.Water, 2).In(R.Pollen, 3).In(R.Clouds, 1).Out(R.Goods, 5).Cycle(6).Vis("factory", 0xE48B4F, 0xA8A8C8));

            Add(new BuildingDef("cloud_factory", "Fábrica de Nubes", IND, 3, 3, 380000).Growth(1.15)
                .Desc("Fábrica nubes a partir de viento, agua y hielo.")
                .Reg(2, "cloud_engineering").Tr(4).In(R.Wind, 2).In(R.Water, 1).In(R.Ice, 1).Out(R.Clouds, 8).Cycle(5).Vis("cloudfactory", 0xF4F8FF, 0x8FB4E8));

            Add(new BuildingDef("atmos_generator", "Generador Atmosférico", IND, 3, 3, 1200000).Growth(1.15)
                .Desc("Transforma viento y nubes en energía atmosférica.")
                .Reg(3, "atmospheric_energy").Tr(4).In(R.Wind, 2).In(R.Clouds, 1).Out(R.Energy, 4).Cycle(4).Vis("generator", 0xFFE04A, 0x6A7BA8));

            Add(new BuildingDef("weather_factory", "Fábrica Meteorológica", IND, 3, 3, 3500000).Growth(1.16)
                .Desc("Domina las tormentas para forjar cristales celestes.")
                .Reg(3, "meteorology").Tr(5).In(R.Energy, 2).In(R.Clouds, 2).In(R.Ice, 1).Out(R.Crystals, 8).Cycle(6).Vis("weather", 0x6A7BA8, 0xFFE04A));

            // ---------------- INFRAESTRUCTURA ----------------
            Add(new BuildingDef("market", "Mercado Flotante", INF, 2, 2, 90).Growth(1.3)
                .Desc("Vende automáticamente los recursos marcados. Cada nivel mejora el precio.")
                .Reg(0, "").Tr(8).AsRole(BuildingRole.Market, 0.06).Max(1).Vis("market", 0xFF8FA3, 0xFFF0C4));

            Add(new BuildingDef("warehouse", "Almacén", INF, 2, 2, 220).Growth(1.18)
                .Desc("Aumenta la capacidad de todos los recursos.")
                .Reg(0, "").Tr(8).AsRole(BuildingRole.Storage, 300).Vis("warehouse", 0xC98F5B, 0xE8C79A));

            Add(new BuildingDef("big_warehouse", "Gran Almacén", INF, 3, 3, 25000).Growth(1.22)
                .Desc("Enorme capacidad de almacenamiento para todos los recursos.")
                .Reg(0, "logistics").Tr(8).AsRole(BuildingRole.Storage, 4000).Vis("bigwarehouse", 0xB07A4A, 0xE8C79A));

            Add(new BuildingDef("air_port", "Puerto Aéreo", INF, 3, 3, 4500).Growth(1.2)
                .Desc("Atrae comerciantes con pedidos de entrega. Mas nivel, más pedidos.")
                .Reg(0, "aviation").Tr(8).AsRole(BuildingRole.Port, 1).Max(1).Vis("port", 0xFF8FA3, 0x8FD3F4));

            Add(new BuildingDef("observatory", "Observatorio", INF, 3, 3, 60000).Growth(1.2)
                .Desc("Estudia el cielo: +3% de producción global por nivel.")
                .Reg(1, "astronomy").Tr(8).AsRole(BuildingRole.Observatory, 0.03).Max(1).Vis("observatory", 0x6A7BA8, 0xFFE04A));

            Add(new BuildingDef("research_center", "Centro de Investigación", INF, 3, 3, 1800).Growth(1.2)
                .Desc("Reduce el coste de las investigaciones un 3% por nivel.")
                .Reg(0, "").Tr(8).AsRole(BuildingRole.ResearchCenter, 0.03).Max(1).Vis("research", 0x7C6CF2, 0xF4F8FF));

            Add(new BuildingDef("power_station", "Estación de Energía", INF, 3, 2, 1400000).Growth(1.15)
                .Desc("Convierte viento en energía atmosférica.")
                .Reg(3, "power_grid").Tr(1).In(R.Wind, 2).Out(R.Energy, 1).Cycle(4).Vis("powerstation", 0xFFE04A, 0x6A7BA8));

            Add(new BuildingDef("comm_tower", "Torre de Comunicaciones", INF, 2, 2, 400000).Growth(1.2)
                .Desc("Mejora las recompensas de comercio un 4% por nivel y atrae más pedidos.")
                .Reg(2, "communications").Tr(8).AsRole(BuildingRole.CommTower, 0.04).Max(1).Vis("tower", 0xFF8FA3, 0xF4F8FF));

            Add(new BuildingDef("climate_core", "Núcleo Climático", INF, 4, 4, 250000000).Growth(1.3)
                .Desc("El corazon del archipielago: +25% de producción global por nivel.")
                .Reg(4, "climate_core").Tr(8).Mat(R.Essence, 200).Mat(R.Crystals, 3000)
                .AsRole(BuildingRole.ClimateCore, 0.25).Max(1).Vis("core", 0xB48CF2, 0xFFE04A));

            Add(new BuildingDef("industrial_center", "Centro Industrial", INF, 4, 4, 40000000).Growth(1.25)
                .Desc("Acelera un 4% por nivel las fábricas y los procesadores.")
                .Reg(3, "industrial_automation").Tr(8).AsRole(BuildingRole.IndustrialCenter, 0.04).Max(1).Vis("industrialcenter", 0xA8A8C8, 0xE48B4F));

            // ---------------- MAQUINA ESPECIAL DE PRESTIGIO ----------------
            Add(new BuildingDef("golden_collector", "Colector Dorado", COL, 2, 2, 1000000).Growth(1.35)
                .Desc("Máquina especial del Renacimiento: acuna monedas del aire.")
                .Reg(0, "").Prestige(5).Tr(0).Out(R.Coins, 2000).Cycle(10).Vis("golden", 0xFFC857, 0xFFF0C4));

            for (int i = 0; i < all.Count; i++)
            {
                BuildingDef d = all[i];
                if (d.IsProducer) d.CostGrowth = 1.0 + (d.CostGrowth - 1.0) * ProducerGrowthScale;
            }
        }
    }
}
