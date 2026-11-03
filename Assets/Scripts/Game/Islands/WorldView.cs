using System.Collections.Generic;
using CloudGarden.Buildings;
using CloudGarden.Core;
using CloudGarden.Islands;
using UnityEngine;

namespace CloudGarden.Islands
{
    /// <summary>
    /// Keeps the 3D scene in sync with the GameState: islands, building views, tile/world conversion.
    /// The model is the single source of truth; this class only reacts to its events.
    /// </summary>
    public sealed class WorldView : MonoBehaviour
    {
        public GameState State { get; private set; }
        public IslandView[] Islands { get; private set; }

        readonly Dictionary<int, BuildingView> views = new Dictionary<int, BuildingView>();
        readonly Dictionary<int, int> shownLevels = new Dictionary<int, int>();
        WorldEnvironment env;

        public void Init(GameState state, WorldEnvironment environment)
        {
            State = state;
            env = environment;

            Islands = new IslandView[RegionDatabase.Count];
            for (int i = 0; i < Islands.Length; i++)
            {
                GameObject go = new GameObject("Island");
                go.transform.SetParent(transform, false);
                IslandView iv = go.AddComponent<IslandView>();
                iv.Init(i, RegionDatabase.Get(i));
                Islands[i] = iv;
            }

            State.StateChanged += OnStateChanged;
            State.BuildingAdded += OnBuildingAdded;
            State.BuildingRemoved += OnBuildingRemoved;
            State.BuildingChanged += OnBuildingChanged;
            State.WorldReset += RebuildAll;
            Qualitywatch();
            RebuildAll();
        }

        void Qualitywatch()
        {
            CloudGarden.Optimization.QualityManager.Changed += OnQualityChanged;
        }

        void OnQualityChanged()
        {
            RefreshIslands();
        }

        void OnDestroy()
        {
            CloudGarden.Optimization.QualityManager.Changed -= OnQualityChanged;
            if (State == null) return;
            State.StateChanged -= OnStateChanged;
            State.BuildingAdded -= OnBuildingAdded;
            State.BuildingRemoved -= OnBuildingRemoved;
            State.BuildingChanged -= OnBuildingChanged;
            State.WorldReset -= RebuildAll;
        }

        // ------------------------------------------------------------------ sync

        void OnStateChanged()
        {
            RefreshIslands();
            bool golden = State.Prestige.Upgrades[6];
            foreach (KeyValuePair<int, BuildingView> kv in views)
                if (kv.Value != null) kv.Value.Refresh(golden);
        }

        void RefreshIslands()
        {
            for (int i = 0; i < Islands.Length; i++)
                Islands[i].Refresh(State.Grids[i].Half, State.RegionUnlocked[i]);
        }

        void RebuildAll()
        {
            foreach (KeyValuePair<int, BuildingView> kv in views)
                if (kv.Value != null) Destroy(kv.Value.gameObject);
            views.Clear();
            shownLevels.Clear();
            RefreshIslands();
            for (int i = 0; i < State.Buildings.Count; i++) CreateView(State.Buildings[i], false);
        }

        void CreateView(BuildingInstance b, bool animate)
        {
            BuildingView v = BuildingView.Create(b);
            v.ApplyPlacement(Islands[b.Region].transform);
            v.Refresh(State.Prestige.Upgrades[6]);
            if (animate) v.PlaySpawn();
            views[b.Uid] = v;
            shownLevels[b.Uid] = b.Level;
        }

        void OnBuildingAdded(BuildingInstance b)
        {
            CreateView(b, true);
        }

        void OnBuildingRemoved(BuildingInstance b)
        {
            BuildingView v;
            if (views.TryGetValue(b.Uid, out v) && v != null) Destroy(v.gameObject);
            views.Remove(b.Uid);
            shownLevels.Remove(b.Uid);
        }

        void OnBuildingChanged(BuildingInstance b)
        {
            BuildingView v;
            if (!views.TryGetValue(b.Uid, out v) || v == null) return;
            v.ApplyPlacement(Islands[b.Region].transform);
            int previous;
            if (shownLevels.TryGetValue(b.Uid, out previous) && b.Level > previous) v.PlayUpgrade();
            shownLevels[b.Uid] = b.Level;
            v.Refresh(State.Prestige.Upgrades[6]);
        }

        // ------------------------------------------------------------------ queries

        public BuildingView ViewOf(int uid)
        {
            BuildingView v;
            return views.TryGetValue(uid, out v) ? v : null;
        }

        public bool GroundPoint(Ray ray, out Vector3 point)
        {
            Plane ground = new Plane(Vector3.up, Vector3.zero);
            float enter;
            if (ground.Raycast(ray, out enter))
            {
                point = ray.GetPoint(enter);
                return true;
            }
            point = Vector3.zero;
            return false;
        }

        /// <summary>Region whose island is closest to the point (optionally only unlocked ones).</summary>
        public int NearestRegion(Vector3 world, bool onlyUnlocked)
        {
            int best = -1;
            float bestDist = float.MaxValue;
            for (int i = 0; i < Islands.Length; i++)
            {
                if (onlyUnlocked && !State.RegionUnlocked[i]) continue;
                Vector3 c = Islands[i].transform.position;
                float dx = Mathf.Abs(world.x - c.x);
                float dz = Mathf.Abs(world.z - c.z);
                float d = Mathf.Max(dx, dz);
                if (d < bestDist) { bestDist = d; best = i; }
            }
            return best;
        }

        /// <summary>True when the point is over the island surface (with a margin).</summary>
        public bool IsOverIsland(int region, Vector3 world, float margin)
        {
            Vector3 c = Islands[region].transform.position;
            float e = Islands[region].Extent + margin;
            return Mathf.Abs(world.x - c.x) <= e && Mathf.Abs(world.z - c.z) <= e;
        }

        /// <summary>Tile origin so that a footprint of fw x fd is centered at the world point.</summary>
        public void WorldToTile(int region, Vector3 world, int fw, int fd, out int x, out int y)
        {
            Vector3 local = world - Islands[region].transform.position;
            x = Mathf.RoundToInt(local.x - fw * 0.5f);
            y = Mathf.RoundToInt(local.z - fd * 0.5f);
        }

        /// <summary>Area the camera may look at: islands already unlocked plus the next one.</summary>
        public Bounds FocusBounds()
        {
            int last = Mathf.Min(State.HighestUnlockedRegion() + 1, Islands.Length - 1);
            Bounds b = new Bounds(Islands[0].transform.position, Vector3.zero);
            for (int i = 0; i <= last; i++)
            {
                float e = Islands[i].Extent + 3f;
                b.Encapsulate(Islands[i].transform.position + new Vector3(e, 0, e));
                b.Encapsulate(Islands[i].transform.position - new Vector3(e, 0, e));
            }
            return b;
        }

        public void SetSkyForPoint(Vector3 world)
        {
            if (env == null) return;
            int r = NearestRegion(world, false);
            if (r < 0) return;
            RegionDef d = RegionDatabase.Get(r);
            env.SetSky(d.SkyTop, d.SkyBottom);
        }
    }
}
