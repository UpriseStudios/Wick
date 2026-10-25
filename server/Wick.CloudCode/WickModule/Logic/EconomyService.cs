using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Wick.Economy;
using Wick.Sim;

namespace Wick.CloudCode
{
    /// <summary>UTC calendar days as yyyymmdd, for daily caps and bonuses.</summary>
    public static class Days
    {
        public static int Of(long ms)
        {
            DateTime d = DateTimeOffset.FromUnixTimeMilliseconds(ms).UtcDateTime;
            return d.Year * 10000 + d.Month * 100 + d.Day;
        }
    }

    /// <summary>Applies grants. Grants only ever hold currency, cosmetics, slots and access (see Grant).</summary>
    public static class Grants
    {
        public static void Apply(Grant g, EconomyConfig cfg, Wallet wallet, Inventory inventory, PassState pass)
        {
            if (g == null)
            {
                return;
            }

            wallet.Gold += g.Gold;
            wallet.Wax += g.Wax;
            wallet.Gems += g.Gems;
            if (inventory != null)
            {
                inventory.Workshops = Math.Min(Math.Max(0, cfg.Workshops.Max - cfg.Workshops.Base), inventory.Workshops + g.Workshops);
                foreach (string c in g.Cosmetics ?? new List<string>())
                {
                    if (!inventory.Cosmetics.Contains(c))
                    {
                        inventory.Cosmetics.Add(c);
                    }
                }
            }

            if (pass != null && !string.IsNullOrEmpty(g.PassPremium) && !pass.PremiumSeasons.Contains(g.PassPremium))
            {
                pass.PremiumSeasons.Add(g.PassPremium);
            }
        }

        public static bool ClubActive(Inventory inventory, long nowMs)
        {
            return inventory.ClubUntilMs > nowMs;
        }

        /// <summary>Base, plus earned slots, plus the club's, up to the cap.</summary>
        public static int Workshops(EconomyConfig cfg, Inventory inventory, long nowMs)
        {
            int slots = cfg.Workshops.Base + inventory.Workshops + (ClubActive(inventory, nowMs) ? cfg.Club.Workshops : 0);
            return Math.Min(cfg.Workshops.Max, slots);
        }
    }

    public static class PassSeason
    {
        /// <summary>Starts a new season's progress when the config moves on; owned premium seasons stay.</summary>
        public static void Sync(PassState pass, EconomyConfig cfg)
        {
            if (pass.Season == cfg.Pass.Season)
            {
                return;
            }

            pass.Season = cfg.Pass.Season;
            pass.Xp = 0;
            pass.ClaimedFree.Clear();
            pass.ClaimedPremium.Clear();
        }

        public static bool Running(EconomyConfig cfg, long nowMs)
        {
            DateTime now = DateTimeOffset.FromUnixTimeMilliseconds(nowMs).UtcDateTime;
            return now >= cfg.Pass.StartsUtc && now < cfg.Pass.EndsUtc;
        }

        public static void AddXp(PassState pass, EconomyConfig cfg, long xp, long nowMs)
        {
            Sync(pass, cfg);
            if (Running(cfg, nowMs))
            {
                pass.Xp += xp;
            }
        }
    }

    public sealed class EconomyResult
    {
        public bool Ok { get; set; }
        public string Reason { get; set; }
        public Grant Granted { get; set; }

        public static EconomyResult Fail(string reason)
        {
            return new EconomyResult { Ok = false, Reason = reason };
        }

        public static EconomyResult Done(string reason, Grant granted = null)
        {
            return new EconomyResult { Ok = true, Reason = reason, Granted = granted };
        }
    }

    /// <summary>
    /// Wallet, upgrades (server-timed, with workshop slots and star gates), the Heist Pass,
    /// achievements and the club's daily gems. Every change is one locked batch.
    /// </summary>
    public sealed class EconomyService
    {
        private readonly IGameStore _store;
        private readonly IClock _clock;
        private readonly IEconomySource _economy;

        public EconomyService(IGameStore store, IClock clock, IEconomySource economy)
        {
            _store = store;
            _clock = clock;
            _economy = economy;
        }

        // ---- State ------------------------------------------------------------------------

