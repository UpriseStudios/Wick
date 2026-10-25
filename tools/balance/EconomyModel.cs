using System;
using System.Collections.Generic;
using Wick.Economy;

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
    /// gold and stars accrue, a free workshop starts the next Vault Level as soon as its gold and
    /// star gate are met, and builds finish on time (free: plus the daily ad speed-ups) or at once
    /// with gems (spender, while gems last). Defence and Wick upgrades are left out: they compete for
    /// the same gold, so these are the fastest each player could level.
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
            long buildEndsHour = -1;
            int adSpeedupsToday = 0;
            AdPlacement doubleLoot = cfg.Placement("raid_double_loot");
            AdPlacement speedup = cfg.Placement("upgrade_speedup");
            AdPlacement chest = cfg.Placement("shop_chest");

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

                // Three sessions a day (morning, lunch, evening).
                if (hourOfDay == 8 || hourOfDay == 13 || hourOfDay == 20)
                {
                    RaidIncome r = income(level);
                    int raids = p.RaidsPerDay / 3;
                    int doubled = hourOfDay == 20 && doubleLoot != null ? Math.Min(doubleLoot.Daily, raids) : 0;
                    gold += (raids + doubled) * r.Gold;
                    stars += raids * r.Stars;
                }

                // Builds: ad speed-ups for everyone (the Club gets them without watching), gems for the spender.
                if (buildEndsHour >= 0)
                {
                    if (speedup != null && adSpeedupsToday < speedup.Daily && hourOfDay >= 8)
                    {
                        buildEndsHour -= Math.Max(0, speedup.Minutes / 60);
                        adSpeedupsToday++;
                    }

                    long leftHours = buildEndsHour - hour;
                    long cost = leftHours > 0 ? cfg.SpeedUp.GemsFor(leftHours * 3_600_000L) : 0;
                    if (p.GemsPerMonth > 0 && leftHours > 0 && gems >= cost)
                    {
                        gems -= cost;
                        buildEndsHour = hour;
                    }

                    if (hour >= buildEndsHour)
                    {
                        level++;
                        reached[level] = hour / 24.0;
                        buildEndsHour = -1;
                    }
                }

                if (buildEndsHour < 0 && level < 10)
                {
                    VaultLevelStep step = cfg.VaultLevelTo(level + 1);
                    if (step != null && gold >= step.Gold && stars >= step.Stars)
                    {
                        gold -= step.Gold;
                        buildEndsHour = hour + Math.Max(1, (step.Minutes + 59) / 60);
                    }
                }
            }

            return reached;
        }
    }
}
