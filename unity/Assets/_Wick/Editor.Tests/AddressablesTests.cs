using NUnit.Framework;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using Wick.Game;

namespace Wick.Editor.Tests
{
    /// <summary>Core ships in the app; skins, themes and seasonal content are remote, one id per entry.</summary>
    public sealed class AddressablesTests
    {
        private static AddressableAssetSettings Settings => AddressableAssetSettingsDefaultObject.GetSettings(false);

        [Test]
        public void CoreIsLocal_SkinsThemesAndSeasonalAreRemote()
        {
            Assert.IsNotNull(Settings, "run Wick > Art > Set Up Addressables");
            Assert.AreEqual(AddressableAssetSettings.kLocalLoadPath, Group("Core").LoadPath.GetName(Settings));
            foreach (string name in AddressablesSetup.RemoteGroups)
            {
                Assert.AreEqual(AddressableAssetSettings.kRemoteLoadPath, Group(name).LoadPath.GetName(Settings), name + " loads remotely");
            }

            Assert.IsTrue(Settings.BuildRemoteCatalog, "a remote catalog lets content update without an app update");
        }

        [Test]
        public void ReleaseLoadsFromTheCdn_DevelopmentStaysOnTheDevice()
        {
            AddressableAssetProfileSettings p = Settings.profileSettings;
            string release = p.GetProfileId(AddressablesSetup.Release);
            string dev = p.GetProfileId(AddressablesSetup.Development);
            Assert.AreEqual(AddressablesSetup.Cdn, p.GetValueByName(release, AddressableAssetSettings.kRemoteLoadPath));
            Assert.AreEqual(p.GetValueByName(dev, AddressableAssetSettings.kLocalLoadPath), p.GetValueByName(dev, AddressableAssetSettings.kRemoteLoadPath));
        }

        [Test]
        public void EveryRemoteSkinAndThemeIsAnEntry_AddressedByItsId()
        {
            foreach (SkinListing s in AssetDatabase.LoadAssetAtPath<SkinCatalog>(SkinBuilder.CatalogPath).Skins)
            {
                if (s.Bundled != null)
                {
                    continue;
                }

                AddressableAssetEntry e = Settings.FindAssetEntry(s.Reference.AssetGUID);
                Assert.IsNotNull(e, s.Id + " isn't addressable");
                Assert.AreEqual("Skins", e.parentGroup.Name);
                Assert.AreEqual(s.Id, e.address);
            }

            foreach (ThemeListing t in AssetDatabase.LoadAssetAtPath<ThemeCatalog>(ThemeBuilder.CatalogPath).Themes)
            {
                if (t.Bundled != null)
                {
                    continue;
                }

                AddressableAssetEntry e = Settings.FindAssetEntry(t.Reference.AssetGUID);
                Assert.IsNotNull(e, t.Id + " isn't addressable");
                Assert.AreEqual("Themes", e.parentGroup.Name);
                Assert.AreEqual(t.Id, e.address);
            }
        }

        private static BundledAssetGroupSchema Group(string name)
        {
            AddressableAssetGroup g = Settings.FindGroup(name);
            Assert.IsNotNull(g, name + " group");
            return g.GetSchema<BundledAssetGroupSchema>();
        }
    }
}