        public async Task<EconomyState> GetStateAsync(string playerId)
        {
            EconomyConfig cfg = await _economy.GetAsync();
            bool needsRetest = await new DefenceSync(_store, _clock).SyncAsync(playerId); // upgrades finish on their own
            var data = await _store.GetProtectedAsync(playerId, new[]
            {
                Keys.Wallet, Keys.Stats, Keys.Profile, EconomyKeys.Upgrades, EconomyKeys.Inventory, EconomyKeys.Pass,
                EconomyKeys.Progress, EconomyKeys.Ads,
            });
            string Get(string k) => data.TryGetValue(k, out StoredItem i) ? i.Json : null;
            long now = _clock.NowMs;
            EconomyState state = EconomyState.Build(cfg, now, Json.Read<Wallet>(Get(Keys.Wallet)), Json.Read<RaidStats>(Get(Keys.Stats)),
                Json.Read<Profile>(Get(Keys.Profile)), Upgrades.Read(Get(EconomyKeys.Upgrades), now), Json.Read<Inventory>(Get(EconomyKeys.Inventory)),
                Json.Read<PassState>(Get(EconomyKeys.Pass)), Json.Read<Progress>(Get(EconomyKeys.Progress)), Json.Read<AdState>(Get(EconomyKeys.Ads)));
            state.VaultNeedsRetest = needsRetest;
            return state;
        }

        /// <summary>The player's Vault Level (1 until they raise it).</summary>
        public async Task<int> VaultLevelAsync(string playerId)
        {
            var data = await _store.GetProtectedAsync(playerId, new[] { EconomyKeys.Upgrades });
            return Upgrades.Read(data.TryGetValue(EconomyKeys.Upgrades, out StoredItem i) ? i.Json : null, _clock.NowMs).Levels.VaultLevel;
        }

        // ---- Upgrades -----------------------------------------------------------------------

