using System;
using System.IO;
using Wick.Sim;

namespace Wick.ReplayCli
{
    /// <summary>
    /// Re-simulates raid files and prints outcome and hash.
    /// Exit codes: 0 all OK, 1 a result differs from "expected", 2 usage or load error.
    /// </summary>
    public static class Program
    {
        public static int Main(string[] args)
        {
            bool showCheckpoints = false;
            bool showEvents = false;
            int files = 0;
            int exitCode = 0;

            foreach (string arg in args)
            {
                if (arg == "--checkpoints")
                {
                    showCheckpoints = true;
                    continue;
                }

                if (arg == "--events")
                {
                    showEvents = true;
                    continue;
                }

                files++;
                exitCode = Math.Max(exitCode, Replay(arg, showCheckpoints, showEvents));
            }

            if (files == 0)
            {
                Console.Error.WriteLine("usage: replay-cli [--checkpoints] [--events] <raid.json> [more.json ...]");
                return 2;
            }

            return exitCode;
        }

        private static int Replay(string path, bool showCheckpoints, bool showEvents)
        {
            string name = Path.GetFileName(path);
            RaidFile raid;
            try
            {
                raid = RaidFile.Parse(File.ReadAllText(path));
            }
            catch (Exception e) when (e is IOException || e is FormatException || e is UnauthorizedAccessException)
            {
                Console.Error.WriteLine(name + ": error: " + e.Message);
                return 2;
            }

            if (raid.SimVersion != SimInfo.Version)
            {
                Console.Error.WriteLine(name + ": error: file is simVersion " + raid.SimVersion +
                    " but this build is simVersion " + SimInfo.Version);
                return 2;
            }

            var sim = new RaidSim(raid.Vault, raid.Seed, raid.Log);
            while (!sim.IsOver)
            {
                int tick = sim.State.Tick;
                sim.Step();
                if (showEvents)
                {
                    foreach (SimEvent e in sim.Events)
                    {
                        Console.WriteLine("  tick " + tick + ": " + e);
                    }
                }
            }

            RaidResult r = sim.GetResult();
            string line = name + ": outcome=" + r.Outcome + " ticks=" + r.Ticks + " stars=" + r.Stars +
                " loot=" + r.Loot + " hash=" + r.HashHex;

            int code = 0;
            if (raid.HasExpected)
            {
                bool ok = raid.Matches(r);
                line += ok
                    ? "  OK"
                    : "  MISMATCH (expected outcome=" + raid.ExpectedOutcome + " ticks=" + raid.ExpectedTicks +
                      (raid.ExpectedStars != null ? " stars=" + raid.ExpectedStars : "") +
                      (raid.ExpectedLoot != null ? " loot=" + raid.ExpectedLoot : "") +
                      " hash=" + raid.ExpectedHash + ")";
                code = ok ? 0 : 1;
            }

            Console.WriteLine(line);
            if (showCheckpoints)
            {
                for (int i = 0; i < r.Checkpoints.Length; i++)
                {
                    Console.WriteLine("  checkpoint " + i + ": " + RaidResult.FormatHash(r.Checkpoints[i]));
                }
            }

            return code;
        }
    }
}
