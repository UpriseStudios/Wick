using System.Collections.Generic;
using UnityEngine;

namespace Wick.Game
{
    /// <summary>Every skin, and which one the player has equipped (kept on the device).</summary>
    [CreateAssetMenu(menuName = "Wick/Skin Catalog", fileName = "SkinCatalog")]
    public sealed class SkinCatalog : ScriptableObject
    {
        public const string DefaultId = "skin_default";
        private const string EquippedKey = "wick.skin";

        public List<SkinDefinition> Skins = new List<SkinDefinition>();

        public SkinDefinition Default => Find(DefaultId) ?? (Skins.Count > 0 ? Skins[0] : null);

        public SkinDefinition Find(string id)
        {
            foreach (SkinDefinition s in Skins)
            {
                if (s != null && s.Id == id)
                {
                    return s;
                }
            }

            return null;
        }

        /// <summary>The equipped skin, or the default if nothing (or an unknown skin) is equipped.</summary>
        public SkinDefinition Equipped => Find(PlayerPrefs.GetString(EquippedKey, DefaultId)) ?? Default;

        /// <summary>Equips a skin the player owns; the default skin is always owned.</summary>
        public static bool Equip(string id, IReadOnlyCollection<string> owned)
        {
            if (id != DefaultId && (owned == null || !Contains(owned, id)))
            {
                return false;
            }

            PlayerPrefs.SetString(EquippedKey, id);
            PlayerPrefs.Save();
            return true;
        }

        private static bool Contains(IReadOnlyCollection<string> list, string id)
        {
            foreach (string s in list)
            {
                if (s == id)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