        public async Task<EconomyResult> StartUpgradeAsync(string playerId, string kind)
        {
            EconomyConfig cfg = await _economy.GetAsync();
            return await Locked.UpdateAsync(_store, playerId, new[] { Keys.Wallet, Keys.Stats, EconomyKeys.Upgrades, EconomyKeys.Inventory }, s =>
            {
                long now = _clock.NowMs;
                Wallet wallet = Json.Read<Wallet>(s.Json(Keys.Wallet));
                RaidStats stats = Json.Read<RaidStats>(s.Json(Keys.Stats));
                Upgrades upgrades = Upgrades.Read(s.Json(EconomyKeys.Upgrades), now);
                Inventory inventory = Json.Read<Inventory>(s.Json(EconomyKeys.Inventory));

                if (upgrades.Find(kind) != null)
                {
                    return new Plan<EconomyResult>(EconomyResult.Fail("That's already being built"));
                }

                if (upgrades.Running.Count >= Grants.Workshops(cfg, inventory, now))
                {
                    return new Plan<EconomyResult>(EconomyResult.Fail("All your workshops are busy"));
                }

                int toLevel;
                int minutes;
                if (kind == UpgradeKinds.VaultLevel)
                {
                    VaultLevelStep step = cfg.VaultLevelTo(upgrades.Levels.VaultLevel + 1);
                    if (step == null)
                    {
                        return new Plan<EconomyResult>(EconomyResult.Fail("Your vault is at the top level"));
                    }

                    // The star gate: stars only come from raids, so money can't skip it.
                    if (stats.Stars < step.Stars)
                    {
                        return new Plan<EconomyResult>(EconomyResult.Fail("Needs " + step.Stars + " stars from raids (you have " + stats.Stars + ")"));
                    }

                    if (wallet.Gold < step.Gold)
                    {
                        return new Plan<EconomyResult>(EconomyResult.Fail("Needs " + step.Gold + " gold"));
                    }

                    wallet.Gold -= step.Gold;
                    toLevel = step.Level;
                    minutes = step.Minutes;
                }
                else if (kind == UpgradeKinds.Lantern)
                {
                    LanternStep step = cfg.LanternTo(upgrades.Levels.Lantern + 1);
                    if (step == null)
                    {
                        return new Plan<EconomyResult>(EconomyResult.Fail("Your lantern is at the top level"));
                    }

                    // Each Lantern level waits for a Vault Level, so at any Vault Level everyone's cap is the same.
                    if (upgrades.Levels.VaultLevel < step.MinVaultLevel)
                    {
                        return new Plan<EconomyResult>(EconomyResult.Fail("Needs Vault Level " + step.MinVaultLevel));
                    }

                    if (wallet.Wax < step.Wax)
                    {
                        return new Plan<EconomyResult>(EconomyResult.Fail("Needs " + step.Wax + " wax"));
                    }

                    wallet.Wax -= step.Wax;
                    toLevel = step.Level;
                    minutes = step.Minutes;
                }
                else if (kind == UpgradeKinds.Decoys)
                {
                    DecoyStep step = cfg.DecoyTo(upgrades.Levels.Decoys + 1);
                    if (step == null)
                    {
                        return new Plan<EconomyResult>(EconomyResult.Fail("You carry every decoy there is"));
                    }

                    if (upgrades.Levels.VaultLevel < step.MinVaultLevel)
                    {
                        return new Plan<EconomyResult>(EconomyResult.Fail("Needs Vault Level " + step.MinVaultLevel));
                    }

                    if (wallet.Wax < step.Wax)
                    {
                        return new Plan<EconomyResult>(EconomyResult.Fail("Needs " + step.Wax + " wax"));
                    }

                    wallet.Wax -= step.Wax;
                    toLevel = step.Level;
                    minutes = step.Minutes;
                }
                else if (UpgradeKinds.TryParseDefence(kind, out ObjectType defence))
                {
                    int unlock = VaultRules.RequiredLevel(defence);
                    if (upgrades.Levels.VaultLevel < unlock)
                    {
                        return new Plan<EconomyResult>(EconomyResult.Fail(defence + " unlocks at Vault Level " + unlock));
                    }

                    int next = upgrades.Levels.DefenceLevel(defence) + 1;
                    DefenceStep step = cfg.DefenceTo(next);
                    if (step == null)
                    {
                        return new Plan<EconomyResult>(EconomyResult.Fail("That defence is at the top level"));
                    }

                    // Capped by Vault Level, so at any Vault Level everyone's defences top out the same.
                    if (next > DefenceLevels.Cap(upgrades.Levels.VaultLevel))
                    {
                        return new Plan<EconomyResult>(EconomyResult.Fail("Level " + next + " needs Vault Level " + (2 * next - 1)));
                    }

                    if (wallet.Gold < step.Gold || wallet.Wax < step.Wax)
                    {
                        return new Plan<EconomyResult>(EconomyResult.Fail("Needs " + step.Gold + " gold and " + step.Wax + " wax"));
                    }

                    wallet.Gold -= step.Gold;
                    wallet.Wax -= step.Wax;
                    toLevel = step.Level;
                    minutes = step.Minutes;
                }
                else
                {
                    return new Plan<EconomyResult>(EconomyResult.Fail("Nothing to upgrade called " + kind));
                }

                upgrades.Running.Add(new RunningUpgrade { Kind = kind, ToLevel = toLevel, StartedMs = now, EndsMs = now + minutes * Rules.MinuteMs });
                return new Plan<EconomyResult>(EconomyResult.Done("Building"))
                    .Write(Keys.Wallet, Json.Write(wallet))
                    .Write(EconomyKeys.Upgrades, Json.Write(upgrades));
            });
        }

        /// <summary>Completes an upgrade whose time is up, or (useGems) pays gems to finish it now.</summary>
        public async Task<EconomyResult> FinishUpgradeAsync(string playerId, string kind, bool useGems)
        {
            EconomyResult result = await FinishAsync(playerId, kind, useGems);
            if (result.Ok && UpgradeKinds.TryParseDefence(kind, out _))
            {
                await new DefenceSync(_store, _clock).SyncAsync(playerId); // the live vault picks up the new level
            }

            return result;
        }

