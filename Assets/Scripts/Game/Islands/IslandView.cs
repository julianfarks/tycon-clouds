using CloudGarden.Optimization;
using UnityEngine;

namespace CloudGarden.Islands
{
    /// <summary>
    /// One floating island (a region): painted ground, rocky underside, vegetation, animated water and the
    /// build grid overlay. The island is rebuilt when the terrain is expanded.
    /// </summary>
    public sealed class IslandView : MonoBehaviour
    {
        public int Region;
        public RegionDef Def;
        public int Half { get; private set; }
        public bool Unlocked { get; private set; }

        GameObject topObj, underObj, decorRoot, waterRoot, gridObj;
        MeshFilter topFilter, underFilter;
        MeshRenderer topRenderer, underRenderer;
        int builtHalf = -1;
        bool builtUnlocked;
        int builtQualityDecor = -1;
        bool gridVisible;

        public float Extent { get { return Half + 1.6f; } }

        public void Init(int region, RegionDef def)
        {
            Region = region;
            Def = def;
            transform.position = new Vector3(def.WorldX, 0f, def.WorldZ);
            gameObject.name = "Island_" + def.Id;

            topObj = new GameObject("Top");
            topObj.transform.SetParent(transform, false);
            topFilter = topObj.AddComponent<MeshFilter>();
            topRenderer = topObj.AddComponent<MeshRenderer>();
            topRenderer.receiveShadows = true;

            underObj = new GameObject("Under");
            underObj.transform.SetParent(transform, false);
            underFilter = underObj.AddComponent<MeshFilter>();
            underRenderer = underObj.AddComponent<MeshRenderer>();
            underRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            decorRoot = new GameObject("Decor");
            decorRoot.transform.SetParent(transform, false);
            waterRoot = new GameObject("Water");
            waterRoot.transform.SetParent(transform, false);
            gridObj = new GameObject("Grid");
            gridObj.transform.SetParent(transform, false);
            gridObj.SetActive(false);
        }

        public void Refresh(int half, bool unlocked)
        {
            int decorKey = Mathf.RoundToInt(QualityManager.DecorDensity * 100f);
            if (half == builtHalf && unlocked == builtUnlocked && decorKey == builtQualityDecor) return;

            bool geometryChanged = half != builtHalf || unlocked != builtUnlocked;
            Half = half;
            Unlocked = unlocked;
            builtHalf = half;
            builtUnlocked = unlocked;
            builtQualityDecor = decorKey;

            if (geometryChanged) RebuildGeometry();
            RebuildDecor();
            RebuildGrid();
        }

