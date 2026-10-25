using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Wick.Bots;
using Wick.Economy;
using Wick.Sim;

namespace Wick.Balance
{
    /// <summary>
    /// The M10 balance simulator (docs/m10-plan.md, B2). For each Vault Level it generates worst-case
    /// vaults (BalanceGen), raids them with the heuristic Bot as a raider with that level's Wick
    /// upgrades, and models time-to-level for a daily free player and a spender (EconomyModel).
    /// Writes a Markdown report and flags levels that look wrong.
    ///
    /// Usage (from the repo root):
    ///   dotnet run -c Release --project tools/balance               full run, writes tools/balance/report.md
    ///   dotnet run -c Release --project tools/balance -- --quick    small sample for CI; exits 1 on a failed check
    ///   options: --vaults N, --seed N, --out path
    /// </summary>
    public static class Program
    {
        private const string EconomyPath = "unity/Assets/_Wick/Resources/Economy/economy-default.json";

        private sealed class Raid
        {
            public Outcome Outcome;
            public int Stars;
            public int Loot;
            public int Ticks;
            public double Paid;
        }

        public static int Main(string[] args)
        {
            bool quick = args.Contains("--quick");
            if (args.Contains("--economy-only"))
            {
                return EconomyOnly(EconomyConfig.Parse(File.ReadAllText(EconomyPath)));
            }

            int vaults = Arg(args, "--vaults", quick ? 12 : 200);
            int seed = Arg(args, "--seed", 2026);
            string outPath = ArgS(args, "--out", quick ? null : "tools/balance/report.md");
            EconomyConfig cfg = EconomyConfig.Parse(File.ReadAllText(EconomyPath));

            var perLevel = new Dictionary<int, List<Raid>>();
            var made = new Dictionary<int, int>();
            for (int level = 1; level <= VaultRules.MaxVaultLevel; level++)
            {
                RaidParams raider = RaiderAt(cfg, level);
                var raids = new Raid[vaults];
                int lvl = level;
                Parallel.For(0, vaults, i =>
                {
                    var rng = new Random(seed * 1000 + lvl * 100_000 + i);
                    VaultDef vault = BalanceGen.Make(lvl, rng, "v_balance_" + lvl + "_" + i);
                    if (vault == null)
                    {
                        return;
                    }

                    RaidResult r = new Bot().Play(vault, rng.Next(), raider, out _);
                    raids[i] = new Raid
                    {
                        Outcome = r.Outcome,
                        Stars = r.Stars,
                        Loot = r.Loot,
                        Ticks = r.Ticks,
                        Paid = r.Outcome == Outcome.Escaped
                            ? Math.Min(r.Loot, cfg.Loot.CapPerLevel * lvl)
                            : cfg.Loot.ConsolationPerLevel * lvl,
                    };
                });

                perLevel[level] = raids.Where(r => r != null).ToList();
                made[level] = perLevel[level].Count;
                Console.WriteLine("Vault Level " + level + ": " + made[level] + " vaults raided");
            }

            RaidIncome Income(int level)
            {
                List<Raid> rs = perLevel[level];
                return new RaidIncome
                {
                    Gold = rs.Average(r => r.Paid),
                    Stars = rs.Average(r => r.Stars),
                    Wax = rs.Average(r => r.Stars) * cfg.Loot.WaxPerStar,
                    SnuffRate = rs.Count(r => r.Outcome == Outcome.Snuffed) / (double)rs.Count,
                };
            }

            if (!quick)
            {
                SaveIncome(Income);
            }

            double[] free = EconomyModel.DaysToLevel(cfg, EconomyModel.Free, Income);
            double[] spender = EconomyModel.DaysToLevel(cfg, EconomyModel.Spender, Income);

            var flags = new List<string>();
            string report = Report(cfg, perLevel, free, spender, flags, vaults, seed);
            if (outPath != null)
            {
                File.WriteAllText(outPath, report);
                Console.WriteLine("Wrote " + outPath);
            }

            foreach (string f in flags)
            {
                Console.WriteLine("FLAG: " + f);
            }

            // CI fails only on what must never happen: a level where nothing could be generated or escaped.
            bool broken = Enumerable.Range(1, VaultRules.MaxVaultLevel)
                .Any(l => made[l] == 0 || perLevel[l].All(r => r.Outcome != Outcome.Escaped));
            if (broken)
            {
                Console.WriteLine("FAILED: a Vault Level produced no valid vaults or no escapes");
            }

            return quick && broken ? 1 : 0;
        }

        private const string IncomePath = "tools/balance/income.json";

