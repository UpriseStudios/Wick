namespace Wick.Sim
{
    /// <summary>
    /// Identifies the simulation rules. Bump Version on any sim rule change
    /// and update the golden replays deliberately (see CLAUDE.md).
    /// </summary>
    public static class SimInfo
    {
        // 2: flame light radius and line of sight. 3: traps, guardians, hits, pickups, scoring.
        // 4 (M10): vault sizes per Vault Level, raid params, new traps, guardians and defence levels.
        // 5 (M10.1): pits (falls, respawn at the entrance, exit lock, level 5 loot drop).
        public const int Version = 5;
    }
}
