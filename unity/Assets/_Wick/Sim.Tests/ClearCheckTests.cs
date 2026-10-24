using System.IO;
using NUnit.Framework;

namespace Wick.Sim.Tests
{
    public class ClearCheckTests
    {
        private static RaidFile Golden(string name)
        {
            return RaidFile.Parse(File.ReadAllText(Path.Combine(GoldenReplayTests.GoldenDirectory(), name)));
        }

        private static ClearCheck FromGolden(RaidFile raid)
        {
            RaidResult r = RaidRunner.RunToEnd(raid.Vault, raid.Seed, raid.Log);
            return ClearCheck.FromRun(raid.Vault, raid.Seed, raid.Log, r);
        }

        [Test]
        public void AnEscapeWithStars_Verifies_AndReproducesTheStoredHash()
        {
            RaidFile heist = Golden("full-heist.json");
            ClearCheck cc = FromGolden(heist);
            Assert.AreEqual(heist.ExpectedHash, cc.Hash);
            Assert.AreEqual(3, cc.Stars);

            ClearCheckResult result = ClearCheck.Verify(heist.Vault, cc);
            Assert.IsTrue(result.Ok, result.Reason);
            Assert.AreEqual(cc.Hash, result.Replay.Value.HashHex);
        }

        [Test]
        public void TamperedHash_Fails()
        {
            RaidFile heist = Golden("full-heist.json");
            ClearCheck cc = FromGolden(heist);
            var forged = new ClearCheck(cc.Seed, cc.Log, "0123456789abcdef", cc.Stars, cc.SimVersion, cc.LayoutHash);
            Assert.IsFalse(ClearCheck.Verify(heist.Vault, forged).Ok);
        }

        [Test]
        public void TamperedLog_Fails()
        {
            RaidFile heist = Golden("full-heist.json");
            ClearCheck cc = FromGolden(heist);
            var commands = new Command[cc.Log.Count - 1];
            for (int i = 0; i < commands.Length; i++)
            {
                commands[i] = cc.Log[i];
            }

            var shortened = new ClearCheck(cc.Seed, new InputLog(commands), cc.Hash, cc.Stars, cc.SimVersion, cc.LayoutHash);
            ClearCheckResult result = ClearCheck.Verify(heist.Vault, shortened);
            Assert.IsFalse(result.Ok);
            StringAssert.Contains("did not escape", result.Reason);
        }

        [Test]
        public void EditingTheVault_MakesTheClearCheckStale()
        {
            RaidFile heist = Golden("full-heist.json");
            ClearCheck cc = FromGolden(heist);
            string edited = VaultJson.Write(heist.Vault).Replace("\"cycle\":60", "\"cycle\":61");
            VaultDef changed = VaultJson.Parse(edited);
            Assert.IsFalse(cc.MatchesLayout(changed));
            StringAssert.Contains("changed", ClearCheck.Verify(changed, cc).Reason);
        }

        [Test]
        public void EscapingWithoutStars_Fails()
        {
            RaidFile drip = Golden("drip-dash.json"); // escapes with 2 flickers, no treasure: 0 stars
            ClearCheckResult result = ClearCheck.Verify(drip.Vault, FromGolden(drip));
            Assert.IsFalse(result.Ok);
            StringAssert.Contains("no stars", result.Reason);
        }

        [Test]
        public void TimingOut_Fails()
        {
            RaidFile wander = Golden("timeout-wander.json");
            Assert.IsFalse(ClearCheck.Verify(wander.Vault, FromGolden(wander)).Ok);
        }

        [Test]
        public void OtherSimVersion_Fails()
        {
            RaidFile heist = Golden("full-heist.json");
            ClearCheck cc = FromGolden(heist);
            var old = new ClearCheck(cc.Seed, cc.Log, cc.Hash, cc.Stars, SimInfo.Version - 1, cc.LayoutHash);
            StringAssert.Contains("sim version", ClearCheck.Verify(heist.Vault, old).Reason);
        }

        [Test]
        public void LayoutHash_IgnoresIdVersionAndFormatting()
        {
            RaidFile heist = Golden("full-heist.json");
            string json = VaultJson.Write(heist.Vault);
            VaultDef renamed = VaultJson.Parse(json.Replace("\"version\":1", "\"version\":9").Replace("v_golden_heist", "v_other"));
            Assert.AreEqual(ClearCheck.LayoutHashOf(heist.Vault), ClearCheck.LayoutHashOf(renamed));
        }

        [Test]
        public void Document_RoundTripsTheClearCheck_AndBuildPoints()
        {
            RaidFile heist = Golden("full-heist.json");
            ClearCheck cc = FromGolden(heist);
            string doc = VaultJson.WriteDocument(heist.Vault, cc);

            JsonNode root = JsonReader.Parse(doc);
            Assert.AreEqual(VaultRules.BuildPoints(heist.Vault), root.GetInt("buildPoints"));
            VaultDef vault = VaultJson.FromNode(root);
            ClearCheck back = VaultJson.ReadClearCheck(root);
            Assert.AreEqual(cc.Hash, back.Hash);
            Assert.AreEqual(cc.LayoutHash, back.LayoutHash);
            Assert.AreEqual(cc.Log.ToBase64(), back.Log.ToBase64());
            Assert.IsTrue(ClearCheck.Verify(vault, back).Ok, "a stored clear-check re-verifies from the document");
            Assert.IsNull(VaultJson.ReadClearCheck(JsonReader.Parse(VaultJson.WriteDocument(vault, null))));
        }

        [Test]
        public void TheRaidLength_IsStored_AndUsedWhenVerifying()
        {
            RaidFile heist = Golden("full-heist.json");
            RaidParams longer = RaidParams.ForSeconds(90);
            RaidResult r = RaidRunner.RunToEnd(heist.Vault, heist.Seed, heist.Log, longer);
            ClearCheck cc = ClearCheck.FromRun(heist.Vault, heist.Seed, heist.Log, r, longer);

            ClearCheck back = VaultJson.ReadClearCheck(JsonReader.Parse(VaultJson.WriteDocument(heist.Vault, cc)));
            Assert.AreEqual(longer.RaidTicks, back.Params.RaidTicks);
            Assert.IsTrue(ClearCheck.Verify(heist.Vault, back).Ok);
        }

        [Test]
        public void AClearCheckWithoutParams_ReadsAsTheDefaultRaid()
        {
            var node = JsonReader.Parse("{\"seed\":1,\"log\":\"\",\"hash\":\"0000000000000000\",\"stars\":1}");
            Assert.AreEqual(SimTuning.RaidTicks, ClearCheck.FromNode(node).Params.RaidTicks);
        }
    }
}
