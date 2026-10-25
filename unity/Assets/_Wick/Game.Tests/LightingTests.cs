using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Wick.Sim;
using Wick.Sim.Tests;

namespace Wick.Game.Tests
{
    /// <summary>
    /// M10.2a (docs/m10-2-lighting.md): the three tile states follow Wick.Sim's lit set exactly,
    /// Remembered is the history of that set (identical after a replay seek), the scout's view is
    /// temporary, Wick's light matches the sim's radius, the static pools are deterministic, and the
    /// always-readable glows stay visible on unlit tiles unless their switch is off.
    /// </summary>
    public class LightingTests
    {
        private readonly List<Object> _spawned = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (Object o in _spawned)
            {
                Object.DestroyImmediate(o);
            }

            _spawned.Clear();
        }

        // ---- Tile states --------------------------------------------------------------------

        [TestCaseSource(typeof(GoldenReplayTests), nameof(GoldenReplayTests.GoldenFiles))]
        public void Remembered_IsEveryTileLitSoFar_AndLitWinsOverIt(string file)
        {
            RaidFile raid = Golden(file);
            (RaidRunner runner, VisibilityMask mask) = Rig(LightingProfile.Default());
            runner.LoadReplay(raid.Vault, raid.Seed, raid.Log);
            var everLit = new bool[raid.Vault.Width * raid.Vault.Height];
            while (true)
            {
                mask.Refresh();
                for (int i = 0; i < everLit.Length; i++)
                {
                    var p = new TilePos(i % raid.Vault.Width, i / raid.Vault.Width);
                    bool lit = runner.Sim.IsLit(p);
                    everLit[i] |= lit;
                    if (mask.IsVisible(p) != lit || mask.IsSeen(p) != everLit[i])
                    {
                        Assert.Fail(file + " tick " + runner.Sim.State.Tick + " tile " + p + ": lit " + mask.IsVisible(p) + "/" + lit +
                            ", seen " + mask.IsSeen(p) + "/" + everLit[i]);
                    }
                }

                if (runner.Mode != RaidMode.Replay)
                {
                    break;
                }

                runner.Advance(TickAccumulator.TickSeconds);
            }
        }

        [Test]
        public void AReplaySeek_RemembersExactlyWhatPlayingThroughDid()
        {
            RaidFile raid = Golden("full-heist.json");
            (RaidRunner straight, VisibilityMask straightMask) = Rig(LightingProfile.Default());
            straight.LoadReplay(raid.Vault, raid.Seed, raid.Log);
            while (straight.Mode == RaidMode.Replay && straight.Sim.State.Tick < 150)
            {
                straight.Advance(TickAccumulator.TickSeconds);
            }

            (RaidRunner seeking, VisibilityMask seekMask) = Rig(LightingProfile.Default());
            seeking.LoadReplay(raid.Vault, raid.Seed, raid.Log);
            seeking.SeekReplay(straight.Sim.State.Tick);
            straightMask.Refresh();
            seekMask.Refresh();
            Assert.AreEqual(straight.Sim.State.Tick, seeking.Sim.State.Tick);
            int remembered = 0;
            ForEachTile(raid.Vault, p =>
            {
                Assert.AreEqual(straightMask.IsSeen(p), seekMask.IsSeen(p), "seen " + p);
                Assert.AreEqual(straightMask.IsVisible(p), seekMask.IsVisible(p), "lit " + p);
                remembered += straightMask.IsSeen(p) && !straightMask.IsVisible(p) ? 1 : 0;
            });
            Assert.Greater(remembered, 0, "the heist has walked away from somewhere by tick 150");
        }

        [Test]
        public void WithRememberedOff_UnlitTilesAreUnseen()
        {
            RaidFile raid = Golden("full-heist.json");
            LightingProfile profile = Profile();
            profile.Remembered = false;
            (RaidRunner runner, VisibilityMask mask) = Rig(profile);
            runner.LoadReplay(raid.Vault, raid.Seed, raid.Log);
            while (runner.Mode == RaidMode.Replay)
            {
                runner.Advance(TickAccumulator.TickSeconds);
                mask.Refresh();
                ForEachTile(raid.Vault, p => Assert.AreEqual(mask.IsVisible(p), mask.IsSeen(p), p.ToString()));
            }
        }

