using System;
using CloudGarden.Construction;
using CloudGarden.Core;
using CloudGarden.Islands;
using CloudGarden.OfflineProduction;
using CloudGarden.Optimization;
using CloudGarden.SaveSystem;
using CloudGarden.UI;
using UnityEngine;

namespace CloudGarden.Bootstrap
{
    /// <summary>
    /// Composition root: loads the save, applies offline production, builds the world and the HUD and runs
    /// the fixed-step simulation. Saves automatically and whenever the app goes to the background.
    /// </summary>
    public sealed class GameController : MonoBehaviour
    {
        const float SimStep = 0.1f;
        const float AutosaveSeconds = 15f;

        GameState state;
        SaveManager saves;
        WorldView world;
        CameraController cameraController;
        PlacementController placement;
        SelectionController selection;
        HudController hud;
        float simAccumulator;
        float autosaveTimer;
        bool booted;

        public GameState State { get { return state; } }

        void Start()
        {
            Boot();
        }

        void Boot()
        {
            Application.runInBackground = false;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            Screen.autorotateToPortrait = false;
            Screen.autorotateToPortraitUpsideDown = false;
            Screen.autorotateToLandscapeLeft = true;
            Screen.autorotateToLandscapeRight = true;
            Screen.orientation = ScreenOrientation.AutoRotation;

            QualityManager.Load();

            saves = SaveManager.CreateDefault();
            state = new GameState();

            long now = DateTime.UtcNow.Ticks;
            LoadSource source;
            SaveData data = saves.Load(out source);
            OfflineReport offline = null;
            bool newGame = data == null;
            if (data != null)
            {
                SaveMapper.Apply(data, state);
                offline = OfflineSimulator.Apply(state, now);
            }
            else
            {
                state.LastSaveUtcTicks = now;
                state.MaxSeenUtcTicks = now;
            }

            Camera cam = Camera.main;
            if (cam == null)
            {
                GameObject camObj = new GameObject("Main Camera");
                camObj.tag = "MainCamera";
                cam = camObj.AddComponent<Camera>();
                camObj.AddComponent<AudioListener>();
            }
            cam.allowHDR = false;

            GameObject envObj = new GameObject("Environment");
            WorldEnvironment env = envObj.AddComponent<WorldEnvironment>();
            env.Init(cam);

            GameObject worldObj = new GameObject("World");
            world = worldObj.AddComponent<WorldView>();
            world.Init(state, env);

            cameraController = new GameObject("CameraController").AddComponent<CameraController>();
            cameraController.Init(cam, world);
            cameraController.SnapTo(world.Islands[0].transform.position, 9f);

            placement = new GameObject("Placement").AddComponent<PlacementController>();
            placement.Init(state, world, cameraController);

            selection = new GameObject("Selection").AddComponent<SelectionController>();
            selection.Init(state, world, cameraController, placement);

            UiContext ctx = new UiContext();
            ctx.State = state;
            ctx.World = world;
            ctx.Camera = cameraController;
            ctx.Placement = placement;
            ctx.Selection = selection;
            ctx.SaveNow = SaveNow;
            ctx.ResetGame = ResetGame;
            hud = new GameObject("HUD").AddComponent<HudController>();
            hud.Init(ctx);

            QualityManager.Apply(QualityManager.Current, false);

            if (source == LoadSource.Backup1 || source == LoadSource.Backup2)
                hud.ShowToast("Se recuperó una copia de seguridad de tu partida");
            if (newGame)
            {
                hud.ShowToast("¡Bienvenido! Toca Recolectar, vende el rocío y construye tu primer Recolector.");
            }
            else if (offline != null)
            {
                hud.ShowOfflineReport(offline);
            }

            booted = true;
            SaveNow();
        }

        // ------------------------------------------------------------------ simulation loop

        void Update()
        {
            if (!booted) return;
            simAccumulator += Mathf.Min(Time.deltaTime, 0.5f);
            int guard = 0;
            while (simAccumulator >= SimStep && guard++ < 8)
            {
                state.Tick(SimStep);
                simAccumulator -= SimStep;
            }
            if (simAccumulator > SimStep * 8) simAccumulator = 0f;

            autosaveTimer += Time.unscaledDeltaTime;
            if (autosaveTimer >= AutosaveSeconds)
            {
                autosaveTimer = 0f;
                SaveNow();
            }
        }

        // ------------------------------------------------------------------ persistence

        public void SaveNow()
        {
            if (!booted || state == null) return;
            long now = DateTime.UtcNow.Ticks;
            SaveData d = SaveMapper.ToData(state, now);
            d.QualityLevel = (int)QualityManager.Current;
            if (saves.Save(d))
            {
                state.LastSaveUtcTicks = now;
                if (now > state.MaxSeenUtcTicks) state.MaxSeenUtcTicks = now;
            }
        }

        public void ResetGame()
        {
            saves.DeleteAll();
            state.HardReset();
            long now = DateTime.UtcNow.Ticks;
            state.LastSaveUtcTicks = now;
            state.MaxSeenUtcTicks = now;
            cameraController.SnapTo(world.Islands[0].transform.position, 9f);
            SaveNow();
            hud.ShowToast("Partida reiniciada");
        }

        void OnApplicationPause(bool paused)
        {
            if (!booted) return;
            if (paused)
            {
                SaveNow();
            }
            else
            {
                // Coming back: credit the time spent in the background (limited and clock-protected).
                OfflineReport r = OfflineSimulator.Apply(state, DateTime.UtcNow.Ticks);
                if (r.HasGains) hud.ShowOfflineReport(r);
            }
        }

        void OnApplicationQuit()
        {
            if (booted) SaveNow();
        }
    }
}
