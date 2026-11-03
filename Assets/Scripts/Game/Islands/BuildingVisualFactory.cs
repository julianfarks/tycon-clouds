using CloudGarden.Buildings;
using CloudGarden.Optimization;
using UnityEngine;

namespace CloudGarden.Islands
{
    /// <summary>
    /// Builds every building model procedurally from rounded shapes. Each building has a signature design
    /// with moving parts and three visual upgrade groups (levels 4, 7 and 10).
    /// </summary>
    public static class BuildingVisualFactory
    {
        public const string GroupLv1 = "Lv1";
        public const string GroupLv2 = "Lv2";
        public const string GroupLv3 = "Lv3";
        public const string GroupLv3Gold = "Lv3Gold";

        // ------------------------------------------------------------------ builder helper

        sealed class VB
        {
            public GameObject Root;
            public Transform Parent;
            public BuildingAnimator Anim;
            public float W;
            public float D;
            public float Top = 2f;
            public Color A, B, Dark, Light, Wood, Metal, Glass, Green;

            public GameObject Part(string name, Mesh mesh, Material mat, Vector3 pos, Vector3 scale, Transform parent)
            {
                GameObject go = new GameObject(name);
                go.transform.SetParent(parent != null ? parent : Parent, false);
                go.transform.localPosition = pos;
                go.transform.localScale = scale;
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                MeshRenderer r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = mat;
                r.shadowCastingMode = QualityManager.Shadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = QualityManager.Shadows;
                if (Anim.VisibilityRenderer == null) Anim.VisibilityRenderer = r;
                return go;
            }

            public GameObject Group(string name, Vector3 pos, Transform parent)
            {
                GameObject g = new GameObject(name);
                g.transform.SetParent(parent != null ? parent : Parent, false);
                g.transform.localPosition = pos;
                return g;
            }

            public GameObject Box(Vector3 pos, Vector3 size, Color c, Transform parent = null)
            {
                return BoxR(pos, size, c, 0.07f, parent);
            }

            public GameObject BoxR(Vector3 pos, Vector3 size, Color c, float radius, Transform parent = null)
            {
                return Part("box", MeshFactory.RoundedBox(size.x, size.y, size.z, radius), MaterialLibrary.Lit(c), pos, Vector3.one, parent);
            }

            public GameObject Cyl(Vector3 basePos, float radius, float height, Color c, Transform parent = null)
            {
                return Part("cyl", MeshFactory.Cylinder(), MaterialLibrary.Lit(c),
                    basePos + Vector3.up * (height * 0.5f), new Vector3(radius * 2f, height * 0.5f, radius * 2f), parent);
            }

            public GameObject Cone(Vector3 basePos, float radius, float height, Color c, int segments = 14, Transform parent = null)
            {
                return Part("cone", MeshFactory.Cone(radius, height, segments), MaterialLibrary.Lit(c), basePos, Vector3.one, parent);
            }

            public GameObject Ball(Vector3 center, float diameter, Color c, Transform parent = null)
            {
                return Part("ball", MeshFactory.Sphere(), MaterialLibrary.Lit(c, 0.4f), center, Vector3.one * diameter, parent);
            }

            public GameObject Ell(Vector3 center, Vector3 size, Color c, Transform parent = null)
            {
                return Part("ell", MeshFactory.Sphere(), MaterialLibrary.Lit(c, 0.35f), center, size, parent);
            }

            public GameObject Ring(Vector3 center, float ringRadius, float tube, Color c, Transform parent = null)
            {
                return Part("ring", MeshFactory.Torus(ringRadius, tube, 28, 8), MaterialLibrary.Lit(c, 0.5f), center, Vector3.one, parent);
            }

            public GameObject Glow(Vector3 center, float diameter, Color c, float intensity, Transform parent = null)
            {
                return Part("glow", MeshFactory.Sphere(), MaterialLibrary.Emissive(c, intensity), center, Vector3.one * diameter, parent);
            }

            public GameObject GlassBall(Vector3 center, float diameter, Color c, Transform parent = null)
            {
                return Part("glass", MeshFactory.Sphere(), MaterialLibrary.Glass(c), center, Vector3.one * diameter, parent);
            }

            public GameObject GlassBox(Vector3 pos, Vector3 size, Color c, Transform parent = null)
            {
                return Part("glassbox", MeshFactory.RoundedBox(size.x, size.y, size.z, 0.08f), MaterialLibrary.Glass(c), pos, Vector3.one, parent);
            }

            public GameObject Pipe(Vector3 a, Vector3 b, float radius, Color c, Transform parent = null)
            {
                Vector3 d = b - a;
                float len = d.magnitude;
                GameObject go = Part("pipe", MeshFactory.Cylinder(), MaterialLibrary.Lit(c, 0.5f),
                    (a + b) * 0.5f, new Vector3(radius * 2f, len * 0.5f, radius * 2f), parent);
                if (len > 0.0001f) go.transform.localRotation = Quaternion.FromToRotation(Vector3.up, d / len);
                return go;
            }

            // ---- animation helpers ----
            public GameObject Spin(GameObject g, Vector3 axis, float dps) { Anim.AddSpin(g.transform, axis, dps); return g; }
            public GameObject Bob(GameObject g, float amp, float freq, float phase) { Anim.AddBob(g.transform, amp, freq, phase); return g; }
            public GameObject Pulse(GameObject g, Color c, float lo, float hi, float freq)
            {
                Renderer r = g.GetComponent<Renderer>();
                if (r != null) Anim.AddPulse(r, c, lo, hi, freq, Random.value * 6f);
                return g;
            }
            public GameObject Orbit(GameObject g, Vector3 center, float radius, float dps, float phase, float bobAmp)
            {
                Anim.AddOrbit(g.transform, center, radius, dps, phase, bobAmp);
                return g;
            }
            public void Slide(GameObject g, Vector3 delta, float period, float offset) { Anim.AddSlide(g.transform, delta, period, offset); }

            public void Smoke(Vector3 pos, float size)
            {
                if (!QualityManager.Smoke) return;
                for (int i = 0; i < 3; i++)
                {
                    GameObject p = Part("smoke", MeshFactory.Sphere(), MaterialLibrary.Lit(new Color(0.92f, 0.92f, 0.95f), 0.1f), pos, Vector3.one * 0.01f, null);
                    Anim.AddPuff(p.transform, 1.1f, 2.4f, i / 3f, size);
                }
            }
        }

        // ------------------------------------------------------------------ public API

        public static GameObject Create(BuildingDef def)
        {
            GameObject root = new GameObject("Building_" + def.Id);
            VB b = new VB();
            b.Root = root;
            b.Parent = root.transform;
            b.Anim = root.AddComponent<BuildingAnimator>();
            b.W = def.Width;
            b.D = def.Depth;
            b.A = ColorUtil.Hex(def.ColorA);
            b.B = ColorUtil.Hex(def.ColorB);
            b.Dark = ColorUtil.Darken(b.A, 0.35f);
            b.Light = ColorUtil.Lighten(b.B, 0.45f);
            b.Wood = ColorUtil.Hex(0xB07A4A);
            b.Metal = ColorUtil.Hex(0x9AA3B5);
            b.Glass = new Color(0.75f, 0.92f, 1f, 0.38f);
            b.Green = ColorUtil.Hex(0x58C47A);

            // Rounded slab shared by every building.
            b.BoxR(new Vector3(0, 0.1f, 0), new Vector3(b.W - 0.08f, 0.2f, b.D - 0.08f),
                Color.Lerp(b.B, new Color(0.88f, 0.84f, 0.74f), 0.72f), 0.1f);

            BuildKind(def, b);

            AddLevelGroups(b, def);

            BoxCollider col = root.AddComponent<BoxCollider>();
            col.size = new Vector3(b.W, Mathf.Max(1.4f, b.Top), b.D);
            col.center = new Vector3(0, Mathf.Max(1.4f, b.Top) * 0.5f, 0);
            return root;
        }