        [Test]
        public void TheScoutsViewIsTemporary()
        {
            RaidFile raid = Golden("full-heist.json");
            (RaidRunner runner, VisibilityMask mask) = Rig(LightingProfile.Default());
            runner.LoadLive(raid.Vault, raid.Seed);
            mask.Refresh();
            int scouted = 0;
            ForEachTile(raid.Vault, p => scouted += mask.IsSeen(p) ? 1 : 0);

            runner.StartClock();
            mask.Refresh();
            int seen = 0;
            ForEachTile(raid.Vault, p =>
            {
                Assert.AreEqual(runner.Sim.IsLit(p), mask.IsSeen(p), "only Wick's own light counts: " + p);
                seen += mask.IsSeen(p) ? 1 : 0;
            });
            Assert.Greater(scouted, seen, "the scout showed more than Wick's first pool");
        }

        // ---- Wick's flame ---------------------------------------------------------------------

        [TestCase(3, false, 4.5f)]
        [TestCase(2, false, 3.5f)]
        [TestCase(1, false, 2.5f)]
        [TestCase(3, true, 2.5f)]
        [TestCase(1, true, 1.5f)]
        public void FlameRange_IsTheSimsRadiusPlusHalfATile(int flickers, bool tiptoe, float expected)
        {
            var wick = new WickState { Flickers = flickers, Tiptoe = tiptoe };
            float radiusTiles = Lighting.RadiusUnits(in wick) / (float)SubTile.PerTile;
            Assert.AreEqual(radiusTiles + 0.5f, WickView.FlameRange(Profile(), in wick), 1e-5f, "follows the sim");
            Assert.AreEqual(expected, WickView.FlameRange(Profile(), in wick), 1e-5f, "today's SimTuning radii (4 / 3 / 2 tiles)");
        }

        [Test]
        public void FlameIntensity_FlickersGently_FranticallyAtOneFlicker_AndFlares()
        {
            LightingProfile p = Profile();
            float min3 = float.MaxValue, max3 = 0f, min1 = float.MaxValue, max1 = 0f;
            for (float t = 0f; t < 3f; t += 0.01f)
            {
                float calm = WickView.FlameIntensity(p, 3, 0f, t);
                float frantic = WickView.FlameIntensity(p, 1, 0f, t);
                min3 = Mathf.Min(min3, calm);
                max3 = Mathf.Max(max3, calm);
                min1 = Mathf.Min(min1, frantic);
                max1 = Mathf.Max(max1, frantic);
            }

            Assert.GreaterOrEqual(min3, 2f * 0.95f - 1e-4f);
            Assert.LessOrEqual(max3, 2f * 1.05f + 1e-4f);
            Assert.Less(max3 - min3, max1 - min1, "frantic flickers harder");
            Assert.LessOrEqual(max1, 2f * 1.15f + 1e-4f);
            Assert.GreaterOrEqual(min1, 2f * 0.85f - 1e-4f);
            Assert.AreEqual(WickView.FlameIntensity(p, 3, 0f, 0.37f) * 1.4f, WickView.FlameIntensity(p, 3, 1f, 0.37f), 1e-4f, "+40% flare");
        }

        // ---- Profile ----------------------------------------------------------------------------

