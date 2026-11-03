using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace CloudGarden.EditorTools
{
    /// <summary>
    /// Command-line Android build. Invoked by CI (game-ci) or manually:
    /// Unity -batchmode -quit -projectPath . -buildTarget Android -executeMethod CloudGarden.EditorTools.BuildScript.BuildAndroid
    /// Optional arguments: -customBuildPath &lt;dir&gt; -customBuildName &lt;name&gt; -buildVersion x.y.z -androidVersionCode N
    /// Release builds are signed with Unity's debug keystore unless a keystore is configured by the CI
    /// through environment variables (never committed).
    /// </summary>
    public static class BuildScript
    {
        const string ApplicationId = "com.julianfarks.cloudgardentycoon";

        static string Arg(string name, string fallback)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == name) return args[i + 1];
            return fallback;
        }

        [MenuItem("Cloud Garden/Build Android APK")]
        public static void BuildAndroid()
        {
            ProjectSetup.EnsureAll();

#if !ENABLE_LEGACY_INPUT_MANAGER
            Debug.LogError("[CloudGarden] Active Input Handling must be 'Input Manager (Old)' or 'Both'. Set it in Project Settings > Player and rebuild.");
            if (Application.isBatchMode) EditorApplication.Exit(2);
            return;
#endif

            string version = Arg("-buildVersion", Environment.GetEnvironmentVariable("BUILD_VERSION") ?? "0.1.0");
            if (string.IsNullOrWhiteSpace(version) || version == "none") version = "0.1.0";
            string versionCode = Arg("-androidVersionCode", Arg("-buildNumber", Environment.GetEnvironmentVariable("BUILD_NUMBER") ?? "1"));
            int buildNumber;
            if (!int.TryParse(versionCode, out buildNumber) || buildNumber < 1) buildNumber = 1;
            string dir = Arg("-customBuildPath", "build/Android");
            string name = Arg("-customBuildName", "CloudGardenTycoon");
            if (!name.EndsWith(".apk", StringComparison.OrdinalIgnoreCase)) name += ".apk";
            string path = Path.Combine(dir, name);
            Directory.CreateDirectory(dir);

            Configure(version, buildNumber);

            BuildPlayerOptions options = new BuildPlayerOptions();
            options.scenes = new[] { ProjectSetup.ScenePath };
            options.locationPathName = path;
            options.target = BuildTarget.Android;
            options.options = BuildOptions.None;

            Debug.Log("[CloudGarden] Building " + path + " (version " + version + ", code " + buildNumber + ")");
            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;
            if (summary.result == BuildResult.Succeeded)
            {
                long size = new FileInfo(path).Length;
                Debug.Log("[CloudGarden] Build succeeded: " + path + " (" + (size / 1024 / 1024) + " MB)");
                if (Application.isBatchMode) EditorApplication.Exit(0);
            }
            else
            {
                Debug.LogError("[CloudGarden] Build failed: " + summary.result + " (" + summary.totalErrors + " errors)");
                if (Application.isBatchMode) EditorApplication.Exit(1);
            }
        }

        static void Configure(string version, int buildNumber)
        {
            PlayerSettings.companyName = "julianfarks";
            PlayerSettings.productName = "Cloud Garden Tycoon";
            PlayerSettings.bundleVersion = version;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, ApplicationId);

            // Landscape only.
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;

            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64 | AndroidArchitecture.ARMv7;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel24;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            PlayerSettings.Android.bundleVersionCode = buildNumber;
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.Android, ManagedStrippingLevel.Low);
            PlayerSettings.stripEngineCode = true;
            PlayerSettings.gcIncremental = true;
            PlayerSettings.colorSpace = ColorSpace.Gamma;
            PlayerSettings.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);
            PlayerSettings.SetStackTraceLogType(LogType.Warning, StackTraceLogType.None);

            EditorUserBuildSettings.buildAppBundle = false;
            EditorUserBuildSettings.androidBuildType = AndroidBuildType.Release;
            EditorUserBuildSettings.development = false;

            // Optional release keystore supplied by the CI through environment variables (never in the repo).
            string keystore = Environment.GetEnvironmentVariable("ANDROID_KEYSTORE_PATH");
            string storePass = Environment.GetEnvironmentVariable("ANDROID_KEYSTORE_PASS");
            string alias = Environment.GetEnvironmentVariable("ANDROID_KEYALIAS_NAME");
            string aliasPass = Environment.GetEnvironmentVariable("ANDROID_KEYALIAS_PASS");
            if (!string.IsNullOrEmpty(keystore) && File.Exists(keystore) && !string.IsNullOrEmpty(storePass))
            {
                PlayerSettings.Android.useCustomKeystore = true;
                PlayerSettings.Android.keystoreName = keystore;
                PlayerSettings.Android.keystorePass = storePass;
                PlayerSettings.Android.keyaliasName = alias;
                PlayerSettings.Android.keyaliasPass = aliasPass;
            }
            else
            {
                PlayerSettings.Android.useCustomKeystore = false;
            }
        }
    }
}
