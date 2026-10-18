using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Wick.CloudCode
{
    /// <summary>What a Locked decision read: each key's JSON (null if missing).</summary>
    public sealed class Snapshot
    {
        private readonly Dictionary<string, StoredItem> _items;

        public Snapshot(Dictionary<string, StoredItem> items)
        {
            _items = items;
        }

        public string Json(string key)
        {
            return _items.TryGetValue(key, out StoredItem item) ? item.Json : null;
        }

        internal string Lock(string key)
        {
            return _items.TryGetValue(key, out StoredItem item) ? item.WriteLock : null;
        }
    }

    /// <summary>A decision: the result to return, and the items to write for it (none is fine).</summary>
    public sealed class Plan<T>
    {
        public Plan(T result)
        {
            Result = result;
        }

        public T Result { get; }
        public List<KeyValuePair<string, string>> Writes { get; } = new List<KeyValuePair<string, string>>();

        public Plan<T> Write(string key, string json)
        {
            Writes.Add(new KeyValuePair<string, string>(key, json));
            return this;
        }
    }

    /// <summary>Gave up after repeated write-lock conflicts. Nothing was written; the client may retry.</summary>
    public sealed class StoreBusyException : Exception
    {
        public StoreBusyException() : base("Busy: too many conflicting writes, try again")
        {
        }
    }

    /// <summary>
    /// Optimistic concurrency for player data: read the keys with their write locks, decide, write
    /// everything in one atomic batch conditioned on those locks, and on a conflict re-read and
    /// decide again. Every wallet, inventory, stats and reward write goes through here.
    /// </summary>
    public static class Locked
    {
        public const int MaxAttempts = 5;

        /// <param name="decide">Pure: it may run several times, once per attempt.</param>
        public static async Task<T> UpdateAsync<T>(IGameStore store, string playerId, IReadOnlyList<string> keys,
            Func<Snapshot, Plan<T>> decide)
        {
            for (int attempt = 1; ; attempt++)
            {
                var snapshot = new Snapshot(await store.GetProtectedAsync(playerId, keys));
                Plan<T> plan = decide(snapshot);
                if (plan.Writes.Count == 0)
                {
                    return plan.Result;
                }

                var writes = new List<ItemWrite>(plan.Writes.Count);
                foreach (KeyValuePair<string, string> w in plan.Writes)
                {
                    if (!Contains(keys, w.Key))
                    {
                        // Writing a key we didn't read would skip its lock and could overwrite blindly.
                        throw new InvalidOperationException("Locked write to unread key " + w.Key);
                    }

                    writes.Add(new ItemWrite(w.Key, w.Value, snapshot.Lock(w.Key)));
                }

                try
                {
                    await store.SetProtectedBatchAsync(playerId, writes);
                    return plan.Result;
                }
                catch (WriteConflictException)
                {
                    if (attempt >= MaxAttempts)
                    {
                        throw new StoreBusyException();
                    }
                }
            }
        }

        private static bool Contains(IReadOnlyList<string> keys, string key)
        {
            for (int i = 0; i < keys.Count; i++)
            {
                if (keys[i] == key)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
