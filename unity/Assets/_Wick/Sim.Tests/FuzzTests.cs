using System;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;

namespace Wick.Sim.Tests
{
    /// <summary>
    /// 2000 random raids on random small vaults, generated from a seeded Pcg32 so Unity and .NET
    /// run exactly the same corpus. Checks: no exceptions, invariants every tick, determinism, and
    /// one combined hash that both runtimes must reproduce.
    /// </summary>
    public class FuzzTests
    {
        private const int Raids = 2000;

        private static readonly string[] Types =
        {
            "drip_pipe", "bellows", "spike_floor", "snuffer_bell", "night_owl", "wax_moths", "treasure", "brazier",
            "swing_axe", "mimic_chest", "sleepy_troll", "fan", "crate",
        };

        [Test]
        public void RandomRaids_RunWithoutExceptions_AndKeepInvariants()
        {
            var rng = new Pcg32(2026, 77);
            var outcomes = new int[4];
            int withStars = 0;
            for (int i = 0; i < Raids; i++)
            {
                string json = RandomVaultJson(ref rng);
                InputLog log = RandomLog(ref rng);
                int seed = (int)rng.NextUInt();

                VaultDef vault;
                try
                {
                    vault = VaultJson.Parse(json);
                }
                catch (FormatException e)
                {
                    Assert.Fail("raid " + i + ": generator made an invalid vault: " + e.Message + "\n" + json);
                    return;
                }

                RaidResult result;
                try
                {
                    result = RunChecked(vault, seed, log, i);
                }
                catch (Exception e) when (!(e is AssertionException))
                {
                    Assert.Fail("raid " + i + " threw " + e + "\nseed " + seed + "\n" + json);
                    return;
                }

                Assert.AreNotEqual(Outcome.Running, result.Outcome, "raid " + i);
                Assert.That(result.Ticks, Is.InRange(1, SimTuning.RaidTicks), "raid " + i);
                Assert.That(result.Stars, Is.InRange(0, 3), "raid " + i);
                Assert.GreaterOrEqual(result.Loot, 0, "raid " + i);
                outcomes[(int)result.Outcome]++;
                withStars += result.Stars > 0 ? 1 : 0;

                if (i % 10 == 0)
                {
                    RaidResult again = RaidRunner.RunToEnd(VaultJson.Parse(json), seed, log);
                    Assert.AreEqual(result.FinalHash, again.FinalHash, "raid " + i + " is not deterministic");
                }
            }

            TestContext.Out.WriteLine("Escaped " + outcomes[(int)Outcome.Escaped] + ", Snuffed " +
                outcomes[(int)Outcome.Snuffed] + ", TimedOut " + outcomes[(int)Outcome.TimedOut] +
                ", with stars " + withStars + " of " + Raids);
            Assert.Greater(outcomes[(int)Outcome.Escaped], 0, "the corpus should reach every outcome");
            Assert.Greater(outcomes[(int)Outcome.Snuffed], 0, "the corpus should reach every outcome");
            Assert.Greater(outcomes[(int)Outcome.TimedOut], 0, "the corpus should reach every outcome");
        }

        /// <summary>
        /// FNV-1a over every raid's final hash, outcome, ticks, stars and loot. The same number in
        /// Unity and .NET means the whole corpus is bit-identical across runtimes. Like the golden
        /// replays, re-record it deliberately when sim rules change (bump SimInfo.Version).
        /// </summary>
        [Test]
        public void Corpus_CombinedHash_IsStable()
        {
            const string expected = "5768607da932e928";

            var rng = new Pcg32(2026, 77);
            var combined = new Fnv1a64();
            for (int i = 0; i < Raids; i++)
            {
                VaultDef vault = VaultJson.Parse(RandomVaultJson(ref rng));
                InputLog log = RandomLog(ref rng);
                int seed = (int)rng.NextUInt();
                RaidResult r = RaidRunner.RunToEnd(vault, seed, log);
                combined.AddLong(r.FinalHash);
                combined.AddByte((byte)r.Outcome);
                combined.AddInt(r.Ticks);
                combined.AddInt(r.Stars);
                combined.AddInt(r.Loot);
            }

            Assert.AreEqual(expected, RaidResult.FormatHash(combined.Value));
        }

        private static RaidResult RunChecked(VaultDef vault, int seed, InputLog log, int index)
        {
            var sim = new RaidSim(vault, seed, log);
            while (!sim.IsOver)
            {
                sim.Step();
                WickState w = sim.State.Wick;
                if (w.Flickers < 0 || w.Flickers > SimTuning.StartFlickers)
                {
                    Assert.Fail("raid " + index + " tick " + sim.State.Tick + ": flickers " + w.Flickers);
                }

                if (!vault.IsWalkable(w.Tile) || (w.Moving && !vault.IsWalkable(w.Tile.Step(w.MoveDir))))
                {
                    Assert.Fail("raid " + index + " tick " + sim.State.Tick + ": Wick off the walkable floor at " + w.Tile);
                }

                foreach (GuardianState g in sim.State.Guardians)
                {
                    if (!vault.IsWalkable(g.Tile))
                    {
                        Assert.Fail("raid " + index + " tick " + sim.State.Tick + ": guardian on a blocked tile " + g.Tile);
                    }
                }
            }

            return sim.GetResult();
        }

        // ---- Generators ------------------------------------------------------------------

        private static string RandomVaultJson(ref Pcg32 rng)
        {
            int w = 5 + rng.NextInt(5);
            int h = 5 + rng.NextInt(5);
            var walls = new bool[w * h];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    bool border = x == 0 || y == 0 || x == w - 1 || y == h - 1;
                    walls[y * w + x] = border || rng.NextInt(100) < 15;
                }
            }

