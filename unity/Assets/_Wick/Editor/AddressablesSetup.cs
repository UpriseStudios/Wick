using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;
using Wick.Game;

namespace Wick.Editor
{
    /// <summary>
    /// The Addressables layout. Core is local and ships in the app. Skins, Themes and Seasonal
    /// are remote (CDN), one bundle per skin or theme, with a versioned remote catalog that
    /// ContentLoader checks at start-up. Two profiles:
    ///   Development: "remote" groups are built into the app too, so dev builds work offline and
    ///                before the CDN exists.
    ///   Release:     remote groups go to ServerData/ for upload, and load from the CDN.
    /// Menu: Wick > Art > Set Up Addressables. Also run by Wick > Rebuild Placeholder Content.
    /// </summary>
    public static class AddressablesSetup
    {
        public const string Cdn = "https://cdn.uprisestudios.co.uk/wick/[BuildTarget]";
        public const string Development = "Development";
        public const string Release = "Release";
        public static readonly string[] RemoteGroups = { "Skins", "Themes", "Seasonal" };

        [MenuItem("Wick/Art/Set Up Addressables")]
        public static void ConfigureMenu()
        {
            Configure(AssetDatabase.LoadAssetAtPath<SkinCatalog>(SkinBuilder.CatalogPath),
                AssetDatabase.LoadAssetAtPath<ThemeCatalog>(ThemeBuilder.CatalogPath));
            AssetDatabase.SaveAssets();
            Debug.Log("AddressablesSetup: groups, profiles and entries updated.");
        }

        public static AddressableAssetSettings Configure(SkinCatalog skins, ThemeCatalog themes)
        {
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
            settings.BuildRemoteCatalog = true;
            settings.RemoteCatalogBuildPath.SetVariableByName(settings, AddressableAssetSettings.kRemoteBuildPath);
            settings.RemoteCatalogLoadPath.SetVariableByName(settings, AddressableAssetSettings.kRemoteLoadPath);
            settings.OverridePlayerVersion = "wick"; // a stable catalog name; its hash file versions it
            settings.DisableCatalogUpdateOnStartup = true; // ContentLoader checks, with a fallback
            settings.BuildAddressablesWithPlayerBuild = AddressableAssetSettings.PlayerBuildOption.BuildWithPlayer;

            Profiles(settings);
            AddressableAssetGroup core = Group(settings, "Core", remote: false);
            settings.DefaultGroup = core;
            AddressableAssetGroup leftover = settings.FindGroup("Default Local Group");
            if (leftover != null && leftover.entries.Count == 0)
            {
                settings.RemoveGroup(leftover); // Core is the one local group
            }
            AddressableAssetGroup skinGroup = Group(settings, "Skins", remote: true);
            AddressableAssetGroup themeGroup = Group(settings, "Themes", remote: true);
            Group(settings, "Seasonal", remote: true);
            foreach (string label in new[] { "skins", "themes", "seasonal" })
            {
                settings.AddLabel(label);
            }

            if (skins != null)
            {
                foreach (SkinListing s in skins.Skins)
                {
                    Entry(settings, skinGroup, s.Reference?.AssetGUID, s.Id, "skins");
                }
            }

            if (themes != null)
            {
                foreach (ThemeListing t in themes.Themes)
                {
                    Entry(settings, themeGroup, t.Reference?.AssetGUID, t.Id, "themes");
                }
            }

            EditorUtility.SetDirty(settings);
            return settings;
        }

        /// <summary>Selects the profile builds use (Development for dev APKs, Release for store builds).</summary>
        public static void UseProfile(string name)
        {
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.GetSettings(false);
            if (settings != null)
            {
                settings.activeProfileId = settings.profileSettings.GetProfileId(name);
                EditorUtility.SetDirty(settings);
            }
        }

        private static void Profiles(AddressableAssetSettings settings)
        {
            AddressableAssetProfileSettings p = settings.profileSettings;
            string baseId = p.GetProfileId("Default");
            string localBuild = p.GetValueByName(baseId, AddressableAssetSettings.kLocalBuildPath);
            string localLoad = p.GetValueByName(baseId, AddressableAssetSettings.kLocalLoadPath);

            string dev = p.GetProfileId(Development);
            if (string.IsNullOrEmpty(dev))
            {
                dev = p.AddProfile(Development, baseId);
            }

            p.SetValue(dev, AddressableAssetSettings.kRemoteBuildPath, localBuild);
            p.SetValue(dev, AddressableAssetSettings.kRemoteLoadPath, localLoad);

            string release = p.GetProfileId(Release);
            if (string.IsNullOrEmpty(release))
            {
                release = p.AddProfile(Release, baseId);
            }

            p.SetValue(release, AddressableAssetSettings.kRemoteBuildPath, "ServerData/[BuildTarget]");
            p.SetValue(release, AddressableAssetSettings.kRemoteLoadPath, Cdn);
            settings.activeProfileId = dev;
        }

        private static AddressableAssetGroup Group(AddressableAssetSettings settings, string name, bool remote)
        {
            AddressableAssetGroup group = settings.FindGroup(name) ??
                settings.CreateGroup(name, false, false, true, null, typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));
            var bundled = group.GetSchema<BundledAssetGroupSchema>() ?? group.AddSchema<BundledAssetGroupSchema>();
            bundled.BuildPath.SetVariableByName(settings, remote ? AddressableAssetSettings.kRemoteBuildPath : AddressableAssetSettings.kLocalBuildPath);
            bundled.LoadPath.SetVariableByName(settings, remote ? AddressableAssetSettings.kRemoteLoadPath : AddressableAssetSettings.kLocalLoadPath);
            bundled.BundleMode = BundledAssetGroupSchema.BundlePackingMode.PackSeparately; // one download per skin or theme
            bundled.RetryCount = remote ? 2 : 0;
            bundled.Timeout = remote ? 20 : 0;
            bundled.UseAssetBundleCrc = remote;
            var update = group.GetSchema<ContentUpdateGroupSchema>() ?? group.AddSchema<ContentUpdateGroupSchema>();
            update.StaticContent = !remote; // remote content can change without an app update
            return group;
        }

        private static void Entry(AddressableAssetSettings settings, AddressableAssetGroup group, string guid, string address, string label)
        {
            if (string.IsNullOrEmpty(guid))
            {
                return; // bundled content (the defaults) isn't addressable
            }

            AddressableAssetEntry entry = settings.CreateOrMoveEntry(guid, group, false, false);
            entry.address = address;
            entry.SetLabel(label, true, false, false);
        }
    }
}
