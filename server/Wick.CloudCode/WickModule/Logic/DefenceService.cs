using System.Collections.Generic;
using System.Threading.Tasks;
using Wick.Economy;
using Wick.Sim;

namespace Wick.CloudCode
{
    /// <summary>
    /// The defender's side of a verified raid (bonus or loss, shield, Crowns, stats, defence log,
    /// batched push), the defence log and replays, push tokens, and seed vaults.
    /// </summary>
    public sealed class DefenceService
    {
        private readonly IGameStore _store;
        private readonly IClock _clock;
        private readonly IPushSender _push;
        private readonly IEconomySource _economy;

        public DefenceService(IGameStore store, IClock clock, IPushSender push = null, IEconomySource economy = null)
        {
            _store = store;
            _clock = clock;
            _push = push ?? new NoPush();
            _economy = economy ?? new FixedEconomy();
        }

        /// <summary>
        /// Applies a raid to its defender, at most once per raid id (it runs again whenever a
        /// SubmitRaid request is retried). A separate locked update from the raider's: the two
        /// players' data can't share one atomic batch.
        /// </summary>
        public async Task ApplyAsync(DefenceEvent ev)
        {
            EconomyConfig cfg = await _economy.GetAsync();
            PushMessage push = null;
            bool applied = await Locked.UpdateAsync(_store, ev.OwnerId,
                new[] { Keys.Wallet, Keys.Profile, Keys.Defence, Keys.Push, EconomyKeys.Pass, EconomyKeys.Progress }, s =>
            {
                push = null;
                DefenceState defence = Json.Read<DefenceState>(s.Json(Keys.Defence));
                if (defence.Applied.Contains(ev.RaidId))
                {
                    return new Plan<bool>(false);
                }

                long now = _clock.NowMs;
                Wallet wallet = Json.Read<Wallet>(s.Json(Keys.Wallet));
                Profile profile = Json.Read<Profile>(s.Json(Keys.Profile));
                long lost = 0;
                long bonus = 0;
                if (ev.Robbed)
                {
                    lost = Rules.DefenderLoss(cfg.Loot, ev.Paid, wallet.Gold);
                    wallet.Gold -= lost;
                    defence.ShieldUntilMs = now + Rules.ShieldMs(cfg.Loot);
                }
                else
                {
                    bonus = Rules.DefenceBonus(cfg.Loot, ev.DefenderLevel);
                    wallet.Gold += bonus;
                }

                PassState pass = Json.Read<PassState>(s.Json(EconomyKeys.Pass));
                Progress progress = Json.Read<Progress>(s.Json(EconomyKeys.Progress));
                if (!ev.Robbed)
                {
                    PassSeason.AddXp(pass, cfg, cfg.Pass.Xp.Defence, now); // a successful defence
                }

                progress.Snuffs += ev.Outcome == Outcome.Snuffed.ToString() ? 1 : 0;

                int crownsBefore = profile.Crowns;
                profile.Crowns = CrownRules.DefenderAfter(profile.Crowns, ev.RaiderCrownsDelta, false);

                defence.Raids++;
                defence.Recent.Insert(0, ev.Outcome == Outcome.Snuffed.ToString());
                if (defence.Recent.Count > DefenceState.RecentSize)
                {
                    defence.Recent.RemoveRange(DefenceState.RecentSize, defence.Recent.Count - DefenceState.RecentSize);
                }

                defence.BonusEarned += bonus;
                defence.GoldLost += lost;
                if (ev.Outcome == Outcome.Snuffed.ToString())
                {
                    defence.Snuffs++;
                    if (ev.SnuffedBy != null)
                    {
                        defence.SnuffsByType.TryGetValue(ev.SnuffedBy, out int n);
                        defence.SnuffsByType[ev.SnuffedBy] = n + 1;
                        defence.DeadliestTrap = Deadliest(defence.SnuffsByType);
                    }
                }

                defence.Log.Insert(0, new DefenceEntry
                {
                    RaidId = ev.RaidId,
                    RaiderId = ev.RaiderId,
                    AtMs = ev.AtMs,
                    Outcome = ev.Outcome,
                    Stars = ev.Stars,
                    GoldLost = lost,
                    Bonus = bonus,
                    CrownsDelta = profile.Crowns - crownsBefore,
                    RevengeVaultId = VaultIds.ForOwner(ev.RaiderId),
                });
                Trim(defence.Log, DefenceState.LogSize);
                defence.Applied.Add(ev.RaidId);
                while (defence.Applied.Count > DefenceState.AppliedSize)
                {
                    defence.Applied.RemoveAt(0);
                }

                PushState pushState = Json.Read<PushState>(s.Json(Keys.Push));
                push = PushPolicy.Add(pushState, ev, lost, now);

                return new Plan<bool>(true)
                    .Write(Keys.Wallet, Json.Write(wallet))
                    .Write(Keys.Profile, Json.Write(profile))
                    .Write(Keys.Defence, Json.Write(defence))
                    .Write(Keys.Push, Json.Write(pushState))
                    .Write(EconomyKeys.Pass, Json.Write(pass))
                    .Write(EconomyKeys.Progress, Json.Write(progress));
            });

            if (!applied)
            {
                return;
            }

            await new Directory(_store).RefreshAsync(ev.OwnerId); // shield, Crowns, difficulty

            if (push != null)
            {
                try
                {
                    await _push.SendAsync(push.Token, push.Title, push.Body);
                }
                catch (System.Exception)
                {
                    // A lost notification isn't worth failing the raid over; the log still has it.
                }
            }
        }

