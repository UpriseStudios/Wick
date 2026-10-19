using Wick.Sim;

namespace Wick.Game
{
    /// <summary>
    /// Which sound each sim event makes (docs/asset-production.md, Audio assets). Sounds are
    /// driven only by sim events and Wick's movement, never the other way round.
    /// </summary>
    public static class SoundMap
    {
        public const string Step = "sfx_step";
        public const string Tiptoe = "sfx_tiptoe";
        public const string Dash = "sfx_dash";
        public const string FlameFlare = "sfx_flame_flare";
        public const string FlickerLost = "sfx_flicker_lost";
        public const string Snuff = "sfx_snuff";
        public const string GoldGrab = "sfx_gold_grab";
        public const string Escape = "sfx_escape";
        public const string Drip = "sfx_drip";
        public const string Bellows = "sfx_bellows";
        public const string SpikeWarning = "sfx_spike_warning";
        public const string SnufferBell = "sfx_snuffer_bell";
        public const string OwlAlert = "sfx_owl_alert";
        public const string Moths = "sfx_moths";
        public const string UiTap = "sfx_ui_tap";

        public static readonly string[] All =
        {
            Step, Tiptoe, Dash, FlameFlare, FlickerLost, Snuff, GoldGrab, Escape, Drip, Bellows, SpikeWarning, SnufferBell, OwlAlert, Moths, UiTap,
        };

        /// <summary>The cue for an event raised by a vault object of type `source` (Wick's own events: any type), or null.</summary>
        public static string For(SimEventType type, ObjectType source)
        {
            switch (type)
            {
                case SimEventType.TrapWarning:
                    return source == ObjectType.SpikeFloor ? SpikeWarning : null;
                case SimEventType.TrapFired:
                    switch (source)
                    {
                        case ObjectType.DripPipe: return Drip;
                        case ObjectType.Bellows: return Bellows;
                        case ObjectType.SnufferBell: return SnufferBell;
                        default: return null;
                    }

                case SimEventType.GuardianAlerted:
                    return source == ObjectType.WaxMoths ? Moths : OwlAlert;
                case SimEventType.FlickerLost: return FlickerLost;
                case SimEventType.Snuffed: return Snuff;
                case SimEventType.TreasureTaken: return GoldGrab;
                case SimEventType.BrazierUsed: return FlameFlare;
                case SimEventType.NearMiss: return Dash;
                default: return null;
            }
        }

        /// <summary>Events that briefly duck the music so the moment lands.</summary>
        public static bool Ducks(SimEventType type)
        {
            return type == SimEventType.Snuffed || type == SimEventType.FlickerLost || type == SimEventType.TreasureTaken;
        }
    }
}
