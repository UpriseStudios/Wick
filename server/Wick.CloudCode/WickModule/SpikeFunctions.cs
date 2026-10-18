using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Unity.Services.CloudCode.Core;
using Wick.Sim;

namespace Wick.CloudCode
{
    /// <summary>M5 spike: is the module alive, and how long does a raid re-simulation take here?</summary>
    public class SpikeFunctions
    {
        [CloudCodeFunction("Ping")]
        public Task<int> Ping()
        {
            return Task.FromResult(SimInfo.Version);
        }

        /// <summary>
        /// Re-simulates a golden raid a number of times and reports the timings: "full-heist"
        /// (317 ticks) or "timeout-wander" (the full 60 seconds, 1800 ticks).
        /// </summary>
        [CloudCodeFunction("TimeResim")]
        public Task<ResimTiming> TimeResim(string golden, int runs)
        {
            runs = Math.Max(1, Math.Min(runs, 200));
            RaidFile file = RaidFile.Parse(Golden(golden == "timeout-wander" ? golden : "full-heist"));

            var times = new double[runs];
            RaidResult last = default;
            for (int i = 0; i < runs; i++)
            {
                Stopwatch sw = Stopwatch.StartNew();
                last = RaidRunner.RunToEnd(file.Vault, file.Seed, file.Log);
                times[i] = sw.Elapsed.TotalMilliseconds;
            }

            double first = times[0];
            Array.Sort(times);
            return Task.FromResult(new ResimTiming
            {
                SimVersion = SimInfo.Version,
                Runs = runs,
                Ticks = last.Ticks,
                FirstMs = first,
                MedianMs = times[runs / 2],
                MaxMs = times[runs - 1],
                Hash = last.HashHex,
                MatchesGolden = file.Matches(last),
            });
        }

        private static string Golden(string name)
        {
            using Stream s = typeof(SpikeFunctions).Assembly.GetManifestResourceStream("golden." + name + ".json");
            using var reader = new StreamReader(s);
            return reader.ReadToEnd();
        }
    }

    public class ResimTiming
    {
        public int SimVersion { get; set; }
        public int Runs { get; set; }
        public int Ticks { get; set; }

        /// <summary>The first run includes JIT warm-up on a fresh worker.</summary>
        public double FirstMs { get; set; }

        public double MedianMs { get; set; }
        public double MaxMs { get; set; }
        public string Hash { get; set; }
        public bool MatchesGolden { get; set; }
    }
}
