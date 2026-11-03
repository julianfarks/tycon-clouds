using System;
using System.IO;
using CloudGarden.Islands;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace CloudGarden.EditorTools
{
    /// <summary>
    /// One-time project configuration that cannot be committed as plain text: the URP pipeline asset,
    /// the template materials (so their shader variants ship in the build), the empty main scene and the
    /// input handling mode. It is idempotent and runs automatically when the editor loads the project.
    /// </summary>
    [InitializeOnLoad]
    public static class ProjectSetup
    {
        public const string ScenePath = "Assets/Scenes/Main.unity";
        const string SettingsDir = "Assets/Settings";
        const string RendererPath = SettingsDir + "/CG_URP_Renderer.asset";
        const string PipelinePath = SettingsDir + "/CG_URP.asset";
        const string MaterialsDir = "Assets/Resources/Materials";

        static ProjectSetup()
        {
            EditorApplication.delayCall += () =>
            {
                try { EnsureAll(); }
                catch (Exception e) { Debug.LogWarning("[CloudGarden] Project setup skipped: " + e.Message); }
            };
        }

        [MenuItem("Cloud Garden/Setup project")]
        public static void EnsureAllMenu()
        {
            EnsureAll();
            Debug.Log("[CloudGarden] Project setup complete.");
        }

        public static void EnsureAll()
        {
            EnsureRenderPipeline();
            EnsureMaterials();
            EnsureAlwaysIncludedShaders();
            EnsureInputHandling();
            EnsureScene();
            PlayerSettings.colorSpace = ColorSpace.Gamma;
            AssetDatabase.SaveAssets();
        }

        // ------------------------------------------------------------------ render pipeline

        static void EnsureRenderPipeline()
        {
            if (!AssetDatabase.IsValidFolder(SettingsDir)) AssetDatabase.CreateFolder("Assets", "Settings");

            UniversalRenderPipelineAsset asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            if (asset == null)
            {
                UniversalRendererData renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
                if (renderer == null)
                {
                    renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                    AssetDatabase.CreateAsset(renderer, RendererPath);
                }
                asset = UniversalRenderPipelineAsset.Create(renderer);
                asset.supportsHDR = false;
                asset.msaaSampleCount = 2;
                asset.shadowDistance = 60f;
                asset.useSRPBatcher = true;
                AssetDatabase.CreateAsset(asset, PipelinePath);
                AssetDatabase.SaveAssets();
            }

            if (GraphicsSettings.defaultRenderPipeline != asset) GraphicsSettings.defaultRenderPipeline = asset;
            int original = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                if (QualitySettings.renderPipeline != asset) QualitySettings.renderPipeline = asset;
            }
            QualitySettings.SetQualityLevel(original, false);
        }

        // ------------------------------------------------------------------ materials

        static void EnsureMaterials()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
            if (!AssetDatabase.IsValidFolder(MaterialsDir)) AssetDatabase.CreateFolder("Assets/Resources", "Materials");

            CreateMaterial("CG_Lit", "Universal Render Pipeline/Lit", false, false);
            CreateMaterial("CG_LitEmissive", "Universal Render Pipeline/Lit", false, true);
            CreateMaterial("CG_LitTransparent", "Universal Render Pipeline/Lit", true, false);
            CreateMaterial("CG_Unlit", "Universal Render Pipeline/Unlit", false, false);
        }

        static void CreateMaterial(string name, string shaderName, bool transparent, bool emission)
        {
            string path = MaterialsDir + "/" + name + ".mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(path) != null) return;
            Shader shader = Shader.Find(shaderName);
            if (shader == null)
            {
                Debug.LogWarning("[CloudGarden] Shader not found: " + shaderName);
                return;
            }
            Material m = new Material(shader);
            m.name = name;
            if (transparent) MaterialLibrary.MakeTransparent(m);
            if (emission)
            {
                m.EnableKeyword("_EMISSION");
                if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", Color.black);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }
            AssetDatabase.CreateAsset(m, path);
        }

        // ------------------------------------------------------------------ shaders & input

        static void EnsureAlwaysIncludedShaders()
        {
            string[] names = { "Universal Render Pipeline/Lit", "Universal Render Pipeline/Unlit", "Sprites/Default", "UI/Default" };
            UnityEngine.Object[] all = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset");
            if (all == null || all.Length == 0) return;
            SerializedObject so = new SerializedObject(all[0]);
            SerializedProperty list = so.FindProperty("m_AlwaysIncludedShaders");
            if (list == null) return;
            bool changed = false;
            for (int n = 0; n < names.Length; n++)
            {
                Shader sh = Shader.Find(names[n]);
                if (sh == null) continue;
                bool found = false;
                for (int i = 0; i < list.arraySize; i++)
                    if (list.GetArrayElementAtIndex(i).objectReferenceValue == sh) { found = true; break; }
                if (found) continue;
                list.InsertArrayElementAtIndex(list.arraySize);
                list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = sh;
                changed = true;
            }
            if (changed) so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void EnsureInputHandling()
        {
            // 0 = Input Manager (Old). The game reads touch input through UnityEngine.Input.
            UnityEngine.Object[] all = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
            if (all == null || all.Length == 0) return;
            SerializedObject so = new SerializedObject(all[0]);
            SerializedProperty p = so.FindProperty("activeInputHandler");
            if (p != null && p.intValue != 0)
            {
                p.intValue = 0;
                so.ApplyModifiedPropertiesWithoutUndo();
                Debug.Log("[CloudGarden] Active Input Handling set to 'Input Manager (Old)'. Restart the editor if prompted.");
            }
        }

        // ------------------------------------------------------------------ scene

        static void EnsureScene()
        {
            if (File.Exists(ScenePath))
            {
                SetBuildScenes();
                return;
            }
            if (!AssetDatabase.IsValidFolder("Assets/Scenes")) AssetDatabase.CreateFolder("Assets", "Scenes");
            // The scene is intentionally empty: GameBootstrap builds the world, camera and HUD from code.
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, ScenePath);
            SetBuildScenes();
        }

        static void SetBuildScenes()
        {
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }
    }
}
