using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Wick.Game;

namespace Wick.Editor
{
    /// <summary>
    /// Wick > Create Boot Scene: the first scene in the build, holding only the Boot component
    /// (vault-link routing and sign-in, then Home). Batch:
    /// Unity.exe -batchmode -quit -projectPath unity -executeMethod Wick.Editor.BootSceneBuilder.Build
    /// </summary>
    public static class BootSceneBuilder
    {
        public const string ScenePath = "Assets/_Wick/Scenes/Boot.unity";

        [MenuItem("Wick/Create Boot Scene")]
        public static void Build()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("Boot").AddComponent<Boot>();
            EditorSceneManager.SaveScene(scene, ScenePath);

            var scenes = new List<EditorBuildSettingsScene> { new EditorBuildSettingsScene(ScenePath, true) };
            foreach (EditorBuildSettingsScene s in EditorBuildSettings.scenes)
            {
                if (s.path != ScenePath)
                {
                    scenes.Add(s);
                }
            }

            EditorBuildSettings.scenes = scenes.ToArray();
            AssetDatabase.SaveAssets();
            Debug.Log("BootSceneBuilder: wrote " + ScenePath + " as the first build scene");
        }
    }
}