        /// <summary>Approximate top of the model (for status icons).</summary>
        public static float HeightOf(GameObject root)
        {
            BoxCollider c = root.GetComponent<BoxCollider>();
            return c != null ? c.size.y : 2f;
        }

        // ------------------------------------------------------------------ designs

        static void BuildKind(BuildingDef def, VB v)
        {
            float hw = v.W * 0.5f;
            float hd = v.D * 0.5f;
            switch (def.Visual)
            {
                case "collector": Collector(v); break;
                case "turbine": Turbine(v); break;
                case "cloudnet": CloudNet(v); break;
                case "condenser": Condenser(v); break;
                case "drill": Drill(v); break;
                case "still": Still(v); break;
                case "mill": Mill(v); break;
                case "refinery": Refinery(v); break;
                case "lab": Lab(v); break;
                case "crystal": CrystalTower(v); break;
                case "greenhouse": Greenhouse(v); break;
                case "garden": Garden(v, false); break;
                case "flowers": Garden(v, true); break;
                case "orchard": Orchard(v); break;
                case "glow": GlowGarden(v); break;
                case "packer": Packer(v); break;
                case "factory": Factory(v); break;
                case "cloudfactory": CloudFactory(v); break;
                case "generator": Generator(v); break;
                case "weather": Weather(v); break;
                case "market": Market(v); break;
                case "warehouse": Warehouse(v, false); break;
                case "bigwarehouse": Warehouse(v, true); break;
                case "port": Port(v); break;
                case "observatory": Observatory(v); break;
                case "research": ResearchLab(v); break;
                case "powerstation": PowerStation(v); break;
                case "tower": Tower(v); break;
                case "core": ClimateCoreModel(v); break;
                case "industrialcenter": IndustrialCenter(v); break;
                case "golden": Golden(v); break;
                default:
                    v.Box(new Vector3(0, 0.7f, 0), new Vector3(hw * 1.4f, 1.0f, hd * 1.4f), v.A);
                    v.Top = 1.4f;
                    break;
            }
        }

        static void Collector(VB v)
        {
            v.Cyl(new Vector3(0, 0.2f, 0), 0.34f, 0.5f, v.Dark);
            GameObject jar = v.GlassBall(new Vector3(0, 1.0f, 0), 0.62f, new Color(0.7f, 0.9f, 1f, 0.45f));
            GameObject drop = v.Glow(new Vector3(0, 1.0f, 0), 0.28f, new Color(0.35f, 0.7f, 1f), 2f);
            v.Bob(drop, 0.05f, 2.2f, 0f);
            v.Pulse(drop, new Color(0.35f, 0.7f, 1f), 1.2f, 2.6f, 2f);
            GameObject funnel = v.Cone(new Vector3(0, 1.95f, 0), 0.72f, 0.62f, v.A, 18);
            funnel.transform.localRotation = Quaternion.Euler(180f, 0f, 0f);
            v.Spin(funnel, Vector3.up, 22f);
            v.Ring(new Vector3(0, 1.95f, 0), 0.72f, 0.05f, v.B);
            for (int i = 0; i < 4; i++)
            {
                GameObject d = v.Glow(Vector3.zero, 0.15f, new Color(0.6f, 0.85f, 1f), 1.5f);
                v.Orbit(d, new Vector3(0, 1.55f, 0), 0.92f, 40f, i * 90f, 0.12f);
            }
            v.Top = 2.2f;
        }

        static void Turbine(VB v)
        {
            v.Ball(new Vector3(0, 0.32f, 0), 0.6f, v.Dark);
            v.Cyl(new Vector3(0, 0.3f, 0), 0.17f, 1.65f, v.B);
            v.BoxR(new Vector3(0, 2.0f, 0.05f), new Vector3(0.34f, 0.34f, 0.6f), v.A, 0.12f);
            GameObject hub = v.Group("hub", new Vector3(0, 2.0f, 0.42f), null);
            v.Spin(hub, Vector3.forward, 150f);
            for (int i = 0; i < 3; i++)
            {
                GameObject pivot = v.Group("blade", Vector3.zero, hub.transform);
                pivot.transform.localRotation = Quaternion.Euler(0, 0, i * 120f);
                v.BoxR(new Vector3(0, 0.55f, 0), new Vector3(0.16f, 1.0f, 0.05f), Color.white, 0.04f, pivot.transform);
            }
            v.Ball(new Vector3(0, 0, 0.0f), 0.26f, v.A, hub.transform);
            v.Top = 3.1f;
        }

        static void CloudNet(VB v)
        {
            float x = v.W * 0.5f - 0.3f;
            float z = v.D * 0.5f - 0.3f;
            for (int i = 0; i < 4; i++)
            {
                float sx = (i & 1) == 0 ? -x : x;
                float sz = (i & 2) == 0 ? -z : z;
                v.Cyl(new Vector3(sx, 0.2f, sz), 0.06f, 1.45f, v.Wood);
            }
            v.Ring(new Vector3(0, 1.62f, 0), 0.85f, 0.05f, v.Wood);
            v.GlassBox(new Vector3(0, 1.1f, 0), new Vector3(1.35f, 0.03f, 1.35f), new Color(0.85f, 0.93f, 1f, 0.35f));
            Color white = new Color(0.98f, 0.99f, 1f);
            v.Bob(v.Ball(new Vector3(-0.2f, 1.45f, 0.05f), 0.62f, white), 0.05f, 1.4f, 0f);
            v.Bob(v.Ball(new Vector3(0.22f, 1.5f, -0.1f), 0.5f, white), 0.06f, 1.7f, 1.7f);
            v.Bob(v.Ball(new Vector3(0.0f, 1.62f, 0.2f), 0.42f, white), 0.05f, 1.2f, 3f);
            v.Top = 2.0f;
        }

        static void Condenser(VB v)
        {
            v.Cyl(new Vector3(-0.42f, 0.2f, -0.1f), 0.38f, 1.15f, v.A);
            v.Ell(new Vector3(-0.42f, 1.35f, -0.1f), new Vector3(0.76f, 0.34f, 0.76f), v.Light);
            v.Ring(new Vector3(-0.42f, 0.85f, -0.1f), 0.38f, 0.04f, v.Metal);
            v.Cyl(new Vector3(0.5f, 0.2f, 0.15f), 0.3f, 0.9f, v.B);
            for (int i = 0; i < 3; i++) v.Ring(new Vector3(0.5f, 0.5f + i * 0.18f, 0.15f), 0.31f, 0.04f, v.Metal);
            v.Pipe(new Vector3(-0.1f, 0.75f, -0.05f), new Vector3(0.3f, 0.75f, 0.1f), 0.07f, v.Metal);
            GameObject drop = v.Glow(new Vector3(0.5f, 1.3f, 0.15f), 0.3f, new Color(0.4f, 0.75f, 1f), 2f);
            v.Pulse(drop, new Color(0.4f, 0.75f, 1f), 1f, 3f, 2.4f);
            v.Bob(drop, 0.05f, 2f, 0f);
            v.Top = 1.7f;
        }