        /// <summary>The object type with the most snuffs; ties go to the alphabetically first.</summary>
        public static string Deadliest(Dictionary<string, int> snuffsByType)
        {
            string best = null;
            int most = 0;
            foreach (KeyValuePair<string, int> pair in snuffsByType)
            {
                bool better = pair.Value > most || (pair.Value == most && string.CompareOrdinal(pair.Key, best) < 0);
                if (better)
                {
                    best = pair.Key;
                    most = pair.Value;
                }
            }

            return best;
        }

        public async Task<DefenceLogResult> GetDefenceLogAsync(string playerId)
        {
            var found = await _store.GetProtectedAsync(playerId, new[] { Keys.Defence });
            DefenceState d = Json.Read<DefenceState>(found.TryGetValue(Keys.Defence, out StoredItem item) ? item.Json : null);
            return new DefenceLogResult
            {
                Raids = d.Raids,
                Snuffs = d.Snuffs,
                DeadliestTrap = d.DeadliestTrap,
                BonusEarned = d.BonusEarned,
                GoldLost = d.GoldLost,
                ShieldUntilMs = d.ShieldUntilMs,
                Entries = d.Log,
            };
        }

        /// <summary>A raid's replay, for the defender who was raided or the raider who did it.</summary>
        public async Task<ReplayResult> GetReplayAsync(string playerId, string raidId)
        {
            if (raidId == null || !raidId.StartsWith("r_") || raidId.Length > 80)
            {
                return ReplayResult.Fail("No such raid");
            }

            string tokenId = raidId.Substring(2);
            var found = await _store.GetPrivateCustomAsync(CustomKeys.RaidPrefix + tokenId, new[] { CustomKeys.Record });
            if (!found.TryGetValue(CustomKeys.Record, out StoredItem item))
            {
                return ReplayResult.Fail("No such raid");
            }

            RaidRecord r = Json.Read<RaidRecord>(item.Json);
            if (r.OwnerId != playerId && r.RaiderId != playerId)
            {
                return ReplayResult.Fail("No such raid"); // don't confirm other players' raids exist
            }

            return new ReplayResult
            {
                Ok = true,
                RaidId = r.RaidId,
                OwnerId = r.OwnerId,
                RaiderId = r.RaiderId,
                Code = r.Code,
                SimVersion = r.SimVersion,
                RaidTicks = r.RaidTicks,
                LightBonus = r.LightBonus,
                Decoys = r.Decoys,
                Vault = r.Vault,
                Seed = r.Seed,
                Log = r.Log,
                Hash = r.Verified?.Hash,
                Outcome = r.Verified?.Outcome,
                Stars = r.Verified?.Stars ?? 0,
                Ticks = r.Verified?.Ticks ?? 0,
                AtMs = r.AtMs,
            };
        }

