using System;
using System.Security.Cryptography;
using Wick.Sim;

namespace Wick.CloudCode
{
    /// <summary>Server time, injectable so token expiry and real-time checks can be tested.</summary>
    public interface IClock
    {
        long NowMs { get; }
    }

    public sealed class SystemClock : IClock
    {
        public long NowMs => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }

    /// <summary>Raid seeds and token ids. Server-side randomness only; the sim's own RNG is seeded from it.</summary>
    public interface IRandomSource
    {
        int NextSeed();
        string NextId();

        /// <summary>A uniform index in [0, count).</summary>
        int NextIndex(int count);
    }

    public sealed class CryptoRandom : IRandomSource
    {
        public int NextSeed()
        {
            return RandomNumberGenerator.GetInt32(1, int.MaxValue);
        }

        public string NextId()
        {
            return Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
        }

        public int NextIndex(int count)
        {
            return RandomNumberGenerator.GetInt32(0, count);
        }
    }

    /// <summary>One live vault per player for now, so its id is derived from the owner's id.</summary>
    public static class VaultIds
    {
        private const string Prefix = "v_";

        public static string ForOwner(string playerId)
        {
            return Prefix + playerId;
        }

        /// <summary>The owner's player id, or null if this isn't a vault id.</summary>
        public static string OwnerOf(string vaultId)
        {
            if (vaultId == null || !vaultId.StartsWith(Prefix, StringComparison.Ordinal) || vaultId.Length <= Prefix.Length ||
                vaultId.Length > 80)
            {
                return null;
            }

            string owner = vaultId.Substring(Prefix.Length);
            foreach (char c in owner)
            {
                if (!char.IsLetterOrDigit(c) && c != '-' && c != '_')
                {
                    return null;
                }
            }

            return owner;
        }

        /// <summary>The same layout with the server's id and version (the clear-check doesn't hash either).</summary>
        public static VaultDef Stamp(VaultDef v, string vaultId, int version)
        {
            var tiles = new TileType[v.Width * v.Height];
            for (int i = 0; i < tiles.Length; i++)
            {
                tiles[i] = v.TileAtIndex(i);
            }

            var objects = new ObjectDef[v.ObjectCount];
            for (int i = 0; i < objects.Length; i++)
            {
                objects[i] = v.GetObject(i);
            }

            var decorations = new DecorationDef[v.DecorationCount];
            for (int i = 0; i < decorations.Length; i++)
            {
                decorations[i] = v.GetDecoration(i);
            }

            return new VaultDef(v.Schema, vaultId, version, v.VaultLevel, v.Width, v.Height, tiles, v.Entrance, objects,
                v.HasHoard, v.Hoard, v.Theme, v.DefenceLevels, decorations);
        }
    }
}
