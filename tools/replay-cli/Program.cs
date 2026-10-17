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
            int files = 0;
            int exitCode = 0;

            foreach (string arg in args)
            {
                if (arg == "--checkpoints")
                {
                    showCheckpoints = true;
                    continue;
                }

                files++;
                exitCode = Math.Max(exitCode, Replay(arg, showCheckpoints));
            }

            if (files == 0)
            {
                Console.Error.WriteLine("usage: replay-cli [--checkpoints] <raid.json> [more.json ...]");
                return 2;
            }

            return exitCode;
        }

        private static int Replay(string path, bool showCheckpoints)
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

            RaidResult r = RaidRunner.RunToEnd(raid.Vault, raid.Seed, raid.Log);
            string line = name + ": outcome=" + r.Outcome + " ticks=" + r.Ticks + " hash=" + r.HashHex;

            int code = 0;
            if (raid.HasExpected)
            {
                bool ok = r.Outcome == raid.ExpectedOutcome && r.Ticks == raid.ExpectedTicks && r.HashHex == raid.ExpectedHash;
                line += ok
                    ? "  OK"
                    : "  MISMATCH (expected outcome=" + raid.ExpectedOutcome + " ticks=" + raid.ExpectedTicks +
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
