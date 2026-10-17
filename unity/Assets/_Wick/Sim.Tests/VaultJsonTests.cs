using System;
using NUnit.Framework;

namespace Wick.Sim.Tests
{
    public class VaultJsonTests
    {
        // The tech-design example vault (9 x 13), with real tile data: walls on the border.
        private static string TechDesignExample()
        {
            var rows = new string[13];
            for (int y = 0; y < 13; y++)
            {
                rows[y] = y == 0 || y == 12 ? "#########" : "#.......#";
            }

            rows[12] = "####E####";
            string tiles = Convert.ToBase64String(TestVaults.MapToTiles(rows));
            return "{\n" +
                "  \"schema\": 1,\n" +
                "  \"vaultId\": \"v_8f3k2q\",\n" +
                "  \"ownerId\": \"p_19x\",\n" +
                "  \"version\": 7,\n" +
                "  \"vaultLevel\": 3,\n" +
                "  \"theme\": \"crypt\",\n" +
                "  \"grid\": { \"w\": 9, \"h\": 13 },\n" +
                "  \"tiles\": \"" + tiles + "\",\n" +
                "  \"entrance\": [4, 12],\n" +
                "  \"objects\": [\n" +
                "    { \"id\": 3, \"type\": \"treasure\", \"x\": 4, \"y\": 1, \"value\": 400 },\n" +
                "    { \"id\": 1, \"type\": \"drip_pipe\", \"x\": 3, \"y\": 8, \"rot\": 0, \"cycle\": 45, \"offset\": 10 },\n" +
                "    { \"id\": 2, \"type\": \"night_owl\", \"x\": 6, \"y\": 4, \"patrol\": [[6,4],[6,9],[2,9]] }\n" +
                "  ],\n" +
                "  \"buildPoints\": 118,\n" +
                "  \"clearCheck\": { \"seed\": 99120, \"log\": \"\", \"hash\": \"a91c\", \"stars\": 2 }\n" +
                "}";
        }

        [Test]
        public void ParsesTechDesignExample()
        {
            VaultDef v = VaultJson.Parse(TechDesignExample());
            Assert.AreEqual("v_8f3k2q", v.VaultId);
            Assert.AreEqual(7, v.Version);
            Assert.AreEqual(3, v.VaultLevel);
            Assert.AreEqual(9, v.Width);
            Assert.AreEqual(13, v.Height);
            Assert.AreEqual(new TilePos(4, 12), v.Entrance);
            Assert.AreEqual(TileType.Wall, v.TileAt(new TilePos(0, 0)));
            Assert.AreEqual(TileType.Floor, v.TileAt(new TilePos(4, 6)));
            Assert.AreEqual(TileType.Wall, v.TileAt(new TilePos(-1, 6)), "out of bounds reads as wall");
        }

        [Test]
        public void SortsObjectsByIdAndReadsFields()
        {
            VaultDef v = VaultJson.Parse(TechDesignExample());
            Assert.AreEqual(3, v.ObjectCount);
            Assert.AreEqual(1, v.GetObject(0).Id);
            Assert.AreEqual(2, v.GetObject(1).Id);
            Assert.AreEqual(3, v.GetObject(2).Id);

            ObjectDef pipe = v.GetObject(0);
            Assert.AreEqual(ObjectType.DripPipe, pipe.Type);
            Assert.AreEqual(45, pipe.Cycle);
            Assert.AreEqual(10, pipe.Offset);

            ObjectDef owl = v.GetObject(1);
            Assert.AreEqual(3, owl.Patrol.Length);
            Assert.AreEqual(new TilePos(2, 9), owl.Patrol[2]);

            Assert.AreEqual(400, v.GetObject(2).Value);
        }

        [Test]
        public void WallsBlockAndOtherTilesAreWalkable()
        {
            VaultDef v = TestVaults.Parse("#DO~", "E...");
            Assert.IsFalse(v.IsWalkable(new TilePos(0, 0)));
            Assert.IsTrue(v.IsWalkable(new TilePos(1, 0)));
            Assert.IsTrue(v.IsWalkable(new TilePos(2, 0)));
            Assert.IsTrue(v.IsWalkable(new TilePos(3, 0)));
            Assert.IsFalse(v.IsWalkable(new TilePos(4, 0)), "off-grid blocks");
        }

