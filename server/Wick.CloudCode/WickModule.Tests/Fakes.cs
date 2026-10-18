using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Wick.Sim;

namespace Wick.CloudCode.Tests
{
    /// <summary>
    /// In-memory Cloud Save with the same write-lock rules as the real service: a batch with any
    /// mismatched lock writes nothing; a null lock overwrites whatever is there.
    /// </summary>
    public sealed class FakeStore : IGameStore
    {
        private readonly object _gate = new object();
        private readonly Dictionary<string, StoredItem> _items = new Dictionary<string, StoredItem>();
        private long _nextLock;

        public readonly Dictionary<string, string> Custom = new Dictionary<string, string>();
        public int Conflicts;
        public int Batches;

        /// <summary>Runs after every read returns its data, so a test can hold readers together.</summary>
        public Func<Task> AfterRead;

        /// <summary>When true, every batch conflicts (a key someone else keeps rewriting).</summary>
        public bool AlwaysConflict;

        public async Task<Dictionary<string, StoredItem>> GetProtectedAsync(string playerId, IReadOnlyList<string> keys)
        {
            var found = new Dictionary<string, StoredItem>();
            lock (_gate)
            {
                foreach (string key in keys)
                {
                    if (_items.TryGetValue(Id(playerId, key), out StoredItem item))
                    {
                        found[key] = new StoredItem(key, item.Json, item.WriteLock);
                    }
                }
            }

            if (AfterRead != null)
            {
                await AfterRead();
            }

            return found;
        }

        public Task SetProtectedBatchAsync(string playerId, IReadOnlyList<ItemWrite> writes)
        {
            lock (_gate)
            {
                foreach (ItemWrite w in writes)
                {
                    bool exists = _items.TryGetValue(Id(playerId, w.Key), out StoredItem current);
                    bool mismatch = w.WriteLock != null && (!exists || current.WriteLock != w.WriteLock);
                    if (mismatch || AlwaysConflict)
                    {
                        Conflicts++;
                        throw new WriteConflictException("conflict on " + w.Key);
                    }
                }

                foreach (ItemWrite w in writes)
                {
                    _items[Id(playerId, w.Key)] = new StoredItem(w.Key, w.Json, "L" + (++_nextLock));
                }

                Batches++;
            }

            return Task.CompletedTask;
        }

        public Task SetPrivateCustomAsync(string customId, string key, string json)
        {
            lock (_gate)
            {
                Custom[customId + "/" + key] = json;
            }

            return Task.CompletedTask;
        }

        public int CustomWrites;
        public int Queries;

        public Task SetPrivateCustomBatchAsync(string customId, IReadOnlyList<ItemWrite> writes)
        {
            lock (_gate)
            {
                foreach (ItemWrite w in writes)
                {
                    Custom[customId + "/" + w.Key] = w.Json;
                }

                CustomWrites++;
            }

            return Task.CompletedTask;
        }

        public Task<Dictionary<string, StoredItem>> GetPrivateCustomAsync(string customId, IReadOnlyList<string> keys)
        {
            var found = new Dictionary<string, StoredItem>();
            lock (_gate)
            {
                foreach (string key in keys)
                {
                    if (Custom.TryGetValue(customId + "/" + key, out string json))
                    {
                        found[key] = new StoredItem(key, json, null);
                    }
                }
            }

            return Task.FromResult(found);
        }

        /// <summary>Like an index query: every filter must hold; results in id order, up to sampleSize.</summary>
        public Task<List<QueryHit>> QueryPrivateCustomAsync(IReadOnlyList<QueryFilter> filters, IReadOnlyList<string> returnKeys, int sampleSize)
        {
            var hits = new List<QueryHit>();
            lock (_gate)
            {
                Queries++;
                var ids = Custom.Keys.Select(k => k.Substring(0, k.IndexOf('/'))).Distinct().OrderBy(id => id, System.StringComparer.Ordinal);
                foreach (string id in ids)
                {
                    if (hits.Count < sampleSize && filters.All(f => Matches(id, f)))
                    {
                        var values = new Dictionary<string, string>();
                        foreach (string key in returnKeys)
                        {
                            if (Custom.TryGetValue(id + "/" + key, out string json))
                            {
                                values[key] = json;
                            }
                        }

                        hits.Add(new QueryHit(id, values));
                    }
                }
            }

            return Task.FromResult(hits);
        }

