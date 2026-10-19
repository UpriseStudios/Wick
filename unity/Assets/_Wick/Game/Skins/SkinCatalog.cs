using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Wick.Game
{
    /// <summary>
    /// One skin as the menus know it. The default skin ships in the app (Bundled); every other
    /// skin's model lives in the remote Skins group and is fetched by ContentLoader.
    /// </summary>
    [Serializable]
    public sealed class SkinListing
    {
        public string Id;
        public string DisplayName;
        public bool StandIn;

        /// <summary>Set only for the default skin, which ships in the app.</summary>
        public SkinDefinition Bundled;

        /// <summary>The skin in the Skins group (address = Id).</summary>
        public AssetReferenceT<SkinDefinition> Reference;
    }

    /// <summary>Every skin, and which one the player has equipped (kept on the device).</summary>
    [CreateAssetMenu(menuName = "Wick/Skin Catalog", fileName = "SkinCatalog")]
    public sealed class SkinCatalog : ScriptableObject
    {
        public const string DefaultId = "skin_default";
        private const string EquippedKey = "wick.skin";

        public List<SkinListing> Skins = new List<SkinListing>();

        /// <summary>The bundled default skin: always there, and the fallback for everything else.</summary>
        public SkinDefinition Default => Listing(DefaultId)?.Bundled;

        public SkinListing Listing(string id)
        {
            foreach (SkinListing s in Skins)
            {
                if (s.Id == id)
                {
                    return s;
                }
            }

            return null;
        }

        /// <summary>The equipped skin's id, or the default's if nothing (or an unknown skin) is equipped.</summary>
        public string EquippedId
        {
            get
            {
                string id = PlayerPrefs.GetString(EquippedKey, DefaultId);
                return Listing(id) != null ? id : DefaultId;
            }
        }

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
