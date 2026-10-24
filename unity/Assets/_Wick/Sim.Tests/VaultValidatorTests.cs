using System.Collections.Generic;
using NUnit.Framework;

namespace Wick.Sim.Tests
{
    /// <summary>One test per rule: a valid base vault, then exactly one thing broken.</summary>
    public class VaultValidatorTests
    {
        // 12 x 16 (the Level 1 grid): three rooms joined by gaps at (3,3) and (3,9); hoard = rows
        // 1-2; entrance (4,12). Columns 9-11 and rows 13-15 are solid wall.
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

        [Test]
        public void BaseVault_IsValid()
        {
            CollectionAssert.IsEmpty(VaultValidator.Validate(Vault(1)));
        }

        [Test]
        public void WrongGridSize()
        {
            string json = TestVaults.Json(new[] { "#####", "#...#", "#.E.#", "#####" }, "[" + Pile(1, 2, 1, 1) + "]");
            json = json.Substring(0, json.Length - 1) + ",\"hoard\":[1,1,3,1]}";
            AssertOnly(VaultJson.Parse(json), VaultIssueCode.WrongGridSize);
        }

        [Test]
        public void GridSizes_FollowTheVaultLevelTable()
        {
            Assert.IsTrue(VaultRules.IsAllowedGrid(12, 16, 1));
            Assert.IsFalse(VaultRules.IsAllowedGrid(14, 18, 2), "Level 3's grid is too big for Level 2");
            Assert.IsFalse(VaultRules.IsAllowedGrid(9, 13, 1), "the vertical slice grid is gone");
            Assert.IsFalse(VaultRules.IsAllowedGrid(12, 18, 3), "only the table's sizes");

            // A vault keeps its size when its owner levels up, until they expand it.
            for (int level = 1; level <= VaultRules.MaxVaultLevel; level++)
            {
                Assert.IsTrue(VaultRules.IsAllowedGrid(12, 16, level));
                Assert.IsTrue(VaultRules.IsAllowedGrid(VaultRules.GridWidth(level), VaultRules.GridHeight(level), level));
            }

            Assert.AreEqual(20, VaultRules.GridWidth(10));
            Assert.AreEqual(28, VaultRules.GridHeight(10));
            CollectionAssert.IsEmpty(VaultValidator.Validate(Vault(3)), "a Level 3 vault may stay 12 x 16");
        }

        [TestCase(0)]
        [TestCase(11)]
        public void VaultLevelOutOfRange(int level)
        {
            Assert.That(Codes(Vault(level)), Has.Member(VaultIssueCode.VaultLevelOutOfRange));
        }

        [Test]
        public void EntranceBlocked()
        {
            VaultDef v = VaultJson.Parse(Json(Rooms, 1, Pile(1, 4, 1, 1)).Replace("\"entrance\":[4,12]", "\"entrance\":[3,12]"));
            Assert.That(Codes(v), Has.Member(VaultIssueCode.EntranceBlocked));
        }

        [Test]
        public void ObjectOnWall()
        {
            AssertOnly(Vault(1, Drip(2, 0, 5)), VaultIssueCode.ObjectOnWall, 2);
        }

        [Test]
        public void BrazierMustBeOnAWallNextToFloor()
        {
            AssertOnly(Vault(1, Obj(2, "brazier", 3, 5)), VaultIssueCode.BrazierNotOnWall, 2);
            AssertOnly(Vault(1, Obj(2, "brazier", 0, 0)), VaultIssueCode.BrazierNotNextToFloor, 2);
            CollectionAssert.IsEmpty(VaultValidator.Validate(Vault(1, Obj(2, "brazier", 0, 5))), "on the wall beside (1,5)");
        }

        [Test]
        public void BellowsMustBeInAWallBlowingIntoFloor()
        {
            const string gust = ",\"cycle\":75,\"rot\":1"; // rot 1 = east
            AssertOnly(Vault(1, Obj(2, "bellows", 3, 5, gust)), VaultIssueCode.BellowsNotOnWall, 2);
            AssertOnly(Vault(1, Obj(2, "bellows", 8, 5, gust)), VaultIssueCode.BellowsFacesWall, 2);
            CollectionAssert.IsEmpty(VaultValidator.Validate(Vault(1, Obj(2, "bellows", 0, 5, gust))), "west wall blowing east");
        }

        [Test]
        public void OneWallFittingPerWallTile()
        {
            AssertOnly(Vault(1, Obj(2, "brazier", 0, 5), Obj(3, "bellows", 0, 5, ",\"cycle\":75,\"rot\":1")),
                VaultIssueCode.TileOccupied, 3);
        }

