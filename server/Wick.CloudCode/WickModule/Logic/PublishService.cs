using System.Threading.Tasks;
using Wick.Sim;

namespace Wick.CloudCode
{
    /// <summary>
    /// PublishVault: parse, run every building rule, re-simulate the clear-check, and only then
    /// write vault_live and list the vault for matchmaking. The re-sim shows the vault can be
    /// escaped under the rules; it says nothing about who or what played the clear-check run.
    /// </summary>
    public sealed class PublishService
    {
        private readonly IGameStore _store;
        private readonly IClock _clock;

        public PublishService(IGameStore store, IClock clock)
        {
            _store = store;
            _clock = clock;
        }

        public async Task<PublishResult> PublishAsync(string playerId, string requestId, string document)
        {
            if (!Idempotency.IsValidRequestId(requestId))
            {
                return PublishResult.Fail("Bad request id");
            }

            // A vault can be built up to the level the player has raised (upgrades, star-gated).
            var levels = await _store.GetProtectedAsync(playerId, new[] { EconomyKeys.Upgrades });
            int vaultLevel = Json.Read<Upgrades>(levels.TryGetValue(EconomyKeys.Upgrades, out StoredItem u) ? u.Json : null).Levels.VaultLevel;
            VaultCheck.Passed passed = VaultCheck.Check(document, vaultLevel, out PublishResult fail);
            if (passed == null)
            {
                return fail;
            }

            string code = await VaultCodes.AssignAsync(_store, VaultIds.ForOwner(playerId));
            PublishResult result = await Locked.UpdateAsync(_store, playerId, new[] { Keys.VaultLive, Keys.Idempotency }, s =>
            {
                if (Idempotency.TryGet(s, requestId, out PublishResult prior))
                {
                    return new Plan<PublishResult>(prior);
                }

                string json = s.Json(Keys.VaultLive);
                int version = json == null ? 1 : Json.Read<LiveVault>(json).Version + 1;
                string vaultId = VaultIds.ForOwner(playerId);
                VaultDef stamped = VaultIds.Stamp(passed.Vault, vaultId, version);

                var live = new LiveVault
                {
                    OwnerId = playerId,
                    VaultId = vaultId,
                    Version = version,
                    PublishedAtMs = _clock.NowMs,
                    ClearTicks = passed.Replay.Ticks,
                    Code = code,
                    Document = VaultJson.WriteDocument(stamped, passed.ClearCheck),
                };
                var published = new PublishResult
                {
                    Ok = true, Reason = "Published", VaultId = vaultId, Version = version, Stars = passed.Replay.Stars, Code = code,
                };
                return new Plan<PublishResult>(published)
                    .Write(Keys.VaultLive, Json.Write(live))
                    .Write(Keys.Idempotency, Idempotency.Add(s, requestId, published));
            });

            // Also on a retried request: the listing is rebuilt from vault_live, so this is idempotent.
            await new Directory(_store).RefreshAsync(playerId);
            return result;
        }
    }
}
