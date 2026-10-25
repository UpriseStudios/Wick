using System.Collections.Generic;
using NUnit.Framework;

namespace Wick.Sim.Tests
{
    /// <summary>One test per rule: a valid base vault, then exactly one thing broken.</summary>
    public class VaultValidatorTests
    {
        // 12 x 16 (the Level 1 grid): three rooms joined by gaps at (3,3) and (3,9); hoard = rows
        // 1-2. The entrance (10,1) opens on a corridor down the east side and back along row 14 to
        // the bottom room, so the walk to the hoard is 36 tiles (VaultRules.MinRouteTiles for 12 x 16).
        private static readonly string[] Rooms =
        {
            "############",
            "#.......##E#",
            "#.......##.#",
            "###.######.#",
            "#.......##.#",
            "#.......##.#",
            "#.......##.#",
            "#.......##.#",
            "#.......##.#",
            "###.######.#",
            "#.......##.#",
            "#.......##.#",
            "#.########.#",
            "#.########.#",
            "#..........#",
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

        [Test]
        public void TheWayInMustWind()
        {
            Assert.AreEqual(36, VaultValidator.RouteTiles(Vault(1)));

            // A gap beside the entrance cuts the walk to 3 tiles.
            string[] shortcut = (string[])Rooms.Clone();
            shortcut[1] = "#.........E#";
            VaultDef cut = VaultJson.Parse(Json(shortcut, 1, Pile(1, 4, 1, 1)));
            Assert.AreEqual(3, VaultValidator.RouteTiles(cut));
            AssertOnly(cut, VaultIssueCode.RouteTooShort);

            // Bigger grids need longer walks; a vault nobody has expanded keeps the small grid's minimum.
            Assert.That(Codes(Vault(5)), Has.No.Member(VaultIssueCode.RouteTooShort));
            Assert.AreEqual(36, VaultRules.MinRouteTiles(12, 16));
            Assert.AreEqual(56, VaultRules.MinRouteTiles(20, 28));
        }

        [Test]
        public void RouteTiles_CountsCratesAndGatesAsOpen()
        {
            // Crates and gates don't lengthen the route; only walls do.
            Assert.AreEqual(36, VaultValidator.RouteTiles(Vault(1, Obj(2, "crate", 10, 8))));
            Assert.AreEqual(-1, VaultValidator.RouteTiles(VaultJson.Parse(TestVaults.Json(Rooms, "[" + Pile(1, 4, 1, 1) + "]"))),
                "no hoard, no route");
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
            VaultDef v = VaultJson.Parse(Json(Rooms, 1, Pile(1, 4, 1, 1)).Replace("\"entrance\":[10,1]", "\"entrance\":[9,1]"));
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
            doors[9] = "###D######.#";
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
            Assert.That(Codes(Vault(1, Obj(2, "crate", 10, 1))), Has.Member(VaultIssueCode.ObjectOnEntrance));
            Assert.That(Codes(Vault(2, Obj(2, "snuffer_bell", 10, 1))), Has.Member(VaultIssueCode.ObjectOnEntrance));
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
            CollectionAssert.IsEmpty(VaultValidator.Validate(Vault(3)), "worth 300 at Level 3");
        }

        [Test]
        public void OverBudget()
        {
            // Twelve drips are 120 points: exactly the Level 1 budget. A thirteenth is over.
            var drips = new List<string>();
            for (int i = 0; i < 13; i++)
            {
                drips.Add(Drip(10 + i, 1 + i % 7, 4 + i / 7));
            }

            AssertOnly(Vault(1, drips.ToArray()), VaultIssueCode.OverBudget);
            drips.RemoveAt(12);
            CollectionAssert.IsEmpty(VaultValidator.Validate(Vault(1, drips.ToArray())));
        }

        [Test]
        public void FromLevel2_AVaultMustUseMostOfItsBudget()
        {
            AssertOnly(VaultJson.Parse(Json(Rooms, 2, Pile(1, 4, 1, 2))), VaultIssueCode.UnderBudget);
            CollectionAssert.IsEmpty(VaultValidator.Validate(Vault(2)), "padded to the minimum");
            CollectionAssert.IsEmpty(VaultValidator.Validate(VaultJson.Parse(Json(Rooms, 1, Pile(1, 4, 1, 1)))), "Level 1 may be empty");
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
            sealedTop[3] = "##########.#";
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
            AssertOnly(Vault(2, Obj(2, "night_owl", 5, 5, ",\"patrol\":[[5,5],[7,11]]"), Obj(3, "crate", 6, 11), Obj(4, "crate", 7, 10)),
                VaultIssueCode.PatrolUnreachable, 2);
        }

        [Test]
        public void BuildPoints_SumObjectCosts()
        {
            VaultDef v = Vault(3, Drip(2, 1, 5), Obj(3, "night_owl", 2, 5), Obj(4, "wax_moths", 3, 5),
                Obj(5, "crate", 5, 6), Obj(6, "brazier", 0, 5));
            Assert.AreEqual(10 + 30 + 25 + 4 + PaddingPoints(3), VaultRules.BuildPoints(v));
            CollectionAssert.IsEmpty(VaultValidator.Validate(v));
        }

        [Test]
        public void Rules_Table()
        {
            Assert.AreEqual(120, VaultRules.Budget(1));
            Assert.AreEqual(160, VaultRules.Budget(2));
            Assert.AreEqual(210, VaultRules.Budget(3));
            Assert.AreEqual(540, VaultRules.Budget(10));
            Assert.AreEqual(0, VaultRules.MinBuildPoints(1), "Level 1 is free-form");
            Assert.AreEqual(96, VaultRules.MinBuildPoints(2));
            Assert.AreEqual(324, VaultRules.MinBuildPoints(10));
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

        // ---- Pits (docs/m10-1-pits.md) ------------------------------------------------------

        [Test]
        public void SeveralPitsOffTheRoute_AreValid()
        {
            VaultDef v = VaultJson.Parse(Json(Pits(6, 5, 6, 6, 7, 7, 7, 11), 1, Pile(1, 4, 1, 1)));
            CollectionAssert.IsEmpty(VaultValidator.Validate(v));
            Assert.AreEqual(36, VaultValidator.RouteTiles(v), "none of them is on the walk");
            Assert.AreEqual(5 * 4, VaultRules.BuildPoints(v), "5 build points a pit");
        }

        [Test]
        public void PitsMakeMazeWalls_LengtheningTheRoute()
        {
            // A pit row across the middle room, open only at x 7: the walk detours 4 tiles each way.
            VaultDef v = VaultJson.Parse(Json(Pits(1, 6, 2, 6, 3, 6, 4, 6, 5, 6, 6, 6), 1, Pile(1, 4, 1, 1)));
            Assert.AreEqual(44, VaultValidator.RouteTiles(v));
            List<TilePos> path = VaultValidator.RoutePath(v);
            Assert.AreEqual(new TilePos(10, 1), path[0], "from the entrance");
            Assert.IsTrue(path.Contains(new TilePos(7, 6)), "through the gap in the pits");
            CollectionAssert.IsEmpty(VaultValidator.Validate(v));
        }

        [Test]
        public void APitLineAcrossTheOnlyCorridor_BlocksTheRoute()
        {
            // The east corridor is one tile wide: a pit at (10,5) cuts off the pile and the hoard.
            List<VaultIssue> issues = VaultValidator.Validate(VaultJson.Parse(Json(Pits(10, 5), 1, Pile(1, 4, 1, 1))));
            Assert.AreEqual(2, issues.Count, "issues: " + string.Join("; ", issues));
            Assert.IsTrue(issues.TrueForAll(i => i.Code == VaultIssueCode.PitBlocksRoute));
            VaultIssue pile = issues.Find(i => i.ObjectId == 1);
            Assert.AreEqual(new TilePos(10, 5), pile.Tile, "names the pit in the way");
            Assert.AreEqual(new TilePos(10, 5), issues.Find(i => i.ObjectId == 0).Tile, "and for the hoard room");
            Assert.AreEqual(-1, VaultValidator.RouteTiles(VaultJson.Parse(Json(Pits(10, 5), 1, Pile(1, 4, 1, 1)))));
        }

        [Test]
        public void APileRingedWithPits_IsCutOff_ButTheHoardIsNot()
        {
            // Pile (4,1) against the north wall, pits west, east and south of it.
            List<VaultIssue> issues = VaultValidator.Validate(VaultJson.Parse(Json(Pits(3, 1, 5, 1, 4, 2), 1, Pile(1, 4, 1, 1))));
            Assert.AreEqual(1, issues.Count, "issues: " + string.Join("; ", issues));
            Assert.AreEqual(VaultIssueCode.PitBlocksRoute, issues[0].Code);
            Assert.AreEqual(1, issues[0].ObjectId);
            CollectionAssert.Contains(new[] { new TilePos(3, 1), new TilePos(4, 2) }, issues[0].Tile, "a ring pit on the way in");
        }

        [Test]
        public void WallsStillReportUnreachable_NotPits()
        {
            // Sealed by wall with a pit elsewhere: the old codes, not PitBlocksRoute.
            string[] sealedTop = Pits(6, 5);
            sealedTop[3] = "##########.#";
            List<VaultIssueCode> codes = Codes(VaultJson.Parse(Json(sealedTop, 1, Pile(1, 4, 1, 1))));
            Assert.That(codes, Has.Member(VaultIssueCode.TreasureUnreachable));
            Assert.That(codes, Has.Member(VaultIssueCode.HoardUnreachable));
            Assert.That(codes, Has.No.Member(VaultIssueCode.PitBlocksRoute));
        }

        [Test]
        public void PitsStayOutOfDoorways()
        {
            // A door at (3,2) between two rooms; the entrance (1,4).
            string[] map = { "#######", "#.....#", "###D###", "#.....#", "#E....#", "#######" };

            Assert.AreEqual(0, DoorwayPits(map, -1, -1), "no pits");
            Assert.AreEqual(1, DoorwayPits(map, 3, 1), "behind the door");
            Assert.AreEqual(1, DoorwayPits(map, 3, 3), "in front of the door");
            Assert.AreEqual(1, DoorwayPits(map, 1, 3), "in front of the entrance");
            Assert.AreEqual(1, DoorwayPits(map, 2, 4), "beside the entrance");
            Assert.AreEqual(0, DoorwayPits(map, 5, 1), "elsewhere in the room");
            Assert.AreEqual(0, DoorwayPits(map, 4, 4), "a tile clear of the entrance");

            // On the entrance itself (moved onto a pit tile).
            string[] onEntrance = (string[])map.Clone();
            onEntrance[4] = "#E.O..#";
            VaultDef v = VaultJson.Parse(TestVaults.Json(onEntrance).Replace("\"entrance\":[1,4]", "\"entrance\":[3,4]"));
            VaultIssue issue = VaultValidator.Validate(v).Find(i => i.Code == VaultIssueCode.PitInDoorway);
            Assert.AreEqual(new TilePos(3, 4), issue.Tile);

            Assert.IsTrue(VaultValidator.InOrBesideDoorway(TestVaults.Parse(map), new TilePos(3, 2)), "the door tile");
        }

        [Test]
        public void AGapInAWall_IsNotADoorway()
        {
            // (3,4) is just inside the middle room's gap at (3,3). It cuts the only way through, but
            // that's the route rule; the gap itself isn't a doorway.
            List<VaultIssueCode> codes = Codes(VaultJson.Parse(Json(Pits(3, 4), 1, Pile(1, 4, 1, 1))));
            Assert.That(codes, Has.No.Member(VaultIssueCode.PitInDoorway));
            Assert.That(codes, Has.Member(VaultIssueCode.PitBlocksRoute));
            Assert.IsFalse(VaultValidator.InOrBesideDoorway(VaultJson.Parse(Json(Rooms, 1, Pile(1, 4, 1, 1))), new TilePos(3, 4)));
        }

        [Test]
        public void ALeverDoorRoute_CountsOnlyIfItsLeverIsPitFree()
        {
            // Entrance (1,1); a gate in the door (4,1) to the treasure room; a pit row (1-3,3) walls off
            // the alcove below. The lever in front of the pits opens the way; one behind them doesn't.
            string[] map = { "##########", "#E..D....#", "#...#....#", "#OOO#....#", "#...#....#", "##########" };
            string gate = Obj(1, "gate", 4, 1);
            string pile = Pile(3, 7, 1, 1);

            VaultDef open = VaultJson.Parse(TestVaults.Json(map, "[" + gate + "," + Lever(2, 2, 2, 1) + "," + pile + "]"));
            Assert.That(Codes(open), Has.No.Member(VaultIssueCode.PitBlocksRoute).And.No.Member(VaultIssueCode.TreasureUnreachable));
            Assert.IsTrue(VaultValidator.ReachableWithGates(open)[1 * 10 + 7]);

            VaultDef across = VaultJson.Parse(TestVaults.Json(map, "[" + gate + "," + Lever(2, 2, 4, 1) + "," + pile + "]"));
            VaultIssue cut = VaultValidator.Validate(across).Find(i => i.ObjectId == 3);
            Assert.AreEqual(VaultIssueCode.PitBlocksRoute, cut.Code, "the lever is only reachable across a pit");
            Assert.AreEqual(new TilePos(7, 1), cut.Tile, "no pit on the walk itself (that goes through the gate): the pile's tile");
            Assert.IsFalse(VaultValidator.ReachableWithGates(across)[1 * 10 + 7]);
        }

        [Test]
        public void NothingStandsOnAPit()
        {
            AssertOnly(VaultJson.Parse(Json(Pits(6, 5), 1, Pile(1, 4, 1, 1), Obj(2, "spike_floor", 6, 5, ",\"cycle\":60"))),
                VaultIssueCode.ObjectOnPit, 2);
        }

        [Test]
        public void OwlPatrols_MayNotCrossOrStopOnAPit()
        {
            // A patrol point on a pit is blocked.
            AssertOnly(VaultJson.Parse(Json(Pits(2, 5), 2, Padded(2, Pile(1, 4, 1, 2), Obj(2, "night_owl", 5, 5, ",\"patrol\":[[5,5],[2,5]]")))),
                VaultIssueCode.PatrolBlocked, 2);

            // A pit row across the middle room: the owl at (5,7) can't walk to (5,5) though the floor is open.
            string[] split = Pits(1, 6, 2, 6, 3, 6, 4, 6, 5, 6, 6, 6, 7, 6);
            List<VaultIssue> issues = VaultValidator.Validate(VaultJson.Parse(Json(split, 2,
                Padded(2, Pile(1, 4, 1, 2), Obj(2, "night_owl", 5, 7, ",\"patrol\":[[5,7],[5,5]]")))));
            Assert.IsTrue(issues.Exists(i => i.Code == VaultIssueCode.PatrolUnreachable && i.ObjectId == 2));
        }

        /// <summary>The base map with pits on the given (x, y) pairs.</summary>
        private static string[] Pits(params int[] xy)
        {
            string[] map = (string[])Rooms.Clone();
            for (int i = 0; i < xy.Length; i += 2)
            {
                char[] row = map[xy[i + 1]].ToCharArray();
                row[xy[i]] = 'O';
                map[xy[i + 1]] = new string(row);
            }

            return map;
        }

        private static string[] Padded(int level, params string[] objects)
        {
            var all = new List<string>(objects);
            all.AddRange(Padding(level));
            return all.ToArray();
        }

        /// <summary>PitInDoorway issues for map with one pit at (x, y), or none for x &lt; 0.</summary>
        private static int DoorwayPits(string[] map, int x, int y)
        {
            string[] m = (string[])map.Clone();
            if (x >= 0)
            {
                char[] row = m[y].ToCharArray();
                row[x] = 'O';
                m[y] = new string(row);
            }

            return VaultValidator.Validate(TestVaults.Parse(m)).FindAll(i => i.Code == VaultIssueCode.PitInDoorway).Count;
        }

        private static string Lever(int id, int x, int y, int gate)
        {
            return Obj(id, "lever", x, y, ",\"links\":[" + gate + "]");
        }

        // ---- Helpers ----------------------------------------------------------------------

        /// <summary>The base vault at a level (its one pile worth the right amount) plus extra objects.</summary>
        private static VaultDef Vault(int level, params string[] extra)
        {
            var objects = new List<string> { Pile(1, 4, 1, level) };
            objects.AddRange(extra);
            objects.AddRange(Padding(level));
            return VaultJson.Parse(Json(Rooms, level, objects.ToArray()));
        }

        /// <summary>
        /// Spike Floors on tiles no test uses (x 5-7 of rows 7-8 and 10-11), enough to meet the
        /// minimum build points from Vault Level 2, so each test sees only the rule it breaks.
        /// </summary>
        private static List<string> Padding(int level)
        {
            var spikes = new List<string>();
            int needed = VaultRules.MinBuildPoints(level < 1 ? 1 : level > 10 ? 10 : level);
            int[] rows = { 7, 8, 10, 11 };
            for (int i = 0; spikes.Count * VaultRules.Cost(ObjectType.SpikeFloor) < needed && i < 12; i++)
            {
                spikes.Add(Obj(200 + i, "spike_floor", 5 + i % 3, rows[i / 3], ",\"cycle\":60"));
            }

            return spikes;
        }

        private static int PaddingPoints(int level)
        {
            return Padding(level).Count * VaultRules.Cost(ObjectType.SpikeFloor);
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
