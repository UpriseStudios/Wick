using System;
using System.Collections.Generic;
using Wick.Economy;

namespace Wick.CloudCode
{
    /// <summary>Economy player keys (Protected, like the rest: readable by the player, written only here).</summary>
    public static class EconomyKeys
    {
        public const string Inventory = "inventory";
        public const string Upgrades = "upgrades";
        public const string Pass = "pass";
        public const string Progress = "progress";
        public const string Ads = "ads";
    }

    /// <summary>What the player owns besides currency.</summary>
    public sealed class Inventory
    {
        public List<string> Cosmetics { get; set; } = new List<string>();

        /// <summary>Workshop slots earned on top of the base (starter pack, achievements).</summary>
        public int Workshops { get; set; }

        public long ClubUntilMs { get; set; }

        /// <summary>The UTC day (yyyymmdd) the club's daily gems were last claimed.</summary>
        public int ClubGemsDay { get; set; }

        /// <summary>Store order ids already granted: a receipt pays once, however often it's sent.</summary>
        public List<string> Orders { get; set; } = new List<string>();

        public List<string> Products { get; set; } = new List<string>();

        /// <summary>Extra Daily Vault attempts from ads, spent when Daily Vault arrives (M11).</summary>
        public int DailyVaultRetries { get; set; }
    }

    /// <summary>The player's Vault Level and Lantern level (both raised through upgrades).</summary>
    public sealed class Progression
    {
        public int VaultLevel { get; set; } = 1;
        public int Lantern { get; set; }

        /// <summary>Defence upgrade levels by type name ("drip_pipe"); missing types are level 1.</summary>
        public Dictionary<string, int> Defences { get; set; } = new Dictionary<string, int>();

        public int DefenceLevel(Wick.Sim.ObjectType type)
        {
            return OwnedDefences.Level(Defences, type);
        }

        /// <summary>The levels a vault built at vaultLevel gets: what the player owns, capped for that level.</summary>
        public Wick.Sim.DefenceLevels DefenceLevelsFor(int vaultLevel)
        {
            return OwnedDefences.For(Defences, vaultLevel);
        }
    }

    public sealed class RunningUpgrade
    {
        public string Kind { get; set; }
        public int ToLevel { get; set; }
        public long StartedMs { get; set; }
        public long EndsMs { get; set; }
    }

    /// <summary>upgrades: progression levels and the builds in progress (one per workshop).</summary>
    public sealed class Upgrades
    {
        public Progression Levels { get; set; } = new Progression();
        public List<RunningUpgrade> Running { get; set; } = new List<RunningUpgrade>();

        public RunningUpgrade Find(string kind)
        {
            return Running.Find(u => u.Kind == kind);
        }

        /// <summary>
        /// The record as of server time now: builds whose time is up count as done, so an upgrade
        /// completes on its own (nobody presses Finish) even while the app is closed. Every reader
        /// goes through this; writers then store the settled record.
        /// </summary>
        public static Upgrades Read(string json, long nowMs)
        {
            Upgrades upgrades = Json.Read<Upgrades>(json);
            upgrades.Settle(nowMs);
            return upgrades;
        }

        /// <summary>Applies every build that has ended; returns the kinds it finished.</summary>
        public List<string> Settle(long nowMs)
        {
            var finished = new List<string>();
            foreach (RunningUpgrade u in Running.FindAll(r => r.EndsMs <= nowMs))
            {
                Apply(u);
                finished.Add(u.Kind);
            }

            return finished;
        }

        /// <summary>Raises the level a build was for and frees its workshop.</summary>
        public void Apply(RunningUpgrade running)
        {
            if (running.Kind == UpgradeKinds.VaultLevel)
            {
                Levels.VaultLevel = Math.Max(Levels.VaultLevel, running.ToLevel);
            }
            else if (UpgradeKinds.TryParseDefence(running.Kind, out Wick.Sim.ObjectType type))
            {
                Levels.Defences[Wick.Sim.ObjectTypes.Name(type)] = Math.Max(Levels.DefenceLevel(type), running.ToLevel);
            }
            else
            {
                Levels.Lantern = Math.Max(Levels.Lantern, running.ToLevel);
            }

            Running.Remove(running);
        }
    }

    /// <summary>pass: this season's Heist Pass progress.</summary>
    public sealed class PassState
    {
        public string Season { get; set; }
        public long Xp { get; set; }

        /// <summary>Seasons whose premium row the player owns (bought before or during them).</summary>
        public List<string> PremiumSeasons { get; set; } = new List<string>();

        public List<int> ClaimedFree { get; set; } = new List<int>();
        public List<int> ClaimedPremium { get; set; } = new List<int>();

        /// <summary>UTC day (yyyymmdd) of the last daily-first-win bonus.</summary>
        public int FirstWinDay { get; set; }

        [Newtonsoft.Json.JsonIgnore]
        public bool Premium => PremiumSeasons.Contains(Season);
    }

    /// <summary>progress: stats behind achievements, and which tiers have been claimed.</summary>
    public sealed class Progress
    {
        public long Raids { get; set; }
        public long PerfectEscapes { get; set; }
        public long Snuffs { get; set; }

        /// <summary>Other players who opened this player's vault link (raided it by code), up to 100.</summary>
        public List<string> LinkOpeners { get; set; } = new List<string>();

        /// <summary>Achievement id to the highest tier claimed (1 bronze, 2 silver, 3 gold).</summary>
        public Dictionary<string, int> Claimed { get; set; } = new Dictionary<string, int>();
    }

    /// <summary>ads: today's rewarded-ad counts and recent grants (for ClaimAdReward and dedupe).</summary>
    public sealed class AdState
    {
        public const int Keep = 40;

        /// <summary>UTC day (yyyymmdd) the counts are for.</summary>
        public int Day { get; set; }

        public Dictionary<string, int> Counts { get; set; } = new Dictionary<string, int>();
        public int Total { get; set; }
        public List<AdGrant> Grants { get; set; } = new List<AdGrant>();

        /// <summary>Raids whose loot an ad already doubled.</summary>
        public List<string> DoubledRaids { get; set; } = new List<string>();
    }

    public sealed class AdGrant
    {
        /// <summary>The client's nonce for this ad view, carried through LevelPlay's callback.</summary>
        public string Nonce { get; set; }

        /// <summary>LevelPlay's event id: a callback repeated by the network grants once.</summary>
        public string EventId { get; set; }

        public string Placement { get; set; }
        public bool Granted { get; set; }
        public string Reason { get; set; }
        public Wick.Economy.Grant Grant { get; set; }
        public long AtMs { get; set; }
    }
}