        static void Drill(VB v)
        {
            v.Ell(new Vector3(0, 0.35f, 0), new Vector3(1.6f, 0.85f, 1.5f), ColorUtil.Hex(0x9A92B5));
            Color crystal = ColorUtil.Hex(0x8B73F2);
            for (int i = 0; i < 5; i++)
            {
                float ang = i * 72f * Mathf.Deg2Rad;
                GameObject c = v.Cone(new Vector3(Mathf.Cos(ang) * 0.5f, 0.55f, Mathf.Sin(ang) * 0.5f - 0.1f), 0.13f, 0.55f + (i % 2) * 0.25f, crystal, 6);
                c.transform.localRotation = Quaternion.Euler(Mathf.Sin(ang) * 18f, 0, -Mathf.Cos(ang) * 18f);
                v.Pulse(c, crystal, 0.4f, 1.2f, 1.6f);
            }
            for (int i = 0; i < 3; i++)
            {
                float ang = (i * 120f + 30f) * Mathf.Deg2Rad;
                v.Pipe(new Vector3(Mathf.Cos(ang) * 0.55f, 0.35f, Mathf.Sin(ang) * 0.55f), new Vector3(0, 1.85f, 0), 0.05f, v.Wood);
            }
            GameObject bit = v.Group("bit", new Vector3(0, 1.55f, 0), null);
            v.Spin(bit, Vector3.up, 420f);
            GameObject tip = v.Cone(new Vector3(0, 0.55f, 0), 0.13f, 0.55f, v.Metal, 8, bit.transform);
            tip.transform.localRotation = Quaternion.Euler(180f, 0, 0);
            v.Cyl(new Vector3(0, 0.45f, 0), 0.05f, 0.5f, v.Metal, bit.transform);
            v.Top = 2.1f;
        }

        static void Still(VB v)
        {
            GameObject fire = v.Glow(new Vector3(0, 0.35f, 0), 0.5f, new Color(1f, 0.55f, 0.15f), 2.2f);
            v.Pulse(fire, new Color(1f, 0.55f, 0.15f), 1.2f, 3f, 5f);
            v.Ball(new Vector3(0, 1.0f, 0), 1.15f, v.A);
            v.Ring(new Vector3(0, 0.75f, 0), 0.5f, 0.04f, v.Dark);
            v.Cyl(new Vector3(0, 1.45f, 0), 0.2f, 0.38f, v.A);
            v.Ball(new Vector3(0, 1.95f, 0), 0.46f, v.A);
            v.Pipe(new Vector3(0.05f, 1.95f, 0), new Vector3(0.65f, 1.65f, 0), 0.06f, v.Dark);
            v.Pipe(new Vector3(0.65f, 1.65f, 0), new Vector3(0.65f, 0.65f, 0), 0.06f, v.Dark);
            v.Cyl(new Vector3(0.66f, 0.2f, 0), 0.28f, 0.5f, v.Wood);
            v.Ring(new Vector3(0.66f, 0.45f, 0), 0.28f, 0.03f, v.Metal);
            GameObject drip = v.Glow(new Vector3(0.65f, 0.9f, 0), 0.12f, new Color(0.4f, 0.75f, 1f), 1.6f);
            v.Bob(drip, 0.12f, 3f, 0f);
            v.Smoke(new Vector3(-0.35f, 1.8f, 0.2f), 0.3f);
            v.Top = 2.3f;
        }

        static void Mill(VB v)
        {
            v.Box(new Vector3(-0.1f, 0.8f, 0), new Vector3(1.3f, 1.2f, 1.1f), v.A);
            GameObject roof = v.Cone(new Vector3(-0.1f, 1.38f, 0), 1.0f, 0.65f, v.B, 4);
            roof.transform.localRotation = Quaternion.Euler(0, 45f, 0);
            v.Cyl(new Vector3(-0.5f, 1.4f, -0.2f), 0.1f, 0.6f, v.Dark);
            GameObject wheel = v.Group("wheel", new Vector3(0.78f, 0.95f, 0), null);
            v.Spin(wheel, Vector3.right, 70f);
            GameObject rim = v.Ring(Vector3.zero, 0.55f, 0.05f, v.Wood, wheel.transform);
            rim.transform.localRotation = Quaternion.Euler(0, 0, 90f);
            for (int i = 0; i < 4; i++)
            {
                GameObject s = v.BoxR(Vector3.zero, new Vector3(0.05f, 1.1f, 0.05f), v.Wood, 0.02f, wheel.transform);
                s.transform.localRotation = Quaternion.Euler(i * 45f, 0, 0);
            }
            v.Box(new Vector3(-0.1f, 0.5f, 0.57f), new Vector3(0.4f, 0.6f, 0.06f), v.Dark);
            v.Smoke(new Vector3(-0.5f, 1.95f, -0.2f), 0.25f);
            v.Top = 2.2f;
        }

        static void Refinery(VB v)
        {
            v.Cyl(new Vector3(-0.45f, 0.2f, -0.3f), 0.36f, 1.3f, v.A);
            v.Cyl(new Vector3(0.42f, 0.2f, -0.35f), 0.3f, 1.0f, v.B);
            v.Cyl(new Vector3(0.1f, 0.2f, 0.4f), 0.32f, 0.8f, v.A);
            v.Ell(new Vector3(-0.45f, 1.5f, -0.3f), new Vector3(0.72f, 0.3f, 0.72f), v.Light);
            v.Pipe(new Vector3(-0.1f, 0.9f, -0.3f), new Vector3(0.15f, 0.9f, -0.3f), 0.06f, v.Metal);
            v.Pipe(new Vector3(0.4f, 0.7f, -0.1f), new Vector3(0.2f, 0.7f, 0.2f), 0.06f, v.Metal);
            v.Cyl(new Vector3(-0.6f, 0.2f, 0.45f), 0.12f, 1.9f, v.Metal);
            GameObject dome = v.Glow(new Vector3(0.42f, 1.3f, -0.35f), 0.42f, ColorUtil.Hex(0x9B86FF), 2f);
            v.Pulse(dome, ColorUtil.Hex(0x9B86FF), 1f, 3f, 2f);
            v.Smoke(new Vector3(-0.6f, 2.15f, 0.45f), 0.34f);
            v.Top = 2.4f;
        }

        static void Lab(VB v)
        {
            v.Cyl(new Vector3(0, 0.2f, 0), 1.25f, 0.3f, v.Light);
            v.GlassBall(new Vector3(0, 1.3f, 0), 1.95f, new Color(0.85f, 0.95f, 1f, 0.32f));
            GameObject core = v.Glow(new Vector3(0, 1.2f, 0), 0.62f, ColorUtil.Hex(0xFFC857), 2.4f);
            v.Pulse(core, ColorUtil.Hex(0xFFC857), 1.2f, 3.4f, 2.2f);
            v.Bob(core, 0.08f, 1.6f, 0f);
            GameObject ringPivot = v.Group("ring", new Vector3(0, 1.2f, 0), null);
            ringPivot.transform.localRotation = Quaternion.Euler(22f, 0, 0);
            v.Ring(Vector3.zero, 1.05f, 0.05f, v.A, ringPivot.transform);
            v.Spin(ringPivot, Vector3.up, 45f);
            for (int s = -1; s <= 1; s += 2)
            {
                v.Box(new Vector3(s * 1.1f, 0.65f, 0.0f), new Vector3(0.7f, 0.75f, 1.2f), v.B);
                GameObject beaker = v.Cyl(new Vector3(s * 1.1f, 1.02f, 0f), 0.14f, 0.3f, new Color(0.6f, 0.9f, 1f, 1f));
                v.Bob(beaker, 0.03f, 2f, s);
                v.Glow(new Vector3(s * 1.1f, 1.45f, 0f), 0.16f, ColorUtil.Hex(0x7CF0C8), 1.8f);
            }
            v.Top = 2.5f;
        }

