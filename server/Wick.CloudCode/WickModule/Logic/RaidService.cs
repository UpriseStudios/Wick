using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Wick.Sim;

namespace Wick.CloudCode
{
    /// <summary>
    /// Raid tokens (matchmaking, friend and revenge raids) and SubmitRaid. A raid pays only if the
    /// server's own re-simulation of the submitted log, on the vault and seed the token was issued
    /// with, reproduces the claim. That proves the raid is possible under the rules; it doesn't
    /// prove a person played it, which is what the anomaly score and the review pool are for.
    /// </summary>
    public sealed class RaidService
    {
        public const int MaxLogChars = 16 * 1024;

        /// <summary>Allowance for timer granularity in the "not faster than real time" check.</summary>
        public const long RealTimeSlackMs = 250;

        public const string KindMatch = "match";
        public const string KindFriend = "friend";
        public const string KindRevenge = "revenge";

        private readonly IGameStore _store;
        private readonly IClock _clock;
        private readonly IRandomSource _random;
        private readonly IIntegrityVerifier _integrity;
        private readonly IPushSender _push;
        private readonly bool _production;
        private readonly Directory _directory;

        public RaidService(IGameStore store, IClock clock, IRandomSource random, IIntegrityVerifier integrity = null,
            IPushSender push = null, bool production = false)
        {
            _store = store;
            _clock = clock;
            _random = random;
            _integrity = integrity ?? new NoIntegrity();
            _push = push ?? new NoPush();
            _production = production;
            _directory = new Directory(store);
        }

        // ---- Tokens ---------------------------------------------------------------------

        /// <summary>
        /// Matchmaking: a live vault near the player's Crowns, from their pool (players first, then
        /// seed vaults at each band), widening the band until something fits.
        /// </summary>
        public async Task<RaidStartResult> GetRaidTargetAsync(string playerId, string platform = null, string integrityToken = null)
        {
            var mine = await _store.GetProtectedAsync(playerId, new[] { Keys.Profile, Keys.RaidTokens });
            Profile profile = Json.Read<Profile>(Value(mine, Keys.Profile));
            TokenBook book = Json.Read<TokenBook>(Value(mine, Keys.RaidTokens));
            long now = _clock.NowMs;

            string limit = LimitReason(book, now);
            if (limit != null)
            {
                return RaidStartResult.Fail(limit);
            }

            MatchEntry target = await FindTargetAsync(playerId, profile, book, now);
            if (target == null)
            {
                return RaidStartResult.Fail("No vaults to raid right now, try again soon");
            }

            return await IssueAsync(playerId, target, KindMatch, platform, integrityToken);
        }

        private async Task<MatchEntry> FindTargetAsync(string playerId, Profile profile, TokenBook book, long now)
        {
            string[] pools = { profile.Pool == Pools.Review ? Pools.Review : Pools.Main, Pools.Bot };
            int crowns = profile.Crowns;
            for (int b = 0; b <= Rules.Bands.Length; b++)
            {
                bool last = b == Rules.Bands.Length;
                int min = last ? 0 : Math.Max(0, crowns - Rules.Bands[b]);
                int? max = last ? (int?)null : crowns + Rules.Bands[b];

                // Both pools at once (players are still preferred), so a band costs one round trip.
                Task<List<MatchEntry>>[] queries = { _directory.QueryAsync(pools[0], min, max), _directory.QueryAsync(pools[1], min, max) };
                await Task.WhenAll(queries);
                foreach (Task<List<MatchEntry>> query in queries)
                {
                    var eligible = new List<MatchEntry>();
                    foreach (MatchEntry e in query.Result)
                    {
                        if (e.OwnerId != playerId && e.ShieldUntilMs <= now && !book.RaidedRecently(e.VaultId, now - Rules.RecentTargetMs))
                        {
                            eligible.Add(e);
                        }
                    }

                    if (eligible.Count > 0)
                    {
                        return eligible[_random.NextIndex(eligible.Count)];
                    }
                }
            }

            return null;
        }

