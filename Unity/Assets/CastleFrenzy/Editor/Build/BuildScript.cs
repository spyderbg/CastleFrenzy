// Project setup and player builds (menu "Castle Frenzy" and batch mode entry points).
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CastleFrenzy.EditorTools
{
    public static class BuildScript
    {
        public const string MainScene = "Assets/CastleFrenzy/Scenes/Main.unity";

        [MenuItem("Castle Frenzy/Setup Project", priority = 100)]
        public static void SetupProject()
        {
            PlayerSettings.companyName = "Castle Frenzy";
            PlayerSettings.productName = "Castle Frenzy";
            PlayerSettings.defaultScreenWidth = 960;
            PlayerSettings.defaultScreenHeight = 640;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = true;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Standalone, "com.castlefrenzy.game");
            PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android, "com.castlefrenzy.game");
            EnsureMainScene();
            AssetDatabase.SaveAssets();
            Debug.Log("[CastleFrenzy] Project configured.");
        }

        public static void EnsureMainScene()
        {
            if (!File.Exists(MainScene))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(MainScene));
                Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                GameObject go = new GameObject("CastleFrenzy");
                Camera cam = go.AddComponent<Camera>();
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = Color.black;
                cam.cullingMask = 0;
                go.AddComponent<GameMain>();
                EditorSceneManager.SaveScene(scene, MainScene);
            }
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(MainScene, true) };
        }

        static string Arg(string name, string fallback)
        {
            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++) if (args[i] == name) return args[i + 1];
            return fallback;
        }

        /// <summary>Lists GameData for GameDataInstaller (Android extracts it from the APK on first launch).</summary>
        public static void WriteGameDataManifest()
        {
            string root = Path.Combine(Application.streamingAssetsPath, "GameData").Replace('\\', '/');
            if (!Directory.Exists(root)) return;
            System.Collections.Generic.List<string> files = new System.Collections.Generic.List<string>();
            foreach (string f in Directory.GetFiles(root, "*", SearchOption.AllDirectories))
            {
                string rel = f.Substring(root.Length + 1).Replace('\\', '/');
                if (rel.EndsWith(".meta") || rel == GameDataInstaller.ManifestName) continue;
                files.Add(rel);
            }
            files.Sort(System.StringComparer.Ordinal);
            string text = "# Castle Frenzy GameData manifest (written by BuildScript)\n" + string.Join("\n", files) + "\n";
            string path = root + "/" + GameDataInstaller.ManifestName;
            if (!File.Exists(path) || File.ReadAllText(path) != text) File.WriteAllText(path, text);
        }

        static void Build(BuildTarget target, BuildTargetGroup group, string defaultPath)
        {
            SetupProject();
            WriteGameDataManifest();
            if (target == BuildTarget.Android)
            {
                PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
                // -cf-android-arch arm64,x86_64 adds x86_64 (emulators, Chrome OS); release builds are ARM64 only.
                AndroidArchitecture arch = AndroidArchitecture.ARM64;
                foreach (string a in Arg("-cf-android-arch", "arm64").Split(','))
                    if (a.Trim() == "x86_64") arch |= AndroidArchitecture.X86_64;
                PlayerSettings.Android.targetArchitectures = arch;
                PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel25;
                PlayerSettings.allowedAutorotateToPortrait = false;
                PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
                PlayerSettings.allowedAutorotateToLandscapeLeft = true;
                PlayerSettings.allowedAutorotateToLandscapeRight = true;
                PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            }
            string path = Arg("-cf-output", defaultPath);
            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = new[] { MainScene },
                locationPathName = path,
                target = target,
                targetGroup = group,
                options = BuildOptions.None,
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            Debug.Log("[CastleFrenzy] Build " + report.summary.result + " -> " + path + " (" + report.summary.totalErrors + " errors)");
            if (Application.isBatchMode && report.summary.result != BuildResult.Succeeded) EditorApplication.Exit(1);
        }

        [MenuItem("Castle Frenzy/Build/Linux Player", priority = 200)]
        public static void BuildLinux() { Build(BuildTarget.StandaloneLinux64, BuildTargetGroup.Standalone, "Builds/Linux/CastleFrenzy.x86_64"); }

        [MenuItem("Castle Frenzy/Build/Windows Player", priority = 201)]
        public static void BuildWindows() { Build(BuildTarget.StandaloneWindows64, BuildTargetGroup.Standalone, "Builds/Windows/CastleFrenzy.exe"); }

        [MenuItem("Castle Frenzy/Build/Android APK", priority = 202)]
        public static void BuildAndroid() { Build(BuildTarget.Android, BuildTargetGroup.Android, "Builds/Android/CastleFrenzy.apk"); }

        /// <summary>Batch mode: compile scripts and exit (errors are reported in the log).</summary>
        public static void CompileCheck()
        {
            Debug.Log("[CastleFrenzy] Scripts compiled.");
        }
    }
}