        static void CrystalTower(VB v)
        {
            v.Cyl(new Vector3(0, 0.2f, 0), 0.62f, 0.25f, v.B);
            GameObject cluster = v.Group("cluster", new Vector3(0, 0.45f, 0), null);
            v.Spin(cluster, Vector3.up, 14f);
            Color ice = v.A;
            v.Cone(Vector3.zero, 0.32f, 1.5f, ice, 6, cluster.transform);
            for (int i = 0; i < 5; i++)
            {
                float ang = i * 72f * Mathf.Deg2Rad;
                GameObject c = v.Cone(new Vector3(Mathf.Cos(ang) * 0.42f, 0, Mathf.Sin(ang) * 0.42f), 0.14f, 0.65f + (i % 2) * 0.2f, ColorUtil.Lighten(ice, 0.2f), 6, cluster.transform);
                c.transform.localRotation = Quaternion.Euler(Mathf.Sin(ang) * 14f, 0, -Mathf.Cos(ang) * 14f);
            }
            for (int i = 0; i < 3; i++)
            {
                GameObject s = v.Glow(Vector3.zero, 0.1f, Color.white, 2f);
                v.Orbit(s, new Vector3(0, 1.0f, 0), 0.75f, 50f, i * 120f, 0.2f);
            }
            v.Top = 2.1f;
        }

        static void Greenhouse(VB v)
        {
            float w = v.W - 0.4f;
            float d = v.D - 0.4f;
            v.Box(new Vector3(0, 0.33f, 0), new Vector3(w, 0.26f, d), v.Wood);
            GameObject roof = v.Part("roof", MeshFactory.Cylinder(), MaterialLibrary.Glass(v.Glass),
                new Vector3(0, 0.62f, 0), new Vector3(d, w * 0.5f, d), null);
            roof.transform.localRotation = Quaternion.Euler(0, 0, 90f);
            v.GlassBox(new Vector3(0, 0.78f, 0), new Vector3(w, 0.5f, d), v.Glass);
            for (int i = 0; i < 4; i++)
            {
                float x = -w * 0.5f + 0.25f + i * (w - 0.5f) / 3f;
                v.Ball(new Vector3(x, 0.62f, -0.25f), 0.34f, v.Green);
                v.Ball(new Vector3(x, 0.62f, 0.25f), 0.34f, v.Green);
                GameObject f = v.Ball(new Vector3(x, 0.84f, (i % 2 == 0) ? -0.25f : 0.25f), 0.15f, ColorUtil.Hex(0xFF8FB8));
                v.Bob(f, 0.02f, 2f, i);
            }
            for (int i = 0; i < 3; i++)
                v.BoxR(new Vector3(-w * 0.5f + i * w * 0.5f, 0.72f, 0), new Vector3(0.05f, 0.7f, d), v.Wood, 0.02f);
            v.Top = 1.6f;
        }

        static void Garden(VB v, bool tall)
        {
            float w = v.W - 0.45f;
            float d = v.D - 0.45f;
            v.Box(new Vector3(0, 0.3f, 0), new Vector3(w, 0.2f, d), ColorUtil.Hex(0x8A5A3C));
            Color[] petals =
            {
                ColorUtil.Hex(0xFF8FB8), ColorUtil.Hex(0xFFD35A), ColorUtil.Hex(0xFFFFFF), ColorUtil.Hex(0xB48CF2), ColorUtil.Hex(0xFF9A6B)
            };
            int nx = tall ? 3 : 4;
            int nz = tall ? 2 : 4;
            for (int i = 0; i < nx; i++)
            {
                for (int j = 0; j < nz; j++)
                {
                    float x = (i + 0.5f) / nx * (w - 0.3f) - (w - 0.3f) * 0.5f;
                    float z = (j + 0.5f) / nz * (d - 0.3f) - (d - 0.3f) * 0.5f;
                    float h = tall ? 0.9f + ((i + j) % 2) * 0.35f : 0.35f;
                    Color pc = tall ? (j % 2 == 0 ? ColorUtil.Hex(0xB48CF2) : ColorUtil.Hex(0xFF8FD0)) : petals[(i * 3 + j) % petals.Length];
                    GameObject stem = v.Cyl(new Vector3(x, 0.38f, z), tall ? 0.04f : 0.025f, h, v.Green);
                    float headSize = tall ? 0.5f : 0.22f;
                    GameObject head = tall
                        ? v.Ell(new Vector3(x, 0.38f + h + 0.04f, z), new Vector3(headSize, headSize * 0.45f, headSize), pc)
                        : v.Ball(new Vector3(x, 0.38f + h + 0.05f, z), headSize, pc);
                    v.Bob(head, 0.025f, 2f + (i + j) * 0.2f, i + j * 1.3f);
                    if (tall) v.Ball(new Vector3(x, 0.38f + h + 0.1f, z), 0.2f, ColorUtil.Hex(0xFFD35A));
                    stem.name = "stem";
                }
            }
            if (tall)
            {
                GameObject swirl = v.Ring(new Vector3(0, 1.45f, 0), 0.8f, 0.04f, new Color(0.85f, 0.95f, 1f));
                v.Spin(swirl, Vector3.up, 60f);
            }
            else
            {
                for (int i = 0; i < 2; i++)
                {
                    GameObject bee = v.Ball(Vector3.zero, 0.12f, ColorUtil.Hex(0xFFD35A));
                    v.Orbit(bee, new Vector3(0, 0.95f, 0), 0.9f, 80f, i * 180f, 0.1f);
                }
            }
            v.Top = tall ? 1.8f : 1.2f;
        }

        static void Orchard(VB v)
        {
            v.BoxR(new Vector3(0, 0.26f, 0), new Vector3(v.W - 0.3f, 0.12f, v.D - 0.3f), ColorUtil.Hex(0x6BCB77), 0.05f);
            Vector3[] spots = { new Vector3(-0.75f, 0, -0.55f), new Vector3(0.7f, 0, -0.35f), new Vector3(-0.1f, 0, 0.65f) };
            for (int i = 0; i < spots.Length; i++)
            {
                Vector3 p = spots[i];
                v.Cyl(new Vector3(p.x, 0.3f, p.z), 0.1f, 0.65f, v.Wood);
                GameObject crown = v.Ball(new Vector3(p.x, 1.1f, p.z), 1.0f, v.Green);
                v.Bob(crown, 0.02f, 1.3f, i);
                for (int f = 0; f < 5; f++)
                {
                    float ang = (f * 72f + i * 20f) * Mathf.Deg2Rad;
                    v.Ball(new Vector3(p.x + Mathf.Cos(ang) * 0.42f, 1.0f + (f % 2) * 0.25f, p.z + Mathf.Sin(ang) * 0.42f), 0.15f, v.A);
                }
            }
            v.Box(new Vector3(1.0f, 0.42f, 0.8f), new Vector3(0.4f, 0.22f, 0.3f), v.Wood);
            v.Ball(new Vector3(1.0f, 0.58f, 0.8f), 0.14f, v.A);
            v.Top = 1.8f;
        }

