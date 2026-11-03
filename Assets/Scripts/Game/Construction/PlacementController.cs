using System;
using CloudGarden.Buildings;
using CloudGarden.Core;
using CloudGarden.Economy;
using CloudGarden.Islands;
using UnityEngine;

namespace CloudGarden.Construction
{
    /// <summary>
    /// Build/move mode. A translucent ghost shows where the building will be; it turns green when the
    /// spot is valid and red when it overlaps something. Drag the ghost or tap a tile to place it.
    /// </summary>
    public sealed class PlacementController : MonoBehaviour
    {
        public enum PlacementMode { None, Place, Move }

        public PlacementMode Mode { get; private set; }
        public BuildingDef Def { get; private set; }
        public int Region { get; private set; }
        public int TileX { get; private set; }
        public int TileY { get; private set; }
        public int Rotation { get; private set; }
        public bool IsValid { get; private set; }
        public bool CanAfford { get; private set; }
        public PlaceResult LastCheck { get; private set; }
        public bool Active { get { return Mode != PlacementMode.None; } }

        public event Action Changed;

        GameState state;
        WorldView world;
        CameraController camCtl;
        GameObject ghost;
        GameObject footprint;
        Renderer[] ghostRenderers;
        int movingUid;
        int lastVersion = -1;
        Vector3 dragOffset;

        public void Init(GameState gameState, WorldView worldView, CameraController cameraController)
        {
            state = gameState;
            world = worldView;
            camCtl = cameraController;
            camCtl.DragCapture = CaptureDrag;
            camCtl.CapturedDrag = OnDrag;
            camCtl.CapturedDragEnd = OnDragEnd;
        }

        // ------------------------------------------------------------------ begin / end

        public void BeginPlace(string defId)
        {
            BuildingDef d = BuildingDatabase.Get(defId);
            if (d == null) return;
            Cancel();
            Mode = PlacementMode.Place;
            Def = d;
            movingUid = 0;
            Rotation = 0;
            StartAtScreenCenter();
            BuildGhost();
            Revalidate();
        }

        public void BeginMove(int uid)
        {
            BuildingInstance b = state.FindBuilding(uid);
            if (b == null) return;
            Cancel();
            Mode = PlacementMode.Move;
            Def = b.Def;
            movingUid = uid;
            Rotation = b.Rotation;
            Region = b.Region;
            TileX = b.X;
            TileY = b.Y;
            BuildGhost();
            Revalidate();
            SetGrid(true);
        }

        public void Cancel()
        {
            if (Mode == PlacementMode.None) return;
            Mode = PlacementMode.None;
            DestroyGhost();
            SetGridAll(false);
            RaiseChanged();
        }

        void StartAtScreenCenter()
        {
            Vector3 p;
            Camera cam = camCtl.Cam;
            if (!world.GroundPoint(cam.ScreenPointToRay(new Vector2(Screen.width * 0.5f, Screen.height * 0.5f)), out p)) p = Vector3.zero;
            int region = world.NearestRegion(p, true);
            if (region < 0) region = 0;
            Region = region;
            int w, h;
            GameState.Footprint(Def, Rotation, out w, out h);
            int x, y;
            world.WorldToTile(region, p, w, h, out x, out y);
            TileX = x;
            TileY = y;
            SetGrid(true);
        }

        // ------------------------------------------------------------------ ghost

        void BuildGhost()
        {
            DestroyGhost();
            ghost = BuildingVisualFactory.Create(Def);
            Collider col = ghost.GetComponent<Collider>();
            if (col != null) Destroy(col);
            BuildingAnimator anim = ghost.GetComponent<BuildingAnimator>();
            if (anim != null) anim.enabled = false;
            for (int i = ghost.transform.childCount - 1; i >= 0; i--)
            {
                Transform c = ghost.transform.GetChild(i);
                if (c.name.StartsWith("Lv")) Destroy(c.gameObject);
            }
            ghostRenderers = ghost.GetComponentsInChildren<Renderer>();
            for (int i = 0; i < ghostRenderers.Length; i++)
                ghostRenderers[i].shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            footprint = new GameObject("Footprint");
            footprint.AddComponent<MeshFilter>();
            footprint.AddComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ApplyTransform();
        }

        void DestroyGhost()
        {
            if (ghost != null) Destroy(ghost);
            if (footprint != null) Destroy(footprint);
            ghost = null;
            footprint = null;
            ghostRenderers = null;
        }

        void ApplyTransform()
        {
            if (ghost == null) return;
            int w, h;
            GameState.Footprint(Def, Rotation, out w, out h);
            IslandView island = world.Islands[Region];
            ghost.transform.SetParent(island.transform, false);
            ghost.transform.localPosition = new Vector3(TileX + w * 0.5f, 0.06f, TileY + h * 0.5f);
            ghost.transform.localRotation = Quaternion.Euler(0f, Rotation * 90f, 0f);

            footprint.transform.SetParent(island.transform, false);
            footprint.transform.localPosition = new Vector3(TileX + w * 0.5f, 0.05f, TileY + h * 0.5f);
            footprint.GetComponent<MeshFilter>().sharedMesh = MeshFactory.Quad(w - 0.06f, h - 0.06f);
        }

        void UpdateGhostColor()
        {
            if (ghostRenderers == null) return;
            Color c = IsValid ? new Color(0.35f, 1f, 0.5f, 0.55f) : new Color(1f, 0.3f, 0.3f, 0.55f);
            Material m = MaterialLibrary.Glass(c);
            for (int i = 0; i < ghostRenderers.Length; i++)
                if (ghostRenderers[i] != null) ghostRenderers[i].sharedMaterial = m;
            if (footprint != null)
                footprint.GetComponent<MeshRenderer>().sharedMaterial = MaterialLibrary.Glass(IsValid ? new Color(0.3f, 1f, 0.5f, 0.55f) : new Color(1f, 0.25f, 0.25f, 0.55f));
        }

