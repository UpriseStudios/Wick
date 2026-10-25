using System;
using System.Collections.Generic;
using Wick.Economy;
using Wick.Sim;

namespace Wick.Balance
{
    /// <summary>What one raid at a Vault Level earns on average, from the bot's raids.</summary>
    public sealed class RaidIncome
    {
        public double Gold;
        public double Wax;
        public double Stars;
        public double SnuffRate;
    }

    /// <summary>
    /// How long it takes to raise each Vault Level, for a daily free player and a spender, from the
    /// economy config and the bot's average raid income. Hour by hour: raids in three sessions a day,
    /// gold and stars accrue. On reaching a Vault Level the player first builds every defence it
    /// unlocks (they must be built before they can be placed), then raises the next Vault Level once
    /// its gold and star gate are met. Builds take workshop time: free players watch the daily ad
    /// speed-ups, the spender finishes builds with gems while they last. Defence upgrades, Wick
    /// upgrades and walls are optional spending, so they're left out.
    /// </summary>
    public static class EconomyModel
    {
        public sealed class Player
        {
            public string Name;
            public int RaidsPerDay = 15;
            public bool Club;
            public bool StarterPack;
            public long GemsPerMonth;
        }

        public static Player Free => new Player { Name = "Daily free player" };

        /// <summary>Starter pack, Wick Club and the £19.99 gem pack each month: a committed spender.</summary>
        public static Player Spender => new Player { Name = "Spender", Club = true, StarterPack = true, GemsPerMonth = 2600 };

        private sealed class Build
        {
            public long Gold;
            public long EndsHour = -1;
            public int Hours;
            public bool VaultLevel;
        }

        /// <summary>Days from the start to reaching each Vault Level (index = level; 1 is day 0). -1 if not within a year.</summary>
        public static double[] DaysToLevel(EconomyConfig cfg, Player p, Func<int, RaidIncome> income)
        {
            var reached = new double[11];
            for (int i = 2; i <= 10; i++)
            {
                reached[i] = -1;
            }

            int level = 1;
            double gold = 0;
            double stars = 0;
            double gems = p.StarterPack ? 500 : 0;
            int adSpeedupsToday = 0;
            var pending = new Queue<Build>(); // defence builds still to start
            var running = new List<Build>();
            AdPlacement doubleLoot = cfg.Placement("raid_double_loot");
            AdPlacement speedup = cfg.Placement("upgrade_speedup");
            AdPlacement chest = cfg.Placement("shop_chest");

            int Workshops() => 1 + (p.StarterPack ? 1 : 0) + (p.Club ? 1 : 0) + (!p.StarterPack && level >= 4 ? 1 : 0);

            for (long hour = 0; hour < 24 * 365 && level < 10; hour++)
            {
                int hourOfDay = (int)(hour % 24);
                if (hourOfDay == 0)
                {
                    adSpeedupsToday = 0;
                    gems += p.Club ? cfg.Club.DailyGems : 0;
                    gems += p.GemsPerMonth / 30.0;
                    if (chest?.Grant != null)
                    {
                        gold += chest.Daily * chest.Grant.Gold;
                        gems += chest.Daily * chest.Grant.Gems;
                    }
                }

                if (hourOfDay == 8 || hourOfDay == 13 || hourOfDay == 20)
                {
                    RaidIncome r = income(level);
                    int raids = p.RaidsPerDay / 3;
                    int doubled = hourOfDay == 20 && doubleLoot != null ? Math.Min(doubleLoot.Daily, raids) : 0;
                    gold += (raids + doubled) * r.Gold;
                    stars += raids * r.Stars;
                }

                // Running builds: ad speed-ups (the Club gets them without watching), gems for the spender.
                foreach (Build b in running)
                {
                    if (speedup != null && adSpeedupsToday < speedup.Daily && hourOfDay >= 8 && b.EndsHour > hour)
                    {
                        b.EndsHour -= Math.Max(0, speedup.Minutes / 60);
                        adSpeedupsToday++;
                    }

                    long left = b.EndsHour - hour;
                    long cost = left > 0 ? cfg.SpeedUp.GemsFor(left * 3_600_000L) : 0;
                    if (p.GemsPerMonth > 0 && left > 0 && gems >= cost)
                    {
                        gems -= cost;
                        b.EndsHour = hour;
                    }
                }

                foreach (Build b in running.FindAll(b => hour >= b.EndsHour))
                {
                    running.Remove(b);
                    if (b.VaultLevel)
                    {
                        level++;
                        reached[level] = hour / 24.0;
                        foreach (ObjectType t in UnlockedAt(level))
                        {
                            DefenceBuild build = cfg.BuildOf(t);
                            if (build != null)
                            {
                                pending.Enqueue(new Build { Gold = build.Gold, Hours = Math.Max(1, (build.Minutes + 59) / 60) });
                            }
                        }
                    }
                }

                // Start defence builds first (they're needed to defend), then the next Vault Level.
                while (pending.Count > 0 && running.Count < Workshops() && gold >= pending.Peek().Gold)
                {
                    Build b = pending.Dequeue();
                    gold -= b.Gold;
                    b.EndsHour = hour + b.Hours;
                    running.Add(b);
                }

                bool vaultLevelBuilding = running.Exists(b => b.VaultLevel);
                if (pending.Count == 0 && !vaultLevelBuilding && running.Count < Workshops() && level < 10)
                {
                    VaultLevelStep step = cfg.VaultLevelTo(level + 1);
                    if (step != null && gold >= step.Gold && stars >= step.Stars)
                    {
                        gold -= step.Gold;
                        running.Add(new Build { VaultLevel = true, EndsHour = hour + Math.Max(1, (step.Minutes + 59) / 60) });
                    }
                }
            }

            return reached;
        }

        private static IEnumerable<ObjectType> UnlockedAt(int level)
        {
            for (int i = 0; i < ObjectTypes.Count; i++)
            {
                var t = (ObjectType)i;
                if (VaultRules.RequiredLevel(t) == level)
                {
                    yield return t;
                }
            }
        }
    }
}