        static void GlowGarden(VB v)
        {
            v.Box(new Vector3(0, 0.3f, 0), new Vector3(v.W - 0.4f, 0.2f, v.D - 0.4f), ColorUtil.Hex(0x4A3B63));
            Color[] caps = { ColorUtil.Hex(0x5BE3C8), ColorUtil.Hex(0x9B86FF), ColorUtil.Hex(0xFF8FD0) };
            for (int i = 0; i < 7; i++)
            {
                float x = -v.W * 0.5f + 0.5f + (i % 4) * ((v.W - 1f) / 3f);
                float z = (i < 4 ? -0.3f : 0.35f);
                float h = 0.4f + (i % 3) * 0.22f;
                v.Cyl(new Vector3(x, 0.38f, z), 0.06f, h, ColorUtil.Hex(0xE8F6F0));
                Color cc = caps[i % 3];
                GameObject cap = v.Part("cap", MeshFactory.Sphere(), MaterialLibrary.Emissive(cc, 1.6f),
                    new Vector3(x, 0.4f + h, z), new Vector3(0.5f, 0.26f, 0.5f), null);
                v.Pulse(cap, cc, 0.6f, 2.2f, 1.5f + i * 0.2f);
            }
            for (int i = 0; i < 4; i++)
            {
                GameObject s = v.Glow(Vector3.zero, 0.07f, ColorUtil.Hex(0xBFFFE8), 2f);
                v.Orbit(s, new Vector3(0, 1.1f, 0), 0.5f + i * 0.2f, 30f + i * 10f, i * 90f, 0.25f);
            }
            v.Top = 1.6f;
        }

        static void Packer(VB v)
        {
            v.Box(new Vector3(-0.35f, 0.7f, 0), new Vector3(0.85f, 1.0f, 1.2f), v.A);
            GameObject press = v.Box(new Vector3(-0.35f, 1.4f, 0), new Vector3(0.5f, 0.35f, 0.5f), v.Dark);
            v.Bob(press, 0.12f, 3f, 0f);
            v.Box(new Vector3(0.35f, 0.3f, 0), new Vector3(0.7f, 0.12f, 1.55f), ColorUtil.Hex(0x4A4F63));
            for (int i = 0; i < 3; i++)
            {
                GameObject crate = v.Box(new Vector3(0.35f, 0.5f, -0.65f), new Vector3(0.36f, 0.3f, 0.36f), v.Wood);
                v.Slide(crate, new Vector3(0, 0, 1.3f), 4f, i / 3f);
            }
            v.Cone(new Vector3(-0.35f, 1.62f, 0), 0.3f, 0.35f, v.B, 10);
            v.Top = 2.0f;
        }

        static void Factory(VB v)
        {
            float w = v.W - 0.5f;
            v.Box(new Vector3(0, 0.95f, -0.25f), new Vector3(w, 1.45f, v.D - 1.0f), v.A);
            for (int i = 0; i < 3; i++)
            {
                GameObject saw = v.Box(new Vector3(-0.8f + i * 0.8f, 1.8f, -0.25f), new Vector3(0.7f, 0.35f, v.D - 1.1f), v.B);
                saw.transform.localRotation = Quaternion.Euler(0, 0, -14f);
            }
            v.Cyl(new Vector3(-0.9f, 1.6f, -0.7f), 0.16f, 1.2f, v.Metal);
            v.Cyl(new Vector3(0.9f, 1.6f, -0.7f), 0.14f, 1.0f, v.Metal);
            v.Smoke(new Vector3(-0.9f, 2.85f, -0.7f), 0.4f);
            v.Smoke(new Vector3(0.9f, 2.65f, -0.7f), 0.34f);
            GameObject gear = v.Group("gear", new Vector3(0.6f, 1.0f, 0.78f), null);
            v.Spin(gear, Vector3.forward, 60f);
            GameObject gr = v.Ring(Vector3.zero, 0.42f, 0.07f, v.Dark, gear.transform);
            gr.transform.localRotation = Quaternion.Euler(90f, 0, 0);
            for (int i = 0; i < 8; i++)
            {
                GameObject tooth = v.BoxR(Vector3.zero, new Vector3(0.1f, 0.14f, 0.1f), v.Dark, 0.02f, gear.transform);
                float ang = i * 45f * Mathf.Deg2Rad;
                tooth.transform.localPosition = new Vector3(Mathf.Cos(ang) * 0.5f, Mathf.Sin(ang) * 0.5f, 0);
            }
            v.Box(new Vector3(-0.6f, 0.55f, 0.64f), new Vector3(0.7f, 0.7f, 0.06f), v.Dark);
            v.GlassBox(new Vector3(0.0f, 1.2f, 0.69f), new Vector3(0.6f, 0.35f, 0.03f), new Color(0.8f, 0.95f, 1f, 0.6f));
            v.Top = 2.9f;
        }

        static void CloudFactory(VB v)
        {
            v.Box(new Vector3(0, 0.7f, 0), new Vector3(v.W - 0.7f, 1.0f, v.D - 0.7f), v.B);
            v.Pipe(new Vector3(-0.7f, 1.0f, -0.7f), new Vector3(-0.35f, 1.9f, -0.2f), 0.09f, v.Metal);
            v.Pipe(new Vector3(0.7f, 1.0f, -0.7f), new Vector3(0.35f, 1.9f, -0.2f), 0.09f, v.Metal);
            v.Pipe(new Vector3(0.0f, 1.2f, 0.7f), new Vector3(0.0f, 1.9f, 0.2f), 0.09f, v.Metal);
            v.Cone(new Vector3(0, 1.2f, 0), 0.6f, 0.6f, v.Dark, 14);
            Color white = new Color(0.98f, 0.99f, 1f);
            for (int i = 0; i < 6; i++)
            {
                float ang = i * 60f * Mathf.Deg2Rad;
                GameObject c = v.Ball(new Vector3(Mathf.Cos(ang) * 0.55f, 2.1f + (i % 2) * 0.2f, Mathf.Sin(ang) * 0.55f), 0.8f - (i % 3) * 0.1f, white);
                v.Bob(c, 0.07f, 1.2f + i * 0.15f, i);
            }
            v.Bob(v.Ball(new Vector3(0, 2.45f, 0), 1.0f, white), 0.06f, 1.0f, 0f);
            v.Top = 3.2f;
        }

        static void Generator(VB v)
        {
            v.Cyl(new Vector3(0, 0.2f, 0), 1.0f, 0.22f, v.Metal);
            v.Ball(new Vector3(0, 1.35f, 0), 1.1f, v.Metal);
            GameObject core = v.Glow(new Vector3(0, 1.35f, 0), 0.6f, ColorUtil.Hex(0xFFE04A), 2.5f);
            v.Pulse(core, ColorUtil.Hex(0xFFE04A), 1.5f, 3.5f, 3f);
            for (int i = 0; i < 3; i++)
            {
                GameObject pivot = v.Group("ringpivot", new Vector3(0, 1.35f, 0), null);
                pivot.transform.localRotation = Quaternion.Euler(i * 60f, i * 40f, 0);
                v.Ring(Vector3.zero, 0.85f, 0.05f, i == 1 ? v.B : v.A, pivot.transform);
                v.Spin(pivot, i == 0 ? Vector3.up : (i == 1 ? Vector3.right : Vector3.forward), 55f + i * 30f);
            }
            for (int i = 0; i < 4; i++)
            {
                float x = (i & 1) == 0 ? -1.1f : 1.1f;
                float z = (i & 2) == 0 ? -1.1f : 1.1f;
                v.Cyl(new Vector3(x, 0.2f, z), 0.1f, 0.9f, v.Dark);
                GameObject tip = v.Glow(new Vector3(x, 1.2f, z), 0.22f, ColorUtil.Hex(0xFFE04A), 2f);
                v.Pulse(tip, ColorUtil.Hex(0xFFE04A), 0.8f, 2.6f, 4f + i);
            }
            v.Top = 2.4f;
        }

