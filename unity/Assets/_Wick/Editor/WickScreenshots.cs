using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Wick.Game;
using Wick.Sim;
using RaidRunner = Wick.Game.RaidRunner;

namespace Wick.Editor
{
    /// <summary>
    /// Renders each dev vault from the Raid scene to PNGs without entering Play mode: the scout
    /// view, the live view at the start of the raid, and the live view with the lit-tile overlay.
    /// Lets the rendering be checked from batch mode. Output: Builds/Screenshots.
    /// Batch: Unity.exe -batchmode -quit -projectPath unity -executeMethod Wick.Editor.WickScreenshots.Capture
    /// </summary>
    public static class WickScreenshots
    {
        private const string OutputDir = "Builds/Screenshots";

        [MenuItem("Wick/Capture Dev Screenshots")]
        public static void Capture()
        {
            EditorSceneManager.OpenScene("Assets/_Wick/Scenes/Raid.unity", OpenSceneMode.Single);
            var runner = Object.FindFirstObjectByType<RaidRunner>();
            var vaultView = Object.FindFirstObjectByType<VaultView>();
            var mask = Object.FindFirstObjectByType<VisibilityMask>();
            var overlay = Object.FindFirstObjectByType<DevOverlay>();
            var camera = Object.FindFirstObjectByType<Camera>();
            var tuning = AssetDatabase.LoadAssetAtPath<RaidTuning>("Assets/_Wick/Data/RaidTuning.asset");
            Directory.CreateDirectory(OutputDir);

            foreach (TextAsset text in Resources.LoadAll<TextAsset>("DevVaults"))
            {
                VaultDef vault = VaultJson.Parse(text.text);
                runner.LoadLive(vault, 1);
                vaultView.Build(runner);

                // Scout: the whole vault, hoard dark.
                vaultView.SyncAll();
                Vector3 centre = SimPose.TileCenter(new TilePos(vault.Width / 2, vault.Height / 2));
                RaidCamera.Place(camera.transform, centre, tuning, 0.55f);
                Render(camera, Path.Combine(OutputDir, text.name + "-1-scout.png"));

                // Live: only Wick's light.
                runner.StartClock();
                runner.Advance(TickAccumulator.TickSeconds);
                vaultView.SyncAll();
                RaidCamera.Place(camera.transform, RaidCamera.FocusFor(vaultView.Wick.transform.position, tuning), tuning);
                Render(camera, Path.Combine(OutputDir, text.name + "-2-live.png"));

                // Live with the sim's lit tiles overlaid: the tint should cover exactly the lit area.
                overlay.ShowLit = true;
                overlay.ShowGrid = true;
                overlay.Draw(camera);
                Render(camera, Path.Combine(OutputDir, text.name + "-3-overlay.png"));
                overlay.ShowLit = false;
                overlay.ShowGrid = false;
            }

            Debug.Log("WickScreenshots: wrote " + Path.GetFullPath(OutputDir));
        }

        private static void Render(Camera camera, string path)
        {
            var rt = new RenderTexture(540, 960, 24, RenderTextureFormat.ARGB32);
            var request = new RenderPipeline.StandardRequest { destination = rt };
            if (RenderPipeline.SupportsRenderRequest(camera, request))
            {
                RenderPipeline.SubmitRenderRequest(camera, request);
            }
            else
            {
                camera.targetTexture = rt;
                camera.Render();
                camera.targetTexture = null;
            }

            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = previous;
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            rt.Release();
            Object.DestroyImmediate(rt);
        }
    }
}
