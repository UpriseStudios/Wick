namespace Wick.Sim
{
    /// <summary>
    /// Sim rule constants. Changing any value changes raid results:
    /// bump SimInfo.Version and update golden replays deliberately.
    /// </summary>
    public static class SimTuning
    {
        public const int TicksPerSecond = 30;
        public const int RaidTicks = 60 * TicksPerSecond;
        public const int HashIntervalTicks = TicksPerSecond;

        public const int StartFlickers = 3;

        /// <summary>Sub-tile units per tick during a dash (5 ticks per tile).</summary>
        public const int DashSpeed = 200;

        /// <summary>Sub-tile units per tick while running (10 ticks per tile).</summary>
        public const int RunSpeed = 100;

        public const int TiptoeSpeedDivisor = 2;

        /// <summary>Sub-tile units per tick while Wick is knocked back (4 ticks per tile).</summary>
        public const int KnockSpeed = 250;

        /// <summary>Sub-tile units per tick while Wick slides on ice at level 1 (4 ticks per tile).</summary>
        public const int IceSlideSpeed = 250;

        /// <summary>Flame light radius in sub-tile units at 3, 2 and 1 flickers.</summary>
        public const int LightRadius3Flickers = 4 * SubTile.PerTile;
        public const int LightRadius2Flickers = 3 * SubTile.PerTile;
        public const int LightRadius1Flicker = 2 * SubTile.PerTile;

        /// <summary>Tiptoe divides the light radius by this.</summary>
        public const int TiptoeLightDivisor = 2;

        /// <summary>How long a decoy distracts guardians (3 seconds).</summary>
        public const int DecoyTicks = 3 * TicksPerSecond;

        /// <summary>A decoy's light strength for wax moths, in sub-tile units.</summary>
        public const int DecoyLightStrength = 4 * SubTile.PerTile;

        // ---- Hits ----------------------------------------------------------------------

        /// <summary>Invulnerability after losing a flicker (1.5 seconds).</summary>
        public const int InvulnTicks = 45;

        // ---- Traps (timed: phase = (tick + offset) % cycle) ------------------------------

        /// <summary>Drip Pipe damages its tile while phase is below this.</summary>
        public const int DripWindowTicks = 6;

        /// <summary>Bellows gust is live while phase is below this.</summary>
        public const int GustTicks = 15;

        /// <summary>How many tiles a gust travels from the bellows.</summary>
        public const int GustRange = 6;

        /// <summary>A gust dims Wick's light by this much for GustDimTicks.</summary>
        public const int GustDimUnits = SubTile.PerTile;
        public const int GustDimTicks = 2 * TicksPerSecond;

        /// <summary>The dimmest a gust can make a lit flame.</summary>
        public const int MinLightRadius = SubTile.PerTile / 2;

        /// <summary>Each pass of a Swing Axe's blade over a tile is live for this many ticks.</summary>
        public const int AxeSweepTicks = 6;

        /// <summary>Spike Floor is raised (damaging) while phase is below this.</summary>
        public const int SpikeRaisedTicks = 20;

        /// <summary>The warning rumble starts this many ticks before the spikes rise.</summary>
        public const int SpikeWarnTicks = 15;

        // ---- Guardians -----------------------------------------------------------------

        public const int OwlPatrolSpeed = 50;
        public const int OwlChaseSpeed = 100;

        /// <summary>Night Owl sight range in tiles.</summary>
        public const int OwlSightRange = 5;

        /// <summary>Ticks without a sighting before the owl gives up the chase.</summary>
        public const int OwlLoseTicks = 2 * TicksPerSecond;

        public const int MothSpeed = 40;

        /// <summary>Moths sense a light from this far beyond its radius, in sub-tile units.</summary>
        public const int MothSenseBonus = 2 * SubTile.PerTile;

        /// <summary>Continuous moth contact drains 1 flicker every this many ticks.</summary>
        public const int MothDrainTicks = 2 * TicksPerSecond;

        /// <summary>Mimic Chest: ticks Wick must stay on or beside it before it bites (level 1).</summary>
        public const int MimicLungeTicks = 18;

        /// <summary>A Mimic Chest's bite costs this many flickers.</summary>
        public const int MimicBiteFlickers = 2;

        // ---- Treasure ------------------------------------------------------------------

        /// <summary>Each treasure pile carried slows Wick by this percentage...</summary>
        public const int SlowPercentPerPile = 10;

        /// <summary>...up to this total.</summary>
        public const int MaxSlowPercent = 40;

        /// <summary>Validation cap per pile, keeping carried totals far from int overflow.</summary>
        public const int MaxTreasureValue = 1000000;
    }
}
