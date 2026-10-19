using UnityEngine;
using Wick.Sim;

namespace Wick.Game
{
    /// <summary>
    /// Which prefab shows each sim entity, and the vault themes that build the tiles (ThemeKit).
    /// Swap prefabs here for real art.
    /// </summary>
    [CreateAssetMenu(menuName = "Wick/View Registry", fileName = "ViewRegistry")]
    public sealed class ViewRegistry : ScriptableObject
    {
        [Header("Wick")]
        public GameObject Wick;
        public GameObject Decoy;

        [Header("Tiles")]
        public ThemeCatalog Themes;

        [Header("Objects")]
        public GameObject DripPipe;
        public GameObject Bellows;
        public GameObject SpikeFloor;
        public GameObject SnufferBell;
        public GameObject NightOwl;
        public GameObject WaxMoths;
        public GameObject Treasure;
        public GameObject Brazier;
        public GameObject Crate;

        public GameObject ForObject(ObjectType type)
        {
            switch (type)
            {
                case ObjectType.DripPipe: return DripPipe;
                case ObjectType.Bellows: return Bellows;
                case ObjectType.SpikeFloor: return SpikeFloor;
                case ObjectType.SnufferBell: return SnufferBell;
                case ObjectType.NightOwl: return NightOwl;
                case ObjectType.WaxMoths: return WaxMoths;
                case ObjectType.Treasure: return Treasure;
                case ObjectType.Brazier: return Brazier;
                case ObjectType.Crate: return Crate;
                default: return null;
            }
        }

        /// <summary>The chosen theme, or the bundled default until a remote one has downloaded.</summary>
        public ThemeDefinition Theme => Themes != null ? ContentLoader.Theme(Themes, Themes.CurrentId) : null;
    }
}
