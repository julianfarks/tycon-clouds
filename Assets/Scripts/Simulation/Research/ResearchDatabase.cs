using System.Collections.Generic;
using CloudGarden.Economy;
using R = CloudGarden.Economy.ResourceType;

namespace CloudGarden.Research
{
    /// <summary>Technology tree. Costs are paid with coins and sometimes materials.</summary>
    public static class ResearchDatabase
    {
        static readonly List<ResearchDef> all = new List<ResearchDef>();
        static readonly Dictionary<string, ResearchDef> byId = new Dictionary<string, ResearchDef>();

        public static IList<ResearchDef> All { get { return all; } }
        public static int Count { get { return all.Count; } }

        public static ResearchDef Get(string id)
        {
            ResearchDef d;
            return id != null && byId.TryGetValue(id, out d) ? d : null;
        }

        static ResearchDef Add(ResearchDef d)
        {
            all.Add(d);
            byId[d.Id] = d;
            return d;
        }

        static ResearchDatabase()
        {
            const ResearchKind U = ResearchKind.Unlock;

            // ---- Fase 1: rocío, agua y primeras máquinas ----
            Add(new ResearchDef("distillation", "Destilación", "Desbloquea la Destiladora: convierte rocío en agua.", 60, U, 0));
            Add(new ResearchDef("manual_harvest", "Cosecha manual", "+100% de rocío al recolectar tocando.", 150, ResearchKind.ClickBonus, 1.0));
            Add(new ResearchDef("botany", "Botánica", "Desbloquea el Invernadero: agua en polen.", 350, U, 0).Pre("distillation"));
            Add(new ResearchDef("aerodynamics", "Aerodinámica", "Desbloquea la Turbina Eólica.", 600, U, 0).Pre("distillation"));
            Add(new ResearchDef("efficient_tools", "Herramientas eficientes", "+10% de producción global.", 1500, ResearchKind.Production, 0.10).Pre("distillation"));
            Add(new ResearchDef("condensation", "Condensación", "Desbloquea el Condensador: viento en rocío.", 1800, U, 0).Pre("aerodynamics"));
            Add(new ResearchDef("market_trading", "Comercio básico", "+10% al precio de venta.", 2500, ResearchKind.SellBonus, 0.10).Pre("botany"));
            Add(new ResearchDef("cloud_catching", "Captura de nubes", "Desbloquea el Atrapanubes.", 3000, U, 0).Pre("aerodynamics"));
            Add(new ResearchDef("logistics", "Logística", "Desbloquea el Gran Almacén y +10% de almacenamiento.", 4000, ResearchKind.Storage, 0.10).Pre("botany"));
            Add(new ResearchDef("aviation", "Aviación", "Desbloquea el Puerto Aéreo y el comercio.", 8000, U, 0).Pre("aerodynamics", "botany"));
            Add(new ResearchDef("offline_1", "Descanso reparador", "Las máquinas trabajan hasta 4 h sin conexión.", 8000, ResearchKind.Offline, 4).Pre("botany"));
            Add(new ResearchDef("pollen_processing", "Procesado de polen", "Desbloquea la Procesadora de Polen.", 9000, U, 0).Pre("botany").Bld("research_center"));
            Add(new ResearchDef("pollen_gardens", "Jardines de polen", "Desbloquea el Jardín de Polen.", 12000, U, 0).Pre("botany"));

            // ---- Fase 2: segunda región ----
            Add(new ResearchDef("sky_navigation", "Navegación celeste", "Permite viajar a la Isla Tropical.", 60000, ResearchKind.RegionGate, 1)
                .Pre("aviation").Bld("air_port"));
            Add(new ResearchDef("tropical_farming", "Agricultura tropical", "Desbloquea el Huerto Tropical.", 20000, U, 0).Pre("sky_navigation"));
            Add(new ResearchDef("packaging", "Empaquetado", "Desbloquea la Empaquetadora.", 35000, U, 0).Pre("tropical_farming"));
            Add(new ResearchDef("celestial_flora", "Flora celestial", "Desbloquea las Flores Celestiales.", 40000, U, 0).Pre("sky_navigation"));
            Add(new ResearchDef("astronomy", "Astronomía", "Desbloquea el Observatorio.", 60000, U, 0).Pre("sky_navigation").Bld("research_center"));
            Add(new ResearchDef("efficient_2", "Ingeniería avanzada", "+15% de producción global.", 120000, ResearchKind.Production, 0.15)
                .Pre("efficient_tools").Bld("research_center"));
            Add(new ResearchDef("mass_production", "Producción en masa", "Desbloquea la Fábrica de Productos.", 150000, U, 0).Pre("packaging"));

            // ---- Fase 3: isla nevada ----
            Add(new ResearchDef("polar_expedition", "Expedición polar", "Permite viajar a la Isla Nevada.", 300000, ResearchKind.RegionGate, 2)
                .Pre("astronomy").Mat(R.Water, 200));
            Add(new ResearchDef("cryo", "Criogenia", "Desbloquea la Cristalizadora: hielo.", 350000, U, 0).Pre("polar_expedition"));
            Add(new ResearchDef("crystal_mining", "Minería de cristales", "Desbloquea el Extractor de Cristales.", 450000, U, 0).Pre("polar_expedition"));
            Add(new ResearchDef("communications", "Comunicaciones", "Desbloquea la Torre de Comunicaciones.", 500000, U, 0).Pre("polar_expedition"));
            Add(new ResearchDef("cloud_engineering", "Ingeniería de nubes", "Desbloquea la Fábrica de Nubes.", 700000, U, 0).Pre("cryo"));
            Add(new ResearchDef("refining", "Refinado", "Desbloquea la Refinadora.", 900000, U, 0).Pre("crystal_mining"));
            Add(new ResearchDef("bioluminescence", "Bioluminiscencia", "Desbloquea el Jardín de Plantas Luminosas.", 1100000, U, 0).Pre("crystal_mining"));
            Add(new ResearchDef("offline_2", "Sueño profundo", "Las máquinas trabajan hasta 6 h sin conexión.", 2000000, ResearchKind.Offline, 6).Pre("communications"));
            Add(new ResearchDef("build_discount_1", "Planificación urbana", "-10% al coste de construcción.", 3000000, ResearchKind.BuildDiscount, 0.10).Pre("refining"));

            // ---- Fase 4: isla tormentosa ----
            Add(new ResearchDef("storm_chasing", "Caza de tormentas", "Permite viajar a la Isla Tormentosa.", 5000000, ResearchKind.RegionGate, 3)
                .Pre("cloud_engineering").Bld("observatory").Mat(R.Ice, 300));
            Add(new ResearchDef("power_grid", "Red eléctrica", "Desbloquea la Estación de Energía.", 6000000, U, 0).Pre("storm_chasing"));
            Add(new ResearchDef("atmospheric_energy", "Energía atmosférica", "Desbloquea el Generador Atmosférico.", 10000000, U, 0).Pre("power_grid"));
            Add(new ResearchDef("trade_bonus", "Rutas comerciales", "+25% a las recompensas de pedidos.", 12000000, ResearchKind.TradeBonus, 0.25)
                .Pre("communications").Bld("comm_tower"));
            Add(new ResearchDef("meteorology", "Meteorología", "Desbloquea la Fábrica Meteorológica.", 18000000, U, 0).Pre("atmospheric_energy"));
            Add(new ResearchDef("storage_2", "Almacenaje avanzado", "+50% de almacenamiento.", 30000000, ResearchKind.Storage, 0.50).Pre("logistics", "meteorology"));
            Add(new ResearchDef("upgrade_discount_1", "Mantenimiento optimizado", "-10% al coste de mejoras.", 30000000, ResearchKind.UpgradeDiscount, 0.10).Pre("meteorology"));
            Add(new ResearchDef("industrial_automation", "Automatización industrial", "Desbloquea el Centro Industrial y +20% de producción.", 35000000, ResearchKind.Production, 0.20)
                .Pre("meteorology"));
            Add(new ResearchDef("efficient_3", "Producción masiva II", "+25% de producción global.", 60000000, ResearchKind.Production, 0.25)
                .Pre("industrial_automation").Bld("research_center"));
            Add(new ResearchDef("offline_3", "Hibernación industrial", "Las máquinas trabajan hasta 8 h sin conexión.", 80000000, ResearchKind.Offline, 8).Pre("offline_2", "industrial_automation"));

            // ---- Fase 5: isla celestial ----
            Add(new ResearchDef("celestial_ascension", "Ascensión celestial", "Permite viajar a la Isla Celestial.", 120000000, ResearchKind.RegionGate, 4)
                .Pre("industrial_automation").Mat(R.Crystals, 1000));
            Add(new ResearchDef("essence_theory", "Teoría de la esencia", "Desbloquea el Laboratorio de Esencia.", 200000000, U, 0).Pre("celestial_ascension"));
            Add(new ResearchDef("build_discount_2", "Arquitectura celestial", "-15% extra al coste de construcción.", 300000000, ResearchKind.BuildDiscount, 0.15).Pre("build_discount_1", "celestial_ascension"));
            Add(new ResearchDef("climate_core", "Núcleo climático", "Desbloquea el Núcleo Climático.", 400000000, U, 0).Pre("essence_theory").Mat(R.Essence, 100));
            Add(new ResearchDef("upgrade_discount_2", "Ingeniería celestial", "-15% extra al coste de mejoras.", 600000000, ResearchKind.UpgradeDiscount, 0.15).Pre("upgrade_discount_1", "essence_theory"));

            // ---- Investigaciones exclusivas del Renacimiento ----
            Add(new ResearchDef("star_harvest", "Cosecha estelar", "+30% de producción global (exclusiva).", 1000000, ResearchKind.Production, 0.30).Rebirth());
            Add(new ResearchDef("star_logistics", "Logística estelar", "+100% de almacenamiento (exclusiva).", 5000000, ResearchKind.Storage, 1.0).Rebirth().Pre("star_harvest"));
        }
    }
}