        private async Task<EconomyResult> FinishAsync(string playerId, string kind, bool useGems)
        {
            EconomyConfig cfg = await _economy.GetAsync();
            return await Locked.UpdateAsync(_store, playerId, new[] { Keys.Wallet, EconomyKeys.Upgrades }, s =>
            {
                long now = _clock.NowMs;
                Wallet wallet = Json.Read<Wallet>(s.Json(Keys.Wallet));
                Upgrades upgrades = Json.Read<Upgrades>(s.Json(EconomyKeys.Upgrades));
                if (upgrades.Settle(now).Contains(kind))
                {
                    return new Plan<EconomyResult>(EconomyResult.Done("Finished")).Write(EconomyKeys.Upgrades, Json.Write(upgrades));
                }

                RunningUpgrade running = upgrades.Find(kind);
                if (running == null)
                {
                    return new Plan<EconomyResult>(EconomyResult.Fail("Nothing is being built there"));
                }

                long remaining = running.EndsMs - now; // server time only: the device clock is never trusted
                var plan = new Plan<EconomyResult>(EconomyResult.Done("Finished"));
                if (remaining > 0)
                {
                    if (!useGems)
                    {
                        return new Plan<EconomyResult>(EconomyResult.Fail((remaining + Rules.MinuteMs - 1) / Rules.MinuteMs + " minutes left"));
                    }

                    long gems = cfg.SpeedUp.GemsFor(remaining);
                    if (wallet.Gems < gems)
                    {
                        return new Plan<EconomyResult>(EconomyResult.Fail("Needs " + gems + " gems"));
                    }

                    wallet.Gems -= gems;
                    plan.Write(Keys.Wallet, Json.Write(wallet));
                }

                upgrades.Apply(running);
                return plan.Write(EconomyKeys.Upgrades, Json.Write(upgrades));
            });
        }

        // ---- Heist Pass -----------------------------------------------------------------------

        public async Task<EconomyResult> ClaimPassTierAsync(string playerId, int tier, bool premium)
        {
            EconomyConfig cfg = await _economy.GetAsync();
            return await Locked.UpdateAsync(_store, playerId, new[] { Keys.Wallet, EconomyKeys.Inventory, EconomyKeys.Pass }, s =>
            {
                Wallet wallet = Json.Read<Wallet>(s.Json(Keys.Wallet));
                Inventory inventory = Json.Read<Inventory>(s.Json(EconomyKeys.Inventory));
                PassState pass = Json.Read<PassState>(s.Json(EconomyKeys.Pass));
                PassSeason.Sync(pass, cfg);

                PassReward reward = cfg.Pass.Reward(tier);
                if (reward == null || tier > cfg.Pass.TierFor(pass.Xp))
                {
                    return new Plan<EconomyResult>(EconomyResult.Fail("Tier " + tier + " isn't reached yet"));
                }

                // Premium works retroactively: buying the pass mid-season unlocks every tier passed.
                if (premium && !pass.Premium)
                {
                    return new Plan<EconomyResult>(EconomyResult.Fail("That's on the premium row"));
                }

                List<int> claimed = premium ? pass.ClaimedPremium : pass.ClaimedFree;
                if (claimed.Contains(tier))
                {
                    return new Plan<EconomyResult>(EconomyResult.Fail("Already claimed"));
                }

                Grant grant = premium ? reward.Premium : reward.Free;
                claimed.Add(tier);
                Grants.Apply(grant, cfg, wallet, inventory, pass);
                return new Plan<EconomyResult>(EconomyResult.Done("Claimed", grant))
                    .Write(Keys.Wallet, Json.Write(wallet))
                    .Write(EconomyKeys.Inventory, Json.Write(inventory))
                    .Write(EconomyKeys.Pass, Json.Write(pass));
            });
        }

        // ---- Achievements -----------------------------------------------------------------------

        public static long StatValue(Achievement a, Progress progress, Upgrades upgrades)
        {
            switch (a.Stat)
            {
                case "snuffs": return progress.Snuffs;
                case "perfectEscapes": return progress.PerfectEscapes;
                case "raids": return progress.Raids;
                case "vaultLevel": return upgrades.Levels.VaultLevel;
                case "linkOpens": return progress.LinkOpeners.Count;
                default: return 0;
            }
        }

