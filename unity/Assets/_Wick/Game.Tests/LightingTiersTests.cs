using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Wick.Sim;
using Wick.Sim.Tests;

namespace Wick.Game.Tests
{
    /// <summary>
    /// M10.2b (docs/m10-2-lighting.md): quality tiers never change what can be seen, Home has no
    /// mask, atmosphere is placed from the layout, the colour grades do what they say, theme profiles
    /// are only data, and the grade follows the profile and tier.
    /// </summary>
    public class LightingTiersTests
    {
        private readonly List<Object> _spawned = new List<Object>();
        private QualityTier _ownTier;
        private bool _hadTier;

        [SetUp]
        public void SetUp()
        {
            _hadTier = LightingQuality.Chosen;
            _ownTier = LightingQuality.Tier;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Object o in _spawned)
            {
                Object.DestroyImmediate(o);
            }

            _spawned.Clear();
            if (_hadTier)
            {
                LightingQuality.Tier = _ownTier;
            }
            else
            {
                PlayerPrefs.DeleteKey("wick.quality");
            }
        }

        // ---- Tiers -----------------------------------------------------------------------------

        [TestCase(10f, QualityTier.High)]
        [TestCase(17.5f, QualityTier.High)]
        [TestCase(20f, QualityTier.Medium)]
        [TestCase(30f, QualityTier.Low)]
        public void TheBenchmarkPicksATierFromFrameTime(float ms, QualityTier expected)
        {
            Assert.AreEqual(expected, LightingQuality.FromFrameMs(ms));
        }

        [Test]
        public void TheBudgetTable()
        {
            Assert.IsTrue(LightingQuality.Bloom(QualityTier.High));
            Assert.IsFalse(LightingQuality.BloomQuarterRes(QualityTier.High), "High blooms at half resolution");
            Assert.IsTrue(LightingQuality.BloomQuarterRes(QualityTier.Medium));
            Assert.IsFalse(LightingQuality.Bloom(QualityTier.Low));
            Assert.IsFalse(LightingQuality.Fog(QualityTier.Low));
            Assert.AreEqual(0f, LightingQuality.ShaftShare(QualityTier.Low));
            Assert.AreEqual(0.5f, LightingQuality.DustShare(QualityTier.Medium));
            Assert.IsFalse(LightingQuality.SoftMaskEdge(QualityTier.Low), "per-tile mask on Low");
            Assert.AreEqual(1, Atmosphere.Scaled(1, 0.5f), "at least one when any");
            Assert.AreEqual(0, Atmosphere.Scaled(2, 0f));
        }

        [Test]
        public void EveryTier_SeesTheSameTilesAndGlows()
        {
            RaidFile raid = Golden("full-heist.json");
            var seen = new Dictionary<QualityTier, string>();
            foreach (QualityTier tier in new[] { QualityTier.High, QualityTier.Medium, QualityTier.Low })
            {
                LightingQuality.Tier = tier;
                (RaidRunner runner, VisibilityMask mask) = Rig();
                runner.LoadReplay(raid.Vault, raid.Seed, raid.Log);
                var sb = new System.Text.StringBuilder();
                while (runner.Mode == RaidMode.Replay)
                {
                    runner.Advance(TickAccumulator.TickSeconds * 7);
                    mask.Refresh();
                    for (int y = 0; y < raid.Vault.Height; y++)
                    {
                        for (int x = 0; x < raid.Vault.Width; x++)
                        {
                            var p = new TilePos(x, y);
                            sb.Append(mask.IsVisible(p) ? 'L' : mask.IsSeen(p) ? 'r' : '.');
                        }
                    }

                    foreach (ReadableKind kind in System.Enum.GetValues(typeof(ReadableKind)))
                    {
                        sb.Append(mask.Profile.IsReadable(kind) ? 'g' : '-');
                    }
                }

                seen[tier] = sb.ToString();
            }

            Assert.AreEqual(seen[QualityTier.High], seen[QualityTier.Medium]);
            Assert.AreEqual(seen[QualityTier.High], seen[QualityTier.Low]);
        }

        [Test]
        public void EveryTier_RendersTheSameLitTiles()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            {
                Assert.Ignore("No GPU in this run");
            }

