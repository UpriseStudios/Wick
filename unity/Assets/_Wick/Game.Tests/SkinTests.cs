using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Wick.Sim;

namespace Wick.Game.Tests
{
    /// <summary>
    /// Every skin works with the one shared rig and controller: same bones, face shapes and flame,
    /// every clip binds, and every reaction plays (tech design: "every reaction works for every skin").
    /// </summary>
    public class SkinTests
    {
        private const string CatalogPath = "Assets/_Wick/Art/Skins/SkinCatalog.asset";
        private const string ControllerPath = "Assets/_Wick/Art/Animation/WickShared.controller";

        private static readonly string[] Bones =
        {
            "root", "body", "head", "arm.L", "arm.R", "foot.L", "foot.R", "flame_base", "flame_mid", "flame_tip", "eye.L", "eye.R",
        };

        private readonly List<GameObject> _spawned = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _spawned)
            {
                Object.DestroyImmediate(go);
            }

            _spawned.Clear();
        }

        public static IEnumerable<string> SkinIds()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<SkinCatalog>(CatalogPath);
            foreach (SkinDefinition s in catalog.Skins)
            {
                yield return s.Id;
            }
        }

        private (WickSkin, ReactionDriver) Dress(string id)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<SkinCatalog>(CatalogPath);
            var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
            var go = new GameObject("Wick (test)");
            _spawned.Add(go);
            WickSkin skin = go.AddComponent<WickSkin>();
            skin.Bind(catalog, controller, go.transform);
            skin.Apply(catalog.Find(id));
            ReactionDriver reactions = go.AddComponent<ReactionDriver>();
            reactions.Bind(skin.Animator, skin.Face);
            skin.Animator.Rebind();
            return (skin, reactions);
        }

        [Test]
        public void TheCatalogHasTheDefaultAndEverySkinTheEconomyGrants()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<SkinCatalog>(CatalogPath);
            Assert.IsNotNull(catalog.Find(SkinCatalog.DefaultId));
            string economy = File.ReadAllText("Assets/_Wick/Resources/Economy/economy-default.json");
            foreach (Match m in Regex.Matches(economy, "\"(skin_[a-z0-9_]+)\""))
            {
                Assert.IsNotNull(catalog.Find(m.Groups[1].Value), m.Groups[1].Value + " can be granted but has no skin");
            }
        }

        [TestCaseSource(nameof(SkinIds))]
        public void EverySkinHasTheSharedRigFaceAndFlame(string id)
        {
            (WickSkin skin, _) = Dress(id);
            foreach (string bone in Bones)
            {
                Assert.IsNotNull(WickSkin.FindDeep(skin.Animator.transform, bone), id + " is missing bone " + bone);
            }

            Assert.IsNotNull(skin.Face, id + " has no body mesh with the face shapes");
            foreach (string shape in ReactionDriver.FaceShapes)
            {
                Assert.GreaterOrEqual(skin.Face.sharedMesh.GetBlendShapeIndex(shape), 0, id + " is missing blend shape " + shape);
            }

            Assert.IsNotNull(skin.FlameRenderer, id + " has no flame mesh");
            Assert.IsNotNull(skin.Flame, id + " has no flame_base bone");
        }

        [TestCaseSource(nameof(SkinIds))]
        public void EveryClipBindsToEverySkin(string id)
        {
            (WickSkin skin, _) = Dress(id);
            GameObject root = skin.Animator.gameObject;
            foreach (AnimationClip clip in skin.Animator.runtimeAnimatorController.animationClips)
            {
                foreach (EditorCurveBinding b in AnimationUtility.GetCurveBindings(clip))
                {
                    Assert.IsNotNull(AnimationUtility.GetAnimatedObject(root, b), id + ": clip " + clip.name + " animates " + b.path + " which isn't there");
                    Assert.IsFalse(b.path.EndsWith("flame_base") && b.propertyName.StartsWith("m_LocalScale"),
                        clip.name + " scales flame_base, which the sim's light radius owns");
                }
            }
        }

        [TestCaseSource(nameof(SkinIds))]
        public void EverySkinPlaysEveryReaction(string id)
        {
            (WickSkin skin, ReactionDriver r) = Dress(id);
            Animator a = skin.Animator;

            AssertBody(r, a, new ReactionInput { Flickers = 3 }, "Idle");
            AssertBody(r, a, new ReactionInput { Flickers = 3, Move = 1 }, "Dash");
            AssertBody(r, a, new ReactionInput { Flickers = 3, Move = 2 }, "Run");
            AssertBody(r, a, new ReactionInput { Flickers = 3, Tiptoe = true }, "Tiptoe");
            AssertBody(r, a, new ReactionInput { Flickers = 1 }, "LowHealth");

            // A transition starts on one animator update and completes on the next, so step twice
            // (both well inside the hit clip).
            r.OnSimEvent(new SimEvent(SimEventType.FlickerLost, 0, default, 2));
            Step(r, a, new ReactionInput { Flickers = 2 }, 0.06f);
            Step(r, a, new ReactionInput { Flickers = 2 }, 0.06f);
            Assert.IsTrue(a.GetCurrentAnimatorStateInfo(1).IsName("Hit"), id + ": no hit reaction " + Layer1(a));

            Step(r, a, new ReactionInput { Flickers = 2 }, 2f);
            r.OnSimEvent(new SimEvent(SimEventType.TreasureTaken, 0, default, 100));
            Step(r, a, new ReactionInput { Flickers = 2 }, 0.06f);
            Step(r, a, new ReactionInput { Flickers = 2 }, 0.06f);
            Assert.IsTrue(a.GetCurrentAnimatorStateInfo(1).IsName("Grab"), id + ": no grab reaction");
            Assert.Greater(Weight(skin, "greed"), 50f, id + ": no greedy grin");

            for (int i = 0; i < 10; i++)
            {
                Step(r, a, new ReactionInput { Flickers = 2, Fear = 1f }, 0.1f);
            }

            Assert.Greater(Weight(skin, "fear"), 90f, id + ": no fear face");

            AssertBody(r, a, new ReactionInput { Flickers = 2, Outcome = (int)Outcome.Escaped }, "Escape");
            Assert.Greater(Weight(skin, "smug"), 50f, id + ": not smug after escaping");
            Step(r, a, new ReactionInput { Flickers = 2, Outcome = (int)Outcome.Escaped }, 3f);
            Assert.IsTrue(a.GetCurrentAnimatorStateInfo(0).IsName("Victory"), id + ": no victory after the escape hop");

            (WickSkin snuffed, ReactionDriver r2) = Dress(id);
            AssertBody(r2, snuffed.Animator, new ReactionInput { Flickers = 0, Outcome = (int)Outcome.Snuffed }, "Snuff");
        }

        private static void Step(ReactionDriver r, Animator a, ReactionInput input, float seconds)
        {
            r.Apply(input, seconds);
            a.Update(seconds);
        }

        private static void AssertBody(ReactionDriver r, Animator a, ReactionInput input, string state)
        {
            for (int i = 0; i < 4; i++)
            {
                Step(r, a, input, 0.25f);
            }

            AnimatorStateInfo info = a.GetCurrentAnimatorStateInfo(0);
            Assert.IsTrue(info.IsName(state), "expected " + state + " for Move " + input.Move + ", Tiptoe " + input.Tiptoe + ", Flickers " +
                input.Flickers + ", Outcome " + input.Outcome);
        }

        private static string Layer1(Animator a)
        {
            string Name(int hash)
            {
                foreach (string n in new[] { "Empty", "Hit", "Grab", "FidgetPat", "FidgetCount" })
                {
                    if (Animator.StringToHash(n) == hash || Animator.StringToHash("Actions." + n) == hash)
                    {
                        return n;
                    }
                }

                return hash.ToString();
            }

            return "(layers " + a.layerCount + ", weight " + a.GetLayerWeight(1) + ", current " + Name(a.GetCurrentAnimatorStateInfo(1).shortNameHash) +
                ", next " + Name(a.GetNextAnimatorStateInfo(1).shortNameHash) + ", in transition " + a.IsInTransition(1) + ")";
        }

        private static float Weight(WickSkin skin, string shape)
        {
            return skin.Face.GetBlendShapeWeight(skin.Face.sharedMesh.GetBlendShapeIndex(shape));
        }
    }
}