        static void Weather(VB v)
        {
            v.Box(new Vector3(0, 0.55f, 0), new Vector3(v.W - 0.8f, 0.7f, v.D - 0.8f), v.A);
            v.Cyl(new Vector3(0, 0.9f, 0), 0.3f, 1.4f, v.Metal);
            Color storm = ColorUtil.Hex(0x59638C);
            v.Bob(v.Ball(new Vector3(-0.35f, 2.65f, 0), 1.0f, storm), 0.06f, 1.1f, 0f);
            v.Bob(v.Ball(new Vector3(0.35f, 2.7f, 0.1f), 0.9f, ColorUtil.Darken(storm, 0.2f)), 0.06f, 1.4f, 1.5f);
            v.Bob(v.Ball(new Vector3(0, 2.95f, -0.1f), 0.8f, ColorUtil.Lighten(storm, 0.1f)), 0.05f, 1.0f, 3f);
            GameObject bolt = v.Part("bolt", MeshFactory.RoundedBox(0.09f, 0.9f, 0.09f, 0.03f), MaterialLibrary.Emissive(ColorUtil.Hex(0xFFE04A), 3f),
                new Vector3(0.1f, 2.0f, 0.15f), Vector3.one, null);
            bolt.transform.localRotation = Quaternion.Euler(0, 0, 18f);
            v.Pulse(bolt, ColorUtil.Hex(0xFFE04A), 0.1f, 4f, 7f);
            v.Pipe(new Vector3(0.9f, 0.4f, 0.9f), new Vector3(0.3f, 0.9f, 0.3f), 0.06f, v.Metal);
            v.Pipe(new Vector3(-0.9f, 0.4f, -0.9f), new Vector3(-0.3f, 0.9f, -0.3f), 0.06f, v.Metal);
            v.Top = 3.4f;
        }

        static void Market(VB v)
        {
            v.Box(new Vector3(0, 0.55f, 0.25f), new Vector3(1.55f, 0.6f, 0.7f), v.Wood);
            for (int i = -1; i <= 1; i += 2)
                v.Cyl(new Vector3(i * 0.7f, 0.2f, -0.45f), 0.05f, 1.45f, v.Wood);
            for (int i = 0; i < 5; i++)
            {
                GameObject s = v.Box(new Vector3(-0.64f + i * 0.32f, 1.62f, -0.12f), new Vector3(0.3f, 0.08f, 1.15f), i % 2 == 0 ? v.A : v.B);
                s.transform.localRotation = Quaternion.Euler(-14f, 0, 0);
            }
            Color[] fruit = { ColorUtil.Hex(0xFF7F50), ColorUtil.Hex(0xFFD35A), ColorUtil.Hex(0xFF8FA3), ColorUtil.Hex(0x7BD88F) };
            for (int i = 0; i < 4; i++) v.Ball(new Vector3(-0.5f + i * 0.33f, 0.95f, 0.25f), 0.2f, fruit[i]);
            GameObject coin = v.Cyl(new Vector3(0.55f, 1.15f, 0.25f), 0.14f, 0.04f, ColorUtil.Hex(0xFFC857));
            coin.transform.localRotation = Quaternion.Euler(90f, 0, 0);
            v.Spin(coin, Vector3.forward, 120f);
            v.Top = 1.9f;
        }

        static void Warehouse(VB v, bool big)
        {
            float w = v.W - 0.35f;
            float d = v.D - 0.35f;
            float h = big ? 1.5f : 1.0f;
            v.Box(new Vector3(0, 0.2f + h * 0.5f, 0), new Vector3(w, h, d), v.A);
            GameObject roof = v.Box(new Vector3(0, 0.2f + h + 0.12f, 0), new Vector3(w + 0.15f, 0.34f, d + 0.15f), v.B);
            v.Box(new Vector3(0, 0.2f + h * 0.5f - 0.1f, d * 0.5f), new Vector3(w * 0.4f, h * 0.7f, 0.06f), v.Dark);
            v.Box(new Vector3(w * 0.5f + 0.12f, 0.4f, d * 0.3f), new Vector3(0.28f, 0.28f, 0.28f), v.Wood);
            v.Box(new Vector3(w * 0.5f + 0.15f, 0.68f, d * 0.3f), new Vector3(0.22f, 0.22f, 0.22f), v.Wood);
            if (big)
            {
                v.Box(new Vector3(0, 0.2f + h + 0.45f, 0), new Vector3(w * 0.6f, 0.4f, d * 0.7f), v.Light);
                v.Cyl(new Vector3(-w * 0.5f, 0.2f + h + 0.2f, -d * 0.5f), 0.04f, 0.9f, v.Wood);
                v.Box(new Vector3(-w * 0.5f + 0.22f, 2.35f, -d * 0.5f), new Vector3(0.4f, 0.22f, 0.03f), v.A);
            }
            v.Top = big ? 2.9f : 1.9f;
        }

        static void Port(VB v)
        {
            v.BoxR(new Vector3(0, 0.3f, 0), new Vector3(v.W - 0.2f, 0.25f, v.D - 0.2f), v.Wood, 0.08f);
            for (int i = 0; i < 4; i++)
            {
                float x = (i & 1) == 0 ? -1.2f : 1.2f;
                float z = (i & 2) == 0 ? -1.2f : 1.2f;
                v.Cyl(new Vector3(x, 0.4f, z), 0.07f, 0.45f, v.Dark);
            }
            GameObject balloon = v.Group("balloon", Vector3.zero, null);
            v.Bob(balloon, 0.12f, 1.0f, 0f);
            v.Ell(new Vector3(0, 2.7f, 0), new Vector3(1.5f, 1.7f, 1.5f), v.A, balloon.transform);
            v.Ring(new Vector3(0, 2.7f, 0), 0.75f, 0.05f, v.B, balloon.transform);
            v.Box(new Vector3(0, 1.55f, 0), new Vector3(0.45f, 0.3f, 0.45f), v.Wood, balloon.transform);
            v.Pipe(new Vector3(-0.2f, 1.7f, -0.2f), new Vector3(-0.5f, 2.2f, -0.4f), 0.02f, v.Dark, balloon.transform);
            v.Pipe(new Vector3(0.2f, 1.7f, 0.2f), new Vector3(0.5f, 2.2f, 0.4f), 0.02f, v.Dark, balloon.transform);
            v.Pipe(new Vector3(0.2f, 1.7f, -0.2f), new Vector3(0.5f, 2.2f, -0.4f), 0.02f, v.Dark, balloon.transform);
            v.Pipe(new Vector3(-0.2f, 1.7f, 0.2f), new Vector3(-0.5f, 2.2f, 0.4f), 0.02f, v.Dark, balloon.transform);
            v.Cyl(new Vector3(1.1f, 0.4f, 1.0f), 0.04f, 1.1f, v.Wood);
            GameObject sock = v.Cone(new Vector3(1.1f, 1.45f, 1.0f), 0.12f, 0.45f, ColorUtil.Hex(0xFF7F50), 8);
            sock.transform.localRotation = Quaternion.Euler(0, 0, -80f);
            v.Spin(sock, Vector3.up, 30f);
            v.Box(new Vector3(-1.0f, 0.62f, 0.9f), new Vector3(0.4f, 0.35f, 0.4f), v.Wood);
            v.Box(new Vector3(-0.6f, 0.58f, 1.1f), new Vector3(0.3f, 0.28f, 0.3f), v.Light);
            v.Top = 3.6f;
        }

