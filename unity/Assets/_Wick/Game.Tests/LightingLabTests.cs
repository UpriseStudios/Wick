using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Wick.Sim;

namespace Wick.Game.Tests
{
    /// <summary>
    /// M10.2c (docs/m10-2-lighting.md): the lab vault really has one of everything and the sim runs
    /// it; Wick's scripted walk is safe, wakes the guardians and leaves Lit, Remembered and Unseen
    /// tiles; the tuning panel covers every profile value; silhouettes hide every glow; and the
    /// readability check tells a glow from its background.
    /// </summary>
    public class LightingLabTests
    {
        private readonly List<Object> _spawned = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            ReadableGlow.Silhouette = false;
            foreach (Object o in _spawned)
            {
                Object.DestroyImmediate(o);
            }

            _spawned.Clear();
        }

        [Test]
        public void TheLabHasOneOfEverything_AndTheSimRunsIt()
        {
            VaultDef lab = LightingLab.Build();
            var types = new HashSet<ObjectType>();
            for (int i = 0; i < lab.ObjectCount; i++)
            {
                types.Add(lab.GetObject(i).Type);
            }

            foreach (ObjectType t in new[]
            {
                ObjectType.DripPipe, ObjectType.SpikeFloor, ObjectType.Bellows, ObjectType.Crate, ObjectType.SnufferBell,
                ObjectType.NightOwl, ObjectType.WaxMoths, ObjectType.SwingAxe, ObjectType.MimicChest, ObjectType.SleepyTroll,
                ObjectType.Fan, ObjectType.Gate, ObjectType.Lever, ObjectType.SirWhiskers, ObjectType.Treasure, ObjectType.Brazier,
            })
            {
                Assert.That(types, Has.Member(t), t + " is in the lab");
            }

            Assert.Greater(VaultRules.PitTileCount(lab), 0, "a pit");
            Assert.Greater(VaultRules.IceTileCount(lab), 0, "ice");
            var kinds = new HashSet<string>();
            for (int i = 0; i < lab.DecorationCount; i++)
            {
                kinds.Add(lab.GetDecoration(i).Kind);
            }

            CollectionAssert.IsSubsetOf(Decorations.Free, kinds, "every free decoration family");
            VaultJson.Parse(VaultJson.Write(lab)); // the sim's own safety checks accept it
        }

        [Test]
        public void TheWalk_IsSafe_WakesTheGuardians_AndLeavesEveryTileState()
        {
            VaultDef lab = LightingLab.Build();
            InputLog walk = LightingLab.ScriptWalk(lab);
            InputLog again = LightingLab.ScriptWalk(lab);
            Assert.AreEqual(walk.Count, again.Count, "deterministic");
            for (int i = 0; i < walk.Count; i++)
            {
                Assert.AreEqual(walk[i].Tick, again[i].Tick);
                Assert.AreEqual(walk[i].Type, again[i].Type);
            }

            var sim = new RaidSim(lab, LightingLab.Seed, walk);
            var everLit = new bool[lab.Width * lab.Height];
            bool reachedFarEnd = false, chased = false, rememberedAndUnseen = false;
            var awake = new HashSet<ObjectType>();
            while (!sim.IsOver)
            {
                sim.Step();
                Assert.AreEqual(SimTuning.StartFlickers, sim.State.Wick.Flickers, "tick " + sim.State.Tick + ": the walk never costs a flicker");
                reachedFarEnd |= sim.State.Wick.Tile == LightingLab.FarEnd;
                foreach (GuardianState g in sim.State.Guardians)
                {
                    if (g.Mode == GuardianMode.Chase)
                    {
                        chased = true;
                        awake.Add(g.Kind);
                    }
                }

                int remembered = 0, unseen = 0;
                for (int i = 0; i < everLit.Length; i++)
                {
                    everLit[i] |= sim.State.Lit[i];
                    remembered += everLit[i] && !sim.State.Lit[i] ? 1 : 0;
                    unseen += everLit[i] ? 0 : 1;
                }

                rememberedAndUnseen |= remembered > 10 && unseen > 10;
            }

            Assert.AreEqual(Outcome.Escaped, sim.GetResult().Outcome, "back out of the entrance");
            Assert.Greater(sim.GetResult().Stars, 0);
            Assert.IsTrue(reachedFarEnd, "to the far end of the gallery");
            Assert.IsTrue(chased, "guardians wake and give chase behind their gates");
            Assert.IsTrue(rememberedAndUnseen, "Lit, Remembered and Unseen all on screen at once");
            Debug.Log("Lab walk: " + sim.GetResult().Ticks + " ticks; awake: " + string.Join(", ", awake));
        }

        [Test]
        public void TheTuningPanel_CoversEveryProfileValue()
        {
            foreach (FieldInfo f in typeof(LightingProfile).GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                bool tunable = f.FieldType == typeof(float) || f.FieldType == typeof(int) || f.FieldType == typeof(bool) ||
                    f.FieldType == typeof(Color) || f.FieldType == typeof(Vector3) || f.FieldType.IsEnum;
                bool texture = typeof(Texture).IsAssignableFrom(f.FieldType);
                Assert.IsTrue(tunable || texture, f.Name + " (" + f.FieldType.Name + ") needs a row in LightingTuner");
            }
        }

        [Test]
        public void Silhouettes_ShowEveryBody_InTheDark_WithNoGlows()
        {
            RaidFile raid = RaidFile.Parse(Resources.Load<TextAsset>("DevGoldens/full-heist").text);
            var go = new GameObject("Silhouette rig (test)");
            _spawned.Add(go);
            RaidRunner runner = go.AddComponent<RaidRunner>();
            VisibilityMask mask = go.AddComponent<VisibilityMask>();
            mask.Bind(runner, null);
            runner.LoadReplay(raid.Vault, raid.Seed, raid.Log);
            mask.Silhouette = true;
            mask.Refresh();
            Assert.IsTrue(mask.IsVisible(new TilePos(raid.Vault.Width - 2, 1)), "bodies everywhere are shown");
            foreach (byte b in mask.Texture.GetPixelData<byte>(0))
            {
                Assert.AreEqual(0, b, "every tile drawn Unseen");
            }

            var glowGo = new GameObject("Glow (test)");
            _spawned.Add(glowGo);
            GameObject.CreatePrimitive(PrimitiveType.Sphere).transform.SetParent(glowGo.transform);
            ReadableGlow glow = glowGo.AddComponent<ReadableGlow>();
            glow.Kind = ReadableKind.Treasure;
            glow.Apply(true, mask.Profile);
            Assert.IsFalse(glow.Renderers[0].enabled, "no glows in a silhouette");
            mask.Silhouette = false;
            glow.Apply(true, mask.Profile);
            Assert.IsTrue(glow.Renderers[0].enabled);
        }

        [Test]
        public void TheReadabilityCheck_TellsAGlowFromItsBackground()
        {
            var image = new Texture2D(100, 100, TextureFormat.RGB24, false);
            _spawned.Add(image);
            var dark = new Color[100 * 100];
            for (int i = 0; i < dark.Length; i++)
            {
                dark[i] = new Color(0.05f, 0.05f, 0.06f);
            }

            image.SetPixels(dark);
            image.SetPixel(50, 50, new Color(0.9f, 0.7f, 0.3f));
            image.Apply();
            Assert.Greater(SignoffCapture.Contrast(image, 50, 50) * 0.5f, SignoffCapture.MinContrast, "a glow in the dark reads");
            Assert.Less(SignoffCapture.Contrast(image, 20, 20) * 0.5f, SignoffCapture.MinContrast, "plain dark doesn't");
        }
    }
}