        /// <summary>Saves each level's average raid income, so --economy-only can retune prices without re-raiding.</summary>
        private static void SaveIncome(Func<int, RaidIncome> income)
        {
            var w = new JsonWriter();
            w.BeginArray();
            for (int level = 1; level <= VaultRules.MaxVaultLevel; level++)
            {
                RaidIncome r = income(level);
                w.BeginObject().Field("level", level).Field("gold", (long)Math.Round(r.Gold)).Field("starsX100", (long)Math.Round(r.Stars * 100))
                    .EndObject();
            }

            w.EndArray();
            File.WriteAllText(IncomePath, w.ToString());
        }

        /// <summary>--economy-only: the time-to-level table from the saved raid income and the current economy config.</summary>
        private static int EconomyOnly(EconomyConfig cfg)
        {
            JsonNode saved = JsonReader.Parse(File.ReadAllText(IncomePath));
            RaidIncome Income(int level)
            {
                JsonNode n = saved[level - 1];
                return new RaidIncome { Gold = n.GetInt("gold"), Stars = n.GetInt("starsX100") / 100.0 };
            }

            double[] free = EconomyModel.DaysToLevel(cfg, EconomyModel.Free, Income);
            double[] spender = EconomyModel.DaysToLevel(cfg, EconomyModel.Spender, Income);
            Console.WriteLine("Level  free days (step)   spender days  speed-up");
            for (int level = 2; level <= VaultRules.MaxVaultLevel; level++)
            {
                Console.WriteLine(level.ToString().PadLeft(5) + "  " + free[level].ToString("0.0").PadLeft(8) + " (" +
                    (free[level] - free[level - 1]).ToString("0.0") + ")   " + spender[level].ToString("0.0").PadLeft(8) + "     " +
                    (spender[level] > 0 ? (free[level] / spender[level]).ToString("0.00") + "x" : "-"));
            }

            return 0;
        }

        /// <summary>A raider at this Vault Level with the best Wick upgrades it allows (Lantern, decoys).</summary>
        private static RaidParams RaiderAt(EconomyConfig cfg, int level)
        {
            int lantern = cfg.Lantern.Where(l => l.MinVaultLevel <= level).Select(l => l.Level).DefaultIfEmpty(0).Max();
            int decoys = cfg.Decoys.Where(d => d.MinVaultLevel <= level).Select(d => d.Level).DefaultIfEmpty(0).Max();
            return cfg.WickParams(cfg.Raid.TicksFor(level), lantern, decoys);
        }

        private static string Report(EconomyConfig cfg, Dictionary<int, List<Raid>> perLevel, double[] free, double[] spender,
            List<string> flags, int vaults, int seed)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# Balance report");
            sb.AppendLine();
            sb.AppendLine("Generated by `tools/balance` (" + vaults + " worst-case vaults per Vault Level, seed " + seed + ", sim version " +
                SimInfo.Version + "). Vaults are filled to most of their build budget with every defence their level unlocks, each at the " +
                "highest upgrade level the Vault Level allows. The raider is a heuristic bot (a decent player, not a perfect one) with " +
                "the best Wick upgrades for that level. Rerun after any change to the sim or the economy.");
            sb.AppendLine();
            sb.AppendLine("## Raids per Vault Level");
            sb.AppendLine();
            sb.AppendLine("| Vault Level | Raid length | Escaped | Snuffed | Timed out | Avg stars | Avg loot (escapes) | Gold paid per raid | Fastest escape | Median escape | Slowest escape |");
            sb.AppendLine("| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |");
            for (int level = 1; level <= VaultRules.MaxVaultLevel; level++)
            {
                List<Raid> rs = perLevel[level];
                if (rs.Count == 0)
                {
                    sb.AppendLine("| " + level + " | no vaults generated | | | | | | | | | |");
                    flags.Add("Vault Level " + level + ": no valid vaults generated");
                    continue;
                }

                List<Raid> esc = rs.Where(r => r.Outcome == Outcome.Escaped).OrderBy(r => r.Ticks).ToList();
                double escRate = esc.Count / (double)rs.Count;
                string Pct(Outcome o) => (rs.Count(r => r.Outcome == o) * 100.0 / rs.Count).ToString("0", CultureInfo.InvariantCulture) + "%";
                string Sec(int ticks) => (ticks / (double)SimTuning.TicksPerSecond).ToString("0.0", CultureInfo.InvariantCulture) + " s";
                int raidSeconds = cfg.Raid.TicksFor(level) / SimTuning.TicksPerSecond;
                sb.AppendLine("| " + level + " | " + raidSeconds + " s | " + Pct(Outcome.Escaped) + " | " + Pct(Outcome.Snuffed) + " | " +
                    Pct(Outcome.TimedOut) + " | " + rs.Average(r => r.Stars).ToString("0.00", CultureInfo.InvariantCulture) + " | " +
                    (esc.Count == 0 ? "-" : esc.Average(r => r.Loot).ToString("0", CultureInfo.InvariantCulture)) + " | " +
                    rs.Average(r => r.Paid).ToString("0", CultureInfo.InvariantCulture) + " | " +
                    (esc.Count == 0 ? "-" : Sec(esc[0].Ticks)) + " | " + (esc.Count == 0 ? "-" : Sec(esc[esc.Count / 2].Ticks)) + " | " +
                    (esc.Count == 0 ? "-" : Sec(esc[esc.Count - 1].Ticks)) + " |");

                if (escRate < 0.4)
                {
                    flags.Add("Vault Level " + level + ": the bot escapes only " + (escRate * 100).ToString("0", CultureInfo.InvariantCulture) +
                        "% of worst-case vaults (below 40%)");
                }

                if (esc.Count > 0 && esc[0].Ticks < 15 * SimTuning.TicksPerSecond)
                {
                    flags.Add("Vault Level " + level + ": fastest escape " + Sec(esc[0].Ticks) + " (under 15 s)");
                }

                double timeouts = rs.Count(r => r.Outcome == Outcome.TimedOut) / (double)rs.Count;
                if (timeouts > 0.25)
                {
                    flags.Add("Vault Level " + level + ": " + (timeouts * 100).ToString("0", CultureInfo.InvariantCulture) +
                        "% of raids run out of time (consider a longer raid at this level)");
                }
            }

