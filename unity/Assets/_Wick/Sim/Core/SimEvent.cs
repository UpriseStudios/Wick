namespace Wick.Sim
{
    public enum SimEventType : byte
    {
        /// <summary>A trap is about to fire (Spike Floor rumble).</summary>
        TrapWarning,

        /// <summary>A trap fired: drip falls, gust starts, spikes rise, bell drops.</summary>
        TrapFired,

        /// <summary>Wick lost a flicker. Value = flickers left.</summary>
        FlickerLost,

        /// <summary>Wick's flame went out.</summary>
        Snuffed,

        /// <summary>Wick took a treasure pile. Value = its value.</summary>
        TreasureTaken,

        /// <summary>Wick relit at a brazier. Value = flickers after.</summary>
        BrazierUsed,

        /// <summary>A Night Owl spotted Wick and started chasing.</summary>
        GuardianAlerted,

        /// <summary>A trap fired right next to Wick without hitting him.</summary>
        NearMiss,

        /// <summary>Wick was knocked back. Value = tiles he will slide; Tile = where he slides from.</summary>
        KnockedBack,
    }

    /// <summary>
    /// Something that happened this tick, for views, audio and clip highlights.
    /// Events are output only: the sim never reads them and they are not hashed.
    /// </summary>
    public readonly struct SimEvent
    {
        public readonly SimEventType Type;

        /// <summary>Vault object id of the source, or 0 for Wick himself.</summary>
        public readonly int EntityId;

        public readonly TilePos Tile;
        public readonly int Value;

        public SimEvent(SimEventType type, int entityId, TilePos tile, int value)
        {
            Type = type;
            EntityId = entityId;
            Tile = tile;
            Value = value;
        }

        public override string ToString()
        {
            return Type + "(id " + EntityId + " at " + Tile + ", " + Value + ")";
        }
    }
}
