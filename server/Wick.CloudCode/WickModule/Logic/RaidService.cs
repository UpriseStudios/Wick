using System;
using System.Threading.Tasks;
using Wick.Sim;

namespace Wick.CloudCode
{
    /// <summary>
    /// Raid tokens and SubmitRaid. A raid pays only if the server's own re-simulation of the
    /// submitted log, on the vault and seed the token was issued with, reproduces the claim. That
    /// proves the raid is possible under the rules; it doesn't prove a person played it (abuse
    /// detection comes in M6).
    /// </summary>
    public sealed class RaidService
    {
        public const long TokenLifetimeMs = 5 * 60 * 1000;
        public const int MaxLogChars = 16 * 1024;

        /// <summary>Allowance for timer granularity in the "not faster than real time" check.</summary>
        public const long RealTimeSlackMs = 250;

        private readonly IGameStore _store;
        private readonly IClock _clock;
        private readonly IRandomSource _random;

        public RaidService(IGameStore store, IClock clock, IRandomSource random)
        {
            _store = store;
            _clock = clock;
            _random = random;
        }

        /// <summary>Issues a token to raid a published vault. allowOwnVault is for the development environment.</summary>
        public async Task<RaidStartResult> StartFriendRaidAsync(string playerId, string vaultId, bool allowOwnVault)
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

            var found = await _store.GetProtectedAsync(owner, new[] { Keys.VaultLive });
            if (!found.TryGetValue(Keys.VaultLive, out StoredItem item))
            {
                return RaidStartResult.Fail("That vault isn't published");
            }

            LiveVault live = Json.Read<LiveVault>(item.Json);
            long now = _clock.NowMs;
            var token = new RaidToken
            {
                Id = _random.NextId(),
                VaultId = live.VaultId,
                VaultVersion = live.Version,
                OwnerId = owner,
                Seed = _random.NextSeed(),
                IssuedAtMs = now,
                ExpiresAtMs = now + TokenLifetimeMs,
                Vault = VaultJson.Write(VaultJson.Parse(live.Document)), // drops the owner's clear-check
            };

            await Locked.UpdateAsync(_store, playerId, new[] { Keys.RaidTokens, Keys.Wallet }, s =>
            {
                TokenBook book = Json.Read<TokenBook>(s.Json(Keys.RaidTokens));
                book.Add(token, now);
                var plan = new Plan<bool>(true).Write(Keys.RaidTokens, Json.Write(book));
                if (s.Json(Keys.Wallet) == null)
                {
                    // Created here, before any payout, so payouts always update an existing wallet
                    // under its write lock.
                    plan.Write(Keys.Wallet, Json.Write(new Wallet()));
                }

                return plan;
            });

            return new RaidStartResult
            {
                Ok = true,
                Token = token.Id,
                Seed = token.Seed,
                VaultId = token.VaultId,
                VaultVersion = token.VaultVersion,
                Vault = token.Vault,
                ExpiresAtMs = token.ExpiresAtMs,
            };
        }

        public async Task<SubmitResult> SubmitRaidAsync(string playerId, string requestId, string tokenId, string log, RaidClaim claim)
        {
            if (!Idempotency.IsValidRequestId(requestId))
            {
                return SubmitResult.Rejected("Bad request id");
            }

            RaidToken spent = null;
            SubmitResult verdict = await Locked.UpdateAsync(_store, playerId,
                new[] { Keys.RaidTokens, Keys.Wallet, Keys.Stats, Keys.Idempotency }, s =>
                {
                    spent = null;
                    if (Idempotency.TryGet(s, requestId, out SubmitResult prior))
                    {
                        return new Plan<SubmitResult>(prior);
                    }

                    long now = _clock.NowMs;
                    TokenBook book = Json.Read<TokenBook>(s.Json(Keys.RaidTokens));
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
                    SubmitResult result = Judge(token, log, claim, now);
                    var plan = new Plan<SubmitResult>(result).Write(Keys.RaidTokens, Json.Write(book));

                    Wallet wallet = Json.Read<Wallet>(s.Json(Keys.Wallet));
                    if (result.Verified)
                    {
                        wallet.Gold += result.Paid;
                        plan.Write(Keys.Wallet, Json.Write(wallet));

                        RaidStats stats = Json.Read<RaidStats>(s.Json(Keys.Stats));
                        stats.Raids++;
                        stats.Escapes += result.Outcome == Outcome.Escaped.ToString() ? 1 : 0;
                        stats.Stars += result.Stars;
                        stats.Loot += result.Paid;
                        plan.Write(Keys.Stats, Json.Write(stats));
                    }

                    result.Gold = wallet.Gold;
                    plan.Write(Keys.Idempotency, Idempotency.Add(s, requestId, result));
                    spent = token;
                    return plan;
                });

            if (spent != null)
            {
                await SaveRecord(playerId, spent, log, claim, verdict);
            }

            return verdict;
        }

        /// <summary>Re-simulates the log on the token's vault and seed and compares it with the claim.</summary>
        public static SubmitResult Judge(RaidToken token, string log, RaidClaim claim, long nowMs)
        {
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

            RaidResult run = RaidRunner.RunToEnd(VaultJson.Parse(token.Vault), token.Seed, inputs);
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
            result.Paid = run.Loot;
            return result;
        }

        /// <summary>
        /// Keeps the submitted raid (server-only) for the defence log and replays in M6. Written
        /// after the payout; a failure here doesn't undo or fail a raid that already paid.
        /// </summary>
        private async Task SaveRecord(string playerId, RaidToken token, string log, RaidClaim claim, SubmitResult verdict)
        {
            var record = new
            {
                raidId = "r_" + token.Id,
                vaultId = token.VaultId,
                vaultVersion = token.VaultVersion,
                ownerId = token.OwnerId,
                raiderId = playerId,
                seed = token.Seed,
                simVersion = SimInfo.Version,
                log,
                claimed = claim,
                verified = verdict,
            };
            try
            {
                await _store.SetPrivateCustomAsync("raid_" + token.Id, "record", Json.Write(record));
            }
            catch (Exception)
            {
                // Best effort until the defence log needs every record (M6).
            }
        }
    }
}