        /// <summary>Claims the next tier of an achievement whose stat has reached it.</summary>
        public async Task<EconomyResult> ClaimAchievementAsync(string playerId, string id)
        {
            EconomyConfig cfg = await _economy.GetAsync();
            Achievement a = cfg.Achievement(id);
            if (a == null)
            {
                return EconomyResult.Fail("No such achievement");
            }

            return await Locked.UpdateAsync(_store, playerId,
                new[] { Keys.Wallet, EconomyKeys.Inventory, EconomyKeys.Progress, EconomyKeys.Upgrades }, s =>
                {
                    Wallet wallet = Json.Read<Wallet>(s.Json(Keys.Wallet));
                    Inventory inventory = Json.Read<Inventory>(s.Json(EconomyKeys.Inventory));
                    Progress progress = Json.Read<Progress>(s.Json(EconomyKeys.Progress));
                    Upgrades upgrades = Upgrades.Read(s.Json(EconomyKeys.Upgrades), _clock.NowMs);
                    progress.Claimed.TryGetValue(id, out int claimedTier);
                    if (claimedTier >= a.Tiers.Count)
                    {
                        return new Plan<EconomyResult>(EconomyResult.Fail("All tiers claimed"));
                    }

                    if (StatValue(a, progress, upgrades) < a.Tiers[claimedTier])
                    {
                        return new Plan<EconomyResult>(EconomyResult.Fail("Not there yet"));
                    }

                    Grant grant = claimedTier < a.Grants.Count ? a.Grants[claimedTier] : null;
                    progress.Claimed[id] = claimedTier + 1;
                    Grants.Apply(grant, cfg, wallet, inventory, null);
                    return new Plan<EconomyResult>(EconomyResult.Done("Claimed " + TierName(claimedTier + 1), grant))
                        .Write(Keys.Wallet, Json.Write(wallet))
                        .Write(EconomyKeys.Inventory, Json.Write(inventory))
                        .Write(EconomyKeys.Progress, Json.Write(progress));
                });
        }

        public static string TierName(int tier)
        {
            return tier == 1 ? "bronze" : tier == 2 ? "silver" : "gold";
        }

        /// <summary>Counts a different player opening this owner's vault link (raiding it by its code).</summary>
        public Task RecordLinkOpenAsync(string ownerId, string openerId)
        {
            return Locked.UpdateAsync(_store, ownerId, new[] { EconomyKeys.Progress }, s =>
            {
                Progress progress = Json.Read<Progress>(s.Json(EconomyKeys.Progress));
                if (progress.LinkOpeners.Contains(openerId) || progress.LinkOpeners.Count >= 100)
                {
                    return new Plan<bool>(false);
                }

                progress.LinkOpeners.Add(openerId);
                return new Plan<bool>(true).Write(EconomyKeys.Progress, Json.Write(progress));
            });
        }

        // ---- Wick Club ------------------------------------------------------------------------

        public async Task<EconomyResult> ClaimClubGemsAsync(string playerId)
        {
            EconomyConfig cfg = await _economy.GetAsync();
            return await Locked.UpdateAsync(_store, playerId, new[] { Keys.Wallet, EconomyKeys.Inventory }, s =>
            {
                long now = _clock.NowMs;
                Wallet wallet = Json.Read<Wallet>(s.Json(Keys.Wallet));
                Inventory inventory = Json.Read<Inventory>(s.Json(EconomyKeys.Inventory));
                if (!Grants.ClubActive(inventory, now))
                {
                    return new Plan<EconomyResult>(EconomyResult.Fail("Wick Club isn't active"));
                }

                if (inventory.ClubGemsDay == Days.Of(now))
                {
                    return new Plan<EconomyResult>(EconomyResult.Fail("Already claimed today"));
                }

                inventory.ClubGemsDay = Days.Of(now);
                wallet.Gems += cfg.Club.DailyGems;
                return new Plan<EconomyResult>(EconomyResult.Done("Claimed", new Grant { Gems = cfg.Club.DailyGems }))
                    .Write(Keys.Wallet, Json.Write(wallet))
                    .Write(EconomyKeys.Inventory, Json.Write(inventory));
            });
        }
    }

