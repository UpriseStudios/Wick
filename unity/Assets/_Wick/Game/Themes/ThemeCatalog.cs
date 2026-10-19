using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Wick.Game
{
    /// <summary>One theme as the menus know it: the default ships in the app, the rest are remote.</summary>
    [Serializable]
    public sealed class ThemeListing
    {
        public string Id;
        public string DisplayName;
        public bool StandIn;

        /// <summary>Set only for the default theme, which ships in the app.</summary>
        public ThemeDefinition Bundled;

        /// <summary>The theme in the Themes group (address = Id).</summary>
        public AssetReferenceT<ThemeDefinition> Reference;
    }

    /// <summary>
    /// Every vault theme, and which one this device shows vaults in. Themes are presentation only,
    /// so the choice stays on the device (vault owners choosing a theme for their vault comes later).
    /// </summary>
    [CreateAssetMenu(menuName = "Wick/Theme Catalog", fileName = "ThemeCatalog")]
    public sealed class ThemeCatalog : ScriptableObject
    {
        public const string DefaultId = "theme_treasury";
        private const string ChosenKey = "wick.theme";

        public List<ThemeListing> Themes = new List<ThemeListing>();

        public ThemeDefinition Default => Listing(DefaultId)?.Bundled;

        public string CurrentId
        {
            get
            {
                string id = PlayerPrefs.GetString(ChosenKey, DefaultId);
                return Listing(id) != null ? id : DefaultId;
            }
        }

        public ThemeListing Listing(string id)
        {
            foreach (ThemeListing t in Themes)
            {
                if (t.Id == id)
                {
                    return t;
                }
            }

            return null;
        }

        public static void Choose(string id)
        {
            PlayerPrefs.SetString(ChosenKey, id);
            PlayerPrefs.Save();
        }
    }
}
