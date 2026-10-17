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

        public RaidResult(Outcome outcome, int ticks, ulong finalHash, ulong[] checkpoints)
        {
            Outcome = outcome;
            Ticks = ticks;
            FinalHash = finalHash;
            Checkpoints = checkpoints;
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
