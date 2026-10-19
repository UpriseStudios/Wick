using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using Wick.Editor.ArtPipeline;

namespace Wick.Editor.Tests
{
    public sealed class ArtRulesTests
    {
        private const string Root = ArtBudgets.IncomingRoot;

        private static AssetFacts GoodWick()
        {
            return new AssetFacts
            {
                Path = Root + "/char/char_wick_default/char_wick_default.fbx",
                Triangles = 4200,
                Materials = 1,
                HasFlameMesh = true,
                Height = 0.8f,
                Width = 0.4f,
                Depth = 0.35f,
                Bones = ArtBudgets.WickBones.ToList(),
                BlendShapes = ArtBudgets.WickBlendShapes.ToList(),
                Clips = { "idle", "run", "snuff" },
            };
        }

        private static List<string> Rules(List<Violation> v)
        {
            return v.Select(x => x.Rule).ToList();
        }

        [Test]
        public void AWickMadeToSpec_Passes()
        {
            Assert.IsEmpty(ArtRules.CheckModel("char", "char_wick_default", "char_wick_default", GoodWick()));
        }

        [Test]
        public void PathsMustFollowCategoryAndAssetId()
        {
            var v = new List<Violation>();
            Assert.IsTrue(ArtRules.TryParsePath(Root + "/trap/trap_drip_pipe/trap_drip_pipe.fbx", out string c, out string id, out string stem, v));
            Assert.AreEqual(("trap", "trap_drip_pipe", "trap_drip_pipe"), (c, id, stem));
            Assert.IsEmpty(v);

            Assert.IsFalse(ArtRules.TryParsePath(Root + "/trap/Trap-DripPipe/x.fbx", out _, out _, out _, v));
            Assert.IsFalse(ArtRules.TryParsePath(Root + "/trap/guard_night_owl/guard_night_owl.fbx", out _, out _, out _, v));
            Assert.IsFalse(ArtRules.TryParsePath(Root + "/hats/hat_top/hat_top.fbx", out _, out _, out _, v));
            Assert.IsFalse(ArtRules.TryParsePath(Root + "/loose.fbx", out _, out _, out _, v));
            Assert.AreEqual(new[] { "Naming", "Naming", "Category", "Location" }, Rules(v));
        }

        [Test]
        public void WorkingFilesAreSkipped()
        {
            var v = new List<Violation>();
            Assert.IsFalse(ArtRules.TryParsePath(Root + "/char/char_wick_default/source/Layered Paint.psd", out _, out _, out _, v));
            Assert.IsFalse(ArtRules.TryParsePath(Root + "/char/char_wick_default/previews/front.png", out _, out _, out _, v));
            Assert.IsFalse(ArtRules.TryParsePath(Root + "/README.md", out _, out _, out _, v));
            Assert.IsFalse(ArtRules.TryParsePath(Root + "/sfx/sfx_snuff/LICENCE.txt", out _, out _, out _, v));
            Assert.IsEmpty(v, "source and previews folders, notes and licences are the artist's, not shipped");
        }

        [Test]
        public void OverBudgetMeshes_AreReported_NotFixed()
        {
            AssetFacts heavy = GoodWick();
            heavy.Triangles = 9000;
            heavy.Materials = 3;
            List<Violation> v = ArtRules.CheckModel("char", "char_wick_default", "char_wick_default", heavy);
            CollectionAssert.AreEquivalent(new[] { "Triangles", "Materials" }, Rules(v));
            StringAssert.Contains("9000", v[0].Detail);
            Assert.AreEqual(9000, heavy.Triangles, "the rules only report");
        }

        [Test]
        public void SirWhiskers_GetsTheShowpieceBudget()
        {
            var cat = new AssetFacts { Path = "x", Triangles = 4800, Materials = 1, Width = 1.6f, Depth = 1.2f, Height = 1f };
            Assert.IsEmpty(ArtRules.CheckModel("guard", "guard_sir_whiskers", "guard_sir_whiskers", cat));
            Assert.AreEqual(new[] { "Triangles" }, Rules(ArtRules.CheckModel("guard", "guard_night_owl", "guard_night_owl", cat)));
        }

        [Test]
        public void PivotAndScale_AreChecked()
        {
            AssetFacts f = GoodWick();
            f.MinY = 0.3f;
            f.CentreX = 0.5f;
            f.Height = 60f; // exported in centimetres
            List<Violation> v = ArtRules.CheckModel("char", "char_wick_default", "char_wick_default", f);
            CollectionAssert.AreEquivalent(new[] { "Pivot", "Pivot", "Scale" }, Rules(v));

            var tile = new AssetFacts { Path = "x", Triangles = 300, Materials = 1, Width = 2f, Depth = 1f, Height = 1f };
            Assert.AreEqual(new[] { "Scale" }, Rules(ArtRules.CheckModel("tile", "tile_treasury_floor", "tile_treasury_floor", tile)));
        }

        [Test]
        public void EverySkinNeedsTheSharedRigAndFace()
        {
            AssetFacts f = GoodWick();
            f.Bones.Remove("flame_tip");
            f.BlendShapes.Remove("smug");
            List<Violation> v = ArtRules.CheckModel("char", "char_pip", "char_pip", f);
            CollectionAssert.AreEquivalent(new[] { "Rig", "Blend shapes" }, Rules(v));
            StringAssert.Contains("flame_tip", v[0].Detail);
        }