        void RebuildGeometry()
        {
            float extent = Extent;
            int seed = Region * 101 + 7;
            if (topFilter.sharedMesh != null) Destroy(topFilter.sharedMesh);
            if (underFilter.sharedMesh != null) Destroy(underFilter.sharedMesh);
            topFilter.sharedMesh = MeshFactory.IslandTop(extent, seed);
            underFilter.sharedMesh = MeshFactory.IslandUnder(extent, 3.2f + Half * 0.55f, seed + 3);

            Color ground = ColorUtil.Hex(Def.Ground);
            Color ground2 = ColorUtil.Hex(Def.Ground2);
            Color rock = ColorUtil.Hex(Def.Rock);
            Color tint = Color.white;
            if (!Unlocked)
            {
                ground = Color.Lerp(ground, new Color(0.45f, 0.5f, 0.62f), 0.75f);
                ground2 = Color.Lerp(ground2, new Color(0.4f, 0.45f, 0.58f), 0.75f);
                rock = Color.Lerp(rock, new Color(0.35f, 0.38f, 0.5f), 0.7f);
                tint = new Color(0.85f, 0.88f, 1f);
            }
            string suffix = Unlocked ? "" : "_locked";
            Texture2D gTex = TextureFactory.Painted("ground" + Region + suffix, ground, ground2, 128, 6, Region * 17 + 3, 110);
            Texture2D rTex = TextureFactory.Painted("rock" + Region + suffix, rock, ColorUtil.Darken(rock, 0.35f), 128, 5, Region * 17 + 11, 90);
            topRenderer.sharedMaterial = MaterialLibrary.Textured(tint, gTex, "gm" + Region + suffix, 1f);
            underRenderer.sharedMaterial = MaterialLibrary.Textured(tint, rTex, "rm" + Region + suffix, 1f);
            topRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            // Hanging rocks and stalactites below the island.
            for (int i = underObj.transform.childCount - 1; i >= 0; i--) Destroy(underObj.transform.GetChild(i).gameObject);
            System.Random rnd = new System.Random(seed);
            int stalactites = 7 + Half;
            for (int i = 0; i < stalactites; i++)
            {
                float ang = (float)(rnd.NextDouble() * Mathf.PI * 2.0);
                float rad = extent * (0.35f + (float)rnd.NextDouble() * 0.4f);
                float depth = 0.9f + (float)rnd.NextDouble() * 1.8f;
                GameObject cone = new GameObject("rock");
                cone.transform.SetParent(underObj.transform, false);
                cone.transform.localPosition = new Vector3(Mathf.Cos(ang) * rad, -0.5f - rad * 0.12f, Mathf.Sin(ang) * rad);
                cone.transform.localRotation = Quaternion.Euler(180f, 0f, 0f);
                cone.AddComponent<MeshFilter>().sharedMesh = MeshFactory.Cone(0.35f + (float)rnd.NextDouble() * 0.35f, depth, 7);
                MeshRenderer mr = cone.AddComponent<MeshRenderer>();
                mr.sharedMaterial = underRenderer.sharedMaterial;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }

        // ------------------------------------------------------------------ decoration

        GameObject Prop(Mesh mesh, Material mat, Vector3 pos, Vector3 scale, Transform parent)
        {
            GameObject go = new GameObject("p");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return go;
        }

        GameObject Cyl(Vector3 basePos, float radius, float height, Color c, Transform parent)
        {
            return Prop(MeshFactory.Cylinder(), MaterialLibrary.Lit(c), basePos + Vector3.up * (height * 0.5f), new Vector3(radius * 2f, height * 0.5f, radius * 2f), parent);
        }

        GameObject Ball(Vector3 center, float d, Color c, Transform parent)
        {
            return Prop(MeshFactory.Sphere(), MaterialLibrary.Lit(c, 0.35f), center, Vector3.one * d, parent);
        }

        GameObject ConeP(Vector3 basePos, float r, float h, Color c, Transform parent)
        {
            return Prop(MeshFactory.Cone(r, h, 8), MaterialLibrary.Lit(c), basePos, Vector3.one, parent);
        }

        void RebuildDecor()
        {
            for (int i = decorRoot.transform.childCount - 1; i >= 0; i--) Destroy(decorRoot.transform.GetChild(i).gameObject);
            for (int i = waterRoot.transform.childCount - 1; i >= 0; i--) Destroy(waterRoot.transform.GetChild(i).gameObject);
            if (!Unlocked) return;

            System.Random rnd = new System.Random(Region * 977 + Half * 13);
            int count = Mathf.RoundToInt((6 + Half * 4) * QualityManager.DecorDensity);
            bool hasStream = Region <= 1;
            float half = Half;

            for (int n = 0; n < count; n++)
            {
                int side = rnd.Next(4);
                float along = (float)(rnd.NextDouble() * 2.0 - 1.0) * (half + 0.9f);
                float offset = half + 0.35f + (float)rnd.NextDouble() * 0.75f;
                Vector3 p;
                switch (side)
                {
                    case 0: p = new Vector3(along, 0f, -offset); break;
                    case 1: p = new Vector3(along, 0f, offset); break;
                    case 2: p = new Vector3(-offset, 0f, along); break;
                    default: p = new Vector3(offset, 0f, along); break;
                }
                // Keep the south margin free for the stream.
                if (hasStream && side == 0 && Mathf.Abs(along) < half + 0.2f) continue;
                if (hasStream && p.z < -half - 0.3f && p.z > -half - 1.25f && Mathf.Abs(p.x) < half + 0.4f) continue;

                float roll = (float)rnd.NextDouble();
                PlaceDecor(p, roll, rnd);
            }

            if (hasStream) BuildStream();
        }

        void PlaceDecor(Vector3 p, float roll, System.Random rnd)
        {
            Transform parent = decorRoot.transform;
            float s = 0.75f + (float)rnd.NextDouble() * 0.6f;
            string kind = Def.Decor;

            if (kind == "meadow")
            {
                if (roll < 0.30f) Tree(p, s, ColorUtil.Hex(0x5CC46E), ColorUtil.Hex(0x9A6B45), parent);
                else if (roll < 0.52f) Bush(p, s, ColorUtil.Hex(0x4FB563), parent);
                else if (roll < 0.88f) Flower(p, rnd, parent);
                else Mushroom(p, s, parent);
            }
            else if (kind == "tropical")
            {
                if (roll < 0.45f) Palm(p, s, parent, rnd);
                else if (roll < 0.65f) Bush(p, s, ColorUtil.Hex(0x39B26A), parent);
                else if (roll < 0.9f) Flower(p, rnd, parent);
                else Rock(p, s * 0.7f, ColorUtil.Hex(0xE8D3A0), parent);
            }
            else if (kind == "snow")
            {
                if (roll < 0.45f) Pine(p, s, parent);
                else if (roll < 0.7f) Ball(p + Vector3.up * 0.15f, 0.9f * s, new Color(0.97f, 0.99f, 1f), parent).transform.localScale = new Vector3(1.1f * s, 0.5f * s, 1.0f * s);
                else if (roll < 0.95f) IceCrystal(p, s, ColorUtil.Hex(0x9ED8FF), parent);
                else Rock(p, s * 0.7f, ColorUtil.Hex(0x9CB7CF), parent);
            }
            else if (kind == "storm")
            {
                if (roll < 0.25f) BentTree(p, s, parent);
                else if (roll < 0.55f) Rock(p, s, ColorUtil.Hex(0x59638C), parent);
                else if (roll < 0.75f) Rod(p, s, parent);
                else if (roll < 0.9f) Bush(p, s * 0.9f, ColorUtil.Hex(0x4B6B5E), parent);
                else IceCrystal(p, s, ColorUtil.Hex(0xFFE04A), parent);
            }
            else
            {
                if (roll < 0.35f) IceCrystal(p, s * 1.3f, rnd.Next(2) == 0 ? ColorUtil.Hex(0xFFC857) : ColorUtil.Hex(0xB48CF2), parent);
                else if (roll < 0.6f) Orb(p, s, parent);
                else if (roll < 0.85f) Tree(p, s, ColorUtil.Hex(0xC9A8FF), ColorUtil.Hex(0xE6D5B8), parent);
                else Flower(p, rnd, parent);
            }
        }

        void Tree(Vector3 p, float s, Color crown, Color trunk, Transform parent)
        {
            GameObject root = new GameObject("tree");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = p;
            Cyl(Vector3.zero, 0.09f * s, 0.55f * s, trunk, root.transform);
            GameObject c1 = Ball(new Vector3(0, 0.85f * s, 0), 0.85f * s, crown, root.transform);
            Ball(new Vector3(0.18f * s, 1.15f * s, 0.05f), 0.55f * s, ColorUtil.Lighten(crown, 0.15f), root.transform);
            AmbientMotion.AddSway(root.transform, 1.6f, 1.2f);
        }

        void Bush(Vector3 p, float s, Color c, Transform parent)
        {
            GameObject root = new GameObject("bush");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = p;
            Ball(new Vector3(0, 0.22f * s, 0), 0.6f * s, c, root.transform);
            Ball(new Vector3(0.25f * s, 0.18f * s, 0.1f), 0.42f * s, ColorUtil.Lighten(c, 0.12f), root.transform);
            AmbientMotion.AddSway(root.transform, 1.2f, 1.6f);
        }

        void Flower(Vector3 p, System.Random rnd, Transform parent)
        {
            Color[] cols = { ColorUtil.Hex(0xFF8FB8), ColorUtil.Hex(0xFFD35A), ColorUtil.Hex(0xFFFFFF), ColorUtil.Hex(0xB48CF2), ColorUtil.Hex(0xFF9A6B) };
            GameObject root = new GameObject("flower");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = p;
            Cyl(Vector3.zero, 0.02f, 0.3f, ColorUtil.Hex(0x4FB563), root.transform);
            Ball(new Vector3(0, 0.34f, 0), 0.2f, cols[rnd.Next(cols.Length)], root.transform);
            AmbientMotion.AddSway(root.transform, 4f, 2.2f);
        }

        void Mushroom(Vector3 p, float s, Transform parent)
        {
            GameObject root = new GameObject("mushroom");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = p;
            Cyl(Vector3.zero, 0.07f * s, 0.25f * s, ColorUtil.Hex(0xF4EBD8), root.transform);
            Prop(MeshFactory.Sphere(), MaterialLibrary.Lit(ColorUtil.Hex(0xFF5A5A), 0.4f), new Vector3(0, 0.27f * s, 0), new Vector3(0.4f * s, 0.24f * s, 0.4f * s), root.transform);
        }

        void Palm(Vector3 p, float s, Transform parent, System.Random rnd)
        {
            GameObject root = new GameObject("palm");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = p;
            GameObject trunk = Cyl(Vector3.zero, 0.08f * s, 1.5f * s, ColorUtil.Hex(0xA8764B), root.transform);
            trunk.transform.localRotation = Quaternion.Euler((float)rnd.NextDouble() * 12f, 0f, (float)rnd.NextDouble() * 12f - 6f);
            GameObject crown = new GameObject("crown");
            crown.transform.SetParent(root.transform, false);
            crown.transform.localPosition = new Vector3(0, 1.5f * s, 0);
            for (int i = 0; i < 6; i++)
            {
                GameObject leaf = Prop(MeshFactory.Sphere(), MaterialLibrary.Lit(ColorUtil.Hex(0x2FAE63), 0.35f),
                    new Vector3(0, 0, 0), new Vector3(0.9f * s, 0.06f * s, 0.26f * s), crown.transform);
                leaf.transform.localRotation = Quaternion.Euler(0f, i * 60f, 18f);
                leaf.transform.localPosition = leaf.transform.localRotation * new Vector3(0.38f * s, -0.05f * s, 0f);
            }
            Ball(new Vector3(0.08f, -0.08f * s, 0.05f), 0.16f * s, ColorUtil.Hex(0x6B4A2B), crown.transform);
            AmbientMotion.AddSway(crown.transform, 3.5f, 1.3f);
        }

        void Pine(Vector3 p, float s, Transform parent)
        {
            GameObject root = new GameObject("pine");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = p;
            Cyl(Vector3.zero, 0.08f * s, 0.3f * s, ColorUtil.Hex(0x8A6A4A), root.transform);
            Color green = ColorUtil.Hex(0x3F8F66);
            ConeP(new Vector3(0, 0.25f * s, 0), 0.5f * s, 0.7f * s, green, root.transform);
            ConeP(new Vector3(0, 0.6f * s, 0), 0.4f * s, 0.65f * s, ColorUtil.Lighten(green, 0.08f), root.transform);
            ConeP(new Vector3(0, 0.95f * s, 0), 0.28f * s, 0.55f * s, ColorUtil.Hex(0xF4FAFF), root.transform);
            AmbientMotion.AddSway(root.transform, 1f, 1.1f);
        }

        void IceCrystal(Vector3 p, float s, Color c, Transform parent)
        {
            GameObject root = new GameObject("crystal");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = p;
            Material mat = MaterialLibrary.Emissive(c, 1f);
            for (int i = 0; i < 3; i++)
            {
                GameObject cone = Prop(MeshFactory.Cone(0.13f * s, (0.55f + i * 0.2f) * s, 6), mat,
                    new Vector3((i - 1) * 0.16f * s, 0, (i % 2) * 0.1f * s), Vector3.one, root.transform);
                cone.transform.localRotation = Quaternion.Euler((i - 1) * 10f, 0, (1 - i) * 9f);
            }
        }

        void Rock(Vector3 p, float s, Color c, Transform parent)
        {
            GameObject rock = Ball(p + Vector3.up * 0.1f * s, 0.6f * s, c, parent);
            rock.transform.localScale = new Vector3(0.75f * s, 0.45f * s, 0.6f * s);
        }

        void BentTree(Vector3 p, float s, Transform parent)
        {
            GameObject root = new GameObject("deadtree");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = p;
            GameObject trunk = Cyl(Vector3.zero, 0.07f * s, 1.0f * s, ColorUtil.Hex(0x4A4558), root.transform);
            trunk.transform.localRotation = Quaternion.Euler(0, 0, 14f);
            Ball(new Vector3(0.28f * s, 1.0f * s, 0), 0.5f * s, ColorUtil.Hex(0x4B6B5E), root.transform);
            AmbientMotion.AddSway(root.transform, 2.2f, 1.8f);
        }

        void Rod(Vector3 p, float s, Transform parent)
        {
            GameObject root = new GameObject("rod");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = p;
            Cyl(Vector3.zero, 0.04f * s, 1.3f * s, ColorUtil.Hex(0x9AA3B5), root.transform);
            Prop(MeshFactory.Sphere(), MaterialLibrary.Emissive(ColorUtil.Hex(0xFFE04A), 2f), new Vector3(0, 1.35f * s, 0), Vector3.one * 0.14f, root.transform);
        }

        void Orb(Vector3 p, float s, Transform parent)
        {
            GameObject root = new GameObject("orb");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = p + Vector3.up * 0.9f;
            Prop(MeshFactory.Sphere(), MaterialLibrary.Emissive(ColorUtil.Hex(0xFFE9A0), 2f), Vector3.zero, Vector3.one * 0.3f * s, root.transform);
            Prop(MeshFactory.Torus(0.28f * s, 0.02f, 20, 6), MaterialLibrary.Emissive(ColorUtil.Hex(0xFFC857), 1f), Vector3.zero, Vector3.one, root.transform);
            AmbientMotion.AddSway(root.transform, 8f, 1.2f);
        }

        void BuildStream()
        {
            float half = Half;
            float length = half * 2f + 1.0f;
            Material mat = MaterialLibrary.WaterMaterial(Region == 0 ? new Color(0.45f, 0.78f, 1f, 0.85f) : new Color(0.25f, 0.85f, 0.85f, 0.85f), TextureFactory.WaterRipples());
            GameObject water = new GameObject("stream");
            water.transform.SetParent(waterRoot.transform, false);
            water.transform.localPosition = new Vector3(0f, 0.04f, -half - 0.78f);
            water.AddComponent<MeshFilter>().sharedMesh = MeshFactory.Quad(length, 0.75f);
            MeshRenderer mr = water.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            water.transform.localScale = Vector3.one;
            mat.SetTextureScale(mat.HasProperty("_BaseMap") ? "_BaseMap" : "_MainTex", new Vector2(length / 2f, 1f));
            AmbientMotion.AddScroll(mat, new Vector2(0.12f, 0.02f));

            // Banks of the stream
            Color bank = ColorUtil.Hex(Region == 0 ? 0xA8E39A : 0xF3D9A4);
            GameObject b1 = Prop(MeshFactory.Quad(length + 0.2f, 0.12f), MaterialLibrary.Lit(bank), new Vector3(0, 0.045f, -half - 0.35f), Vector3.one, waterRoot.transform);
            GameObject b2 = Prop(MeshFactory.Quad(length + 0.2f, 0.12f), MaterialLibrary.Lit(bank), new Vector3(0, 0.045f, -half - 1.2f), Vector3.one, waterRoot.transform);
            b1.GetComponent<MeshRenderer>().receiveShadows = false;
            b2.GetComponent<MeshRenderer>().receiveShadows = false;
        }

        // ------------------------------------------------------------------ grid

        void RebuildGrid()
        {
            MeshFilter mf = gridObj.GetComponent<MeshFilter>();
            if (mf == null)
            {
                mf = gridObj.AddComponent<MeshFilter>();
                MeshRenderer mr = gridObj.AddComponent<MeshRenderer>();
                mr.sharedMaterial = MaterialLibrary.Glass(new Color(1f, 1f, 1f, 0.35f));
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
            }
            if (mf.sharedMesh != null) Destroy(mf.sharedMesh);
            mf.sharedMesh = MeshFactory.GridLines(-Half, Half - 1, 0.04f);
            gridObj.transform.localPosition = new Vector3(0, 0.035f, 0);
            gridObj.SetActive(gridVisible && Unlocked);
        }

        public void SetGridVisible(bool visible)
        {
            gridVisible = visible;
            if (gridObj != null) gridObj.SetActive(visible && Unlocked);
        }

        /// <summary>World position of the center of a tile.</summary>
        public Vector3 TileCenter(int x, int y)
        {
            return transform.position + new Vector3(x + 0.5f, 0f, y + 0.5f);
        }
    }
}
