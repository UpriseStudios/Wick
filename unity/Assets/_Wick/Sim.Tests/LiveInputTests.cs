using System;
using System.IO;
using NUnit.Framework;

namespace Wick.Sim.Tests
{
    public class LiveInputTests
    {
        [TestCaseSource(typeof(GoldenReplayTests), nameof(GoldenReplayTests.GoldenFiles))]
        public void SubmittingLive_MatchesThePrerecordedLog(string file)
        {
            RaidFile raid = RaidFile.Parse(File.ReadAllText(Path.Combine(GoldenReplayTests.GoldenDirectory(), file)));
            ulong expected = RaidRunner.RunToEnd(raid.Vault, raid.Seed, raid.Log).FinalHash;

            // Feed each command just before its tick, the way the client does.
            var sim = new RaidSim(raid.Vault, raid.Seed);
            int next = 0;
            while (!sim.IsOver)
            {
                while (next < raid.Log.Count && raid.Log[next].Tick == sim.State.Tick)
                {
                    sim.Submit(raid.Log[next++]);
                }

                sim.Step();
            }

            Assert.AreEqual(expected, sim.GetResult().FinalHash);

            // The recorded log replays to the same result.
            Assert.AreEqual(expected, RaidRunner.RunToEnd(raid.Vault, raid.Seed, sim.RecordedLog).FinalHash);
        }

        [Test]
        public void Submit_RejectsPastTicks()
        {
            var sim = new RaidSim(TestVaults.Parse("E.."), 1);
            sim.Step();
            sim.Step();
            Assert.Throws<ArgumentException>(() => sim.Submit(Command.Dash(1, Dir.E)));
            sim.Submit(Command.Dash(2, Dir.E));
            Assert.Throws<ArgumentException>(() => sim.Submit(Command.Dash(1, Dir.E)));
        }

        [Test]
        public void Submit_AfterTheEnd_Throws()
        {
            var sim = new RaidSim(TestVaults.Parse("E.."), 1);
            while (!sim.IsOver)
            {
                sim.Step();
            }

            Assert.Throws<InvalidOperationException>(() => sim.Submit(Command.Dash(sim.State.Tick, Dir.E)));
        }

        [Test]
        public void Scout_RevealsEverythingButTheHoard()
        {
            string json = TestVaults.Json(new[] { "#####", "#...#", "#E..#", "#####" });
            VaultDef noHoard = VaultJson.Parse(json);
            VaultDef hoard = VaultJson.Parse(json.Substring(0, json.Length - 1) + ",\"hoard\":[2,1,3,1]}");

            var lit = new bool[noHoard.Width * noHoard.Height];
            Lighting.ComputeScout(noHoard, lit);
            CollectionAssert.DoesNotContain(lit, false);

            Lighting.ComputeScout(hoard, lit);
            Assert.IsFalse(lit[1 * 5 + 2]);
            Assert.IsFalse(lit[1 * 5 + 3]);
            Assert.IsTrue(lit[1 * 5 + 1]);
            Assert.IsTrue(lit[0], "walls outside the hoard are shown");
        }
    }
}
