using System.Collections;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Wick.Game;
using Wick.Sim;

namespace Wick.Editor
{
    /// <summary>
    /// Renders a golden raid's share clip in the Editor with the PNG stand-in encoder, to check the
    /// highlight window, camera, slow motion, watermark and colour conversion without a phone.
    /// Batch: Unity.exe -batchmode -quit -projectPath unity -executeMethod Wick.Editor.ClipPreview.Render
    /// Frames land in unity/Builds/Screenshots/clip-preview-frames.
    /// </summary>
    public static class ClipPreview
    {
        [MenuItem("Wick/Preview Share Clip")]
        public static void Render()
        {
            EditorSceneManager.OpenScene("Assets/_Wick/Scenes/Raid.unity", OpenSceneMode.Single);
            var runner = Object.FindAnyObjectByType<Wick.Game.RaidRunner>();
            var view = Object.FindAnyObjectByType<VaultView>();
            var tuning = AssetDatabase.LoadAssetAtPath<RaidTuning>("Assets/_Wick/Data/RaidTuning.asset");
            runner.Loaded += view.Build; // views subscribe in OnEnable, which doesn't run outside Play mode

            var clips = new GameObject("Clip Preview").AddComponent<ClipRenderer>();
            clips.Bind(runner, view, tuning, Object.FindAnyObjectByType<Camera>());
            RaidFile raid = RaidFile.Parse(Resources.Load<TextAsset>("DevGoldens/full-heist").text);
            string path = Path.GetFullPath("Builds/Screenshots/clip-preview.mp4");
            Directory.CreateDirectory(Path.GetDirectoryName(path));

            ClipResult result = null;
            IEnumerator steps = clips.Render(raid.Vault, raid.Seed, raid.Log, "Can you crack this vault?\nwick.uprisestudios.co.uk/v/ABCD2345",
                path, r => result = r);
            while (steps.MoveNext())
            {
                AsyncGPUReadback.WaitAllRequests();
            }

            Debug.Log("ClipPreview: " + result.Reason + ", " + result.Frames + " frames in " + result.Seconds.ToString("0.00") + " s, window " +
                result.Window.StartTick + "-" + result.Window.EndTick + " key " + result.Window.KeyTick + " score " + result.Window.Score);
            Object.DestroyImmediate(clips.gameObject);
        }
    }
}
