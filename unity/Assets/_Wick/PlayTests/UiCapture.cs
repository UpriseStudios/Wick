using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Wick.Game;
using Wick.Sim;

namespace Wick.PlayTests
{
    /// <summary>
    /// Not a test: a capture tool. Plays the Raid scene and saves the title, Home, a menu and the raid
    /// HUD (3D view and UI composited) to Builds/Screenshots/ui-*.png, so the look can be reviewed
    /// without a phone. Explicit, so it only runs when asked for:
    /// Unity.exe -batchmode -projectPath unity -runTests -testPlatform PlayMode -testFilter Wick.PlayTests.UiCapture
    /// </summary>
    [Explicit]
    public class UiCapture
    {
        private const int Width = 540, Height = 1170; // a 19.5:9 phone at half size
        private const string Out = "Builds/Screenshots";

        private Camera _camera;
        private RenderTexture _world, _ui;
        private UIDocument _doc;

        [UnityTest]
        public IEnumerator CaptureScreens()
        {
            LogAssert.ignoreFailingMessages = true; // offline: sign-in and server calls fail, which is fine here
            Directory.CreateDirectory(Out);
            SceneManager.LoadScene("Raid");
            for (int i = 0; i < 10; i++)
            {
                yield return null;
            }

            var ui = Object.FindAnyObjectByType<RaidUI>();
            var runner = Object.FindAnyObjectByType<Wick.Game.RaidRunner>();
            Setup();

            yield return Frames(20);
            Capture("ui-1-title");

            ui.HideTitle();
            ui.SetWallet(12480, 340, 85, 1240);
            ui.SetHomeStats("Vault Level 3  ·  24 ★");
            ui.SetBadge("pass", 2);
            yield return Frames(30);
            Capture("ui-2-home");

            var rows = new List<RaidUI.SheetRow>
            {
                new RaidUI.SheetRow { Text = "Vault Level 3\nNext: Level 4: 1,200 gold, 15 raid stars (you have 24), 15 min" }.Button("Start", () => { }),
                new RaidUI.SheetRow { Text = "Lantern 1\nNext: Level 2: 150 wax, needs Vault Level 4, 30 min" }.Button("Start", () => { }, false),
                new RaidUI.SheetRow { Text = "Vault Level 2 -> 3\nReady in 1 min 40 s" }.Button("Finish now (4 gems)", () => { }).Button("Watch ad: -30 min (1/3)", () => { }),
            };
            ui.ShowSheet("Upgrades", "12,480 gold  ·  340 wax  ·  85 gems  ·  24 stars", rows);
            yield return Frames(20);
            Capture("ui-3-menu");

            var tiers = new List<RaidUI.LadderTier>();
            string[] icons = { "coin", "wax", "gem", "coin", "skins", "wax", "coin", "gem", "build", "skins" };
            for (int t = 1; t <= 10; t++)
            {
                tiers.Add(new RaidUI.LadderTier
                {
                    Tier = t, FreeIcon = icons[(t + 2) % 10], FreeText = icons[(t + 2) % 10] == "skins" ? "Twist" : (t * 150).ToString("N0"),
                    PremiumIcon = icons[t - 1], PremiumText = icons[t - 1] == "skins" ? "Gilded Twist" : (t * 40).ToString("N0"),
                    Reached = t <= 4, FreeClaimed = t <= 2, ClaimFree = () => { }, ClaimPremium = () => { },
                });
            }

            ui.ShowLadder("Season 1", 4, 40, 0.62f, "620 / 1000 XP to tier 5", false, "Unlock premium  £4.99", () => { }, tiers);
            yield return Frames(30);
            Capture("ui-6-pass");

            TextAsset vault = Resources.LoadAll<TextAsset>("DevVaults")[1];
            runner.LoadLive(VaultJson.Parse(vault.text), 7);
            runner.StartClock();
            ui.ShowHud(null);
            yield return Frames(40);
            Capture("ui-4-raid");

            // Results after a live raid, with everything it can show.
            ui.ShowResults(new RaidResult(Outcome.Escaped, 1490, 0, null, 3, 260, 2), "a1b2c3d4", "a1b2c3d4", true);
            ui.SetResultRewards(260, 12, 18);
            ui.SetServerVerdict("Server check passed  ·  12,740 gold in your vault", true);
            ui.SetShare(true, true, null);
            ui.SetDoubleLoot(true);
            yield return Frames(20);
            Capture("ui-5-results");

            // The vault editor, in its own scene.
            SceneManager.LoadScene("Editor");
            yield return Frames(30);
            Setup(Object.FindAnyObjectByType<VaultEditorController>().GetComponentInChildren<UIDocument>() ??
                Object.FindAnyObjectByType<UIDocument>());
            yield return Frames(30);
            Capture("ui-7-editor");

            Object.DestroyImmediate(_world);
            Object.DestroyImmediate(_ui);
        }

        private void Setup(UIDocument doc = null)
        {
            _camera = Camera.main;
            _world = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
            _ui = new RenderTexture(Width, Height, 0, RenderTextureFormat.ARGB32);
            _camera.targetTexture = _world;
            _camera.aspect = Width / (float)Height;
            _doc = doc != null ? doc : Object.FindAnyObjectByType<RaidUI>().GetComponent<UIDocument>();
            PanelSettings copy = Object.Instantiate(_doc.panelSettings); // never touch the asset
            copy.targetTexture = _ui;
            copy.clearColor = true;
            copy.colorClearValue = Color.clear;
            _doc.panelSettings = copy;
        }

        private static IEnumerator Frames(int n)
        {
            for (int i = 0; i < n; i++)
            {
                yield return null; // WaitForEndOfFrame never fires in batch mode
            }
        }

        /// <summary>The UI over the 3D view, saved as a PNG.</summary>
        private void Capture(string name)
        {
            Texture2D world = Read(_world), ui = Read(_ui);
            Color[] w = world.GetPixels(), u = ui.GetPixels();
            for (int i = 0; i < w.Length; i++)
            {
                w[i] = Color.Lerp(w[i], new Color(u[i].r, u[i].g, u[i].b, 1f), u[i].a);
            }

            world.SetPixels(w);
            File.WriteAllBytes(Path.Combine(Out, name + ".png"), world.EncodeToPNG());
            Object.DestroyImmediate(world);
            Object.DestroyImmediate(ui);
        }

        private static Texture2D Read(RenderTexture rt)
        {
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = previous;
            return tex;
        }
    }
}
