using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Wick.Game.Tests
{
    /// <summary>Remote content falls back to the bundled default; it never leaves a raid without a skin or theme.</summary>
    public class ContentLoaderTests
    {
        [Test]
        public void BundledWins_ThenFetched_ThenTheDefault()
        {
            var bundled = ScriptableObject.CreateInstance<SkinDefinition>();
            var fallback = ScriptableObject.CreateInstance<SkinDefinition>();
            try
            {
                Assert.AreSame(bundled, ContentLoader.Resolve(bundled, null, fallback));
                Assert.AreSame(fallback, ContentLoader.Resolve<SkinDefinition>(null, null, fallback), "nothing to fetch: the default");
                Assert.AreSame(fallback, ContentLoader.Resolve<SkinDefinition>(null, new AssetReference("not-a-guid"), fallback),
                    "a broken reference: the default");
            }
            finally
            {
                Object.DestroyImmediate(bundled);
                Object.DestroyImmediate(fallback);
            }
        }

        [Test]
        public void TheDefaultsShipInTheApp_AndEveryListingResolvesToItself()
        {
            var skins = AssetDatabase.LoadAssetAtPath<SkinCatalog>("Assets/_Wick/Art/Skins/SkinCatalog.asset");
            var themes = AssetDatabase.LoadAssetAtPath<ThemeCatalog>("Assets/_Wick/Art/Themes/ThemeCatalog.asset");
            Assert.IsNotNull(skins.Default);
            Assert.IsNotNull(themes.Default);
            foreach (SkinListing s in skins.Skins)
            {
                Assert.AreEqual(s.Id, ContentLoader.Skin(skins, s.Id).Id);
            }

            foreach (ThemeListing t in themes.Themes)
            {
                Assert.AreEqual(t.Id, ContentLoader.Theme(themes, t.Id).Id);
            }

            Assert.AreEqual(SkinCatalog.DefaultId, ContentLoader.Skin(skins, "skin_never_made").Id);
        }
    }
}
