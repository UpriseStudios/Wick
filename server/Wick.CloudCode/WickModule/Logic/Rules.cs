using System;

namespace Wick.CloudCode
{
    /// <summary>
    /// Server-side raid economy and matchmaking numbers in one place, to tune now and move into
    /// Remote Config with the economy in M8. Crown deltas are in Wick.Sim (CrownRules).
    /// </summary>
    public static class Rules
    {
        public const long MinuteMs = 60 * 1000;
        public const long HourMs = 60 * MinuteMs;

        // ---- Raid tokens -------------------------------------------------------------

        public const long TokenLifetimeMs = 5 * MinuteMs;

        /// <summary>Tokens a player can take per rolling hour (matchmaking, friend and revenge raids).</summary>
        public const int RaidsPerHour = 20;

        /// <summary>Matchmaking skips vaults the player raided this recently.</summary>
        public const long RecentTargetMs = 24 * HourMs;

        // ---- Matchmaking -------------------------------------------------------------

        /// <summary>Crown band half-widths, tried in order; after the last, any rating will do.</summary>
        public static readonly int[] Bands = { 100, 200, 400, 800, 1600 };

        /// <summary>Random candidates fetched per query.</summary>
        public const int SampleSize = 25;

        // ---- Loot and defence ----------------------------------------------------------

        /// <summary>Most gold one raid can pay out, by the defender's Vault Level.</summary>
        public static long LootCap(int defenderLevel)
        {
            return 200L * Math.Max(1, defenderLevel);
        }

        /// <summary>Gold a defender earns each time a raid on their vault fails.</summary>
        public static long DefenceBonus(int defenderLevel)
        {
            return 10L * Math.Max(1, defenderLevel);
        }

        /// <summary>A robbed defender loses the raider's payout, but never more than this share of their gold.</summary>
        public const int MaxLossPercent = 20;

        public static long DefenderLoss(long payout, long defenderGold)
        {
            return Math.Max(0, Math.Min(payout, defenderGold * MaxLossPercent / 100));
        }

        /// <summary>After a successful raid the vault can't be raided for this long.</summary>
        public const long ShieldMs = 2 * HourMs;

        // ---- Push ----------------------------------------------------------------------

        /// <summary>At most one defence push per player per this long; news in between is batched.</summary>
        public const long PushIntervalMs = 30 * MinuteMs;

        // ---- Anomaly scoring -----------------------------------------------------------

        /// <summary>A single raid scoring this or more is flagged in its record.</summary>
        public const int FlagRaidScore = 60;

        /// <summary>Scores kept per player, and how many are needed before the average counts.</summary>
        public const int ScoreWindow = 10;

        public const int ScoreMinimumRaids = 5;

        /// <summary>An average at or above this moves the account to the review pool (no ban).</summary>
        public const int ReviewAverage = 50;
    }
}