        static void Observatory(VB v)
        {
            v.Cyl(new Vector3(0, 0.2f, 0), 1.05f, 1.0f, v.A);
            v.Ring(new Vector3(0, 1.0f, 0), 1.05f, 0.05f, v.B);
            GameObject dome = v.Ell(new Vector3(0, 1.2f, 0), new Vector3(2.0f, 1.8f, 2.0f), v.B);
            dome.name = "dome";
            v.Box(new Vector3(0, 1.45f, 0.82f), new Vector3(0.35f, 1.0f, 0.4f), v.Dark);
            GameObject scope = v.Group("scope", new Vector3(0, 1.3f, 0), null);
            v.Spin(scope, Vector3.up, 12f);
            v.Pipe(new Vector3(0, 0.2f, 0.2f), new Vector3(0.0f, 1.05f, 0.95f), 0.1f, v.Metal, scope.transform);
            for (int i = 0; i < 3; i++)
            {
                GameObject star = v.Glow(Vector3.zero, 0.1f, ColorUtil.Hex(0xFFE04A), 2.2f);
                v.Orbit(star, new Vector3(0, 2.4f, 0), 0.9f + i * 0.15f, 25f + i * 8f, i * 120f, 0.15f);
            }
            v.Top = 3.0f;
        }

        static void ResearchLab(VB v)
        {
            v.Box(new Vector3(0, 0.8f, -0.15f), new Vector3(v.W - 0.9f, 1.2f, v.D - 1.1f), v.A);
            GameObject roof = v.Cone(new Vector3(0, 1.4f, -0.15f), 1.5f, 0.75f, v.B, 4);
            roof.transform.localRotation = Quaternion.Euler(0, 45f, 0);
            for (int i = 0; i < 4; i++)
                v.Cyl(new Vector3(-0.75f + i * 0.5f, 0.2f, 0.85f), 0.08f, 1.15f, Color.white);
            v.Box(new Vector3(0, 1.4f, 0.85f), new Vector3(2.1f, 0.12f, 0.3f), v.Light);
            GameObject win = v.Part("window", MeshFactory.RoundedBox(0.5f, 0.55f, 0.04f, 0.02f), MaterialLibrary.Emissive(ColorUtil.Hex(0xB9A3FF), 1.8f),
                new Vector3(0, 0.75f, 0.75f), Vector3.one, null);
            v.Pulse(win, ColorUtil.Hex(0xB9A3FF), 0.8f, 2.4f, 1.8f);
            GameObject dish = v.Group("ant", new Vector3(0.7f, 2.2f, -0.3f), null);
            v.Pipe(new Vector3(0, -0.4f, 0), new Vector3(0, 0.2f, 0), 0.04f, v.Metal, dish.transform);
            GameObject ring = v.Ring(new Vector3(0, 0.2f, 0), 0.25f, 0.03f, v.Metal, dish.transform);
            v.Spin(dish, Vector3.up, 90f);
            Color[] bk = { ColorUtil.Hex(0xFF8FA3), ColorUtil.Hex(0x7CF0C8), ColorUtil.Hex(0xFFD35A) };
            for (int i = 0; i < 3; i++)
            {
                GameObject book = v.Box(new Vector3(-1.0f + i * 0.28f, 2.05f + i * 0.1f, -0.2f), new Vector3(0.24f, 0.05f, 0.32f), bk[i]);
                v.Bob(book, 0.07f, 1.5f, i * 2f);
            }
            v.Top = 2.9f;
        }

        static void PowerStation(VB v)
        {
            v.Box(new Vector3(-0.85f, 0.6f, 0), new Vector3(0.7f, 0.8f, 1.0f), v.A);
            v.Box(new Vector3(0.9f, 0.55f, 0.1f), new Vector3(0.7f, 0.7f, 0.8f), v.B);
            v.Cyl(new Vector3(0, 0.2f, 0), 0.32f, 1.4f, v.Metal);
            for (int i = 0; i < 4; i++) v.Ring(new Vector3(0, 0.5f + i * 0.28f, 0), 0.36f, 0.04f, v.Dark);
            GameObject orb = v.Glow(new Vector3(0, 1.85f, 0), 0.5f, ColorUtil.Hex(0xFFE04A), 2.6f);
            v.Pulse(orb, ColorUtil.Hex(0xFFE04A), 1f, 3.6f, 3.4f);
            v.Pipe(new Vector3(-0.5f, 0.9f, 0), new Vector3(-0.2f, 0.9f, 0), 0.05f, v.Dark);
            v.Pipe(new Vector3(0.2f, 0.9f, 0), new Vector3(0.55f, 0.8f, 0.1f), 0.05f, v.Dark);
            for (int i = 0; i < 3; i++)
            {
                GameObject s = v.Glow(Vector3.zero, 0.09f, ColorUtil.Hex(0xFFF3A0), 2.4f);
                v.Orbit(s, new Vector3(0, 1.85f, 0), 0.45f, 160f, i * 120f, 0.1f);
            }
            v.Top = 2.3f;
        }

        static void Tower(VB v)
        {
            float[] widths = { 0.95f, 0.75f, 0.58f, 0.42f };
            for (int i = 0; i < widths.Length; i++)
                v.Box(new Vector3(0, 0.45f + i * 0.5f, 0), new Vector3(widths[i], 0.5f, widths[i]), i % 2 == 0 ? v.A : v.B);
            GameObject dish = v.Group("dish", new Vector3(0, 2.55f, 0), null);
            v.Spin(dish, Vector3.up, 35f);
            GameObject cup = v.Ell(new Vector3(0.18f, 0, 0), new Vector3(0.7f, 0.22f, 0.7f), v.Light, dish.transform);
            cup.transform.localRotation = Quaternion.Euler(0, 0, -50f);
            v.Pipe(new Vector3(0, 0, 0), new Vector3(0.38f, 0.22f, 0), 0.03f, v.Metal, dish.transform);
            GameObject beacon = v.Glow(new Vector3(0, 2.95f, 0), 0.2f, ColorUtil.Hex(0xFF4A5A), 2.4f);
            v.Pulse(beacon, ColorUtil.Hex(0xFF4A5A), 0.2f, 3f, 4f);
            v.Pipe(new Vector3(0, 2.55f, 0), new Vector3(0, 2.9f, 0), 0.03f, v.Metal);
            v.Top = 3.1f;
        }

        static void ClimateCoreModel(VB v)
        {
            v.Cyl(new Vector3(0, 0.2f, 0), 1.5f, 0.3f, v.Light);
            v.Cyl(new Vector3(0, 0.45f, 0), 1.0f, 0.4f, v.Metal);
            GameObject core = v.Glow(new Vector3(0, 2.1f, 0), 1.5f, ColorUtil.Hex(0xB48CF2), 2.4f);
            v.Pulse(core, ColorUtil.Hex(0xB48CF2), 1.2f, 3.2f, 1.4f);
            v.Bob(core, 0.1f, 1.0f, 0f);
            GameObject inner = v.Glow(new Vector3(0, 2.1f, 0), 0.75f, ColorUtil.Hex(0xFFE04A), 3f);
            v.Bob(inner, 0.1f, 1.0f, 0f);
            for (int i = 0; i < 3; i++)
            {
                GameObject pivot = v.Group("orbit", new Vector3(0, 2.1f, 0), null);
                pivot.transform.localRotation = Quaternion.Euler(i * 60f, i * 35f, 0);
                v.Ring(Vector3.zero, 1.35f + i * 0.12f, 0.05f, i == 1 ? v.B : v.A, pivot.transform);
                v.Spin(pivot, i == 1 ? Vector3.right : Vector3.up, 30f + i * 25f);
            }
            for (int i = 0; i < 4; i++)
            {
                float x = (i & 1) == 0 ? -1.55f : 1.55f;
                float z = (i & 2) == 0 ? -1.55f : 1.55f;
                v.Cyl(new Vector3(x, 0.3f, z), 0.12f, 1.4f, v.Dark);
                GameObject tip = v.Glow(new Vector3(x, 1.85f, z), 0.28f, ColorUtil.Hex(0xB48CF2), 2.2f);
                v.Pulse(tip, ColorUtil.Hex(0xB48CF2), 0.8f, 2.8f, 2f + i * 0.4f);
            }
            v.Top = 3.6f;
        }

