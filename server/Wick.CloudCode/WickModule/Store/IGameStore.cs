using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Wick.CloudCode
{
    /// <summary>A stored value (JSON) and the write lock it was read with.</summary>
    public sealed class StoredItem
    {
        public StoredItem(string key, string json, string writeLock)
        {
            Key = key;
            Json = json;
            WriteLock = writeLock;
        }

        public string Key { get; }
        public string Json { get; }
        public string WriteLock { get; }
    }

    /// <summary>
    /// One item in an atomic batch. WriteLock is the lock it was read with; null creates the item
    /// (Cloud Save overwrites unconditionally when the lock is null, so only Locked passes null,
    /// and only for an item its read found missing).
    /// </summary>
    public sealed class ItemWrite
    {
        public ItemWrite(string key, string json, string writeLock)
        {
            Key = key;
            Json = json;
            WriteLock = writeLock;
        }

        public string Key { get; }
        public string Json { get; }
        public string WriteLock { get; }
    }

    /// <summary>A write lock didn't match: someone else wrote since we read. Nothing was written.</summary>
    public sealed class WriteConflictException : Exception
    {
        public WriteConflictException(string message) : base(message)
        {
        }
    }

    /// <summary>
    /// The Cloud Save calls the game logic needs, so it can run against a fake in tests.
    /// Player keys use the Protected access class: the player can read them, only the server writes.
    /// </summary>
    public interface IGameStore
    {
        /// <summary>The keys that exist, with their write locks. Missing keys are left out.</summary>
        Task<Dictionary<string, StoredItem>> GetProtectedAsync(string playerId, IReadOnlyList<string> keys);

        /// <summary>
        /// Writes every item or none (one access class, so the batch is atomic). Throws
        /// WriteConflictException if any lock doesn't match.
        /// </summary>
        Task SetProtectedBatchAsync(string playerId, IReadOnlyList<ItemWrite> writes);

        /// <summary>Server-only custom item, for records no player needs to write (raid records).</summary>
        Task SetPrivateCustomAsync(string customId, string key, string json);
    }
}
