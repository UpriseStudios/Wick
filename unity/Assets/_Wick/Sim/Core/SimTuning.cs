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

        /// <summary>How long a decoy distracts guardians (3 seconds).</summary>
        public const int DecoyTicks = 3 * TicksPerSecond;
    }
}