            RaidFile raid = Golden("full-heist.json");
            var looks = new Dictionary<QualityTier, string>();
            foreach (QualityTier tier in new[] { QualityTier.High, QualityTier.Medium, QualityTier.Low })
            {
                LightingQuality.Tier = tier;
                LightingProfile profile = Profile();
                profile.LitBoost = 0.3f;
                profile.Remembered = false;
                profile.UnseenBrightness = 0.04f;
                (RaidRunner runner, VisibilityMask mask) = Rig(profile);
                runner.LoadReplay(raid.Vault, raid.Seed, raid.Log);
                while (runner.Mode == RaidMode.Replay && runner.Sim.State.Tick < 120)
                {
                    runner.Advance(TickAccumulator.TickSeconds);
                }

                mask.Refresh();
                looks[tier] = TileCentres(raid.Vault);
            }

            Assert.AreEqual(looks[QualityTier.High], looks[QualityTier.Low], "tile centres read the same on every tier");
            Assert.AreEqual(looks[QualityTier.High], looks[QualityTier.Medium]);
        }

        // ---- Screens ----------------------------------------------------------------------------

        [Test]
        public void Home_HasNoMask()
        {
            RaidFile raid = Golden("full-heist.json");
            (RaidRunner runner, VisibilityMask mask) = Rig();
            runner.LoadLive(raid.Vault, raid.Seed);
            mask.FullyLit = true;
            mask.Refresh();
            Assert.AreEqual(0f, Shader.GetGlobalVector("_WickVisGrid").z, "the shaders see no mask");
            for (int y = 0; y < raid.Vault.Height; y++)
            {
                for (int x = 0; x < raid.Vault.Width; x++)
                {
                    Assert.IsTrue(mask.IsVisible(new TilePos(x, y)), "every view shows");
                }
            }

            mask.FullyLit = false;
            Assert.AreEqual(1f, Shader.GetGlobalVector("_WickVisGrid").z, "back on for a raid");
        }

        [Test]
        public void TheEditorPreview_ShowsAStillPicture()
        {
            var go = new GameObject("Preview (test)");
            _spawned.Add(go);
            VisibilityMask mask = go.AddComponent<VisibilityMask>();
            VaultDef vault = TestVaults.Parse("#####", "#E..#", "#####");
            bool[] lit = { false, false, false, false, false, false, true, true, false, false, false, false, false, false, false };
            mask.ShowStill(vault, lit, lit);
            mask.Refresh(); // no runner: a still picture stays
            Assert.IsTrue(mask.IsVisible(new TilePos(1, 1)));
            Assert.IsFalse(mask.IsVisible(new TilePos(3, 1)));
            Assert.AreEqual(1f, Shader.GetGlobalVector("_WickVisGrid").z);
            mask.ShowStill(null, null, null);
            Assert.AreEqual(Vector4.zero, Shader.GetGlobalVector("_WickVisGrid"), "put away");
        }

        // ---- Atmosphere -------------------------------------------------------------------------

        [Test]
        public void Rooms_SplitAtDoors()
        {
            // Two rooms joined by a door at (3,1); a pit belongs to its room.
            VaultDef vault = TestVaults.Parse("#######", "#E.D.O#", "#######");
            List<List<TilePos>> rooms = Atmosphere.Rooms(vault);
            Assert.AreEqual(2, rooms.Count);
            CollectionAssert.AreEqual(new[] { new TilePos(1, 1), new TilePos(2, 1) }, rooms[0]);
            CollectionAssert.AreEqual(new[] { new TilePos(4, 1), new TilePos(5, 1) }, rooms[1]);
        }

        [Test]
        public void Fog_PoolsInPitsFirst_ThenFarCorners()
        {
            VaultDef vault = TestVaults.Parse("#######", "#.....#", "#..O..#", "#..E..#", "#######");
            List<TilePos> fog = Atmosphere.FogSpots(vault, Atmosphere.Rooms(vault), 2);
            Assert.AreEqual(2, fog.Count);
            Assert.AreEqual(new TilePos(3, 2), fog[0], "the pit");
            Assert.AreEqual(1, fog[1].Y, "a corner on the far side from the entrance");
            CollectionAssert.AreEqual(fog, Atmosphere.FogSpots(vault, Atmosphere.Rooms(vault), 2), "deterministic");
            CollectionAssert.IsEmpty(Atmosphere.FogSpots(vault, Atmosphere.Rooms(vault), 0));
        }

        [Test]
        public void Shafts_FallFromTheBackWall_SpreadFromTheMiddle()
        {
            VaultDef vault = TestVaults.Parse("#########", "#.......#", "#...E...#", "#########");
            List<TilePos> walls = Atmosphere.ShaftWalls(vault, Atmosphere.Rooms(vault), 2);
            Assert.AreEqual(2, walls.Count);
            foreach (TilePos w in walls)
            {
                Assert.AreEqual(TileType.Wall, vault.TileAt(w));
                Assert.AreEqual(0, w.Y, "the back (north) wall");
            }

            Assert.AreEqual(4, walls[0].X, "the middle first");
            Assert.GreaterOrEqual(Mathf.Abs(walls[1].X - walls[0].X), 3, "spread out");
        }

        // ---- Grades and profiles ---------------------------------------------------------------

        [Test]
        public void TheNeutralLut_ChangesNothing_AndTheGradesLeanTheRightWay()
        {
            var grey = new Color(0.5f, 0.5f, 0.5f);
            var skin = new Color(0.8f, 0.6f, 0.45f);
            Assert.AreEqual(skin, LutGrades.Apply(LutGrade.Neutral, skin));
            Color[] strip = LutGrades.Strip(LutGrade.Neutral);
            Assert.AreEqual(LutGrades.Size * LutGrades.Size * LutGrades.Size, strip.Length);
            Assert.AreEqual(Color.white, strip[strip.Length - 1], "top right is white");

            Color victory = LutGrades.Apply(LutGrade.Victory, grey);
            Assert.Greater(victory.r - victory.b, 0.05f, "victory is warm");
            Color defeat = LutGrades.Apply(LutGrade.Defeat, skin);
            Assert.Less(defeat.r - defeat.b, skin.r - skin.b, "defeat drains colour");
            Assert.Less(defeat.grayscale, skin.grayscale, "and darkens");
            Color cool = LutGrades.Apply(LutGrade.CoolVaultDepth, grey);
            Assert.Greater(cool.b, cool.r, "cool vault depth is cool");
        }

        [Test]
        public void ThemeProfiles_AreOnlyData()
        {
            var treasury = AssetDatabase.LoadAssetAtPath<LightingProfile>("Assets/_Wick/Art/Themes/lighting_treasury.asset");
            var crypt = AssetDatabase.LoadAssetAtPath<LightingProfile>("Assets/_Wick/Art/Lighting/lighting_crypt.asset");
            var sunken = AssetDatabase.LoadAssetAtPath<LightingProfile>("Assets/_Wick/Art/Lighting/lighting_sunken_ship.asset");
            Assert.IsNotNull(crypt);
            Assert.IsNotNull(sunken);
            Assert.AreSame(crypt, AssetDatabase.LoadAssetAtPath<ThemeDefinition>("Assets/_Wick/Art/Themes/theme_crypt.asset").Lighting);
            Assert.IsNotNull(treasury.ColorGradeLut, "Classic Treasury's warm interior LUT");
            Assert.AreNotEqual(treasury.ColorGradeLut, crypt.ColorGradeLut);
            Assert.AreEqual(DustKind.Bubbles, sunken.Dust);

            // The same vault built under each profile: the same objects; only colours and amounts differ.
            RaidFile raid = Golden("pit-fall-recover.json");
            string shape = null;
            foreach (LightingProfile p in new[] { treasury, crypt, sunken })
            {
                (GameObject go, VaultView view) = BuiltVault(raid, p);
                var names = new System.Text.StringBuilder();
                foreach (Transform t in go.GetComponentsInChildren<Transform>(true))
                {
                    // How much fog, shaft and dust a theme has is data (counts and amounts); everything else must match.
                    if (t.GetComponentInParent<ParticleSystem>(true) == null && !UnderAtmosphereCard(t))
                    {
                        names.Append(t.name).Append('|');
                    }
                }

                shape ??= names.ToString();
                Assert.AreEqual(shape, names.ToString(), p.name + " builds the same vault");
            }
        }

        [Test]
        public void TheGrade_FollowsTheProfileAndTier()
        {
            var go = new GameObject("Grade (test)");
            _spawned.Add(go);
            go.SetActive(false);
            Volume volume = go.AddComponent<Volume>();
            volume.sharedProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/_Wick/Data/WickGrade.asset");
            Grade grade = go.AddComponent<Grade>();
            grade.Bind(volume);
            go.SetActive(true);
            var treasury = AssetDatabase.LoadAssetAtPath<LightingProfile>("Assets/_Wick/Art/Themes/lighting_treasury.asset");

            LightingQuality.Tier = QualityTier.High;
            grade.ApplyProfile(treasury);
            Assert.IsTrue(volume.profile.TryGet(out Bloom bloom) && bloom.active);
            Assert.IsTrue(volume.profile.TryGet(out ColorLookup look) && look.texture.value == treasury.ColorGradeLut);
            Assert.AreNotSame(volume.sharedProfile, volume.profile, "the asset is never edited");

            LightingQuality.Tier = QualityTier.Low;
            grade.ApplyProfile(treasury); // edit mode runs no OnEnable, so the tier event isn't hooked up
            Assert.IsFalse(bloom.active, "no bloom on Low");
            grade.ShowResult(true);
            grade.ClearResult();
        }

        [Test]
        public void BenchmarkStats_AverageAndOnePercentLow()
        {
            var frames = new float[100];
            for (int i = 0; i < 100; i++)
            {
                frames[i] = i == 99 ? 50f : 16f;
            }

            (float avg, float low) = LightingBenchmark.Stats(frames, 100);
            Assert.AreEqual(16.34f, avg, 1e-3f);
            Assert.AreEqual(50f, low);
        }

        // ---- Helpers -----------------------------------------------------------------------------

        private string TileCentres(VaultDef vault)
        {
            // One upward floor quad per tile through Wick/StylisedLit, from straight above.
            const int px = 8;
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            for (int y = 0; y < vault.Height; y++)
            {
                for (int x = 0; x < vault.Width; x++)
                {
                    int i = vertices.Count;
                    vertices.Add(new Vector3(x - 0.5f, 0, -y + 0.5f));
                    vertices.Add(new Vector3(x + 0.5f, 0, -y + 0.5f));
                    vertices.Add(new Vector3(x + 0.5f, 0, -y - 0.5f));
                    vertices.Add(new Vector3(x - 0.5f, 0, -y - 0.5f));
                    triangles.AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3 });
                }
            }

            var mesh = new Mesh { indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(vertices);
            mesh.SetNormals(vertices.ConvertAll(_ => Vector3.up));
            mesh.SetTriangles(triangles, 0);
            _spawned.Add(mesh);
            var material = new Material(Shader.Find("Wick/StylisedLit"));
            material.SetColor("_BaseColor", Color.white);
            _spawned.Add(material);
            var floor = new GameObject("Floor (test)");
            _spawned.Add(floor);
            floor.AddComponent<MeshFilter>().sharedMesh = mesh;
            floor.AddComponent<MeshRenderer>().sharedMaterial = material;
            var camGo = new GameObject("Camera (test)");
            _spawned.Add(camGo);
            Camera camera = camGo.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = vault.Height * 0.5f;
            camera.aspect = (float)vault.Width / vault.Height;
            camera.transform.position = new Vector3((vault.Width - 1) * 0.5f, 10f, -(vault.Height - 1) * 0.5f);
            camera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.nearClipPlane = 1f;
            camera.farClipPlane = 20f;

            var rt = new RenderTexture(vault.Width * px, vault.Height * px, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
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
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false, true);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = previous;
            rt.Release();
            Object.DestroyImmediate(rt);
            _spawned.Add(tex);
            Object.DestroyImmediate(floor);
            Object.DestroyImmediate(camGo);

            var sb = new System.Text.StringBuilder();
            for (int y = 0; y < vault.Height; y++)
            {
                for (int x = 0; x < vault.Width; x++)
                {
                    sb.Append(tex.GetPixel(x * px + px / 2, (vault.Height - 1 - y) * px + px / 2).r > 0.12f ? 'L' : '.');
                }
            }

            return sb.ToString();
        }

        private static bool UnderAtmosphereCard(Transform t)
        {
            for (; t != null; t = t.parent)
            {
                if (t.name.StartsWith("fog_card") || t.name.StartsWith("shaft_card"))
                {
                    return true;
                }
            }

            return false;
        }

        private (GameObject, VaultView) BuiltVault(RaidFile raid, LightingProfile profile)
        {
            var go = new GameObject("Vault rig (test)");
            _spawned.Add(go);
            RaidRunner runner = go.AddComponent<RaidRunner>();
            VisibilityMask mask = go.AddComponent<VisibilityMask>();
            VaultView view = go.AddComponent<VaultView>();
            var tuning = ScriptableObject.CreateInstance<RaidTuning>();
            _spawned.Add(tuning);
            mask.Bind(runner, null);
            view.Bind(runner, AssetDatabase.LoadAssetAtPath<ViewRegistry>("Assets/_Wick/Data/ViewRegistry.asset"), mask, tuning);
            view.LightingOverride = profile;
            runner.LoadReplay(raid.Vault, raid.Seed, raid.Log);
            view.Build(runner); // edit mode runs no OnEnable, so build by hand
            return (go, view);
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

        private (RaidRunner, VisibilityMask) Rig(LightingProfile profile = null)
        {
            var go = new GameObject("Lighting rig (test)");
            _spawned.Add(go);
            RaidRunner runner = go.AddComponent<RaidRunner>();
            VisibilityMask mask = go.AddComponent<VisibilityMask>();
            mask.Bind(runner, profile);
            return (runner, mask);
        }
    }
}
