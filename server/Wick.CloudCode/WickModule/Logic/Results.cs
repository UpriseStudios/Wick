using System.Collections.Generic;

namespace Wick.CloudCode
{
    public sealed class PublishResult
    {
        public bool Ok { get; set; }
        public string Reason { get; set; }
        public List<string> Issues { get; set; }
        public string VaultId { get; set; }
        public int Version { get; set; }
        public int Stars { get; set; }

        public static PublishResult Fail(string reason, List<string> issues = null)
        {
            return new PublishResult { Ok = false, Reason = reason, Issues = issues };
        }
    }

    public sealed class RaidStartResult
    {
        public bool Ok { get; set; }
        public string Reason { get; set; }
        public string Token { get; set; }
        public int Seed { get; set; }
        public string VaultId { get; set; }
        public int VaultVersion { get; set; }

        /// <summary>The vault to play (VaultJson, without the owner's clear-check).</summary>
        public string Vault { get; set; }

        public long ExpiresAtMs { get; set; }

        public static RaidStartResult Fail(string reason)
        {
            return new RaidStartResult { Ok = false, Reason = reason };
        }
    }

    /// <summary>What the client says its raid produced. Only used to compare; payouts come from the re-sim.</summary>
    public sealed class RaidClaim
    {
        public int Stars { get; set; }
        public int Loot { get; set; }
        public string Hash { get; set; }
    }

    /// <summary>
    /// The server's verdict on a submitted raid. Verified means the re-simulation reproduced the
    /// claimed result under the rules; Paid is what the server granted from its own re-sim.
    /// </summary>
    public sealed class SubmitResult
    {
        public bool Verified { get; set; }
        public string Reason { get; set; }
        public string Outcome { get; set; }
        public int Stars { get; set; }
        public int Loot { get; set; }
        public string Hash { get; set; }
        public int Ticks { get; set; }
        public long Paid { get; set; }
        public long Gold { get; set; }

        public static SubmitResult Rejected(string reason)
        {
            return new SubmitResult { Verified = false, Reason = reason };
        }
    }
}