        /// <summary>A raid on a specific vault: a friend's link, a revenge raid, or (development) your own.</summary>
        public async Task<RaidStartResult> StartFriendRaidAsync(string playerId, string vaultId, bool allowOwnVault,
            string kind = KindFriend, string platform = null, string integrityToken = null)
        {
            string owner = VaultIds.OwnerOf(vaultId);
            if (owner == null)
            {
                return RaidStartResult.Fail("No such vault");
            }

            if (owner == playerId && !allowOwnVault)
            {
                return RaidStartResult.Fail("You can't raid your own vault");
            }

            MatchEntry target = await _directory.GetAsync(vaultId);
            if (target == null)
            {
                return RaidStartResult.Fail("That vault isn't published");
            }

            long now = _clock.NowMs;
            if (owner != playerId && target.ShieldUntilMs > now)
            {
                long minutes = (target.ShieldUntilMs - now + Rules.MinuteMs - 1) / Rules.MinuteMs;
                return RaidStartResult.Fail("That vault is shielded for " + minutes + " more minutes");
            }

            return await IssueAsync(playerId, target, kind == KindRevenge ? KindRevenge : KindFriend, platform, integrityToken);
        }

        private async Task<RaidStartResult> IssueAsync(string playerId, MatchEntry target, string kind, string platform, string integrityToken)
        {
            IntegrityVerdict verdict = await _integrity.VerifyAsync(playerId, platform, integrityToken);
            long now = _clock.NowMs;
            var token = new RaidToken
            {
                Id = _random.NextId(),
                Kind = kind,
                VaultId = target.VaultId,
                VaultVersion = target.Version,
                OwnerId = target.OwnerId,
                DefenderBot = target.Bot,
                DefenderCrowns = target.Crowns,
                DefenderLevel = target.VaultLevel,
                Difficulty = target.Difficulty,
                ClearTicks = target.ClearTicks,
                Seed = _random.NextSeed(),
                IssuedAtMs = now,
                ExpiresAtMs = now + Rules.TokenLifetimeMs,
                Vault = target.Vault,
            };

            bool movedToReview = false;
            RaidStartResult result = await Locked.UpdateAsync(_store, playerId, new[] { Keys.RaidTokens, Keys.Wallet, Keys.Profile }, s =>
            {
                movedToReview = false;
                TokenBook book = Json.Read<TokenBook>(s.Json(Keys.RaidTokens));
                string limit = LimitReason(book, now);
                if (limit != null)
                {
                    return new Plan<RaidStartResult>(RaidStartResult.Fail(limit));
                }

                book.Add(token, now);
                var plan = new Plan<RaidStartResult>(Started(token)).Write(Keys.RaidTokens, Json.Write(book));
                if (s.Json(Keys.Wallet) == null)
                {
                    // Created here, before any payout, so payouts always update an existing wallet
                    // under its write lock.
                    plan.Write(Keys.Wallet, Json.Write(new Wallet()));
                }

                Profile profile = Json.Read<Profile>(s.Json(Keys.Profile));
                profile.Integrity = verdict.Detail;
                if (_production && verdict.Checked && !verdict.Passed && profile.Pool != Pools.Review)
                {
                    profile.Pool = Pools.Review; // routed for review, never refused or banned
                    movedToReview = true;
                }

                return plan.Write(Keys.Profile, Json.Write(profile));
            });

            if (movedToReview)
            {
                await _directory.RefreshAsync(playerId);
            }

            return result;
        }

        private static RaidStartResult Started(RaidToken token)
        {
            return new RaidStartResult
            {
                Ok = true,
                Token = token.Id,
                Kind = token.Kind,
                Seed = token.Seed,
                VaultId = token.VaultId,
                VaultVersion = token.VaultVersion,
                Vault = token.Vault,
                ExpiresAtMs = token.ExpiresAtMs,
            };
        }

        private static string LimitReason(TokenBook book, long now)
        {
            if (book.IssuedSince(now - Rules.HourMs) < Rules.RaidsPerHour)
            {
                return null;
            }

            long oldest = long.MaxValue;
            foreach (long t in book.IssuedAtMs)
            {
                if (t >= now - Rules.HourMs && t < oldest)
                {
                    oldest = t;
                }
            }

            long minutes = (oldest + Rules.HourMs - now + Rules.MinuteMs - 1) / Rules.MinuteMs;
            return "Raid limit reached (" + Rules.RaidsPerHour + " an hour). Try again in " + minutes + " minutes";
        }

        // ---- Submit ---------------------------------------------------------------------