        private bool Matches(string id, QueryFilter f)
        {
            if (!Custom.TryGetValue(id + "/" + f.Key, out string json))
            {
                return false;
            }

            JToken stored = JToken.Parse(json);
            if (f.Op == QueryOp.Eq)
            {
                return stored.ToString() == f.Value.ToString();
            }

            long a = stored.Value<long>();
            long b = System.Convert.ToInt64(f.Value);
            return f.Op == QueryOp.Ge ? a >= b : a <= b;
        }

        public string Read(string playerId, string key)
        {
            lock (_gate)
            {
                return _items.TryGetValue(Id(playerId, key), out StoredItem item) ? item.Json : null;
            }
        }

        public int Count
        {
            get
            {
                lock (_gate)
                {
                    return _items.Count;
                }
            }
        }

        private static string Id(string playerId, string key)
        {
            return playerId + "/" + key;
        }
    }

    public sealed class FakeClock : IClock
    {
        public long NowMs { get; set; } = 1_000_000;

        public void Advance(long ms)
        {
            NowMs += ms;
        }
    }

    public sealed class FakeRandom : IRandomSource
    {
        private int _ids;

        public int Seed = 4242;

        public int NextSeed()
        {
            return Seed;
        }

        public string NextId()
        {
            return "tok" + (++_ids).ToString("D5");
        }

        public int NextIndex(int count)
        {
            return 0;
        }
    }

    /// <summary>Vaults and runs built from the Three Halls template.</summary>
    public static class TestVaults
    {
        public static VaultDef Halls()
        {
            string path = Path.Combine(TestContext.CurrentContext.TestDirectory, "Data", "three-halls.json");
            return VaultJson.Parse(File.ReadAllText(path));
        }

        /// <summary>Runs up the middle to the hoard and back out: escapes with 3 stars.</summary>
        public static readonly InputLog Escape = new InputLog(new[] { Command.RunStart(0, Dir.N), Command.RunStart(150, Dir.S) });

        public const int Seed = 4242;

        /// <summary>The document the editor publishes: the vault plus a clear-check of the escape run.</summary>
        public static string PublishDocument(VaultDef vault)
        {
            RaidResult run = RaidRunner.RunToEnd(vault, Seed, Escape);
            return VaultJson.WriteDocument(vault, ClearCheck.FromRun(vault, Seed, Escape, run));
        }

        public static VaultDef WithObjects(VaultDef v, params ObjectDef[] extra)
        {
            var tiles = new TileType[v.Width * v.Height];
            for (int i = 0; i < tiles.Length; i++)
            {
                tiles[i] = v.TileAtIndex(i);
            }

            var objects = new List<ObjectDef>();
            for (int i = 0; i < v.ObjectCount; i++)
            {
                objects.Add(v.GetObject(i));
            }

            objects.AddRange(extra);
            return new VaultDef(v.Schema, v.VaultId, v.Version, v.VaultLevel, v.Width, v.Height, tiles, v.Entrance,
                objects.ToArray(), v.HasHoard, v.Hoard, v.Theme);
        }

        public static ObjectDef Drip(int id, int x, int y)
        {
            return new ObjectDef(id, ObjectType.DripPipe, new TilePos(x, y), 0, 45, 0, 0, new TilePos[0]);
        }
    }

    public sealed class FakeIntegrity : IIntegrityVerifier
    {
        public IntegrityVerdict Verdict = IntegrityVerdict.Unchecked("fake");

        public Task<IntegrityVerdict> VerifyAsync(string playerId, string platform, string token)
        {
            return Task.FromResult(Verdict);
        }
    }

    public sealed class FakePush : IPushSender
    {
        public readonly List<(string Token, string Title, string Body)> Sent = new List<(string, string, string)>();

        public Task SendAsync(string deviceToken, string title, string body)
        {
            lock (Sent)
            {
                Sent.Add((deviceToken, title, body));
            }

            return Task.CompletedTask;
        }
    }

    public sealed class FakeHttp : IHttp
    {
        public readonly List<(string Url, string Body, string Bearer)> Calls = new List<(string, string, string)>();
        public System.Func<string, (int, string)> Respond = url => (200, "{}");

        public Task<(int Status, string Body)> PostAsync(string url, string contentType, string body, string bearerToken)
        {
            Calls.Add((url, body, bearerToken));
            return Task.FromResult(Respond(url));
        }
    }
}
