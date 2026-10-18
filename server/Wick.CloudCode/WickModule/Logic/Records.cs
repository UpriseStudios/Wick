using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Wick.CloudCode
{
    /// <summary>
    /// Player data keys. All Protected: the player can read them, only Cloud Code writes. Cloud Save
    /// keys allow only letters, digits, _ and -.
    /// </summary>
    public static class Keys
    {
        public const string Wallet = "wallet";
        public const string Stats = "stats";
        public const string VaultLive = "vault_live";
        public const string RaidTokens = "raid_tokens";
        public const string Idempotency = "idempotency";
    }

    public static class Json
    {
        private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            NullValueHandling = NullValueHandling.Ignore,
        };

        public static string Write(object value)
        {
            return JsonConvert.SerializeObject(value, Settings);
        }

        /// <summary>Null JSON (a missing key) reads as a fresh T.</summary>
        public static T Read<T>(string json) where T : new()
        {
            return json == null ? new T() : JsonConvert.DeserializeObject<T>(json, Settings) ?? new T();
        }
    }

    public sealed class Wallet
    {
        public long Gold { get; set; }
    }

    public sealed class RaidStats
    {
        public int Raids { get; set; }
        public int Escapes { get; set; }
        public int Stars { get; set; }
        public long Loot { get; set; }
    }

    /// <summary>vault_live: the published vault document (with its clear-check) and who owns it.</summary>
    public sealed class LiveVault
    {
        public string OwnerId { get; set; }
        public string VaultId { get; set; }
        public int Version { get; set; }
        public long PublishedAtMs { get; set; }

        /// <summary>VaultJson document including the owner's clear-check. Never sent to raiders.</summary>
        public string Document { get; set; }
    }

    /// <summary>A single-use permission to raid one vault version with one seed.</summary>
    public sealed class RaidToken
    {
        public string Id { get; set; }
        public string VaultId { get; set; }
        public int VaultVersion { get; set; }
        public string OwnerId { get; set; }
        public int Seed { get; set; }
        public long IssuedAtMs { get; set; }
        public long ExpiresAtMs { get; set; }
        public bool Used { get; set; }

        /// <summary>The vault as issued (no clear-check), so a republish mid-raid can't change it.</summary>
        public string Vault { get; set; }
    }

    /// <summary>raid_tokens: the player's recent tokens. Expired ones are pruned on every write.</summary>
    public sealed class TokenBook
    {
        public const int MaxTokens = 8;

        public List<RaidToken> Tokens { get; set; } = new List<RaidToken>();

        public RaidToken Find(string id)
        {
            return Tokens.Find(t => t.Id == id);
        }

        public void Add(RaidToken token, long nowMs)
        {
            Tokens.RemoveAll(t => t.ExpiresAtMs < nowMs);
            Tokens.Add(token);
            while (Tokens.Count > MaxTokens)
            {
                Tokens.RemoveAt(0); // oldest first
            }
        }
    }
}