        [Test]
        public void ClipNames_FollowTheFamilyList()
        {
            AssetFacts f = GoodWick();
            f.Clips.Add("Take 001");
            Assert.AreEqual(new[] { "Clips" }, Rules(ArtRules.CheckModel("char", "char_wick_default", "char_wick_default", f)));

            var trap = new AssetFacts { Path = "x", Triangles = 2000, Materials = 1, Width = 1f, Depth = 1f, Height = 1f, Clips = { "idle", "warn", "fire", "reset" } };
            Assert.IsEmpty(ArtRules.CheckModel("trap", "trap_spike_floor", "trap_spike_floor", trap));
        }

        [Test]
        public void AnimationOnlyFiles_SkipMeshChecks()
        {
            var clipFile = new AssetFacts { Path = "x", Clips = { "dash" } };
            Assert.IsEmpty(ArtRules.CheckModel("char", "char_wick_default", "char_wick_default@dash", clipFile));
        }

        [Test]
        public void Textures_FollowNamingSizeAndPowerOfTwo()
        {
            var ok = new AssetFacts { Path = "x", TextureWidth = 1024, TextureHeight = 1024 };
            Assert.IsEmpty(ArtRules.CheckTexture("char", "char_wick_default", "char_wick_default_albedo", ok));

            var big = new AssetFacts { Path = "x", TextureWidth = 2048, TextureHeight = 2048 };
            Assert.AreEqual(new[] { "Texture size" }, Rules(ArtRules.CheckTexture("trap", "trap_bellows", "trap_bellows_mask", big)));

            var odd = new AssetFacts { Path = "x", TextureWidth = 500, TextureHeight = 512 };
            Assert.AreEqual(new[] { "Naming", "Texture size" }, Rules(ArtRules.CheckTexture("tile", "tile_treasury_wall", "WallColour", odd)));

            var sprite = new AssetFacts { Path = "x", TextureWidth = 330, TextureHeight = 120 };
            Assert.IsEmpty(ArtRules.CheckTexture("ui", "ui_button_primary", "ui_button_primary@3x", sprite), "UI sprites needn't be powers of two");
        }

        [Test]
        public void SoundEffects_AreMono()
        {
            var stereo = new AssetFacts { Path = "x", Channels = 2 };
            Assert.AreEqual(new[] { "Audio" }, Rules(ArtRules.CheckAudio("sfx", "sfx_snuff", "sfx_snuff", stereo)));
            Assert.IsEmpty(ArtRules.CheckAudio("music", "music_calm", "music_calm", stereo));
        }

        /// <summary>CI's gate: every supplied asset meets the handoff specs (the report lists what to fix).</summary>
        [Test]
        public void EverythingInIncoming_MeetsTheHandoffSpecs()
        {
            List<Violation> violations = ArtReport.CheckAll(out _);
            Assert.IsEmpty(violations, "See " + ArtReport.ReportPath + ":\n" + string.Join("\n", violations));
        }

        // ---- Through the real importer -----------------------------------------------------------

        private const string TestFolder = Root + "/prop";
        private readonly List<string> _created = new List<string>();

        [TearDown]
        public void RemoveTestAssets()
        {
            foreach (string folder in _created)
            {
                AssetDatabase.DeleteAsset(folder);
            }

            _created.Clear();
            if (AssetDatabase.IsValidFolder(TestFolder) && AssetDatabase.FindAssets("", new[] { TestFolder }).Length == 0)
            {
                AssetDatabase.DeleteAsset(TestFolder);
            }
        }

        private string ImportCube(string assetId, string fileStem, float baseY)
        {
            string folder = TestFolder + "/" + assetId;
            Directory.CreateDirectory(folder);
            _created.Add(folder);
            string path = folder + "/" + fileStem + ".obj";
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < 8; i++)
            {
                float x = (i & 1) == 0 ? -0.4f : 0.4f, y = baseY + ((i & 2) == 0 ? 0f : 0.8f), z = (i & 4) == 0 ? -0.4f : 0.4f;
                sb.AppendLine(string.Format(System.Globalization.CultureInfo.InvariantCulture, "v {0} {1} {2}", x, y, z));
            }

            foreach (string face in new[] { "1 3 4 2", "5 6 8 7", "1 2 6 5", "3 7 8 4", "1 5 7 3", "2 4 8 6" })
            {
                sb.AppendLine("f " + face);
            }

            File.WriteAllText(path, sb.ToString());
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            return path;
        }

        [Test]
        public void TheImporter_ChecksARealModel()
        {
            string good = ImportCube("prop_zztest_crate", "prop_zztest_crate", 0f);
            string lifted = ImportCube("prop_zztest_lifted", "prop_zztest_lifted", 0.5f);
            string misnamed = ImportCube("prop_zztest_named", "Crate Final v2", 0f);

            List<Violation> all = ArtReport.CheckAll(out _);
            Assert.IsFalse(all.Any(v => v.Path == good), string.Join("\n", all.Where(v => v.Path == good)));
            Assert.IsTrue(all.Any(v => v.Path == lifted && v.Rule == "Pivot"), "base half a metre up");
            Assert.IsTrue(all.Any(v => v.Path == misnamed && v.Rule == "Naming"));
            Assert.IsTrue(File.Exists(ArtReport.ReportPath));
        }
    }
}