        public Task<bool> RegisterPushTokenAsync(string playerId, string token)
        {
            if (string.IsNullOrEmpty(token) || token.Length > 4096)
            {
                return Task.FromResult(false);
            }

            return Locked.UpdateAsync(_store, playerId, new[] { Keys.Push }, s =>
            {
                PushState push = Json.Read<PushState>(s.Json(Keys.Push));
                if (push.Token == token)
                {
                    return new Plan<bool>(true);
                }

                push.Token = token;
                return new Plan<bool>(true).Write(Keys.Push, Json.Write(push));
            });
        }

        /// <summary>
        /// Lists a designer vault as a bot-owned live vault in the seed pool. It must pass the same
        /// checks as a player's vault, clear-check included. Service accounts only.
        /// </summary>
        public async Task<SeedResult> SeedVaultAsync(string name, string document, int crowns)
        {
            if (string.IsNullOrEmpty(name) || name.Length > 40 || VaultIds.OwnerOf("v_bot_" + name) == null)
            {
                return new SeedResult { Ok = false, Reason = "Bad seed vault name" };
            }

            EconomyConfig cfg = await _economy.GetAsync();
            VaultCheck.Passed passed = VaultCheck.Check(document, VaultRules.MaxVaultLevel, cfg.Raid, out PublishResult fail);
            if (passed == null)
            {
                return new SeedResult { Ok = false, Reason = fail.Reason, Issues = fail.Issues };
            }

            string vaultId = "v_bot_" + name;
            string code = await VaultCodes.AssignAsync(_store, vaultId);
            VaultDef stamped = VaultIds.Stamp(passed.Vault, vaultId, 1);
            await new Directory(_store).WriteAsync(new MatchEntry
            {
                VaultId = vaultId,
                Code = code,
                OwnerId = "bot_" + name,
                Version = 1,
                Bot = true,
                VaultLevel = stamped.VaultLevel,
                Crowns = System.Math.Max(0, crowns),
                Pool = Pools.Bot,
                ClearTicks = passed.Replay.Ticks,
                Vault = VaultJson.Write(stamped),
            });
            return new SeedResult { Ok = true, Reason = "Seeded", VaultId = vaultId, Code = code };
        }

        /// <summary>The public card a share link's web page shows: level, rating and how deadly it is.</summary>
        public async Task<VaultCard> GetVaultCardAsync(string code)
        {
            string normal = VaultCodes.Normalize(code);
            string vaultId = normal == null ? null : await VaultCodes.ResolveAsync(_store, normal);
            MatchEntry e = vaultId == null ? null : await new Directory(_store).GetAsync(vaultId);
            if (e == null)
            {
                return new VaultCard { Ok = false, Reason = "No such vault" };
            }

            return new VaultCard
            {
                Ok = true,
                Code = normal,
                VaultLevel = e.VaultLevel,
                Crowns = e.Crowns,
                Raids = e.Raids,
                Snuffs = e.Snuffs,
                Traps = TrapCount(e.Vault),
                Shielded = e.ShieldUntilMs > _clock.NowMs,
            };
        }

        private static int TrapCount(string vault)
        {
            VaultDef v = VaultJson.Parse(vault);
            int n = 0;
            for (int i = 0; i < v.ObjectCount; i++)
            {
                ObjectType t = v.GetObject(i).Type;
                n += ObjectTypes.IsTrap(t) || ObjectTypes.IsGuardian(t) ? 1 : 0;
            }

            return n;
        }

        private static void Trim<T>(List<T> list, int max)
        {
            while (list.Count > max)
            {
                list.RemoveAt(list.Count - 1);
            }
        }
    }
}