        [Test]
        public void CratesCannotBlockADoorway()
        {
            string[] doors = (string[])Rooms.Clone();
            doors[9] = "###D########";
            Assert.That(Codes(VaultJson.Parse(Json(doors, 1, Pile(1, 4, 1, 1), Obj(2, "crate", 3, 9)))),
                Has.Member(VaultIssueCode.CrateInDoorway));
            CollectionAssert.IsEmpty(VaultValidator.Validate(VaultJson.Parse(Json(doors, 1, Pile(1, 4, 1, 1)))));
        }

        [Test]
        public void NothingSharesATileWithACrate()
        {
            AssertOnly(Vault(1, Obj(2, "crate", 5, 5), Drip(3, 5, 5)), VaultIssueCode.ObjectUnderCrate, 3);
        }

        [Test]
        public void TwoTrapsCannotShareATile_ButATrapCanGuardTreasure()
        {
            AssertOnly(Vault(1, Drip(2, 5, 5), Obj(3, "spike_floor", 5, 5, ",\"cycle\":60")), VaultIssueCode.TileOccupied, 3);
            CollectionAssert.IsEmpty(VaultValidator.Validate(Vault(1, Drip(2, 4, 1))));
        }

        [Test]
        public void CrateOrBellCannotSitOnTheEntrance()
        {
            Assert.That(Codes(Vault(1, Obj(2, "crate", 4, 12))), Has.Member(VaultIssueCode.ObjectOnEntrance));
            Assert.That(Codes(Vault(2, Obj(2, "snuffer_bell", 4, 12))), Has.Member(VaultIssueCode.ObjectOnEntrance));
        }

        [Test]
        public void LevelTooLow()
        {
            AssertOnly(Vault(1, Obj(2, "night_owl", 5, 5)), VaultIssueCode.LevelTooLow, 2);
            CollectionAssert.IsEmpty(VaultValidator.Validate(Vault(2, Obj(2, "night_owl", 5, 5))));
            AssertOnly(Vault(2, Obj(2, "wax_moths", 5, 5)), VaultIssueCode.LevelTooLow, 2);
        }

        [Test]
        public void TooManyBraziers()
        {
            AssertOnly(Vault(1, Obj(2, "brazier", 0, 5), Obj(3, "brazier", 0, 6)), VaultIssueCode.TooManyBraziers);
            CollectionAssert.IsEmpty(VaultValidator.Validate(Vault(3, Obj(2, "brazier", 0, 5), Obj(3, "brazier", 0, 6))));
        }

        [Test]
        public void NoTreasure()
        {
            Assert.That(Codes(VaultJson.Parse(Json(Rooms, 1))), Has.Member(VaultIssueCode.NoTreasure));
        }

        [Test]
        public void TooManyTreasurePiles()
        {
            AssertOnly(Vault(1, Pile(2, 1, 1, 1), Pile(3, 2, 1, 1), Pile(4, 3, 1, 1)), VaultIssueCode.TooManyTreasurePiles);
        }

        [Test]
        public void TreasureValueIsSetByLevel()
        {
            VaultDef wrong = VaultJson.Parse(Json(Rooms, 1, Pile(1, 4, 1, 1).Replace("100", "150")));
            AssertOnly(wrong, VaultIssueCode.WrongTreasureValue, 1);
            CollectionAssert.IsEmpty(VaultValidator.Validate(VaultJson.Parse(Json(Rooms, 3, Pile(1, 4, 1, 3)))));
        }

        [Test]
        public void OverBudget()
        {
            // Eight drips are 80 points: exactly the Level 1 budget. A ninth is over.
            var drips = new List<string>();
            for (int i = 0; i < 9; i++)
            {
                drips.Add(Drip(10 + i, 1 + i % 7, 5 + i / 7));
            }

            AssertOnly(Vault(1, drips.ToArray()), VaultIssueCode.OverBudget);
            drips.RemoveAt(8);
            CollectionAssert.IsEmpty(VaultValidator.Validate(Vault(1, drips.ToArray())));
        }

        [Test]
        public void HoardIsRequired_AndNeedsTreasure()
        {
            AssertOnly(VaultJson.Parse(TestVaults.Json(Rooms, "[" + Pile(1, 4, 1, 1) + "]")), VaultIssueCode.NoHoard);
            AssertOnly(VaultJson.Parse(Json(Rooms, 1, Pile(1, 4, 6, 1))), VaultIssueCode.HoardWithoutTreasure);
        }

        [Test]
        public void TreasureAndHoardMustBeReachable()
        {
            string[] sealedTop = (string[])Rooms.Clone();
            sealedTop[3] = "############";
            List<VaultIssueCode> codes = Codes(VaultJson.Parse(Json(sealedTop, 1, Pile(1, 4, 1, 1))));
            Assert.That(codes, Has.Member(VaultIssueCode.TreasureUnreachable));
            Assert.That(codes, Has.Member(VaultIssueCode.HoardUnreachable));
        }

        [Test]
        public void ACrateCanSealOffTheHoard()
        {
            Assert.That(Codes(Vault(1, Obj(2, "crate", 3, 3))), Has.Member(VaultIssueCode.HoardUnreachable));
        }