        public async Task<SubmitResult> SubmitRaidAsync(string playerId, string requestId, string tokenId, string log, RaidClaim claim)
        {
            if (!Idempotency.IsValidRequestId(requestId))
            {
                return SubmitResult.Rejected("Bad request id");
            }

            RaidToken spent = null;
            RaidAnalysis analysis = null;
            int score = 0;
            bool repeat = false;
            bool raiderListingChanged = false;

            SubmitResult verdict = await Locked.UpdateAsync(_store, playerId,
                new[] { Keys.RaidTokens, Keys.Wallet, Keys.Stats, Keys.Profile, Keys.Idempotency }, s =>
                {
                    spent = null;
                    analysis = null;
                    repeat = false;
                    raiderListingChanged = false;
                    TokenBook book = Json.Read<TokenBook>(s.Json(Keys.RaidTokens));
                    if (Idempotency.TryGet(s, requestId, out SubmitResult prior))
                    {
                        repeat = true;
                        spent = book.Find(tokenId); // to finish anything the first attempt left undone
                        return new Plan<SubmitResult>(prior);
                    }

                    long now = _clock.NowMs;
                    RaidToken token = book.Find(tokenId);
                    if (token == null)
                    {
                        return new Plan<SubmitResult>(SubmitResult.Rejected("Unknown raid token"));
                    }

                    if (token.Used)
                    {
                        return new Plan<SubmitResult>(SubmitResult.Rejected("This raid was already submitted"));
                    }

                    if (now > token.ExpiresAtMs)
                    {
                        return new Plan<SubmitResult>(SubmitResult.Rejected("The raid token expired"));
                    }

                    token.Used = true; // single use, whatever the verdict
                    SubmitResult result = Judge(token, log, claim, now, out analysis);
                    Profile profile = Json.Read<Profile>(s.Json(Keys.Profile));
                    string poolBefore = profile.Pool;
                    int crownsBefore = profile.Crowns;
                    Wallet wallet = Json.Read<Wallet>(s.Json(Keys.Wallet));
                    var plan = new Plan<SubmitResult>(result);

                    if (result.Verified)
                    {
                        bool self = token.OwnerId == playerId;
                        result.Paid = Math.Min(analysis.Result.Loot, Rules.LootCap(token.DefenderLevel));
                        if (!self)
                        {
                            CrownChange crowns = CrownRules.Apply(profile.Crowns, token.DefenderCrowns, analysis.Result.Stars, token.DefenderBot);
                            profile.Crowns = crowns.Raider;
                            result.CrownsDelta = crowns.RaiderDelta;
                            book.AddRecent(token.VaultId, now);
                            if (!token.DefenderBot)
                            {
                                token.Defence = new DefenceEvent
                                {
                                    RaidId = RaidIdFor(token),
                                    RaiderId = playerId,
                                    OwnerId = token.OwnerId,
                                    Outcome = result.Outcome,
                                    Stars = result.Stars,
                                    Robbed = analysis.Result.Outcome == Outcome.Escaped && result.Stars >= 1,
                                    Paid = result.Paid,
                                    RaiderCrownsDelta = crowns.RaiderDelta,
                                    SnuffedBy = analysis.SnuffedBy,
                                    DefenderLevel = token.DefenderLevel,
                                    AtMs = now,
                                };
                            }
                        }

                        wallet.Gold += result.Paid;
                        plan.Write(Keys.Wallet, Json.Write(wallet));

                        RaidStats stats = Json.Read<RaidStats>(s.Json(Keys.Stats));
                        stats.Raids++;
                        stats.Escapes += result.Outcome == Outcome.Escaped.ToString() ? 1 : 0;
                        stats.Stars += result.Stars;
                        stats.Loot += result.Paid;
                        plan.Write(Keys.Stats, Json.Write(stats));
                    }

                    // A claim the re-sim can't reproduce scores as fully suspicious; repeated
                    // mismatches move the account to review like any other high score.
                    score = analysis == null || !result.Verified ? 100 : AnomalyScore.Score(analysis, token);
                    if (analysis != null)
                    {
                        AnomalyScore.Record(profile, score);
                    }

                    result.Crowns = profile.Crowns;
                    result.Gold = wallet.Gold;
                    raiderListingChanged = profile.Pool != poolBefore || profile.Crowns != crownsBefore;
                    spent = token;
                    return plan
                        .Write(Keys.RaidTokens, Json.Write(book))
                        .Write(Keys.Profile, Json.Write(profile))
                        .Write(Keys.Idempotency, Idempotency.Add(s, requestId, result));
                });

            if (spent != null && spent.Used)
            {
                // A failure from here on fails the call; the client retries with the same request id,
                // which lands in the repeat branch and finishes the job.
                if (!repeat)
                {
                    await SaveRecordAsync(playerId, spent, log, claim, verdict, analysis, score);
                }
                else if (!await RecordExistsAsync(spent))
                {
                    Judge(spent, log, claim, long.MaxValue, out RaidAnalysis again);
                    int againScore = again == null || !verdict.Verified ? 100 : AnomalyScore.Score(again, spent);
                    await SaveRecordAsync(playerId, spent, log, claim, verdict, again, againScore);
                }

                if (spent.Defence != null)
                {
                    // Applied once per raid id, so a retried request can safely run this again.
                    await new DefenceService(_store, _clock, _push).ApplyAsync(spent.Defence);
                }

                if (raiderListingChanged || repeat)
                {
                    await _directory.RefreshAsync(playerId);
                }
            }

            return verdict;
        }