        [Test]
        public void RejectsWrongTileCount()
        {
            string json = TestVaults.Json(new[] { "E." }).Replace("\"w\":2", "\"w\":3");
            Assert.Throws<FormatException>(() => VaultJson.Parse(json));
        }

        [Test]
        public void RejectsUnknownTileByte()
        {
            string tiles = Convert.ToBase64String(new byte[] { 0, 9 });
            string json = "{\"schema\":1,\"grid\":{\"w\":2,\"h\":1},\"tiles\":\"" + tiles + "\",\"entrance\":[0,0]}";
            Assert.Throws<FormatException>(() => VaultJson.Parse(json));
        }

        [Test]
        public void RejectsEntranceOnWallOrOutOfBounds()
        {
            string onWall = TestVaults.Json(new[] { "E#" }).Replace("\"entrance\":[0,0]", "\"entrance\":[1,0]");
            string outside = TestVaults.Json(new[] { "E#" }).Replace("\"entrance\":[0,0]", "\"entrance\":[5,0]");
            Assert.Throws<FormatException>(() => VaultJson.Parse(onWall));
            Assert.Throws<FormatException>(() => VaultJson.Parse(outside));
        }

        [Test]
        public void RejectsDuplicateObjectIds()
        {
            string objects = "[{\"id\":1,\"type\":\"wax_moths\",\"x\":0,\"y\":0},{\"id\":1,\"type\":\"wax_moths\",\"x\":1,\"y\":0}]";
            var e = Assert.Throws<FormatException>(() => VaultJson.Parse(TestVaults.Json(new[] { "E." }, objects)));
            StringAssert.Contains("Duplicate object id", e.Message);
        }

        // Row 0 is wall; floor at y = 1..2. Entrance (1,2).
        private static readonly string[] Small =
        {
            "#####",
            "#...#",
            "#E..#",
            "#####",
        };

        private static VaultDef WithObjects(string objectsJson, string extra = "")
        {
            string json = TestVaults.Json(Small, objectsJson);
            if (extra.Length > 0)
            {
                json = json.Substring(0, json.Length - 1) + "," + extra + "}";
            }

            return VaultJson.Parse(json);
        }

        [Test]
        public void ParsesEveryObjectType()
        {
            VaultDef v = WithObjects("[" +
                "{\"id\":1,\"type\":\"drip_pipe\",\"x\":1,\"y\":1,\"cycle\":30},\n" +
                "{\"id\":2,\"type\":\"bellows\",\"x\":2,\"y\":1,\"rot\":1,\"cycle\":60,\"offset\":5},\n" +
                "{\"id\":3,\"type\":\"spike_floor\",\"x\":3,\"y\":1,\"cycle\":60},\n" +
                "{\"id\":4,\"type\":\"snuffer_bell\",\"x\":3,\"y\":2},\n" +
                "{\"id\":5,\"type\":\"night_owl\",\"x\":2,\"y\":2,\"patrol\":[[2,2],[3,2]]},\n" +
                "{\"id\":6,\"type\":\"wax_moths\",\"x\":2,\"y\":2},\n" +
                "{\"id\":7,\"type\":\"treasure\",\"x\":1,\"y\":1,\"value\":100},\n" +
                "{\"id\":8,\"type\":\"brazier\",\"x\":2,\"y\":0}]");
            Assert.AreEqual(8, v.ObjectCount);
            Assert.AreEqual(ObjectType.Bellows, v.GetObject(1).Type);
            Assert.AreEqual(Dir.E, v.GetObject(1).Facing);
            Assert.AreEqual(ObjectType.Brazier, v.GetObject(7).Type);
        }

        [Test]
        public void Crate_BlocksWalkingButNotLight()
        {
            VaultDef v = WithObjects("[{\"id\":1,\"type\":\"crate\",\"x\":2,\"y\":1}]");
            Assert.IsFalse(v.IsWalkable(new TilePos(2, 1)));
            Assert.IsTrue(v.BlocksGust(new TilePos(2, 1)));
            Assert.AreEqual(TileType.Floor, v.TileAt(new TilePos(2, 1)), "the tile itself is still floor");
        }

