using System.Threading.Tasks;
using Wick.Sim;

namespace Wick.CloudCode
{
    /// <summary>
    /// Keeps a live vault's defence levels in step with its owner's upgrades (decision C in
    /// docs/m10-plan.md). When they differ, the stored clear-check run is re-simulated with the new
    /// levels: if it still escapes with a star, the live vault takes them straight away; if not, it
    /// keeps the old levels and is flagged so the game asks the owner to test again. Idempotent, so
    /// it is safe to run on every economy read and after every finished upgrade.
    /// </summary>
    public sealed class DefenceSync
    {
        private readonly IGameStore _store;
        private readonly IClock _clock;

        public DefenceSync(IGameStore store, IClock clock)
        {
            _store = store;
            _clock = clock;
        }

        /// <returns>Whether the live vault now needs a re-test.</returns>
        public async Task<bool> SyncAsync(string playerId)
        {
            bool listingChanged = false;
            bool needsRetest = await Locked.UpdateAsync(_store, playerId, new[] { Keys.VaultLive, EconomyKeys.Upgrades }, s =>
            {
                listingChanged = false;
                string json = s.Json(Keys.VaultLive);
                if (json == null)
                {
                    return new Plan<bool>(false);
                }

                LiveVault live = Json.Read<LiveVault>(json);
                JsonNode root = JsonReader.Parse(live.Document);
                VaultDef vault = VaultJson.FromNode(root);
                ClearCheck clearCheck = VaultJson.ReadClearCheck(root);
                DefenceLevels want = Upgrades.Read(s.Json(EconomyKeys.Upgrades), _clock.NowMs).Levels.DefenceLevelsFor(vault.VaultLevel);
                if (vault.DefenceLevels.SameAs(want) || clearCheck == null)
                {
                    return new Plan<bool>(live.NeedsRetest);
                }

                VaultDef upgraded = VaultIds.Stamp(vault.WithDefenceLevels(want), live.VaultId, live.Version + 1);
                RaidResult run = RaidRunner.RunToEnd(upgraded, clearCheck.Seed, clearCheck.Log, clearCheck.Params);
                if (run.Outcome != Outcome.Escaped || run.Stars < 1)
                {
                    if (live.NeedsRetest)
                    {
                        return new Plan<bool>(true);
                    }

                    live.NeedsRetest = true;
                    return new Plan<bool>(true).Write(Keys.VaultLive, Json.Write(live));
                }

                live.Version++;
                live.ClearTicks = run.Ticks;
                live.NeedsRetest = false;
                live.Document = VaultJson.WriteDocument(upgraded,
                    ClearCheck.FromRun(upgraded, clearCheck.Seed, clearCheck.Log, run, clearCheck.Params));
                listingChanged = true;
                return new Plan<bool>(false).Write(Keys.VaultLive, Json.Write(live));
            });

            if (listingChanged)
            {
                await new Directory(_store).RefreshAsync(playerId);
            }

            return needsRetest;
        }
    }
}