        public static string RaidIdFor(RaidToken token)
        {
            return "r_" + token.Id;
        }

        /// <summary>Re-simulates the log on the token's vault and seed and compares it with the claim.</summary>
        public static SubmitResult Judge(RaidToken token, string log, RaidClaim claim, long nowMs, out RaidAnalysis analysis)
        {
            analysis = null;

            // An empty log is valid: a raid where the player never moved.
            if (log == null || log.Length > MaxLogChars)
            {
                return SubmitResult.Rejected("The raid log is missing or too large");
            }

            InputLog inputs;
            try
            {
                inputs = InputLog.FromBase64(log);
            }
            catch (Exception e) when (e is FormatException || e is ArgumentException)
            {
                return SubmitResult.Rejected("The raid log couldn't be read");
            }

            analysis = RaidAnalysis.Run(VaultJson.Parse(token.Vault), token.Seed, inputs);
            RaidResult run = analysis.Result;
            var result = new SubmitResult
            {
                Outcome = run.Outcome.ToString(),
                Stars = run.Stars,
                Loot = run.Loot,
                Hash = run.HashHex,
                Ticks = run.Ticks,
            };

            long playableMs = run.Ticks * 1000L / SimTuning.TicksPerSecond;
            if (nowMs - token.IssuedAtMs < playableMs - RealTimeSlackMs)
            {
                result.Reason = "The raid was submitted faster than it could be played";
                return result;
            }

            if (claim == null || claim.Hash != run.HashHex || claim.Stars != run.Stars || claim.Loot != run.Loot)
            {
                result.Reason = "The raid didn't match the server's re-simulation";
                return result;
            }

            result.Verified = true;
            result.Reason = "Verified";
            return result;
        }

        /// <summary>The submitted raid, kept server-only for the defence log's replays.</summary>
        private async Task SaveRecordAsync(string playerId, RaidToken token, string log, RaidClaim claim, SubmitResult verdict,
            RaidAnalysis analysis, int score)
        {
            var record = new RaidRecord
            {
                RaidId = RaidIdFor(token),
                Kind = token.Kind,
                VaultId = token.VaultId,
                VaultVersion = token.VaultVersion,
                OwnerId = token.OwnerId,
                RaiderId = playerId,
                Seed = token.Seed,
                SimVersion = SimInfo.Version,
                Vault = token.Vault,
                Log = log,
                Claimed = claim,
                Verified = verdict,
                SnuffedBy = analysis?.SnuffedBy,
                AnomalyScore = score,
                Flagged = score >= Rules.FlagRaidScore,
                AtMs = _clock.NowMs,
            };
            await _store.SetPrivateCustomAsync(CustomKeys.RaidPrefix + token.Id, CustomKeys.Record, Json.Write(record));
        }

        private async Task<bool> RecordExistsAsync(RaidToken token)
        {
            var found = await _store.GetPrivateCustomAsync(CustomKeys.RaidPrefix + token.Id, new[] { CustomKeys.Record });
            return found.ContainsKey(CustomKeys.Record);
        }

        private static string Value(Dictionary<string, StoredItem> items, string key)
        {
            return items.TryGetValue(key, out StoredItem item) ? item.Json : null;
        }
    }

    /// <summary>A submitted raid as stored for replays: everything needed to re-simulate it.</summary>
    public sealed class RaidRecord
    {
        public string RaidId { get; set; }
        public string Kind { get; set; }
        public string VaultId { get; set; }
        public int VaultVersion { get; set; }
        public string OwnerId { get; set; }
        public string RaiderId { get; set; }
        public int Seed { get; set; }
        public int SimVersion { get; set; }
        public string Vault { get; set; }
        public string Log { get; set; }
        public RaidClaim Claimed { get; set; }
        public SubmitResult Verified { get; set; }
        public string SnuffedBy { get; set; }
        public int AnomalyScore { get; set; }
        public bool Flagged { get; set; }
        public long AtMs { get; set; }
    }
}