            var entrance = new TilePos(1 + rng.NextInt(w - 2), 1 + rng.NextInt(h - 2));
            walls[entrance.Y * w + entrance.X] = false;

            var crates = new bool[w * h];
            var objects = new List<string>();
            int count = rng.NextInt(7);

            // Crates first, so everything else can avoid them.
            for (int i = 0; i < count; i++)
            {
                if (rng.NextInt(5) != 0)
                {
                    continue;
                }

                TilePos p = RandomFloor(ref rng, w, h, walls, crates);
                if (p != entrance)
                {
                    crates[p.Y * w + p.X] = true;
                    objects.Add(Obj(objects.Count + 1, "crate", p, ""));
                }
            }

            int others = count - objects.Count;
            for (int i = 0; i < others; i++)
            {
                string type = Types[rng.NextInt(Types.Length - 1)]; // anything but crate
                int id = objects.Count + 1;
                if (type == "brazier")
                {
                    if (FindBrazierSpot(ref rng, w, h, walls, crates, out TilePos wallTile))
                    {
                        objects.Add(Obj(id, type, wallTile, ""));
                    }

                    continue;
                }

                TilePos p = RandomFloor(ref rng, w, h, walls, crates);
                string extra = "";
                switch (type)
                {
                    case "drip_pipe":
                    case "bellows":
                    case "spike_floor":
                    case "swing_axe":
                    case "fan":
                        int cycle = ObjectTypes.MinCycle(ObjectTypes.Parse(type)) + rng.NextInt(60);
                        extra = ",\"cycle\":" + cycle + ",\"offset\":" + rng.NextInt(cycle) + ",\"rot\":" + rng.NextInt(4);
                        break;
                    case "snuffer_bell":
                        if (p == entrance)
                        {
                            continue;
                        }

                        break;
                    case "night_owl":
                        var patrol = new StringBuilder("[");
                        int points = rng.NextInt(4);
                        for (int k = 0; k < points; k++)
                        {
                            TilePos q = RandomFloor(ref rng, w, h, walls, crates);
                            patrol.Append(k > 0 ? "," : "").Append('[').Append(q.X).Append(',').Append(q.Y).Append(']');
                        }

                        extra = ",\"rot\":" + rng.NextInt(4) + ",\"patrol\":" + patrol.Append(']');
                        break;
                    case "treasure":
                        extra = ",\"value\":" + (1 + rng.NextInt(500));
                        break;
                }

                objects.Add(Obj(id, type, p, extra));
            }

            var bytes = new byte[w * h];
            for (int i = 0; i < bytes.Length; i++)
            {
                bytes[i] = walls[i] ? (byte)TileType.Wall : rng.NextInt(8) == 0 ? (byte)TileType.Ice : (byte)TileType.Floor;
            }

            string hoard = rng.NextInt(2) == 0 ? "" : ",\"hoard\":[1,1," + (w - 2) + "," + (1 + rng.NextInt(h - 2)) + "]";

            // Half the corpus has upgraded defences, so every level's rules get fuzzed too.
            var levels = new List<string>();
            if (rng.NextInt(2) == 0)
            {
                foreach (string type in new[] { "drip_pipe", "bellows", "spike_floor", "snuffer_bell", "night_owl", "wax_moths", "swing_axe", "mimic_chest", "sleepy_troll", "fan" })
                {
                    levels.Add("\"" + type + "\":" + (1 + rng.NextInt(DefenceLevels.MaxLevel)));
                }
            }

            string defence = levels.Count == 0 ? "" : ",\"defenceLevels\":{" + string.Join(",", levels) + "}";
            return "{\"schema\":1,\"grid\":{\"w\":" + w + ",\"h\":" + h + "},\"tiles\":\"" +
                Convert.ToBase64String(bytes) + "\",\"entrance\":[" + entrance.X + "," + entrance.Y + "]," +
                "\"objects\":[" + string.Join(",", objects) + "]" + hoard + defence + "}";
        }

        private static InputLog RandomLog(ref Pcg32 rng)
        {
            var commands = new Command[rng.NextInt(41)];
            int tick = 0;
            for (int i = 0; i < commands.Length; i++)
            {
                tick += rng.NextInt(4) == 0 ? 0 : rng.NextInt(90);
                commands[i] = new Command(tick, (CommandType)rng.NextInt(6), (Dir)rng.NextInt(4));
            }

            return new InputLog(commands);
        }

        private static TilePos RandomFloor(ref Pcg32 rng, int w, int h, bool[] walls, bool[] crates)
        {
            while (true)
            {
                var p = new TilePos(1 + rng.NextInt(w - 2), 1 + rng.NextInt(h - 2));
                if (!walls[p.Y * w + p.X] && !crates[p.Y * w + p.X])
                {
                    return p;
                }
            }
        }

        private static bool FindBrazierSpot(ref Pcg32 rng, int w, int h, bool[] walls, bool[] crates, out TilePos spot)
        {
            for (int attempt = 0; attempt < 20; attempt++)
            {
                var p = new TilePos(rng.NextInt(w), rng.NextInt(h));
                if (!walls[p.Y * w + p.X])
                {
                    continue;
                }

                for (int d = 0; d < 4; d++)
                {
                    TilePos n = p.Step((Dir)d);
                    bool inside = n.X >= 0 && n.Y >= 0 && n.X < w && n.Y < h;
                    if (inside && !walls[n.Y * w + n.X] && !crates[n.Y * w + n.X])
                    {
                        spot = p;
                        return true;
                    }
                }
            }

            spot = default;
            return false;
        }

        private static string Obj(int id, string type, TilePos p, string extra)
        {
            return "{\"id\":" + id + ",\"type\":\"" + type + "\",\"x\":" + p.X + ",\"y\":" + p.Y + extra + "}";
        }
    }
}
