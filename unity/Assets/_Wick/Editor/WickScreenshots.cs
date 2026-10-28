using System.Collections.Generic;
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

            CaptureLighting();
            CaptureEditor();
            Debug.Log("WickScreenshots: wrote " + Path.GetFullPath(OutputDir));
        }

        /// <summary>
        /// M10.2a lighting shots (docs/m10-2-lighting.md), each a real moment from a golden replay seen
        /// from the gameplay camera: the three tile states together (and with the sim's lit tiles
        /// overlaid, which must match), then each flame mood. Output: Builds/Screenshots/lighting.
        /// Batch: -executeMethod Wick.Editor.WickScreenshots.CaptureLightingOnly
        /// </summary>
        public static void CaptureLightingOnly()
        {
            CaptureLighting();
            Debug.Log("WickScreenshots: wrote " + Path.GetFullPath(OutputDir + "/lighting"));
        }

        private static void CaptureLighting()
        {
            EditorSceneManager.OpenScene("Assets/_Wick/Scenes/Raid.unity", OpenSceneMode.Single);
            var runner = Object.FindFirstObjectByType<RaidRunner>();
            var vaultView = Object.FindFirstObjectByType<VaultView>();
            var overlay = Object.FindFirstObjectByType<DevOverlay>();
            var camera = Object.FindFirstObjectByType<Camera>();
            camera.aspect = 540f / 960f;
            var mask = Object.FindAnyObjectByType<VisibilityMask>();
            var grade = Object.FindAnyObjectByType<Grade>(); // edit mode: it never enables itself, so apply it by hand
            var tuning = AssetDatabase.LoadAssetAtPath<RaidTuning>("Assets/_Wick/Data/RaidTuning.asset");
            string dir = OutputDir + "/lighting";
            QualityTier ownTier = LightingQuality.Tier;
            Directory.CreateDirectory(dir);

            // Edit mode runs no OnEnable, so route Wick's events (the flare) by hand.
            runner.SimEventRaised += e => vaultView.Wick?.OnSimEvent(e);
            bool treasureTaken = false;
            runner.SimEventRaised += e => treasureTaken |= e.Type == SimEventType.TreasureTaken;

            string golden = Path.GetFullPath(Path.Combine(Application.dataPath, "../../tests/golden"));
            void Shot(string file, string name, System.Func<RaidRunner, bool> when, bool withOverlay = false)
            {
                RaidFile raid = RaidFile.Parse(File.ReadAllText(Path.Combine(golden, file)));
                runner.LoadReplay(raid.Vault, raid.Seed, raid.Log);
                vaultView.Build(runner);
                grade?.ApplyProfile(mask.Profile);
                treasureTaken = false;
                bool found = false;
                while (true)
                {
                    vaultView.SyncAll();
                    if (when(runner))
                    {
                        found = true;
                        break;
                    }

                    if (runner.Mode != RaidMode.Replay)
                    {
                        break;
                    }

                    treasureTaken = false;
                    runner.Advance(TickAccumulator.TickSeconds);
                }

                if (!found)
                {
                    Debug.LogWarning("WickScreenshots: no moment for " + name + " in " + file);
                    return;
                }

                for (int i = 0; i < 3; i++)
                {
                    vaultView.SyncAll(); // let the flame light settle on this frame's values
                }

                RaidCamera.Place(camera.transform, RaidCamera.FocusFor(vaultView.Wick.transform.position, tuning), tuning);
                Render(camera, Path.Combine(dir, name + ".png")); // a throwaway first: batch mode's first frame can come out garbled
                Render(camera, Path.Combine(dir, name + ".png"));
                if (withOverlay)
                {
                    overlay.ShowLit = true;
                    overlay.ShowGrid = true;
                    overlay.Draw(camera);
                    Render(camera, Path.Combine(dir, name + "-overlay.png"));
                    overlay.ShowLit = false;
                    overlay.ShowGrid = false;
                }
            }

            WickState W(RaidRunner r) => r.Sim.State.Wick;
            Shot("full-heist.json", "1-tile-states", r => r.Sim.State.Tick >= 200, withOverlay: true);
            Shot("full-heist.json", "2-flame-calm", r => r.Sim.State.Tick >= 20 && W(r).Flickers == 3 && !W(r).Tiptoe);
            Shot("full-heist.json", "3-flame-flare", r => treasureTaken);
            Shot("pit-last-flicker.json", "4-flame-frantic", r => W(r).Flickers == 1 && !W(r).Falling && r.Sim.State.Tick > 10);
            Shot("troll-tiptoe.json", "5-flame-tiptoe", r => W(r).Tiptoe && W(r).Flickers == 3 && r.Sim.State.Tick > 30);
            Shot("pit-last-flicker.json", "6-flame-snuffed", r => W(r).Flickers == 0);
            Shot("pit-fall-recover.json", "7-pit-edges", r => r.Sim.State.Tick >= 1);

            // M10.2b: the same moment at each quality tier (the lit tiles must match), then the other screens.
            foreach (QualityTier tier in new[] { QualityTier.High, QualityTier.Medium, QualityTier.Low })
            {
                LightingQuality.Tier = tier;
                Shot("full-heist.json", "8-tier-" + tier.ToString().ToLowerInvariant(), r => r.Sim.State.Tick >= 200, withOverlay: true);
            }

            LightingQuality.Tier = ownTier;
            Shot("full-heist.json", "9-results-victory", r => r.Mode == RaidMode.Ended);
            grade?.ShowResult(true, instant: true);
            Render(camera, Path.Combine(dir, "9-results-victory.png"));
            grade?.ShowResult(false, instant: true);
            Render(camera, Path.Combine(dir, "9-results-defeat.png"));
            grade?.ClearResult();

            // Scout: the outer rooms' layout at Remembered brightness, defences hidden.
            VaultDef scoutVault = VaultJson.Parse(Resources.Load<TextAsset>("DevVaults/2-owl-gallery").text);
            runner.LoadLive(scoutVault, 1);
            vaultView.Build(runner);
            grade?.ApplyProfile(mask.Profile);
            vaultView.SyncAll();
            RaidCamera.Place(camera.transform, SimPose.TileCenter(new TilePos(scoutVault.Width / 2, scoutVault.Height / 2)), tuning, 0.55f);
            Render(camera, Path.Combine(dir, "10-scout.png"));
            Render(camera, Path.Combine(dir, "10-scout.png"));

            // Home: the same vault fully lit, no mask.
            mask.FullyLit = true;
            vaultView.SyncAll();
            Render(camera, Path.Combine(dir, "11-home.png"));
            mask.FullyLit = false;

            CaptureEditorPreview(dir);

            SideBySide(dir, "sheet-tiers", "8-tier-high", "8-tier-medium", "8-tier-low");
            SideBySide(dir, "sheet-tiers-overlay", "8-tier-high-overlay", "8-tier-medium-overlay", "8-tier-low-overlay");
            SideBySide(dir, "sheet-screens", "10-scout", "11-home", "9-results-victory", "9-results-defeat");
            SideBySide(dir, "sheet-editor", "20-editor", "21-editor-preview-darkness", "1-tile-states");
        }

        /// <summary>Puts shots next to each other in one PNG, left to right, for comparing.</summary>
        private static void SideBySide(string dir, string name, params string[] shots)
        {
            var images = new Texture2D[shots.Length];
            int width = 0, height = 0;
            for (int i = 0; i < shots.Length; i++)
            {
                images[i] = new Texture2D(2, 2);
                images[i].LoadImage(File.ReadAllBytes(Path.Combine(dir, shots[i] + ".png")));
                width += images[i].width;
                height = Mathf.Max(height, images[i].height);
            }

            var sheet = new Texture2D(width, height, TextureFormat.RGB24, false);
            int x = 0;
            foreach (Texture2D image in images)
            {
                sheet.SetPixels(x, 0, image.width, image.height, image.GetPixels());
                x += image.width;
                Object.DestroyImmediate(image);
            }

            sheet.Apply();
            File.WriteAllBytes(Path.Combine(dir, name + ".png"), sheet.EncodeToPNG());
            Object.DestroyImmediate(sheet);
        }

        /// <summary>The vault editor normally, then with "Preview darkness" (what a raider sees at the start).</summary>
        private static void CaptureEditorPreview(string dir)
        {
            foreach ((string name, Texture2D image) in EditorShots(540, 960))
            {
                File.WriteAllBytes(Path.Combine(dir, name + ".png"), image.EncodeToPNG());
                Object.DestroyImmediate(image);
            }
        }

        /// <summary>
        /// The vault editor (Three Halls with an owl and spikes) as builders see it, then with "Preview
        /// darkness", at the given size. Opens the Editor scene. Used by the lighting sign-off too.
        /// </summary>
        public static List<(string name, Texture2D image)> EditorShots(int width, int height)
        {
            EditorSceneManager.OpenScene("Assets/_Wick/Scenes/Editor.unity", OpenSceneMode.Single);
            var view = Object.FindAnyObjectByType<EditorView>();
            var editorCamera = Object.FindAnyObjectByType<EditorCamera>();
            var camera = editorCamera.GetComponent<Camera>();
            camera.aspect = width / (float)height;
            var assets = AssetDatabase.LoadAssetAtPath<EditorAssets>("Assets/_Wick/Data/EditorAssets.asset");
            LightingProfile lighting = assets.Registry.Theme.Lighting;
            VaultDraft draft = VaultDraft.FromVault(VaultJson.Parse(Resources.Load<TextAsset>("VaultTemplates/1-three-halls").text), "preview");
            draft.Place(ObjectType.NightOwl, new TilePos(5, 6));
            draft.Place(ObjectType.SpikeFloor, new TilePos(3, 7));
            VaultDef vault = draft.ToVaultDef();
            editorCamera.Frame(draft.Width, draft.Height);
            var shots = new List<(string, Texture2D)>();

            Shader.SetGlobalVector("_WickVisGrid", Vector4.zero);
            Shader.SetGlobalFloat("_WickLitBoost", assets.Tuning.LitBoost);
            view.Build(draft, VaultValidator.Validate(vault), EditorView.Marks.None);
            Object.DestroyImmediate(RenderImage(camera, width, height));
            shots.Add(("20-editor", RenderImage(camera, width, height)));

            var lit = new bool[vault.Width * vault.Height];
            var wick = new WickState { Tile = vault.Entrance, Flickers = SimTuning.StartFlickers, Facing = Dir.N };
            Lighting.Compute(vault, in wick, lit);
            var maskGo = new GameObject("Preview mask (capture)");
            VisibilityMask mask = maskGo.AddComponent<VisibilityMask>();
            mask.SetProfile(lighting);
            mask.ShowStill(vault, lit, lit);
            mask.SetStaticLight(StaticLights.Compute(vault, StaticLights.PlaceSconces(vault, lighting.SconceSpacingTiles), lighting.TorchRadiusTiles));
            lighting.ApplyAmbient();
            view.Build(draft, VaultValidator.Validate(vault), new EditorView.Marks { Selected = -1, Moving = -1, PreviewLit = lit, Lighting = lighting });
            Object.DestroyImmediate(RenderImage(camera, width, height));
            shots.Add(("21-editor-preview-darkness", RenderImage(camera, width, height)));
            Object.DestroyImmediate(maskGo);
            return shots;
        }

        private static Texture2D RenderImage(Camera camera, int width, int height)
        {
            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
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
            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();
            RenderTexture.active = previous;
            rt.Release();
            Object.DestroyImmediate(rt);
            return tex;
        }

        /// <summary>The vault editor with each template, plus one draft with problems shown in red.</summary>
        private static void CaptureEditor()
        {
            EditorSceneManager.OpenScene("Assets/_Wick/Scenes/Editor.unity", OpenSceneMode.Single);
            var view = Object.FindFirstObjectByType<EditorView>();
            var editorCamera = Object.FindFirstObjectByType<EditorCamera>();
            var camera = editorCamera.GetComponent<Camera>();
            camera.aspect = 540f / 960f; // render as a portrait phone, whatever the batch screen is
            var assets = AssetDatabase.LoadAssetAtPath<EditorAssets>("Assets/_Wick/Data/EditorAssets.asset");
            Shader.SetGlobalVector("_WickVisGrid", Vector4.zero);
            Shader.SetGlobalFloat("_WickLitBoost", assets.Tuning.LitBoost);

            foreach (TextAsset text in Resources.LoadAll<TextAsset>("VaultTemplates"))
            {
                VaultDraft draft = VaultDraft.FromVault(VaultJson.Parse(text.text), text.name);
                view.Build(draft, VaultValidator.Validate(draft.ToVaultDef()), EditorView.Marks.None);
                editorCamera.Frame(draft.Width, draft.Height);
                Render(camera, Path.Combine(OutputDir, "editor-" + text.name + ".png"));
            }

            // Problems in red: a drip on a wall, an owl below its level, and the owl's patrol selected.
            VaultDraft broken = VaultDraft.FromVault(VaultJson.Parse(Resources.Load<TextAsset>("VaultTemplates/1-three-halls").text), "broken");
            broken.Place(ObjectType.DripPipe, new TilePos(0, 6));
            broken.Place(ObjectType.SpikeFloor, new TilePos(4, 7));
            DraftObject owl = broken.Place(ObjectType.NightOwl, new TilePos(2, 6));
            broken.AddWaypoint(owl.Id, new TilePos(2, 6));
            broken.AddWaypoint(owl.Id, new TilePos(6, 6));
            broken.AddWaypoint(owl.Id, new TilePos(6, 8));
            broken.Place(ObjectType.Brazier, new TilePos(8, 6));
            view.Build(broken, VaultValidator.Validate(broken.ToVaultDef()), new EditorView.Marks { Selected = owl.Id, Moving = -1 });
            editorCamera.Frame(broken.Width, broken.Height);
            Render(camera, Path.Combine(OutputDir, "editor-issues.png"));
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
