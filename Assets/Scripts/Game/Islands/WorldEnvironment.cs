using System.Collections.Generic;
using CloudGarden.Islands;
using CloudGarden.Optimization;
using UnityEngine;
using UnityEngine.Rendering;

namespace CloudGarden.Islands
{
    /// <summary>Lighting, gradient sky behind the camera and the drifting sea of clouds.</summary>
    public sealed class WorldEnvironment : MonoBehaviour
    {
        Camera cam;
        Light sun;
        GameObject skyQuad;
        Texture2D skyTexture;
        Color curTop, curBottom, tgtTop, tgtBottom;
        float skyTimer;

        sealed class Cloud
        {
            public Transform T;
            public float Speed;
            public float Bob;
            public float Phase;
            public float BaseY;
        }

        readonly List<Cloud> clouds = new List<Cloud>();
        GameObject cloudRoot;
        int builtCloudCount = -1;
        const float WrapX = 95f;

        public void Init(Camera camera)
        {
            cam = camera;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.72f, 0.86f, 1f);
            cam.orthographic = true;

            GameObject lightObj = new GameObject("Sun");
            lightObj.transform.SetParent(transform, false);
            lightObj.transform.rotation = Quaternion.Euler(52f, -35f, 0f);
            sun = lightObj.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.96f, 0.88f);
            sun.intensity = 1.15f;
            sun.shadows = QualityManager.Shadows ? LightShadows.Soft : LightShadows.None;
            sun.shadowStrength = 0.55f;

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.66f, 0.7f, 0.82f);
            RenderSettings.fog = false;

            skyQuad = new GameObject("Sky");
            skyQuad.transform.SetParent(cam.transform, false);
            skyQuad.transform.localPosition = new Vector3(0, 0, 120f);
            skyQuad.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            skyQuad.AddComponent<MeshFilter>().sharedMesh = MeshFactory.Quad(1f, 1f);
            MeshRenderer mr = skyQuad.AddComponent<MeshRenderer>();
            curTop = tgtTop = new Color(0.55f, 0.78f, 1f);
            curBottom = tgtBottom = new Color(1f, 0.95f, 0.85f);
            skyTexture = TextureFactory.Gradient(curTop, curBottom, 64, null);
            mr.sharedMaterial = MaterialLibrary.UnlitTextured(skyTexture);
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;

            QualityManager.Changed += OnQualityChanged;
            RebuildClouds();
        }

        void OnDestroy()
        {
            QualityManager.Changed -= OnQualityChanged;
        }

        void OnQualityChanged()
        {
            if (sun != null) sun.shadows = QualityManager.Shadows ? LightShadows.Soft : LightShadows.None;
            RebuildClouds();
        }

        public void SetSky(uint top, uint bottom)
        {
            tgtTop = ColorUtil.Hex(top);
            tgtBottom = ColorUtil.Hex(bottom);
        }

        void RebuildClouds()
        {
            int target = QualityManager.CloudCount;
            if (target == builtCloudCount) return;
            builtCloudCount = target;
            if (cloudRoot != null) Destroy(cloudRoot);
            cloudRoot = new GameObject("Clouds");
            cloudRoot.transform.SetParent(transform, false);
            clouds.Clear();

            System.Random rnd = new System.Random(4242);
            Color white = new Color(0.98f, 0.99f, 1f);
            for (int i = 0; i < target; i++)
            {
                bool high = i % 5 == 0;
                GameObject g = new GameObject("cloud");
                g.transform.SetParent(cloudRoot.transform, false);
                float y = high ? 5f + (float)rnd.NextDouble() * 5f : -6f - (float)rnd.NextDouble() * 16f;
                g.transform.position = new Vector3((float)(rnd.NextDouble() * 2 - 1) * WrapX, y, (float)(rnd.NextDouble() * 2 - 1) * 70f);
                float scale = high ? 1.6f : 2.4f + (float)rnd.NextDouble() * 2.2f;
                int puffs = 3 + rnd.Next(3);
                for (int p = 0; p < puffs; p++)
                {
                    GameObject puff = new GameObject("puff");
                    puff.transform.SetParent(g.transform, false);
                    float size = (0.9f + (float)rnd.NextDouble() * 0.9f) * scale;
                    puff.transform.localPosition = new Vector3((p - puffs * 0.5f) * 0.9f * scale, (float)rnd.NextDouble() * 0.5f * scale, (float)(rnd.NextDouble() - 0.5) * 0.8f * scale);
                    puff.transform.localScale = new Vector3(size, size * 0.62f, size * 0.9f);
                    puff.AddComponent<MeshFilter>().sharedMesh = MeshFactory.Sphere();
                    MeshRenderer mr = puff.AddComponent<MeshRenderer>();
                    mr.sharedMaterial = MaterialLibrary.Lit(Color.Lerp(white, new Color(0.86f, 0.9f, 1f), (float)rnd.NextDouble() * 0.6f), 0.05f);
                    mr.shadowCastingMode = ShadowCastingMode.Off;
                    mr.receiveShadows = false;
                }
                Cloud c = new Cloud();
                c.T = g.transform;
                c.Speed = 0.25f + (float)rnd.NextDouble() * 0.6f;
                c.Bob = 0.2f + (float)rnd.NextDouble() * 0.3f;
                c.Phase = (float)rnd.NextDouble() * 6f;
                c.BaseY = y;
                clouds.Add(c);
            }
        }

        void LateUpdate()
        {
            if (cam == null) return;

            // Keep the sky quad covering the whole view.
            float h = cam.orthographicSize * 2.4f;
            float w = h * cam.aspect * 1.3f;
            skyQuad.transform.localScale = new Vector3(w, 1f, h);

            skyTimer += Time.deltaTime;
            if (skyTimer > 0.05f)
            {
                skyTimer = 0f;
                if (!Approximately(curTop, tgtTop) || !Approximately(curBottom, tgtBottom))
                {
                    curTop = Color.Lerp(curTop, tgtTop, 0.08f);
                    curBottom = Color.Lerp(curBottom, tgtBottom, 0.08f);
                    TextureFactory.Gradient(curTop, curBottom, 64, skyTexture);
                    cam.backgroundColor = curTop;
                }
            }

            float time = Time.time;
            float dt = Time.deltaTime;
            for (int i = 0; i < clouds.Count; i++)
            {
                Cloud c = clouds[i];
                Vector3 p = c.T.position;
                p.x += c.Speed * dt;
                if (p.x > WrapX) p.x = -WrapX;
                p.y = c.BaseY + Mathf.Sin(time * 0.3f + c.Phase) * c.Bob;
                c.T.position = p;
            }
        }

        static bool Approximately(Color a, Color b)
        {
            return Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b) < 0.01f;
        }
    }
}