        [Test]
        public void ParsesOptionalHoard()
        {
            Assert.IsFalse(WithObjects("[]").HasHoard);
            VaultDef v = WithObjects("[]", "\"hoard\":[2,1,3,2]");
            Assert.IsTrue(v.HasHoard);
            Assert.IsTrue(v.Hoard.Contains(new TilePos(3, 2)));
            Assert.IsFalse(v.Hoard.Contains(new TilePos(1, 2)));
        }

        [TestCase("[{\"id\":1,\"type\":\"dragon\",\"x\":1,\"y\":1}]", TestName = "UnknownType")]
        [TestCase("[{\"id\":1,\"type\":\"treasure\",\"x\":0,\"y\":0,\"value\":5}]", TestName = "TreasureOnWall")]
        [TestCase("[{\"id\":1,\"type\":\"treasure\",\"x\":1,\"y\":1,\"value\":0}]", TestName = "TreasureNoValue")]
        [TestCase("[{\"id\":1,\"type\":\"brazier\",\"x\":2,\"y\":1}]", TestName = "BrazierOnFloor")]
        [TestCase("[{\"id\":1,\"type\":\"brazier\",\"x\":0,\"y\":0}]", TestName = "BrazierUnreachable")]
        [TestCase("[{\"id\":1,\"type\":\"drip_pipe\",\"x\":1,\"y\":1,\"cycle\":6}]", TestName = "DripCycleTooShort")]
        [TestCase("[{\"id\":1,\"type\":\"spike_floor\",\"x\":1,\"y\":1,\"cycle\":35}]", TestName = "SpikeCycleTooShort")]
        [TestCase("[{\"id\":1,\"type\":\"bellows\",\"x\":1,\"y\":1,\"cycle\":30,\"offset\":30}]", TestName = "OffsetNotBelowCycle")]
        [TestCase("[{\"id\":1,\"type\":\"bellows\",\"x\":1,\"y\":1,\"cycle\":30,\"rot\":4}]", TestName = "BadRot")]
        [TestCase("[{\"id\":1,\"type\":\"night_owl\",\"x\":2,\"y\":1,\"patrol\":[[2,1],[2,0]]}]", TestName = "PatrolIntoWall")]
        [TestCase("[{\"id\":1,\"type\":\"crate\",\"x\":1,\"y\":2}]", TestName = "CrateOnEntrance")]
        [TestCase("[{\"id\":1,\"type\":\"snuffer_bell\",\"x\":1,\"y\":2}]", TestName = "BellOnEntrance")]
        [TestCase("[{\"id\":1,\"type\":\"crate\",\"x\":2,\"y\":1},{\"id\":2,\"type\":\"treasure\",\"x\":2,\"y\":1,\"value\":5}]", TestName = "TreasureUnderCrate")]
        [TestCase("[{\"id\":1,\"type\":\"crate\",\"x\":2,\"y\":1},{\"id\":2,\"type\":\"night_owl\",\"x\":3,\"y\":1,\"patrol\":[[2,1]]}]", TestName = "PatrolIntoCrate")]
        public void RejectsInvalidObjects(string objects)
        {
            Assert.Throws<FormatException>(() => WithObjects(objects));
        }

        [TestCase("\"hoard\":[2,1,5,2]", TestName = "HoardOutOfBounds")]
        [TestCase("\"hoard\":[3,1,2,2]", TestName = "HoardInverted")]
        [TestCase("\"hoard\":[1,1,2]", TestName = "HoardWrongShape")]
        public void RejectsBadHoard(string hoard)
        {
            Assert.Throws<FormatException>(() => WithObjects("[]", hoard));
        }

        [Test]
        public void RejectsUnsupportedSchema()
        {
            string json = TestVaults.Json(new[] { "E." }).Replace("\"schema\":1", "\"schema\":2");
            Assert.Throws<FormatException>(() => VaultJson.Parse(json));
        }
    }
}
