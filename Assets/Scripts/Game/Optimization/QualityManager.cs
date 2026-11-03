using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace CloudGarden.Optimization
{
    public enum QualityProfile
    {
        Low = 0,
        Medium = 1,
        High = 2,
        Ultra = 3
    }

    /// <summary>Four graphics profiles (Bajo/Medio/Alto/Ultra) tuned for 60 FPS on Android.</summary>
    public static class QualityManager
    {
        public static readonly string[] Names = { "Bajo", "Medio", "Alto", "Ultra" };

        public static QualityProfile Current { get; private set; }
        public static event Action Changed;

        // Values consumed by the visual systems.
        public static int CloudCount { get; private set; }
        public static float DecorDensity { get; private set; }
        public static bool Shadows { get; private set; }
        public static bool Sparkles { get; private set; }
        public static bool Smoke { get; private set; }

        const string PrefKey = "cg_quality";

        public static QualityProfile Load()
        {
            int stored = PlayerPrefs.GetInt(PrefKey, -1);
            QualityProfile p = stored >= 0 && stored <= 3 ? (QualityProfile)stored : AutoDetect();
            Apply(p, false);
            return p;
        }

        public static QualityProfile AutoDetect()
        {
            int mem = SystemInfo.systemMemorySize;
            int cores = SystemInfo.processorCount;
            if (mem >= 8000 && cores >= 8) return QualityProfile.Ultra;
            if (mem >= 5500 && cores >= 6) return QualityProfile.High;
            if (mem >= 3000) return QualityProfile.Medium;
            return QualityProfile.Low;
        }

        public static void Apply(QualityProfile p, bool save)
        {
            Current = p;
            float renderScale;
            int msaa;
            float shadowDistance;
            int cascades;
            int fps;
            switch (p)
            {
                case QualityProfile.Low:
                    renderScale = 0.7f; msaa = 1; shadowDistance = 0f; cascades = 1; fps = 45;
                    CloudCount = 8; DecorDensity = 0.35f; Shadows = false; Sparkles = false; Smoke = false;
                    break;
                case QualityProfile.Medium:
                    renderScale = 0.85f; msaa = 1; shadowDistance = 45f; cascades = 1; fps = 60;
                    CloudCount = 14; DecorDensity = 0.65f; Shadows = true; Sparkles = false; Smoke = true;
                    break;
                case QualityProfile.High:
                    renderScale = 1f; msaa = 2; shadowDistance = 65f; cascades = 1; fps = 60;
                    CloudCount = 22; DecorDensity = 1f; Shadows = true; Sparkles = true; Smoke = true;
                    break;
                default:
                    renderScale = 1f; msaa = 4; shadowDistance = 85f; cascades = 2; fps = 60;
                    CloudCount = 32; DecorDensity = 1.4f; Shadows = true; Sparkles = true; Smoke = true;
                    break;
            }

            Application.targetFrameRate = fps;
            QualitySettings.vSyncCount = 0;

            UniversalRenderPipelineAsset urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (urp != null)
            {
                urp.renderScale = renderScale;
                urp.msaaSampleCount = msaa;
                urp.shadowDistance = Mathf.Max(1f, shadowDistance);
                urp.shadowCascadeCount = cascades;
            }

            Light[] lights = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
            for (int i = 0; i < lights.Length; i++)
            {
                if (lights[i].type == LightType.Directional)
                    lights[i].shadows = Shadows ? LightShadows.Soft : LightShadows.None;
            }

            if (save)
            {
                PlayerPrefs.SetInt(PrefKey, (int)p);
                PlayerPrefs.Save();
            }
            if (Changed != null) Changed();
        }
    }
}
