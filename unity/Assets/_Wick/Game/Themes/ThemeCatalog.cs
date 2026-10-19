using System.Collections.Generic;
using UnityEngine;

namespace Wick.Game
{
    /// <summary>
    /// Every vault theme, and which one this device shows vaults in. Themes are presentation only,
    /// so the choice stays on the device (vault owners choosing a theme for their vault comes later).
    /// </summary>
    [CreateAssetMenu(menuName = "Wick/Theme Catalog", fileName = "ThemeCatalog")]
    public sealed class ThemeCatalog : ScriptableObject
    {
        public const string DefaultId = "theme_treasury";
        private const string ChosenKey = "wick.theme";

        public List<ThemeDefinition> Themes = new List<ThemeDefinition>();

        public ThemeDefinition Default => Find(DefaultId) ?? (Themes.Count > 0 ? Themes[0] : null);

        public ThemeDefinition Current => Find(PlayerPrefs.GetString(ChosenKey, DefaultId)) ?? Default;

        public ThemeDefinition Find(string id)
        {
            foreach (ThemeDefinition t in Themes)
            {
                if (t != null && t.Id == id)
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
