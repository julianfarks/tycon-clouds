using CloudGarden.Buildings;
using UnityEngine;

namespace CloudGarden.Islands
{
    /// <summary>Scene representation of one BuildingInstance: model, animation state and visual level.</summary>
    public sealed class BuildingView : MonoBehaviour
    {
        public BuildingInstance Model;
        public BuildingDef Def;

        BuildingAnimator anim;
        GameObject lv1, lv2, lv3, lv3Gold;
        GameObject statusOrb;
        Renderer orbRenderer;
        float orbHeight = 2f;
        int shownTier = -1;
        bool shownGolden;
        BuildingStatus shownStatus = BuildingStatus.Passive;
        float nextStatusCheck;
        float spawnTimer = -1f;
        float popTimer = -1f;
        float baseScale = 1f;

        public static BuildingView Create(BuildingInstance model)
        {
            GameObject root = BuildingVisualFactory.Create(model.Def);
            BuildingView v = root.AddComponent<BuildingView>();
            v.Model = model;
            v.Def = model.Def;
            v.anim = root.GetComponent<BuildingAnimator>();
            v.lv1 = Find(root, BuildingVisualFactory.GroupLv1);
            v.lv2 = Find(root, BuildingVisualFactory.GroupLv2);
            v.lv3 = Find(root, BuildingVisualFactory.GroupLv3);
            v.lv3Gold = Find(root, BuildingVisualFactory.GroupLv3Gold);
            v.orbHeight = BuildingVisualFactory.HeightOf(root) + 0.45f;
            v.BuildStatusOrb();
            return v;
        }

        static GameObject Find(GameObject root, string name)
        {
            Transform t = root.transform.Find(name);
            return t != null ? t.gameObject : null;
        }

        void BuildStatusOrb()
        {
            statusOrb = new GameObject("StatusOrb");
            statusOrb.transform.SetParent(transform, false);
            statusOrb.transform.localPosition = new Vector3(0, orbHeight, 0);
            statusOrb.transform.localScale = Vector3.one * 0.32f;
            statusOrb.AddComponent<MeshFilter>().sharedMesh = MeshFactory.Sphere();
            orbRenderer = statusOrb.AddComponent<MeshRenderer>();
            orbRenderer.sharedMaterial = MaterialLibrary.Emissive(new Color(1f, 0.8f, 0.2f), 2f);
            orbRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            statusOrb.SetActive(false);
        }

        /// <summary>Positions the building on the island according to the model.</summary>
        public void ApplyPlacement(Transform island)
        {
            transform.SetParent(island, false);
            float fw = Model.FootprintWidth;
            float fd = Model.FootprintDepth;
            transform.localPosition = new Vector3(Model.X + fw * 0.5f, 0f, Model.Y + fd * 0.5f);
            transform.localRotation = Quaternion.Euler(0f, Model.Rotation * 90f, 0f);
        }

        public void Refresh(bool goldenCosmetic)
        {
            int tier = LevelMath.VisualTier(Model.Level);
            if (tier == shownTier && goldenCosmetic == shownGolden) return;
            shownTier = tier;
            shownGolden = goldenCosmetic;
            if (lv1 != null) lv1.SetActive(tier >= 1);
            if (lv2 != null) lv2.SetActive(tier >= 2);
            if (lv3 != null) lv3.SetActive(tier >= 3 && !goldenCosmetic);
            if (lv3Gold != null) lv3Gold.SetActive(tier >= 3 && goldenCosmetic);
            baseScale = tier >= 3 ? 1.05f : 1f;
            if (spawnTimer < 0f && popTimer < 0f) transform.localScale = Vector3.one * baseScale;
        }

        public void PlaySpawn()
        {
            spawnTimer = 0f;
            transform.localScale = Vector3.one * 0.01f;
        }

        public void PlayUpgrade()
        {
            popTimer = 0f;
        }

        static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            float p = t - 1f;
            return 1f + c3 * p * p * p + c1 * p * p;
        }

        void Update()
        {
            float dt = Time.deltaTime;

            if (spawnTimer >= 0f)
            {
                spawnTimer += dt;
                float t = Mathf.Clamp01(spawnTimer / 0.55f);
                transform.localScale = Vector3.one * (Mathf.Max(0.01f, EaseOutBack(t)) * baseScale);
                if (t >= 1f)
                {
                    spawnTimer = -1f;
                    transform.localScale = Vector3.one * baseScale;
                }
            }
            else if (popTimer >= 0f)
            {
                popTimer += dt;
                float t = Mathf.Clamp01(popTimer / 0.4f);
                transform.localScale = Vector3.one * baseScale * (1f + Mathf.Sin(t * Mathf.PI) * 0.16f);
                if (t >= 1f)
                {
                    popTimer = -1f;
                    transform.localScale = Vector3.one * baseScale;
                }
            }

            if (Model == null) return;
            if (anim != null) anim.Activity = Mathf.MoveTowards(anim.Activity, (float)Model.Activity, dt * 2f);

            nextStatusCheck -= dt;
            if (nextStatusCheck <= 0f)
            {
                nextStatusCheck = 0.3f;
                UpdateStatusOrb();
            }
            if (statusOrb != null && statusOrb.activeSelf)
            {
                float s = 0.3f + Mathf.Sin(Time.time * 5f) * 0.05f;
                statusOrb.transform.localScale = Vector3.one * s;
            }
        }

        void UpdateStatusOrb()
        {
            if (statusOrb == null) return;
            BuildingStatus st = Model.Status;
            if (st == shownStatus) return;
            shownStatus = st;
            if (st == BuildingStatus.MissingInput)
            {
                statusOrb.SetActive(true);
                orbRenderer.sharedMaterial = MaterialLibrary.Emissive(new Color(1f, 0.8f, 0.2f), 2f);
            }
            else if (st == BuildingStatus.StorageFull)
            {
                statusOrb.SetActive(true);
                orbRenderer.sharedMaterial = MaterialLibrary.Emissive(new Color(1f, 0.3f, 0.3f), 2f);
            }
            else
            {
                statusOrb.SetActive(false);
            }
        }
    }
}