    /// <summary>Everything the economy screens show, in one read.</summary>
    public sealed class EconomyState
    {
        public long ServerTimeMs { get; set; }
        public int ConfigVersion { get; set; }
        public long Gold { get; set; }
        public long Wax { get; set; }
        public long Gems { get; set; }
        public int Crowns { get; set; }
        public int Stars { get; set; }
        public int VaultLevel { get; set; }
        public int Lantern { get; set; }

        /// <summary>Defence upgrade levels by type name; missing types are level 1.</summary>
        public Dictionary<string, int> Defences { get; set; } = new Dictionary<string, int>();

        /// <summary>The owner's newest defence levels failed their stored clear-check: test and publish again.</summary>
        public bool VaultNeedsRetest { get; set; }

        public int Workshops { get; set; }
        public List<RunningUpgrade> Running { get; set; } = new List<RunningUpgrade>();
        public VaultLevelStep NextVaultLevel { get; set; }
        public LanternStep NextLantern { get; set; }
        public int Decoys { get; set; }
        public DecoyStep NextDecoy { get; set; }
        public long ClubUntilMs { get; set; }
        public List<string> Cosmetics { get; set; } = new List<string>();
        public int DailyVaultRetries { get; set; }
        public string PassSeason { get; set; }
        public long PassXp { get; set; }
        public int PassTier { get; set; }
        public bool PassPremium { get; set; }
        public List<int> PassClaimedFree { get; set; } = new List<int>();
        public List<int> PassClaimedPremium { get; set; } = new List<int>();
        public List<AchievementProgress> Achievements { get; set; } = new List<AchievementProgress>();
        public Dictionary<string, int> AdsToday { get; set; } = new Dictionary<string, int>();
        public int AdsTodayTotal { get; set; }

        public static EconomyState Build(EconomyConfig cfg, long now, Wallet w, RaidStats stats, Profile profile, Upgrades upgrades,
            Inventory inventory, PassState pass, Progress progress, AdState ads)
        {
            Wick.CloudCode.PassSeason.Sync(pass, cfg);
            bool today = ads.Day == Days.Of(now);
            var state = new EconomyState
            {
                ServerTimeMs = now,
                ConfigVersion = cfg.Version,
                Gold = w.Gold,
                Wax = w.Wax,
                Gems = w.Gems,
                Crowns = profile.Crowns,
                Stars = stats.Stars,
                VaultLevel = upgrades.Levels.VaultLevel,
                Lantern = upgrades.Levels.Lantern,
                Defences = upgrades.Levels.Defences,
                Workshops = Grants.Workshops(cfg, inventory, now),
                Running = upgrades.Running,
                NextVaultLevel = cfg.VaultLevelTo(upgrades.Levels.VaultLevel + 1),
                NextLantern = cfg.LanternTo(upgrades.Levels.Lantern + 1),
                Decoys = upgrades.Levels.Decoys,
                NextDecoy = cfg.DecoyTo(upgrades.Levels.Decoys + 1),
                ClubUntilMs = inventory.ClubUntilMs,
                Cosmetics = inventory.Cosmetics,
                DailyVaultRetries = inventory.DailyVaultRetries,
                PassSeason = pass.Season,
                PassXp = pass.Xp,
                PassTier = cfg.Pass.TierFor(pass.Xp),
                PassPremium = pass.Premium,
                PassClaimedFree = pass.ClaimedFree,
                PassClaimedPremium = pass.ClaimedPremium,
                AdsToday = today ? ads.Counts : new Dictionary<string, int>(),
                AdsTodayTotal = today ? ads.Total : 0,
            };
            foreach (Achievement a in cfg.Achievements)
            {
                progress.Claimed.TryGetValue(a.Id, out int claimed);
                state.Achievements.Add(new AchievementProgress { Id = a.Id, Value = EconomyService.StatValue(a, progress, upgrades), Claimed = claimed });
            }

            return state;
        }
    }

    public sealed class AchievementProgress
    {
        public string Id { get; set; }
        public long Value { get; set; }

        /// <summary>Tiers claimed: 0 none, 1 bronze, 2 silver, 3 gold.</summary>
        public int Claimed { get; set; }
    }
}