            sb.AppendLine();
            sb.AppendLine("## Time to reach each Vault Level");
            sb.AppendLine();
            sb.AppendLine("Days from starting the game, raiding " + EconomyModel.Free.RaidsPerDay + " times a day with the bot's average income at " +
                "the player's own level. The free player watches the rewarded ads; the spender has the starter pack, Wick Club and the " +
                "£19.99 gem pack every month, and spends gems finishing Vault Level builds. Defence and Wick upgrades aren't bought, so " +
                "these are the fastest each could level. Target (game spec): the spender at most about 2x faster.");
            sb.AppendLine();
            sb.AppendLine("| Vault Level | Free player (days) | Days for this level | Spender (days) | Spender speed-up |");
            sb.AppendLine("| --- | --- | --- | --- | --- |");
            double previousStep = 0;
            for (int level = 2; level <= VaultRules.MaxVaultLevel; level++)
            {
                string D(double d) => d < 0 ? "over a year" : d.ToString("0.0", CultureInfo.InvariantCulture);
                double step = free[level] >= 0 && free[level - 1] >= 0 ? free[level] - free[level - 1] : -1;
                double ratio = free[level] > 0 && spender[level] > 0 ? free[level] / spender[level] : 0;
                sb.AppendLine("| " + level + " | " + D(free[level]) + " | " + D(step) + " | " + D(spender[level]) + " | " +
                    (ratio > 0 ? ratio.ToString("0.00", CultureInfo.InvariantCulture) + "x" : "-") + " |");

                if (ratio > 2.0)
                {
                    flags.Add("Vault Level " + level + ": the spender is " + ratio.ToString("0.0", CultureInfo.InvariantCulture) +
                        "x faster than a daily free player (target: about 2x at most)");
                }

                if (previousStep > 0 && step > previousStep * 1.5)
                {
                    flags.Add("Vault Level " + level + ": " + step.ToString("0.0", CultureInfo.InvariantCulture) + " days for this level, " +
                        (step / previousStep * 100 - 100).ToString("0", CultureInfo.InvariantCulture) + "% more than the level before (over 50%)");
                }

                if (step < 0)
                {
                    flags.Add("Vault Level " + level + ": a daily free player doesn't reach it within a year");
                }

                previousStep = step > 0 ? step : previousStep;
            }

            sb.AppendLine();
            sb.AppendLine("## Flags");
            sb.AppendLine();
            if (flags.Count == 0)
            {
                sb.AppendLine("None.");
            }

            foreach (string f in flags)
            {
                sb.AppendLine("- " + f);
            }

            return sb.ToString();
        }

        private static int Arg(string[] args, string name, int fallback)
        {
            int i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length && int.TryParse(args[i + 1], out int v) ? v : fallback;
        }

        private static string ArgS(string[] args, string name, string fallback)
        {
            int i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : fallback;
        }
    }
}
