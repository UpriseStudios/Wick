using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Wick.Sim;

namespace Wick.Game.Tests
{
    public class DevVaultTests
    {
        [Test]
        public void ThreeDevVaults_ParseAndPlayToAnEnd()
        {
            TextAsset[] vaults = Resources.LoadAll<TextAsset>("DevVaults");
            Assert.AreEqual(3, vaults.Length);
            foreach (TextAsset text in vaults)
            {
                VaultDef vault = VaultJson.Parse(text.text);
                Assert.IsTrue(vault.HasHoard, text.name + " marks its hoard room");
                Assert.Greater(Scoring.TotalTreasure(vault), 0, text.name + " has treasure");

                RaidResult idle = Sim.RaidRunner.RunToEnd(vault, 1, InputLog.Empty);
                Assert.AreEqual(Outcome.TimedOut, idle.Outcome, text.name + ": standing still at the entrance is safe");
            }
        }

        [Test]
        public void EveryDevVault_CanReachItsTreasureAndHoardFromTheEntrance()
        {
            foreach (TextAsset text in Resources.LoadAll<TextAsset>("DevVaults"))
            {
                VaultDef vault = VaultJson.Parse(text.text);
                bool[] reachable = Reachable(vault);

                for (int i = 0; i < vault.ObjectCount; i++)
                {
                    ObjectDef o = vault.GetObject(i);
                    if (o.Type == ObjectType.Treasure)
                    {
                        Assert.IsTrue(reachable[o.Pos.Y * vault.Width + o.Pos.X],
                            text.name + ": treasure " + o.Id + " at " + o.Pos + " cannot be reached");
                    }
                }

                bool hoardReachable = false;
                for (int y = vault.Hoard.MinY; y <= vault.Hoard.MaxY; y++)
                {
                    for (int x = vault.Hoard.MinX; x <= vault.Hoard.MaxX; x++)
                    {
                        hoardReachable |= reachable[y * vault.Width + x];
                    }
                }

                Assert.IsTrue(hoardReachable, text.name + ": the hoard room cannot be reached");
            }
        }

        /// <summary>Tiles Wick can walk to from the entrance (walls and crates block).</summary>
        private static bool[] Reachable(VaultDef vault)
        {
            var seen = new bool[vault.Width * vault.Height];
            var queue = new Queue<TilePos>();
            queue.Enqueue(vault.Entrance);
            seen[vault.Entrance.Y * vault.Width + vault.Entrance.X] = true;
            while (queue.Count > 0)
            {
                TilePos p = queue.Dequeue();
                for (int d = 0; d < 4; d++)
                {
                    TilePos n = p.Step((Dir)d);
                    if (vault.IsWalkable(n) && !seen[n.Y * vault.Width + n.X])
                    {
                        seen[n.Y * vault.Width + n.X] = true;
                        queue.Enqueue(n);
                    }
                }
            }

            return seen;
        }
    }
}