        static void IndustrialCenter(VB v)
        {
            v.Box(new Vector3(0, 1.2f, -0.35f), new Vector3(v.W - 0.7f, 2.0f, v.D - 1.4f), v.A);
            v.Box(new Vector3(0, 0.5f, 1.3f), new Vector3(2.8f, 0.5f, 0.9f), v.B);
            GameObject gear = v.Group("gear", new Vector3(0, 2.7f, -0.35f), null);
            v.Spin(gear, Vector3.forward, 24f);
            GameObject gr = v.Ring(Vector3.zero, 0.9f, 0.14f, v.Dark, gear.transform);
            gr.transform.localRotation = Quaternion.Euler(90f, 0, 0);
            for (int i = 0; i < 12; i++)
            {
                GameObject tooth = v.BoxR(Vector3.zero, new Vector3(0.2f, 0.26f, 0.2f), v.Dark, 0.04f, gear.transform);
                float ang = i * 30f * Mathf.Deg2Rad;
                tooth.transform.localPosition = new Vector3(Mathf.Cos(ang) * 1.05f, Mathf.Sin(ang) * 1.05f, 0);
            }
            for (int i = 0; i < 3; i++)
            {
                v.Cyl(new Vector3(-1.3f + i * 1.3f, 2.2f, -1.0f), 0.18f, 1.2f, v.Metal);
                v.Smoke(new Vector3(-1.3f + i * 1.3f, 3.45f, -1.0f), 0.42f);
            }
            GameObject crane = v.Group("crane", new Vector3(1.4f, 2.25f, 0.5f), null);
            v.Spin(crane, Vector3.up, 18f);
            v.Cyl(Vector3.zero, 0.1f, 0.5f, v.Dark, crane.transform);
            v.Box(new Vector3(0.7f, 0.5f, 0), new Vector3(1.5f, 0.1f, 0.12f), v.B, crane.transform);
            v.Box(new Vector3(1.35f, 0.3f, 0), new Vector3(0.18f, 0.2f, 0.18f), v.Wood, crane.transform);
            for (int i = 0; i < 3; i++)
            {
                GameObject crate = v.Box(new Vector3(-1.1f, 0.85f, 1.3f), new Vector3(0.3f, 0.25f, 0.3f), v.Wood);
                v.Slide(crate, new Vector3(2.2f, 0, 0), 5f, i / 3f);
            }
            v.Top = 3.8f;
        }

        static void Golden(VB v)
        {
            Color gold = ColorUtil.Hex(0xFFC857);
            v.Cyl(new Vector3(0, 0.2f, 0), 0.5f, 0.55f, v.B);
            v.Ring(new Vector3(0, 0.55f, 0), 0.5f, 0.05f, gold);
            GameObject coin = v.Part("coin", MeshFactory.Cylinder(), MaterialLibrary.Emissive(gold, 1.2f),
                new Vector3(0, 1.4f, 0), new Vector3(0.9f, 0.06f, 0.9f), null);
            coin.transform.localRotation = Quaternion.Euler(90f, 0, 0);
            GameObject holder = v.Group("holder", new Vector3(0, 1.4f, 0), null);
            coin.transform.SetParent(holder.transform, false);
            coin.transform.localPosition = Vector3.zero;
            v.Spin(holder, Vector3.up, 100f);
            v.Pulse(coin, gold, 0.8f, 2f, 2f);
            for (int i = 0; i < 4; i++)
            {
                GameObject s = v.Glow(Vector3.zero, 0.09f, ColorUtil.Hex(0xFFF0C4), 2.4f);
                v.Orbit(s, new Vector3(0, 1.4f, 0), 0.75f, 70f, i * 90f, 0.2f);
            }
            v.Top = 2.2f;
        }

        // ------------------------------------------------------------------ level upgrade looks

        static void AddLevelGroups(VB v, BuildingDef def)
        {
            float top = v.Top;
            float hw = v.W * 0.5f - 0.2f;
            float hd = v.D * 0.5f - 0.2f;
            Color accent = v.B;

            // Level 4+: banner pole at a corner.
            GameObject g1 = new GameObject(GroupLv1);
            g1.transform.SetParent(v.Root.transform, false);
            v.Parent = g1.transform;
            v.Cyl(new Vector3(-hw, 0.2f, -hd), 0.035f, 1.25f, v.Wood);
            GameObject flag = v.Box(new Vector3(-hw + 0.22f, 1.28f, -hd), new Vector3(0.4f, 0.24f, 0.03f), accent);
            v.Bob(flag, 0.015f, 3f, 0f);
            v.Ball(new Vector3(-hw, 1.48f, -hd), 0.1f, ColorUtil.Hex(0xFFC857));
            g1.SetActive(false);

            // Level 7+: glowing orbs and a light halo.
            GameObject g2 = new GameObject(GroupLv2);
            g2.transform.SetParent(v.Root.transform, false);
            v.Parent = g2.transform;
            Color glow = ColorUtil.Lighten(v.A, 0.35f);
            for (int i = 0; i < 2; i++)
            {
                GameObject orb = v.Glow(Vector3.zero, 0.2f, glow, 2.2f);
                v.Orbit(orb, new Vector3(0, top * 0.62f, 0), Mathf.Max(hw, hd) + 0.1f, 45f, i * 180f, 0.15f);
            }
            GameObject halo = v.Ring(new Vector3(0, 0.26f, 0), Mathf.Max(hw, hd) + 0.2f, 0.03f, ColorUtil.Hex(0xFFE9A0));
            v.Spin(halo, Vector3.up, 35f);
            g2.SetActive(false);

            // Level 10: silver crown and gold crown (prestige cosmetic).
            BuildCrown(v, GroupLv3, top, Color.white, ColorUtil.Hex(0xB9C6FF));
            BuildCrown(v, GroupLv3Gold, top, ColorUtil.Hex(0xFFC857), ColorUtil.Hex(0xFFE9A0));
            v.Parent = v.Root.transform;
        }

        static void BuildCrown(VB v, string groupName, float top, Color main, Color jewel)
        {
            GameObject g = new GameObject(groupName);
            g.transform.SetParent(v.Root.transform, false);
            v.Parent = g.transform;
            GameObject crown = v.Group("crown", new Vector3(0, top + 0.15f, 0), null);
            v.Bob(crown, 0.06f, 1.5f, 0f);
            v.Spin(crown, Vector3.up, 40f);
            v.Ring(Vector3.zero, 0.34f, 0.06f, main, crown.transform);
            for (int i = 0; i < 5; i++)
            {
                float ang = i * 72f * Mathf.Deg2Rad;
                v.Cone(new Vector3(Mathf.Cos(ang) * 0.34f, 0.04f, Mathf.Sin(ang) * 0.34f), 0.07f, 0.3f, main, 6, crown.transform);
                v.Glow(new Vector3(Mathf.Cos(ang) * 0.34f, 0.38f, Mathf.Sin(ang) * 0.34f), 0.09f, jewel, 2.5f, crown.transform);
            }
            if (QualityManager.Sparkles)
            {
                for (int i = 0; i < 4; i++)
                {
                    GameObject s = v.Glow(Vector3.zero, 0.07f, jewel, 3f);
                    v.Orbit(s, new Vector3(0, top * 0.5f, 0), Mathf.Max(v.W, v.D) * 0.55f, 60f, i * 90f, 0.4f);
                }
            }
            g.SetActive(false);
        }
    }
}
