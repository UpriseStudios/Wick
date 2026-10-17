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
            Assert.AreEqual("drip_pipe", pipe.Type);
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
            string objects = "[{\"id\":1,\"type\":\"a\",\"x\":0,\"y\":0},{\"id\":1,\"type\":\"b\",\"x\":1,\"y\":0}]";
            Assert.Throws<FormatException>(() => VaultJson.Parse(TestVaults.Json(new[] { "E." }, objects)));
        }

        [Test]
        public void RejectsUnsupportedSchema()
        {
            string json = TestVaults.Json(new[] { "E." }).Replace("\"schema\":1", "\"schema\":2");
            Assert.Throws<FormatException>(() => VaultJson.Parse(json));
        }
    }
}
