namespace Wick.Sim
{
    public readonly struct RaidResult
    {
        public readonly Outcome Outcome;

        /// <summary>Number of ticks simulated.</summary>
        public readonly int Ticks;

        public readonly ulong FinalHash;

        /// <summary>Running hash at each fold, to pinpoint a mismatch to a one-second window.</summary>
        public readonly ulong[] Checkpoints;

        /// <summary>0-3, see Scoring.Stars.</summary>
        public readonly int Stars;

        /// <summary>Treasure value kept: carried value if Wick escaped, otherwise 0.</summary>
        public readonly int Loot;

        public readonly int Flickers;

        public RaidResult(Outcome outcome, int ticks, ulong finalHash, ulong[] checkpoints, int stars, int loot, int flickers)
        {
            Outcome = outcome;
            Ticks = ticks;
            FinalHash = finalHash;
            Checkpoints = checkpoints;
            Stars = stars;
            Loot = loot;
            Flickers = flickers;
        }

        public string HashHex => FormatHash(FinalHash);

        public static string FormatHash(ulong hash)
        {
            return hash.ToString("x16");
        }
    }

    /// <summary>Headless runner: simulates a whole raid with no presentation.</summary>
    public static class RaidRunner
    {
        public static RaidResult RunToEnd(VaultDef vault, int seed, InputLog log)
        {
            var sim = new RaidSim(vault, seed, log);
            while (!sim.IsOver)
            {
                sim.Step();
            }

            return sim.GetResult();
        }
    }
}
