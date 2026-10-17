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
                CollectionAssert.IsEmpty(VaultValidator.Validate(vault), text.name + " passes the vault rules");

                RaidResult idle = Sim.RaidRunner.RunToEnd(vault, 1, InputLog.Empty);
                Assert.AreEqual(Outcome.TimedOut, idle.Outcome, text.name + ": standing still at the entrance is safe");
            }
        }
    }
}
