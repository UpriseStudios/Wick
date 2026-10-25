using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using Wick.Sim;

namespace Wick.CloudCode.Tests
{
    /// <summary>A small backend world: players publish, raid and get raided through the real services.</summary>
    public sealed class World
    {
        public readonly FakeStore Store = new FakeStore();
        public readonly FakeClock Clock = new FakeClock();
        public readonly FakeRandom Random = new FakeRandom();
        public readonly FakePush Push = new FakePush();
        public readonly FakeIntegrity Integrity = new FakeIntegrity();
        public bool Production;

        public RaidService Raids => new RaidService(Store, Clock, Random, Integrity, Push, Production);
        public DefenceService Defence => new DefenceService(Store, Clock, Push);

        private int _requests;

        public string NextRequest()
        {
            return "req-" + (++_requests).ToString("D6");
        }

        public async Task Publish(string player, VaultDef vault = null)
        {
            PublishResult r = await new PublishService(Store, Clock).PublishAsync(player, NextRequest(), TestVaults.PublishDocument(vault ?? TestVaults.Halls()));
            Assert.IsTrue(r.Ok, r.Reason);
        }

        public async Task Seed(string name, int crowns)
        {
            SeedResult r = await Defence.SeedVaultAsync(name, TestVaults.PublishDocument(TestVaults.Halls()), crowns);
            Assert.IsTrue(r.Ok, r.Reason);
        }

        /// <summary>Sets server-owned player data directly (as if earlier raids had happened).</summary>
        public async Task Set(string player, string key, object value)
        {
            await Store.SetProtectedBatchAsync(player, new[] { new ItemWrite(key, Json.Write(value), null) });
            await new Directory(Store).RefreshAsync(player);
        }

        public T Get<T>(string player, string key) where T : new()
        {
            return Json.Read<T>(Store.Read(player, key));
        }

        /// <summary>Plays the log honestly on the token's vault and submits it after enough real time.</summary>
        public async Task<SubmitResult> Raid(string raider, RaidStartResult start, InputLog log, string requestId = null)
        {
            Assert.IsTrue(start.Ok, start.Reason);
            RaidResult r = RaidRunner.RunToEnd(VaultJson.Parse(start.Vault), start.Seed, log, new RaidParams(start.RaidTicks));
            Clock.Advance(r.Ticks * 1000L / SimTuning.TicksPerSecond + 1000);
            return await Raids.SubmitRaidAsync(raider, requestId ?? NextRequest(), start.Token, log.ToBase64(),
                new RaidClaim { Stars = r.Stars, Loot = r.Loot, Hash = r.HashHex });
        }

        public async Task<SubmitResult> RaidVault(string raider, string vaultId, InputLog log)
        {
            return await Raid(raider, await Raids.StartFriendRaidAsync(raider, vaultId, false), log);
        }
    }

    public static class Players
    {
        /// <summary>
        /// Plays the test halls in along the zigzag and up the middle column to the treasure, and back: a bot moves the instant a
        /// move allows and onto each drip the frame it turns safe; a person waits a few frames.
        /// </summary>
        public static InputLog UpAndBack(VaultDef vault, int seed, bool bot, int humanSeed = 7)
        {
            var sim = new RaidSim(vault, seed);
            var rng = new System.Random(humanSeed);
            int readyAt = 0;
            while (!sim.IsOver && sim.State.Tick < SimTuning.RaidTicks)
            {
                int tick = sim.State.Tick;
                WickState w = sim.State.Wick;
                if (!w.Moving && tick >= readyAt)
                {
                    Dir dir = StepToward(vault, w.Tile, w.CarriedPiles == 0 ? vault.GetObject(0).Pos : vault.Entrance);
                    TilePos next = w.Tile.Step(dir);

                    ObjectDef drip = DripAt(vault, next);
                    bool go = drip == null
                        ? true
                        : bot ? JustSafe(drip, tick) : SafeFor(drip, tick, 4) && SafeSince(drip, tick) >= rng.Next(3, 9);
                    if (go)
                    {
                        sim.Submit(Command.Dash(tick, dir));
                        readyAt = bot ? tick : tick + rng.Next(2, 9);
                    }
                }

                sim.Step();
            }

            return sim.RecordedLog;
        }

        /// <summary>The first step of a shortest walk from `from` to `to` (walls block).</summary>
        private static Dir StepToward(VaultDef vault, TilePos from, TilePos to)
        {
            // Walk back from the goal: the neighbour of `from` nearest to it is the next step.
            var dist = new Dictionary<TilePos, int> { [to] = 0 };
            var queue = new Queue<TilePos>();
            queue.Enqueue(to);
            while (queue.Count > 0)
            {
                TilePos p = queue.Dequeue();
                for (int d = 0; d < 4; d++)
                {
                    TilePos n = p.Step((Dir)d);
                    if (vault.TileAt(n) != TileType.Wall && !dist.ContainsKey(n))
                    {
                        dist[n] = dist[p] + 1;
                        queue.Enqueue(n);
                    }
                }
            }

            Dir best = Dir.S;
            int bestDist = int.MaxValue;
            for (int d = 0; d < 4; d++)
            {
                if (dist.TryGetValue(from.Step((Dir)d), out int n) && n < bestDist)
                {
                    best = (Dir)d;
                    bestDist = n;
                }
            }

            return best;
        }

        private static ObjectDef DripAt(VaultDef vault, TilePos p)
        {
            for (int i = 0; i < vault.ObjectCount; i++)
            {
                ObjectDef o = vault.GetObject(i);
                if (o.Type == ObjectType.DripPipe && o.Pos == p)
                {
                    return o;
                }
            }

            return null;
        }

        private static bool JustSafe(ObjectDef trap, int t)
        {
            return t > 0 && RaidSim.IsTrapActive(t - 1, trap, 1) && !RaidSim.IsTrapActive(t, trap, 1);
        }

        private static int SafeSince(ObjectDef trap, int t)
        {
            int n = 0;
            while (t - n > 0 && !RaidSim.IsTrapActive(t - n - 1, trap, 1))
            {
                n++;
            }

            return n;
        }

        private static bool SafeFor(ObjectDef trap, int t, int ticks)
        {
            for (int i = 0; i <= ticks; i++)
            {
                if (RaidSim.IsTrapActive(t + i, trap, 1))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>Three Halls with three drips up the middle column.</summary>
        public static VaultDef DripHalls()
        {
            return TestVaults.WithObjects(TestVaults.Halls(),
                new ObjectDef(20, ObjectType.DripPipe, new TilePos(4, 5), 0, 45, 0, 0, new TilePos[0]),
                new ObjectDef(21, ObjectType.DripPipe, new TilePos(4, 6), 0, 45, 15, 0, new TilePos[0]),
                new ObjectDef(22, ObjectType.DripPipe, new TilePos(4, 7), 0, 45, 30, 0, new TilePos[0]));
        }
    }
}
