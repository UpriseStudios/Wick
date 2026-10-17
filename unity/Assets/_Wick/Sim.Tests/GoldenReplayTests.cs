using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;

namespace Wick.Sim.Tests
{
    /// <summary>
    /// Replays tests/golden/*.json. The same files run in Unity and .NET, so passing on both
    /// means both produce identical hashes.
    /// </summary>
    public class GoldenReplayTests
    {
        public static IEnumerable<string> GoldenFiles()
        {
            string[] files = Directory.GetFiles(GoldenDirectory(), "*.json");
            Array.Sort(files, StringComparer.Ordinal);
            foreach (string f in files)
            {
                yield return Path.GetFileName(f);
            }
        }

        [Test]
        public void AtLeastThreeGoldensExist()
        {
            Assert.GreaterOrEqual(Directory.GetFiles(GoldenDirectory(), "*.json").Length, 3);
        }

        [TestCaseSource(nameof(GoldenFiles))]
        public void ReplayMatchesExpected(string file)
        {
            RaidFile raid = Load(file);
            Assert.AreEqual(SimInfo.Version, raid.SimVersion,
                "Golden was recorded for another sim version; re-record it deliberately (see CLAUDE.md)");
            Assert.IsTrue(raid.HasExpected, "golden replays must have an expected block");

            RaidResult r = RaidRunner.RunToEnd(raid.Vault, raid.Seed, raid.Log);
            Assert.AreEqual(raid.ExpectedOutcome, r.Outcome, "outcome");
            Assert.AreEqual(raid.ExpectedTicks, r.Ticks, "ticks");
            if (raid.ExpectedStars != null)
            {
                Assert.AreEqual(raid.ExpectedStars, r.Stars, "stars");
            }

            if (raid.ExpectedLoot != null)
            {
                Assert.AreEqual(raid.ExpectedLoot, r.Loot, "loot");
            }

            Assert.AreEqual(raid.ExpectedHash, r.HashHex, "hash");
        }

        /// <summary>
        /// Optional "expectedEvents": { "FlickerLost": [14, 60], "GuardianAlerted": [] }. For each
        /// listed type, the ticks it fires on must match exactly (hand-worked in the file's _note).
        /// </summary>
        [TestCaseSource(nameof(GoldenFiles))]
        public void ListedEventsFireOnTheirTicks(string file)
        {
            RaidFile raid = Load(file);
            JsonNode expected = raid.Root.Find("expectedEvents");
            if (expected == null)
            {
                Assert.Pass("no expectedEvents");
            }

            var ticks = new List<int>[Enum.GetValues(typeof(SimEventType)).Length];
            for (int i = 0; i < ticks.Length; i++)
            {
                ticks[i] = new List<int>();
            }

            var sim = new RaidSim(raid.Vault, raid.Seed, raid.Log);
            while (!sim.IsOver)
            {
                int tick = sim.State.Tick;
                sim.Step();
                foreach (SimEvent e in sim.Events)
                {
                    ticks[(int)e.Type].Add(tick);
                }
            }

            foreach (SimEventType type in Enum.GetValues(typeof(SimEventType)))
            {
                JsonNode list = expected.Find(type.ToString());
                if (list == null)
                {
                    continue;
                }

                var want = new int[list.Count];
                for (int i = 0; i < want.Length; i++)
                {
                    want[i] = list[i].AsInt();
                }

                CollectionAssert.AreEqual(want, ticks[(int)type], type + " ticks");
            }
        }

        [TestCaseSource(nameof(GoldenFiles))]
        public void ReadableMapMatchesTiles(string file)
        {
            RaidFile raid = Load(file);
            JsonNode map = raid.Root.Get("vault").Find("_map");
            if (map == null)
            {
                Assert.Pass("no _map");
            }

            var rows = new string[map.Count];
            for (int i = 0; i < rows.Length; i++)
            {
                rows[i] = map[i].AsString();
            }

            byte[] expected = TestVaults.MapToTiles(rows);
            Assert.AreEqual(raid.Vault.Width * raid.Vault.Height, expected.Length, "_map size");
            for (int y = 0; y < raid.Vault.Height; y++)
            {
                for (int x = 0; x < raid.Vault.Width; x++)
                {
                    Assert.AreEqual((TileType)expected[y * raid.Vault.Width + x], raid.Vault.TileAt(new TilePos(x, y)),
                        "tile (" + x + "," + y + ")");
                }
            }
        }

        [TestCaseSource(nameof(GoldenFiles))]
        public void ReadableCommandsMatchBase64Log(string file)
        {
            RaidFile raid = Load(file);
            JsonNode readable = raid.Root.Find("_commands");
            if (readable == null)
            {
                Assert.Pass("no _commands");
            }

            Assert.AreEqual(readable.Count, raid.Log.Count, "command count");
            for (int i = 0; i < readable.Count; i++)
            {
                JsonNode c = readable[i];
                CommandType type = RaidFile.ParseCommandType(c.GetString("type"));
                Assert.AreEqual(c.GetInt("tick"), raid.Log[i].Tick, "tick " + i);
                Assert.AreEqual(type, raid.Log[i].Type, "type " + i);
                if (Command.HasDir(type))
                {
                    Assert.AreEqual(RaidFile.ParseDir(c.GetString("dir")), raid.Log[i].Dir, "dir " + i);
                }
            }
        }

        [Test]
        public void SameRaid1000Times_GivesOneHash()
        {
            string json = File.ReadAllText(Path.Combine(GoldenDirectory(), "escape-run.json"));
            string other = File.ReadAllText(Path.Combine(GoldenDirectory(), "timeout-wander.json"));

            ulong first = 0;
            int distinct = 0;
            for (int i = 0; i < 1000; i++)
            {
                RaidFile raid = RaidFile.Parse(json);
                ulong hash = RaidRunner.RunToEnd(raid.Vault, raid.Seed, raid.Log).FinalHash;
                if (i == 0)
                {
                    first = hash;
                    distinct = 1;
                }
                else if (hash != first)
                {
                    distinct++;
                }

                // Run a different raid in between to catch state leaking across runs.
                RaidFile noise = RaidFile.Parse(other);
                RaidRunner.RunToEnd(noise.Vault, noise.Seed, noise.Log);
            }

            Assert.AreEqual(1, distinct, "distinct hashes across 1000 runs");
        }

        private static RaidFile Load(string file)
        {
            return RaidFile.Parse(File.ReadAllText(Path.Combine(GoldenDirectory(), file)));
        }

        /// <summary>
        /// Finds tests/golden by walking up from the working directory (unity/ in Unity)
        /// and from the test assembly's directory (bin/... in .NET).
        /// </summary>
        public static string GoldenDirectory()
        {
            string[] starts = { Directory.GetCurrentDirectory(), AppDomain.CurrentDomain.BaseDirectory };
            foreach (string start in starts)
            {
                var dir = new DirectoryInfo(start);
                while (dir != null)
                {
                    string candidate = Path.Combine(dir.FullName, "tests", "golden");
                    if (Directory.Exists(candidate))
                    {
                        return candidate;
                    }

                    dir = dir.Parent;
                }
            }

            throw new DirectoryNotFoundException("Could not find tests/golden above " + starts[0]);
        }
    }
}
