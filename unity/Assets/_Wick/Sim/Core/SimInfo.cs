namespace Wick.Sim
{
    /// <summary>
    /// Identifies the simulation rules. Bump Version on any sim rule change
    /// and update the golden replays deliberately (see CLAUDE.md).
    /// </summary>
    public static class SimInfo
    {
        // 2: flame light radius and line of sight. 3: traps, guardians, hits, pickups, scoring.
        public const int Version = 3;
    }
}