        [Test]
        public void ClassicTreasuryProfile_StartsFromThePacksValues()
        {
            LightingProfile p = AssetDatabase.LoadAssetAtPath<LightingProfile>("Assets/_Wick/Art/Themes/lighting_treasury.asset");
            Assert.IsNotNull(p, "ThemeBuilder creates it");
            var treasury = AssetDatabase.LoadAssetAtPath<ThemeDefinition>("Assets/_Wick/Art/Themes/" + ThemeCatalog.DefaultId + ".asset");
            Assert.AreSame(p, treasury.Lighting);
            Assert.IsNotNull(treasury.Sconce);
            Assert.IsNotNull(treasury.PitEdge);
            Assert.IsNotNull(treasury.EntranceGlow);

            LightingProfile d = Profile();
            Assert.AreEqual("3A2F6B", ColorUtility.ToHtmlStringRGB(d.AmbientSky));
            Assert.AreEqual("2A2550", ColorUtility.ToHtmlStringRGB(d.AmbientEquator));
            Assert.AreEqual("1E1B3A", ColorUtility.ToHtmlStringRGB(d.AmbientGround));
            Assert.AreEqual("5B6B9A", ColorUtility.ToHtmlStringRGB(d.MainLightColor));
            Assert.AreEqual("FF9F1C", ColorUtility.ToHtmlStringRGB(d.FlameColor));
            Assert.AreEqual("FFE29A", ColorUtility.ToHtmlStringRGB(d.FlameCoreColor));
            Assert.AreEqual(0.15f, d.MainLightIntensity);
            Assert.AreEqual(2f, d.FlameIntensity);
            Assert.AreEqual(0.35f, d.RememberedBrightness);
            Assert.AreEqual(0.6f, d.RememberedDesaturation);
            Assert.AreEqual(0.12f, d.UnseenBrightness, "faint, never pitch black");
            Assert.AreEqual(0.5f, d.EdgeSoftnessTiles);
            Assert.AreEqual(1.5f, d.BlobRadiusTiles);
            Assert.AreEqual(0.6f, d.BlobOpacity);
            Assert.AreEqual(0.5f, d.BlobFadeSeconds);
            Assert.AreEqual(0.3f, d.SnuffSeconds);
            Assert.IsTrue(d.Remembered);
            foreach (ReadableKind kind in System.Enum.GetValues(typeof(ReadableKind)))
            {
                Assert.IsTrue(d.IsReadable(kind), kind + " is readable by default");
            }
        }

        // ---- Static pools ------------------------------------------------------------------------

        [Test]
        public void Sconces_HangOnWallsBesideFloor_SpacedOut_AndClearOfFittings()
        {
            foreach (TextAsset text in Resources.LoadAll<TextAsset>("DevVaults"))
            {
                VaultDef vault = VaultJson.Parse(text.text);
                List<Sconce> sconces = StaticLights.PlaceSconces(vault, 5);
                Assert.Greater(sconces.Count, 0, text.name);
                CollectionAssert.AreEqual(sconces, StaticLights.PlaceSconces(vault, 5), "deterministic");
                for (int i = 0; i < sconces.Count; i++)
                {
                    Sconce s = sconces[i];
                    Assert.AreEqual(TileType.Wall, vault.TileAt(s.Wall), text.name);
                    Assert.AreEqual(TileType.Floor, vault.TileAt(s.Floor), text.name);
                    for (int o = 0; o < vault.ObjectCount; o++)
                    {
                        Assert.AreNotEqual(vault.GetObject(o).Pos, s.Wall, text.name + ": not on a brazier or bellows");
                    }

                    for (int j = 0; j < i; j++)
                    {
                        int gap = Mathf.Max(Mathf.Abs(sconces[j].Wall.X - s.Wall.X), Mathf.Abs(sconces[j].Wall.Y - s.Wall.Y));
                        Assert.GreaterOrEqual(gap, 5, text.name);
                    }
                }
            }
        }

        [Test]
        public void StaticPools_LightNearbyTilesInSight_AndNothingFar()
        {
            // Entrance (1,1); a wall between the left and right halves of the corridor row.
            VaultDef vault = TestVaults.Parse("#########", "#E..#...#", "#########");
            float[] light = StaticLights.Compute(vault, new List<Sconce>(), 1.5f);
            float At(int x, int y) => light[y * vault.Width + x];
            Assert.Greater(At(1, 1), 0.5f, "the entrance is a source");
            Assert.Greater(At(2, 1), 0f);
            Assert.AreEqual(0f, At(7, 1), "out of reach");
            Assert.AreEqual(0f, At(5, 1), "behind the wall");
        }

        // ---- Always readable ----------------------------------------------------------------------

        [Test]
        public void AGlow_ShowsInTheDark_OnlyWhileActiveAndAllowed()
        {
            var go = new GameObject("Glow (test)");
            _spawned.Add(go);
            GameObject eye = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            eye.transform.SetParent(go.transform);
            ReadableGlow glow = go.AddComponent<ReadableGlow>();
            glow.Kind = ReadableKind.GuardianEyes;
            Renderer r = eye.GetComponent<Renderer>();
            LightingProfile p = Profile();

            glow.Apply(false, p);
            Assert.IsTrue(r.enabled, "readable in the dark");
            glow.Active = false;
            glow.Apply(true, p);
            Assert.IsFalse(r.enabled, "nothing to show (eyes shut)");
            glow.Active = true;
            p.ReadableGuardianEyes = false;
            glow.Apply(false, p);
            Assert.IsFalse(r.enabled, "switched off: follows the mask");
            glow.Apply(true, p);
            Assert.IsTrue(r.enabled, "still shows in Wick's light");
        }

