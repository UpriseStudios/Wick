using System.Collections.Generic;
using NUnit.Framework;

namespace Wick.Sim.Tests
{
    /// <summary>M10 decorations: cosmetic props the raid sim never reads (docs/m10-plan.md, A5).</summary>
    public class DecorationTests
    {
        // 12 x 16 three rooms (as VaultValidatorTests): hoard rows 1-2, entrance (4,12).
        private static readonly string[] Rooms =
        {
            "############",
            "#.......####",
            "#.......####",
            "###.########",
            "#.......####",
            "#.......####",
            "#.......####",
            "#.......####",
            "#.......####",
            "###.########",
            "#.......####",
            "#.......####",
            "####E#######",
            "############",
            "############",
            "############",
        };

        private const string Pile = "{\"id\":1,\"type\":\"treasure\",\"x\":4,\"y\":1,\"value\":100}";

        private static VaultDef Vault(params string[] decor)
        {
            string json = TestVaults.Json(Rooms, "[" + Pile + "]");
            json = json.Substring(0, json.Length - 1) + ",\"hoard\":[1,1,7,2],\"decor\":[" + string.Join(",", decor) + "]}";
            return VaultJson.Parse(json);
        }

        private static string Decor(int id, string kind, int x, int y, int rot = 0)
        {
            return "{\"id\":" + id + ",\"kind\":\"" + kind + "\",\"x\":" + x + ",\"y\":" + y + ",\"rot\":" + rot + "}";
        }

        private static List<VaultIssueCode> Codes(VaultDef v)
        {
            return VaultValidator.Validate(v).ConvertAll(i => i.Code);
        }

        [Test]
        public void Decorations_RoundTrip_AndPlainVaultsWriteNoField()
        {
            StringAssert.DoesNotContain("decor", VaultJson.Write(Vault()));
            VaultDef v = Vault(Decor(20, Decorations.Statue, 2, 6), Decor(21, Decorations.Banner, 0, 5, 1));
            VaultDef back = VaultJson.Parse(VaultJson.Write(v));
            Assert.AreEqual(2, back.DecorationCount);
            Assert.AreEqual(Decorations.Statue, back.GetDecoration(0).Kind);
            Assert.AreEqual(new TilePos(0, 5), back.GetDecoration(1).Pos);
            Assert.AreEqual(1, back.GetDecoration(1).Rot);
        }

        [Test]
        public void TheSimNeverReadsThem_AndTheyArentPartOfTheLayout()
        {
            VaultDef plain = Vault();
            VaultDef decorated = Vault(Decor(20, Decorations.Statue, 4, 6), Decor(21, Decorations.GoldPile, 3, 7));
            var log = new InputLog(new[] { Command.RunStart(0, Dir.N), Command.RunStart(130, Dir.S) });
            Assert.AreEqual(RaidRunner.RunToEnd(plain, 7, log).FinalHash, RaidRunner.RunToEnd(decorated, 7, log).FinalHash,
                "a statue in Wick's path changes nothing: he walks through props");
            Assert.AreEqual(ClearCheck.LayoutHashOf(plain), ClearCheck.LayoutHashOf(decorated), "redecorating needs no new test run");
        }

        [Test]
        public void ValidDecorations_PassTheRules()
        {
            CollectionAssert.IsEmpty(Codes(Vault(
                Decor(20, Decorations.Statue, 2, 6),
                Decor(21, Decorations.Banner, 0, 5, 1),
                Decor(22, "prop_trophy_vault_snuffer_gold", 2, 2))));
        }

        [Test]
        public void PlacementRules()
        {
            Assert.That(Codes(Vault(Decor(20, Decorations.Statue, 0, 5))), Has.Member(VaultIssueCode.DecorationPlacement), "floor prop on a wall");
            Assert.That(Codes(Vault(Decor(20, Decorations.Banner, 2, 5))), Has.Member(VaultIssueCode.DecorationPlacement), "wall prop on floor");
            Assert.That(Codes(Vault(Decor(20, Decorations.Torch, 11, 15))), Has.Member(VaultIssueCode.DecorationPlacement), "a wall with no floor beside it");
            Assert.That(Codes(Vault(Decor(20, Decorations.Statue, 4, 12))), Has.Member(VaultIssueCode.DecorationPlacement), "the entrance");
            Assert.That(Codes(Vault(Decor(20, Decorations.Statue, 4, 1))), Has.Member(VaultIssueCode.DecorationPlacement), "on the treasure");
            Assert.That(Codes(Vault(Decor(20, Decorations.Statue, 2, 6), Decor(21, Decorations.GoldPile, 2, 6))),
                Has.Member(VaultIssueCode.DecorationPlacement), "two on one tile");
            Assert.That(Codes(Vault(Decor(20, "prop_disco_ball", 2, 6))), Has.Member(VaultIssueCode.DecorationUnknown));
        }

        [Test]
        public void TrophiesStandInTheHoard()
        {
            Assert.That(Codes(Vault(Decor(20, "prop_trophy_raider_bronze", 2, 6))), Has.Member(VaultIssueCode.TrophyOutsideHoard));
        }

        [Test]
        public void TheVaultLevelLimitsHowMany()
        {
            var decor = new List<string>();
            for (int i = 0; i < VaultRules.MaxDecorations(1) + 1; i++)
            {
                decor.Add(Decor(20 + i, Decorations.Statue, 1 + i % 7, 5 + i / 7));
            }

            Assert.That(Codes(Vault(decor.ToArray())), Has.Member(VaultIssueCode.TooManyDecorations));
            Assert.AreEqual(4, VaultRules.MaxDecorations(1));
            Assert.AreEqual(22, VaultRules.MaxDecorations(10));
        }

        [Test]
        public void TrophyKinds_MapToTheCosmeticsThatUnlockThem()
        {
            Assert.AreEqual("prop_trophy_flawless_silver", Decorations.TrophyKind("trophy_flawless_silver"));
            Assert.AreEqual("trophy_flawless_silver", Decorations.CosmeticOf("prop_trophy_flawless_silver"));
            Assert.IsNull(Decorations.CosmeticOf(Decorations.Statue));
            Assert.IsFalse(Decorations.IsTrophy("prop_trophy_"));
        }
    }
}