        // ------------------------------------------------------------------ validation

        void Revalidate()
        {
            LastCheck = state.CheckPlacement(Def, Region, TileX, TileY, Rotation, Mode == PlacementMode.Move ? movingUid : 0);
            IsValid = LastCheck == PlaceResult.Ok;
            CanAfford = Mode == PlacementMode.Move || state.CanAffordBuild(Def);
            lastVersion = state.Version;
            ApplyTransform();
            UpdateGhostColor();
            RaiseChanged();
        }

        public double CurrentCost { get { return Mode == PlacementMode.Place && Def != null ? state.BuildCoinCost(Def) : 0; } }

        public string StatusText()
        {
            if (!Active) return "";
            switch (LastCheck)
            {
                case PlaceResult.Ok:
                    if (!CanAfford) return "Recursos insuficientes";
                    return Mode == PlacementMode.Move ? "Listo para mover" : "Listo para construir";
                case PlaceResult.Overlap: return "Espacio ocupado";
                case PlaceResult.OutOfBounds: return "Fuera de la isla";
                case PlaceResult.MaxCount: return "Límite alcanzado";
                case PlaceResult.Locked: return "Edificio bloqueado";
                case PlaceResult.RegionLocked: return "Región bloqueada";
                default: return "No se puede colocar aquí";
            }
        }

        // ------------------------------------------------------------------ actions

        public void Rotate()
        {
            if (!Active) return;
            Rotation = (Rotation + 1) & 3;
            // Keep the footprint centered while rotating.
            Revalidate();
        }

        public void Confirm()
        {
            if (!Active) return;
            if (!IsValid)
            {
                state.RaiseToast(StatusText());
                return;
            }
            if (Mode == PlacementMode.Place)
            {
                if (!CanAfford)
                {
                    state.RaiseToast("Recursos insuficientes");
                    return;
                }
                BuildingInstance created;
                PlaceResult r = state.TryBuild(Def.Id, Region, TileX, TileY, Rotation, out created);
                if (r != PlaceResult.Ok)
                {
                    state.RaiseToast("No se pudo construir");
                    Revalidate();
                    return;
                }
                // Stay in build mode for repeated construction while the player can pay for more.
                if (Def.MaxCount > 0 && state.CountOf(Def.Id) >= Def.MaxCount) Cancel();
                else Revalidate();
            }
            else
            {
                PlaceResult r = state.TryMove(movingUid, Region, TileX, TileY, Rotation);
                if (r == PlaceResult.Ok) Cancel();
                else Revalidate();
            }
        }

        // ------------------------------------------------------------------ input

        bool CaptureDrag(Vector2 screen)
        {
            if (!Active || ghost == null) return false;
            Vector3 p;
            if (!world.GroundPoint(camCtl.Cam.ScreenPointToRay(screen), out p)) return false;
            Vector3 local = p - world.Islands[Region].transform.position;
            int w, h;
            GameState.Footprint(Def, Rotation, out w, out h);
            bool inside = local.x >= TileX - 0.3f && local.x <= TileX + w + 0.3f && local.z >= TileY - 0.3f && local.z <= TileY + h + 0.3f;
            if (inside)
            {
                dragOffset = new Vector3(TileX + w * 0.5f, 0, TileY + h * 0.5f) - local;
                dragOffset.y = 0;
            }
            return inside;
        }

        void OnDrag(Vector2 screen)
        {
            MoveGhostTo(screen, true);
        }

        void OnDragEnd(Vector2 screen)
        {
            MoveGhostTo(screen, true);
        }

        /// <summary>Called for taps on the world while building: jump the ghost to the tapped tile.</summary>
        public bool HandleTap(Vector2 screen)
        {
            if (!Active) return false;
            MoveGhostTo(screen, false);
            return true;
        }

        void MoveGhostTo(Vector2 screen, bool useOffset)
        {
            Vector3 p;
            if (!world.GroundPoint(camCtl.Cam.ScreenPointToRay(screen), out p)) return;
            int region = world.NearestRegion(p, true);
            if (region < 0) return;
            if (!world.IsOverIsland(region, p, 1.5f)) return;

            Vector3 local = p - world.Islands[region].transform.position;
            if (useOffset && region == Region) local += dragOffset;
            int w, h;
            GameState.Footprint(Def, Rotation, out w, out h);
            int x = Mathf.RoundToInt(local.x - w * 0.5f);
            int y = Mathf.RoundToInt(local.z - h * 0.5f);
            if (region == Region && x == TileX && y == TileY) return;
            if (region != Region) { SetGridAll(false); Region = region; SetGrid(true); }
            TileX = x;
            TileY = y;
            Revalidate();
        }

        void SetGrid(bool visible)
        {
            SetGridAll(false);
            if (visible && Region >= 0 && Region < world.Islands.Length) world.Islands[Region].SetGridVisible(true);
        }

        void SetGridAll(bool visible)
        {
            for (int i = 0; i < world.Islands.Length; i++) world.Islands[i].SetGridVisible(visible);
        }

        void Update()
        {
            if (!Active) return;
            if (state.Version != lastVersion) Revalidate();
            if (ghost != null)
            {
                float bob = 0.06f + Mathf.Abs(Mathf.Sin(Time.time * 3f)) * 0.08f;
                Vector3 pos = ghost.transform.localPosition;
                pos.y = bob;
                ghost.transform.localPosition = pos;
            }
        }

        void RaiseChanged()
        {
            if (Changed != null) Changed();
        }
    }
}