        [Test]
        public void InTheBuiltVault_PitEdgesGlowOnEveryOpenSide()
        {
            (GameObject go, VisibilityMask _, VaultDef vault) = BuiltVault("pit-loot-drop.json");
            int pitEdges = 0;
            foreach (ReadableGlow g in go.GetComponentsInChildren<ReadableGlow>(true))
            {
                pitEdges += g.Kind == ReadableKind.PitEdge ? 1 : 0;
            }

            Assert.AreEqual(PitEdgesOf(vault), pitEdges, "one strip per pit side open to the floor");
        }

        [Test]
        public void InTheBuiltVault_TreasureTwinklesInTheDark_ButThePileWaits()
        {
            (GameObject go, VisibilityMask mask, VaultDef vault) = BuiltVault("full-heist.json");
            int darkTreasure = 0;
            foreach (TreasureView pile in go.GetComponentsInChildren<TreasureView>(true))
            {
                if (mask.IsVisible(vault.GetObject(vault.IndexOfObject(pile.EntityId)).Pos))
                {
                    continue;
                }

                darkTreasure++;
                foreach (Renderer r in pile.GetComponentsInChildren<Renderer>(true))
                {
                    bool glow = r.GetComponentInParent<ReadableGlow>(true) != null;
                    Assert.AreEqual(glow, r.enabled, glow ? "the twinkle shows in the dark" : "the pile itself waits for Wick's light");
                }
            }

            Assert.Greater(darkTreasure, 0, "a pile starts out of Wick's light");
        }

        private (GameObject, VisibilityMask, VaultDef) BuiltVault(string file)
        {
            RaidFile raid = Golden(file);
            var go = new GameObject("Vault rig (test)");
            _spawned.Add(go);
            RaidRunner runner = go.AddComponent<RaidRunner>();
            VisibilityMask mask = go.AddComponent<VisibilityMask>();
            VaultView view = go.AddComponent<VaultView>();
            var tuning = ScriptableObject.CreateInstance<RaidTuning>();
            _spawned.Add(tuning);
            mask.Bind(runner, null);
            view.Bind(runner, AssetDatabase.LoadAssetAtPath<ViewRegistry>("Assets/_Wick/Data/ViewRegistry.asset"), mask, tuning);
            runner.LoadReplay(raid.Vault, raid.Seed, raid.Log);
            view.Build(runner); // edit mode runs no OnEnable, so build by hand
            view.SyncAll();
            return (go, mask, raid.Vault);
        }

        // ---- Helpers --------------------------------------------------------------------------------

        private static int PitEdgesOf(VaultDef vault)
        {
            int n = 0;
            ForEachTile(vault, p =>
            {
                if (vault.TileAt(p) == TileType.Pit)
                {
                    for (int d = 0; d < 4; d++)
                    {
                        TileType t = vault.TileAt(p.Step((Dir)d));
                        n += t != TileType.Pit && t != TileType.Wall ? 1 : 0;
                    }
                }
            });
            return n;
        }

        private static void ForEachTile(VaultDef vault, System.Action<TilePos> act)
        {
            for (int y = 0; y < vault.Height; y++)
            {
                for (int x = 0; x < vault.Width; x++)
                {
                    act(new TilePos(x, y));
                }
            }
        }

        private static RaidFile Golden(string file)
        {
            return RaidFile.Parse(File.ReadAllText(Path.Combine(GoldenReplayTests.GoldenDirectory(), file)));
        }

        private LightingProfile Profile()
        {
            LightingProfile p = LightingProfile.Default();
            _spawned.Add(p);
            return p;
        }

        private (RaidRunner, VisibilityMask) Rig(LightingProfile profile)
        {
            _spawned.Add(profile);
            var go = new GameObject("Lighting rig (test)");
            _spawned.Add(go);
            RaidRunner runner = go.AddComponent<RaidRunner>();
            VisibilityMask mask = go.AddComponent<VisibilityMask>();
            mask.Bind(runner, profile);
            return (runner, mask);
        }
    }
}
