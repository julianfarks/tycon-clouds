using System.Collections.Generic;
using UnityEngine;

namespace CloudGarden.Islands
{
    /// <summary>
    /// Central updater for cheap ambient animation (swaying vegetation, scrolling water). One Update loop
    /// for hundreds of objects keeps the cost low.
    /// </summary>
    public sealed class AmbientMotion : MonoBehaviour
    {
        struct SwayItem
        {
            public Transform T;
            public Quaternion BaseRot;
            public float Amp;
            public float Speed;
            public float Phase;
        }

        struct ScrollItem
        {
            public Material M;
            public Vector2 Velocity;
            public string Prop;
        }

        static AmbientMotion instance;
        readonly List<SwayItem> sways = new List<SwayItem>();
        readonly List<ScrollItem> scrolls = new List<ScrollItem>();
        float cleanTimer;

        static AmbientMotion Instance
        {
            get
            {
                if (instance == null)
                {
                    GameObject go = new GameObject("AmbientMotion");
                    instance = go.AddComponent<AmbientMotion>();
                }
                return instance;
            }
        }

        public static void AddSway(Transform t, float amplitudeDegrees, float speed)
        {
            SwayItem item = new SwayItem();
            item.T = t;
            item.BaseRot = t.localRotation;
            item.Amp = amplitudeDegrees;
            item.Speed = speed;
            item.Phase = Random.value * 6.28f;
            Instance.sways.Add(item);
        }

        public static void AddScroll(Material m, Vector2 velocity)
        {
            ScrollItem item = new ScrollItem();
            item.M = m;
            item.Velocity = velocity;
            item.Prop = m.HasProperty("_BaseMap") ? "_BaseMap" : "_MainTex";
            Instance.scrolls.Add(item);
        }

        void Update()
        {
            float time = Time.time;
            for (int i = 0; i < sways.Count; i++)
            {
                SwayItem s = sways[i];
                if (s.T == null) continue;
                float a = Mathf.Sin(time * s.Speed + s.Phase) * s.Amp;
                float b = Mathf.Cos(time * s.Speed * 0.8f + s.Phase) * s.Amp * 0.7f;
                s.T.localRotation = s.BaseRot * Quaternion.Euler(a, 0f, b);
            }
            for (int i = 0; i < scrolls.Count; i++)
            {
                ScrollItem sc = scrolls[i];
                if (sc.M == null) continue;
                Vector2 off = sc.Velocity * time;
                off.x = Mathf.Repeat(off.x, 1f);
                off.y = Mathf.Repeat(off.y, 1f);
                sc.M.SetTextureOffset(sc.Prop, off);
            }

            cleanTimer += Time.deltaTime;
            if (cleanTimer > 5f)
            {
                cleanTimer = 0f;
                sways.RemoveAll(x => x.T == null);
                scrolls.RemoveAll(x => x.M == null);
            }
        }
    }
}
