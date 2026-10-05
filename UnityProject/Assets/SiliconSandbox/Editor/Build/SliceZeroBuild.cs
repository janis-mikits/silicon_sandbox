using System;
using System.IO;
using SiliconSandbox.Bootstrap;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SiliconSandbox.EditorBuild
{
    public static class SliceZeroBuild
    {
        public const string ScenePath = "Assets/SiliconSandbox/Scenes/FlatWorld.unity";
        public const string PlayableScenePath =
            "Assets/SiliconSandbox/Scenes/PlayableWorld.unity";

        public static void EnsureScene()
        {
            if (File.Exists(ScenePath))
            {
                ConfigureBuildScene();
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = FlatWorldSmoke.FloorName;
            floor.transform.position = new Vector3(16f, 0.5f, 16f);
            floor.transform.localScale = new Vector3(32, 1, 32);
            floor.AddComponent<FlatWorldSmoke>();

            var cameraObject = new GameObject("Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(15.5f, 8, 3);
            cameraObject.transform.LookAt(new Vector3(15.5f, 0, 15.5f));
            cameraObject.AddComponent<Camera>();

            var lightObject = new GameObject("Directional Light");
            lightObject.transform.rotation = Quaternion.Euler(50, -30, 0);
            lightObject.AddComponent<Light>().type = LightType.Directional;

            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new InvalidOperationException("Failed to save slice 0 smoke scene.");

            ConfigureBuildScene();
            AssetDatabase.SaveAssets();
        }

        public static void BuildPlayer()
        {
            EnsureScene();
            if (!File.Exists(PlayableScenePath))
                throw new InvalidOperationException("Playable scene is missing.");
            PlayerSettings.productName = "SiliconSandbox";
            PlayerSettings.enableFrameTimingStats = true;
            var target = EditorUserBuildSettings.activeBuildTarget;
            string destination;
            if (target == BuildTarget.StandaloneOSX)
                destination = "Builds/macOS/SiliconSandbox.app";
            else if (target == BuildTarget.StandaloneWindows64)
                destination = "Builds/Windows/SiliconSandbox.exe";
            else
                throw new InvalidOperationException("Unsupported slice 0 build target: " + target);

            Directory.CreateDirectory(Path.GetDirectoryName(destination));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { PlayableScenePath, ScenePath },
                locationPathName = destination,
                target = target,
                options = BuildOptions.None
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException("Slice 0 build failed: " + report.summary.result);

            Debug.Log("Slice 0 build succeeded: " + destination);
        }

        private static void ConfigureBuildScene()
        {
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(PlayableScenePath, true),
                new EditorBuildSettingsScene(ScenePath, true)
            };
        }
    }
}
