using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Unity.Collections;
using UnityEngine;
using Wick.Sim;
using Wick.Sim.Tests;

namespace Wick.Game.Tests
{
    /// <summary>
    /// The mask texture the shader reads must be exactly Wick.Sim's visibility: the lit set
    /// while the clock runs, the scout set before it. Checked on every tick of every golden.
    /// </summary>
    public class VisibilityMaskTests
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

        [TestCaseSource(typeof(GoldenReplayTests), nameof(GoldenReplayTests.GoldenFiles))]
        public void MaskEqualsSimLitSet_OnEveryTick(string file)
        {
            RaidFile raid = RaidFile.Parse(File.ReadAllText(Path.Combine(GoldenReplayTests.GoldenDirectory(), file)));
            (RaidRunner runner, VisibilityMask mask) = NewRig();

            runner.LoadReplay(raid.Vault, raid.Seed, raid.Log);
            mask.Refresh();
            AssertMatchesSim(runner, mask, "tick 0");

            while (runner.Mode == RaidMode.Replay)
            {
                runner.Advance(TickAccumulator.TickSeconds);
                mask.Refresh();
                AssertMatchesSim(runner, mask, "tick " + runner.Sim.State.Tick);
            }
        }

        [TestCaseSource(typeof(GoldenReplayTests), nameof(GoldenReplayTests.GoldenFiles))]
        public void MaskShowsTheScoutSet_UntilTheClockStarts(string file)
        {
            RaidFile raid = RaidFile.Parse(File.ReadAllText(Path.Combine(GoldenReplayTests.GoldenDirectory(), file)));
            (RaidRunner runner, VisibilityMask mask) = NewRig();

            runner.LoadLive(raid.Vault, raid.Seed);
            mask.Refresh();
            var scout = new bool[raid.Vault.Width * raid.Vault.Height];
            Lighting.ComputeScout(raid.Vault, scout);
            AssertTexture(mask, raid.Vault, scout, "scout");

            runner.StartClock();
            mask.Refresh();
            AssertMatchesSim(runner, mask, "clock started");
        }

        private static void AssertMatchesSim(RaidRunner runner, VisibilityMask mask, string when)
        {
            VaultDef vault = runner.Vault;
            var lit = new bool[vault.Width * vault.Height];
            for (int y = 0; y < vault.Height; y++)
            {
                for (int x = 0; x < vault.Width; x++)
                {
                    lit[y * vault.Width + x] = runner.Sim.IsLit(new TilePos(x, y));
                }
            }

            AssertTexture(mask, vault, lit, when);
        }

        private static void AssertTexture(VisibilityMask mask, VaultDef vault, bool[] expected, string when)
        {
            Texture2D tex = mask.Texture;
            Assert.AreEqual(vault.Width, tex.width);
            Assert.AreEqual(vault.Height, tex.height);
            Assert.AreEqual(FilterMode.Point, tex.filterMode);

            NativeArray<byte> data = tex.GetPixelData<byte>(0); // R = lit, G = seen
            for (int y = 0; y < vault.Height; y++)
            {
                for (int x = 0; x < vault.Width; x++)
                {
                    int i = y * vault.Width + x;
                    bool texel = data[i * 2] != 0;
                    if (texel != expected[i] || mask.IsVisible(new TilePos(x, y)) != expected[i])
                    {
                        Assert.Fail(when + ": tile (" + x + "," + y + ") mask " + texel + ", sim " + expected[i]);
                    }
                }
            }
        }

        private (RaidRunner, VisibilityMask) NewRig()
        {
            var go = new GameObject("Mask rig (test)");
            _spawned.Add(go);
            RaidRunner runner = go.AddComponent<RaidRunner>();
            VisibilityMask mask = go.AddComponent<VisibilityMask>();
            mask.Bind(runner, null);
            return (runner, mask);
        }
    }
}
