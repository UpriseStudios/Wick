using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Wick.Sim;

namespace Wick.CloudCode
{
    /// <summary>
    /// PublishVault: parse, run every building rule, re-simulate the clear-check, and only then
    /// write vault_live. The re-sim shows the vault can be escaped under the rules; it says
    /// nothing about who or what played the clear-check run.
    /// </summary>
    public sealed class PublishService
    {
        public const int MaxDocumentChars = 64 * 1024;

        /// <summary>Highest Vault Level a player may publish until Vault Level progression exists (M8).</summary>
        public const int MaxPublishLevel = 3;

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

            if (string.IsNullOrEmpty(document) || document.Length > MaxDocumentChars)
            {
                return PublishResult.Fail("That vault is empty or too large");
            }

            VaultDef vault;
            ClearCheck clearCheck;
            try
            {
                JsonNode root = JsonReader.Parse(document);
                vault = VaultJson.FromNode(root);
                clearCheck = VaultJson.ReadClearCheck(root);
            }
            catch (Exception e) when (e is FormatException || e is ArgumentException)
            {
                return PublishResult.Fail("That vault couldn't be read: " + e.Message);
            }

            if (vault.VaultLevel > MaxPublishLevel)
            {
                return PublishResult.Fail("Vault Level " + vault.VaultLevel + " isn't unlocked yet");
            }

            List<VaultIssue> issues = VaultValidator.Validate(vault);
            if (issues.Count > 0)
            {
                return PublishResult.Fail("The vault breaks the building rules", issues.ConvertAll(i => i.Message));
            }

            ClearCheckResult check = ClearCheck.Verify(vault, clearCheck);
            if (!check.Ok)
            {
                return PublishResult.Fail("The clear-check run didn't verify: " + check.Reason);
            }

            int stars = check.Replay.Value.Stars;
            return await Locked.UpdateAsync(_store, playerId, new[] { Keys.VaultLive, Keys.Idempotency }, s =>
            {
                if (Idempotency.TryGet(s, requestId, out PublishResult prior))
                {
                    return new Plan<PublishResult>(prior);
                }

                string json = s.Json(Keys.VaultLive);
                int version = json == null ? 1 : Json.Read<LiveVault>(json).Version + 1;
                string vaultId = VaultIds.ForOwner(playerId);
                VaultDef stamped = VaultIds.Stamp(vault, vaultId, version);

                var live = new LiveVault
                {
                    OwnerId = playerId,
                    VaultId = vaultId,
                    Version = version,
                    PublishedAtMs = _clock.NowMs,
                    Document = VaultJson.WriteDocument(stamped, clearCheck),
                };
                var result = new PublishResult { Ok = true, Reason = "Published", VaultId = vaultId, Version = version, Stars = stars };
                return new Plan<PublishResult>(result)
                    .Write(Keys.VaultLive, Json.Write(live))
                    .Write(Keys.Idempotency, Idempotency.Add(s, requestId, result));
            });
        }
    }
}
