using System;
using System.Collections.Generic;
using UnityEngine;

namespace Wick.Game
{
    /// <summary>What an effect is about, which decides the colours it may use (VfxRules).</summary>
    public enum VfxRole : byte
    {
        /// <summary>Wick's flame, braziers: Candle Flame and Flame Core only.</summary>
        Flame,

        /// <summary>Traps and guardians: Danger Red or Trap Teal, never flame orange.</summary>
        Danger,

        /// <summary>Gold, smoke, dust: palette colours, no rule beyond few, large particles.</summary>
        Neutral,
    }

    [Serializable]
    public sealed class VfxEntry
    {
        public string Id;
        public VfxRole Role;
        public GameObject Prefab;
        public bool StandIn;
    }

    /// <summary>Every effect, built by Wick > Art > Build VFX from Art/Incoming/vfx (stand-ins until then).</summary>
    [CreateAssetMenu(menuName = "Wick/Vfx Library", fileName = "VfxLibrary")]
    public sealed class VfxLibrary : ScriptableObject
    {
        public const string CoinBurst = "vfx_coin_burst";
        public const string FlameFlare = "vfx_flame_flare";
        public const string FlameSputter = "vfx_flame_sputter";
        public const string FlameSparks = "vfx_flame_sparks";
        public const string SnuffPuff = "vfx_snuff_puff";
        public const string WaterSplash = "vfx_water_splash";
        public const string GustSwirl = "vfx_gust_swirl";
        public const string WarningGlow = "vfx_warning_glow";

        public List<VfxEntry> Entries = new List<VfxEntry>();

        public VfxEntry Find(string id)
        {
            foreach (VfxEntry e in Entries)
            {
                if (e.Id == id)
                {
                    return e;
                }
            }

            return null;
        }
    }
}