        [Test]
        public void OwlPatrolMustBeWalkableAndReachable()
        {
            AssertOnly(Vault(2, Obj(2, "night_owl", 5, 5, ",\"patrol\":[[5,5],[0,5]]")), VaultIssueCode.PatrolBlocked, 2);

            // (1,11) is boxed in by crates on (2,11) and (1,10): the owl in the middle room cannot get there.
            AssertOnly(Vault(2, Obj(2, "night_owl", 5, 5, ",\"patrol\":[[5,5],[1,11]]"), Obj(3, "crate", 2, 11), Obj(4, "crate", 1, 10)),
                VaultIssueCode.PatrolUnreachable, 2);
        }

        [Test]
        public void BuildPoints_SumObjectCosts()
        {
            VaultDef v = Vault(3, Drip(2, 1, 5), Obj(3, "night_owl", 2, 5), Obj(4, "wax_moths", 3, 5),
                Obj(5, "crate", 5, 6), Obj(6, "brazier", 0, 5));
            Assert.AreEqual(10 + 30 + 25 + 4, VaultRules.BuildPoints(v));
            CollectionAssert.IsEmpty(VaultValidator.Validate(v));
        }

        [Test]
        public void Rules_Table()
        {
            Assert.AreEqual(80, VaultRules.Budget(1));
            Assert.AreEqual(110, VaultRules.Budget(2));
            Assert.AreEqual(150, VaultRules.Budget(3));
            Assert.AreEqual(520, VaultRules.Budget(10));
            Assert.AreEqual(1, VaultRules.BrazierLimit(2));
            Assert.AreEqual(4, VaultRules.BrazierLimit(10));
            Assert.AreEqual(3, VaultRules.MaxTreasurePiles(1));
            Assert.AreEqual(8, VaultRules.MaxTreasurePiles(10));
            Assert.AreEqual(2, VaultRules.RequiredLevel(ObjectType.NightOwl));
            Assert.AreEqual(3, VaultRules.RequiredLevel(ObjectType.WaxMoths));
            Assert.AreEqual(0, VaultRules.Cost(ObjectType.Treasure));
            Assert.AreEqual(0, VaultRules.Cost(ObjectType.Brazier));
            Assert.AreEqual(300, VaultRules.TreasureValue(3));
        }

        [Test]
        public void AllIssuesAreReported_NotJustTheFirst()
        {
            List<VaultIssueCode> codes = Codes(Vault(1, Obj(2, "night_owl", 0, 5), Drip(3, 0, 6)));
            Assert.That(codes, Has.Member(VaultIssueCode.LevelTooLow));
            Assert.AreEqual(2, codes.FindAll(c => c == VaultIssueCode.ObjectOnWall).Count);
        }

        // ---- Helpers ----------------------------------------------------------------------

        /// <summary>The base vault at a level (its one pile worth the right amount) plus extra objects.</summary>
        private static VaultDef Vault(int level, params string[] extra)
        {
            var objects = new List<string> { Pile(1, 4, 1, level) };
            objects.AddRange(extra);
            return VaultJson.Parse(Json(Rooms, level, objects.ToArray()));
        }

        private static string Json(string[] map, int level, params string[] objects)
        {
            string json = TestVaults.Json(map, "[" + string.Join(",", objects) + "]")
                .Replace("\"vaultLevel\":1", "\"vaultLevel\":" + level);
            return json.Substring(0, json.Length - 1) + ",\"hoard\":[1,1,7,2]}";
        }

        private static string Pile(int id, int x, int y, int level)
        {
            return Obj(id, "treasure", x, y, ",\"value\":" + VaultRules.TreasureValue(level < 1 ? 1 : level > 10 ? 10 : level));
        }

        private static string Drip(int id, int x, int y)
        {
            return Obj(id, "drip_pipe", x, y, ",\"cycle\":30");
        }

        private static string Obj(int id, string type, int x, int y, string extra = "")
        {
            return "{\"id\":" + id + ",\"type\":\"" + type + "\",\"x\":" + x + ",\"y\":" + y + extra + "}";
        }

        private static List<VaultIssueCode> Codes(VaultDef v)
        {
            var codes = new List<VaultIssueCode>();
            foreach (VaultIssue i in VaultValidator.Validate(v))
            {
                codes.Add(i.Code);
            }

            return codes;
        }

        private static void AssertOnly(VaultDef v, VaultIssueCode code, int objectId = 0)
        {
            List<VaultIssue> issues = VaultValidator.Validate(v);
            Assert.AreEqual(1, issues.Count, "issues: " + string.Join("; ", issues));
            Assert.AreEqual(code, issues[0].Code);
            if (objectId != 0)
            {
                Assert.AreEqual(objectId, issues[0].ObjectId);
            }
        }
    }
}
