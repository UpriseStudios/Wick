using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Wick.Game.Tests
{
    /// <summary>Every effect follows the art pack's VFX rules: few large particles, danger never in flame orange.</summary>
    public class VfxTests
    {
        private const string LibraryPath = "Assets/_Wick/Art/Vfx/VfxLibrary.asset";
        private const string SkinCatalogPath = "Assets/_Wick/Art/Skins/SkinCatalog.asset";

        public static IEnumerable<string> EffectIds()
        {
            foreach (VfxEntry e in AssetDatabase.LoadAssetAtPath<VfxLibrary>(LibraryPath).Entries)
            {
                yield return e.Id;
            }
        }

        [TestCaseSource(nameof(EffectIds))]
        public void EveryEffectFollowsTheRules(string id)
        {
            VfxEntry e = AssetDatabase.LoadAssetAtPath<VfxLibrary>(LibraryPath).Find(id);
            Assert.IsNotNull(e.Prefab, id + " has no prefab");
            List<string> problems = VfxRules.Check(id, e.Role, e.Prefab);
            Assert.IsEmpty(problems, string.Join("\n", problems));
        }

        [Test]
        public void EverySkinsFlameAndSnuffFollowTheRules()
        {
            foreach (SkinDefinition s in AssetDatabase.LoadAssetAtPath<SkinCatalog>(SkinCatalogPath).Skins)
            {
                Assert.IsNotNull(s.SnuffVfx, s.Id + " has no snuff effect");
                Assert.IsEmpty(VfxRules.Check(s.Id + " snuff", VfxRole.Neutral, s.SnuffVfx));
                if (s.FlameVfx != null)
                {
                    Assert.IsEmpty(VfxRules.Check(s.Id + " flame", VfxRole.Flame, s.FlameVfx));
                }
            }
        }

        [Test]
        public void DangerInFlameOrange_IsCaught()
        {
            var go = new GameObject("Bad effect (test)");
            try
            {
                ParticleSystem.MainModule main = go.AddComponent<ParticleSystem>().main;
                main.startColor = new Color(1f, 0.62f, 0.11f); // Candle Flame on a trap
                main.startSize = 0.5f;
                main.maxParticles = 4;
                Assert.IsNotEmpty(VfxRules.Check("bad", VfxRole.Danger, go));
                Assert.IsEmpty(VfxRules.Check("fine", VfxRole.Flame, go), "the same colour is right for a flame");

                main.startColor = new Color(0.9f, 0.28f, 0.3f); // Danger Red
                Assert.IsEmpty(VfxRules.Check("red", VfxRole.Danger, go));

                main.maxParticles = 200;
                Assert.IsNotEmpty(VfxRules.Check("busy", VfxRole.Danger, go), "a swarm of tiny particles breaks the style");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
