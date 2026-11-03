using System;
using CloudGarden.Buildings;
using CloudGarden.Core;
using CloudGarden.Islands;
using UnityEngine;

namespace CloudGarden.Construction
{
    /// <summary>Tap selection of buildings and locked islands, with a glowing ring under the selection.</summary>
    public sealed class SelectionController : MonoBehaviour
    {
        public int SelectedUid { get; private set; }

        public event Action<int> SelectionChanged;      // uid, 0 when cleared
        public event Action<int> LockedIslandTapped;    // region index

        GameState state;
        WorldView world;
        CameraController camCtl;
        PlacementController placement;
        GameObject ring;

        public void Init(GameState gameState, WorldView worldView, CameraController cameraController, PlacementController placementController)
        {
            state = gameState;
            world = worldView;
            camCtl = cameraController;
            placement = placementController;
            camCtl.Tap = OnTap;

            ring = new GameObject("SelectionRing");
            ring.AddComponent<MeshFilter>().sharedMesh = MeshFactory.Torus(0.5f, 0.045f, 40, 8);
            MeshRenderer mr = ring.AddComponent<MeshRenderer>();
            mr.sharedMaterial = MaterialLibrary.Emissive(new Color(1f, 0.95f, 0.55f), 2f);
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ring.SetActive(false);

            state.BuildingRemoved += OnRemoved;
            state.BuildingChanged += OnChanged;
            state.WorldReset += Clear;
        }

        void OnDestroy()
        {
            if (state == null) return;
            state.BuildingRemoved -= OnRemoved;
            state.BuildingChanged -= OnChanged;
            state.WorldReset -= Clear;
        }

        void OnRemoved(BuildingInstance b)
        {
            if (b.Uid == SelectedUid) Clear();
        }

        void OnChanged(BuildingInstance b)
        {
            if (b.Uid == SelectedUid) UpdateRing();
        }

        void OnTap(Vector2 screen)
        {
            if (placement != null && placement.HandleTap(screen)) return;

            RaycastHit hit;
            Ray ray = camCtl.Cam.ScreenPointToRay(screen);
            if (Physics.Raycast(ray, out hit, 500f))
            {
                BuildingView v = hit.collider.GetComponentInParent<BuildingView>();
                if (v != null && v.Model != null)
                {
                    Select(v.Model.Uid);
                    return;
                }
            }

            Vector3 p;
            if (world.GroundPoint(ray, out p))
            {
                int region = world.NearestRegion(p, false);
                if (region >= 0 && !state.RegionUnlocked[region] && world.IsOverIsland(region, p, 0.5f))
                {
                    Clear();
                    if (LockedIslandTapped != null) LockedIslandTapped(region);
                    return;
                }
            }
            Clear();
        }

        public void Select(int uid)
        {
            SelectedUid = uid;
            UpdateRing();
            if (SelectionChanged != null) SelectionChanged(uid);
        }

        public void Clear()
        {
            if (SelectedUid == 0) { if (ring != null) ring.SetActive(false); return; }
            SelectedUid = 0;
            if (ring != null) ring.SetActive(false);
            if (SelectionChanged != null) SelectionChanged(0);
        }

        void UpdateRing()
        {
            BuildingInstance b = state.FindBuilding(SelectedUid);
            if (b == null) { ring.SetActive(false); return; }
            IslandView island = world.Islands[b.Region];
            float fw = b.FootprintWidth;
            float fd = b.FootprintDepth;
            ring.transform.SetParent(island.transform, false);
            ring.transform.localPosition = new Vector3(b.X + fw * 0.5f, 0.09f, b.Y + fd * 0.5f);
            float s = Mathf.Max(fw, fd) * 0.78f;
            ring.transform.localScale = new Vector3(s, 1f, s);
            ring.SetActive(true);
        }

        void Update()
        {
            if (ring != null && ring.activeSelf)
            {
                float pulse = 1f + Mathf.Sin(Time.time * 4f) * 0.04f;
                Vector3 sc = ring.transform.localScale;
                float baseS = Mathf.Max(sc.x, sc.z);
                ring.transform.Rotate(Vector3.up, 30f * Time.deltaTime, Space.Self);
            }
        }
    }
}
