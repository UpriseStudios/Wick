using System.Collections.Generic;
using System.Threading.Tasks;
using Wick.Economy;
using Wick.Sim;

namespace Wick.CloudCode
{
    /// <summary>
    /// The matchmaking directory: one server-only custom item per live vault (mm_&lt;vaultId&gt;),
    /// with the pool and Crowns indexed for GetRaidTarget. Player entries are rebuilt from the
    /// owner's data after anything that changes them (publish, a raid, a flag), so the directory is
    /// a cache of player data and never the source of truth. Seed (bot) entries are written once.
    /// </summary>
    public sealed class Directory
    {
        private static readonly string[] EntryKeys = { CustomKeys.Entry };

        private readonly IGameStore _store;

        public Directory(IGameStore store)
        {
            _store = store;
        }

        public static string IdFor(string vaultId)
        {
            return CustomKeys.MatchPrefix + vaultId;
        }

        public Task WriteAsync(MatchEntry e)
        {
            return _store.SetPrivateCustomBatchAsync(IdFor(e.VaultId), new[]
            {
                new ItemWrite(CustomKeys.Pool, Json.Write(e.Pool), null),
                new ItemWrite(CustomKeys.Crowns, e.Crowns.ToString(), null),
                new ItemWrite(CustomKeys.Entry, Json.Write(e), null),
            });
        }

        public async Task<MatchEntry> GetAsync(string vaultId)
        {
            var found = await _store.GetPrivateCustomAsync(IdFor(vaultId), EntryKeys);
            return found.TryGetValue(CustomKeys.Entry, out StoredItem item) ? Json.Read<MatchEntry>(item.Json) : null;
        }

        /// <summary>Rebuilds a player's entry from their vault_live, profile and defence data.</summary>
        public async Task RefreshAsync(string playerId)
        {
            var data = await _store.GetProtectedAsync(playerId, new[] { Keys.VaultLive, Keys.Profile, Keys.Defence });
            if (!data.TryGetValue(Keys.VaultLive, out StoredItem liveItem))
            {
                return; // nothing published
            }

            LiveVault live = Json.Read<LiveVault>(liveItem.Json);
            Profile profile = Json.Read<Profile>(data.TryGetValue(Keys.Profile, out StoredItem p) ? p.Json : null);
            DefenceState defence = Json.Read<DefenceState>(data.TryGetValue(Keys.Defence, out StoredItem d) ? d.Json : null);
            VaultDef vault = VaultJson.Parse(live.Document);

            await WriteAsync(new MatchEntry
            {
                VaultId = live.VaultId,
                Code = live.Code,
                OwnerId = playerId,
                Version = live.Version,
                Bot = false,
                VaultLevel = vault.VaultLevel,
                Crowns = profile.Crowns,
                Pool = profile.Pool,
                ShieldUntilMs = defence.ShieldUntilMs,
                ClearTicks = live.ClearTicks,
                Raids = defence.Raids,
                Snuffs = defence.Snuffs,
                Vault = VaultJson.Write(vault), // no clear-check
            });
        }

        /// <summary>Random entries in a pool with Crowns in [min, max] (max null: no upper bound).</summary>
        public async Task<List<MatchEntry>> QueryAsync(string pool, int min, int? max)
        {
            var filters = new List<QueryFilter>
            {
                new QueryFilter(CustomKeys.Pool, QueryOp.Eq, pool),
                new QueryFilter(CustomKeys.Crowns, QueryOp.Ge, min),
            };
            if (max.HasValue)
            {
                filters.Add(new QueryFilter(CustomKeys.Crowns, QueryOp.Le, max.Value));
            }

            var entries = new List<MatchEntry>();
            foreach (QueryHit hit in await _store.QueryPrivateCustomAsync(filters, EntryKeys, Rules.SampleSize))
            {
                if (hit.Values.TryGetValue(CustomKeys.Entry, out string json))
                {
                    entries.Add(Json.Read<MatchEntry>(json));
                }
            }

            return entries;
        }
    }

    /// <summary>Everything a vault document must pass before it can go live (player or seed).</summary>
    public static class VaultCheck
    {
        public const int MaxDocumentChars = 64 * 1024;

        public sealed class Passed
        {
            public VaultDef Vault;
            public ClearCheck ClearCheck;
            public RaidResult Replay;
        }

        /// <returns>The checked vault, or null with the reason in fail.</returns>
        public static Passed Check(string document, int maxLevel, RaidConfig raid, out PublishResult fail)
        {
            fail = null;
            if (string.IsNullOrEmpty(document) || document.Length > MaxDocumentChars)
            {
                fail = PublishResult.Fail("That vault is empty or too large");
                return null;
            }

            VaultDef vault;
            ClearCheck clearCheck;
            try
            {
                JsonNode root = JsonReader.Parse(document);
                vault = VaultJson.FromNode(root);
                clearCheck = VaultJson.ReadClearCheck(root);
            }
            catch (System.Exception e) when (e is System.FormatException || e is System.ArgumentException)
            {
                fail = PublishResult.Fail("That vault couldn't be read: " + e.Message);
                return null;
            }

            if (vault.VaultLevel > maxLevel)
            {
                fail = PublishResult.Fail("Vault Level " + vault.VaultLevel + " isn't unlocked yet");
                return null;
            }

            List<VaultIssue> issues = VaultValidator.Validate(vault);
            if (issues.Count > 0)
            {
                fail = PublishResult.Fail("The vault breaks the building rules", issues.ConvertAll(i => i.Message));
                return null;
            }

            // The proof run must not have had more time than raiders get at this level.
            int raidTicks = raid.TicksFor(vault.VaultLevel);
            if (clearCheck != null && clearCheck.Params.RaidTicks > raidTicks)
            {
                fail = PublishResult.Fail("The clear-check run had a longer clock than raids at Vault Level " + vault.VaultLevel +
                    "; test the vault again");
                return null;
            }

            ClearCheckResult check = ClearCheck.Verify(vault, clearCheck);
            if (!check.Ok)
            {
                fail = PublishResult.Fail("The clear-check run didn't verify: " + check.Reason);
                return null;
            }

            return new Passed { Vault = vault, ClearCheck = clearCheck, Replay = check.Replay.Value };
        }
    }
}
