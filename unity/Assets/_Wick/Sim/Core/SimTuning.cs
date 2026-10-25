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

        /// <summary>A fan blows for this many ticks at the start of each quarter turn.</summary>
        public const int FanGustTicks = 10;

        /// <summary>How many tiles a fan's wind reaches.</summary>
        public const int FanRange = 3;

        /// <summary>A Snuffer Bell lands this many ticks after Wick touches its plate (level 1).</summary>
        public const int BellDropTicks = 8;

        /// <summary>Spike Floor is raised (damaging) while phase is below this.</summary>
        public const int SpikeRaisedTicks = 20;

        /// <summary>The warning rumble starts this many ticks before the spikes rise.</summary>
        public const int SpikeWarnTicks = 15;

        /// <summary>Lever Doors: how long a gate stays open after Wick steps off its lever (level 1).</summary>
        public const int GateOpenTicks = 8 * TicksPerSecond;

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

        /// <summary>Sleepy Troll: Wick's light radius (sub-tile units) that wakes it at level 1.</summary>
        public const int TrollWakeLight = 3 * SubTile.PerTile;

        /// <summary>A bright enough flame wakes the troll from this many tiles away (Chebyshev).</summary>
        public const int TrollWakeRange = 2;

        /// <summary>Ticks from waking to the first stomp, then between stomps.</summary>
        public const int TrollStompDelay = 15;
        public const int TrollStompCycle = 30;

        /// <summary>Undisturbed this long, an awake troll falls back asleep.</summary>
        public const int TrollSleepTicks = 3 * TicksPerSecond;

        /// <summary>Sir Whiskers wakes when Wick's flame lights his tile from within this many tiles.</summary>
        public const int WhiskersWakeRange = 3;

        /// <summary>Sub-tile units per tick: stalking at a crouch (slower than Wick's run), and padding home.</summary>
        public const int WhiskersStalkSpeed = 70;
        public const int WhiskersReturnSpeed = 50;

        /// <summary>Wick still for this long and Sir Whiskers loses interest (1.5 s).</summary>
        public const int WhiskersFreezeTicks = 45;

        /// <summary>The wiggle before every pounce: the tell, exactly 30 ticks (the wiggle clip's length).</summary>
        public const int WhiskersWiggleTicks = 30;

        /// <summary>After a pounce he sits and licks a paw, very pleased with himself, for this long.</summary>
        public const int WhiskersLickTicks = 60;

        /// <summary>A swat knocks Wick this many tiles.</summary>
        public const int WhiskersKnockback = 2;

        /// <summary>He notices a decoy from this far (with line of sight).</summary>
        public const int WhiskersDecoyRange = 6;

        // ---- Pits (docs/m10-1-pits.md) ---------------------------------------------------

        /// <summary>Ticks Wick spends in a level 1 pit before popping out at the entrance (0.8 s).</summary>
        public const int PitFallTicks = 24;

        /// <summary>After a pit respawn the exit stays shut this long (8 s), so diving in saves no time. 0 disables it.</summary>
        public const int PitRespawnExitLockTicks = 240;

        /// <summary>Whether knockback and fan pushes can shove Wick into a pit; if not, they stop at its edge. To be decided from the balance simulator.</summary>
        public const bool KnockbackIntoPits = false;

        // ---- Treasure ------------------------------------------------------------------

        /// <summary>Each treasure pile carried slows Wick by this percentage...</summary>
        public const int SlowPercentPerPile = 10;

        /// <summary>...up to this total.</summary>
        public const int MaxSlowPercent = 40;

        /// <summary>Validation cap per pile, keeping carried totals far from int overflow.</summary>
        public const int MaxTreasureValue = 1000000;
    }
}
