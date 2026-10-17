using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using Wick.Sim;
using Wick.Sim.Tests;

namespace Wick.Game.Tests
{
    public class RaidRunnerTests
    {
        private readonly List<GameObject> _spawned = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _spawned)
            {
                Object.DestroyImmediate(go);
            }

            _spawned.Clear();
        }

        // ---- TickAccumulator ----------------------------------------------------------

        [Test]
        public void Accumulator_StepsWholeTicks_AndReportsAlpha()
        {
            var acc = new TickAccumulator();
            Assert.AreEqual(0, acc.Advance(TickAccumulator.TickSeconds * 0.5));
            Assert.AreEqual(0.5f, acc.Alpha, 1e-4f);
            Assert.AreEqual(1, acc.Advance(TickAccumulator.TickSeconds * 0.75));
            Assert.AreEqual(0.25f, acc.Alpha, 1e-4f);
            Assert.AreEqual(2, acc.Advance(TickAccumulator.TickSeconds * 2));
        }

        [Test]
        public void Accumulator_CapsALongHitch_AndDropsTheBacklog()
        {
            var acc = new TickAccumulator();
            Assert.AreEqual(TickAccumulator.MaxStepsPerFrame, acc.Advance(1.0));
            Assert.AreEqual(0f, acc.Alpha);
            Assert.AreEqual(0, acc.Advance(0));
        }

        [Test]
        public void SixtyFpsFrames_StepThirtyTicksASecond()
        {
            var acc = new TickAccumulator();
            int ticks = 0;
            for (int frame = 0; frame < 60; frame++)
            {
                ticks += acc.Advance(1f / 60f);
            }

            Assert.That(ticks, Is.InRange(29, 30));
        }

        // ---- SimPose ------------------------------------------------------------------

        [Test]
        public void Pose_NorthIsPlusZ_AndProgressMovesAlongTheDirection()
        {
            Assert.AreEqual(new Vector3(2, 0, -3), SimPose.TileCenter(new TilePos(2, 3)));
            Vector3 halfway = SimPose.Position(new TilePos(2, 3), true, Dir.N, 500);
            Assert.AreEqual(new Vector3(2, 0, -2.5f), halfway);
            Assert.AreEqual(new TilePos(2, 3), SimPose.TileAt(new Vector3(2.2f, 0, -2.8f)));
        }

        // ---- RaidRunner ---------------------------------------------------------------

        [TestCaseSource(typeof(GoldenReplayTests), nameof(GoldenReplayTests.GoldenFiles))]
        public void LiveRun_HashEqualsReplayOfItsRecordedLog(string file)
        {
            RaidFile raid = RaidFile.Parse(File.ReadAllText(Path.Combine(GoldenReplayTests.GoldenDirectory(), file)));
            RaidRunner runner = NewRunner();
            runner.LoadLive(raid.Vault, raid.Seed);
            Assert.AreEqual(RaidMode.Scout, runner.Mode);
            Assert.IsFalse(runner.Enqueue(CommandType.Dash, Dir.N), "no input during the scout");
            runner.StartClock();

            // Play the golden's commands as if a player made them, one frame per tick.
            int next = 0;
            RaidResult? ended = null;
            runner.Ended += (r, result) => ended = result;
            while (runner.Mode == RaidMode.Live)
            {
                while (next < raid.Log.Count && raid.Log[next].Tick == runner.Sim.State.Tick)
                {
                    Assert.IsTrue(runner.Enqueue(raid.Log[next].Type, raid.Log[next].Dir));
                    next++;
                }

                runner.Advance(TickAccumulator.TickSeconds);
            }

            Assert.IsTrue(ended.HasValue);
            Assert.AreEqual(raid.ExpectedHash, ended.Value.HashHex, "live run matches the golden");

            RaidResult replay = Sim.RaidRunner.RunToEnd(raid.Vault, raid.Seed, runner.Sim.RecordedLog);
            Assert.AreEqual(ended.Value.FinalHash, replay.FinalHash, "recorded log replays to the same hash");
        }

        [Test]
        public void Snapshots_InterpolateAcrossATick()
        {
            RaidRunner runner = NewRunner();
            runner.LoadLive(TestVaults.Parse("#####", "#...#", "#.E.#", "#####"), 1);
            runner.StartClock();
            runner.Enqueue(CommandType.Dash, Dir.N);
            runner.Advance(TickAccumulator.TickSeconds * 1.5);

            Assert.AreEqual(200, runner.Current.Wick.Progress);
            Assert.AreEqual(0, runner.Previous.Wick.Progress);
            Assert.AreEqual(0.5f, runner.Alpha, 1e-3f);

            Vector3 prev = SimPose.Position(runner.Previous.Wick);
            Vector3 cur = SimPose.Position(runner.Current.Wick);
            Assert.AreEqual(-2f + 0.1f, Vector3.Lerp(prev, cur, runner.Alpha).z, 1e-4f);
        }

        [Test]
        public void Replay_IgnoresInput()
        {
            RaidRunner runner = NewRunner();
            runner.LoadReplay(TestVaults.Parse("#####", "#...#", "#.E.#", "#####"), 1, InputLog.Empty);
            Assert.IsFalse(runner.Enqueue(CommandType.Dash, Dir.N));
            runner.Advance(TickAccumulator.TickSeconds * 3);
            Assert.AreEqual(3, runner.Sim.State.Tick);
        }

        private RaidRunner NewRunner()
        {
            var go = new GameObject("RaidRunner (test)");
            _spawned.Add(go);
            return go.AddComponent<RaidRunner>();
        }
    }
}
