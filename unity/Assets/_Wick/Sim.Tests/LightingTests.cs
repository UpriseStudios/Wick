using NUnit.Framework;

namespace Wick.Sim.Tests
{
    public class LightingTests
    {
        // 11 x 11, open floor x/y = 1..9, entrance in the middle at (5,5).
        private static readonly string[] OpenRoom =
        {
            "###########",
            "#.........#",
            "#.........#",
            "#.........#",
            "#.........#",
            "#....E....#",
            "#.........#",
            "#.........#",
            "#.........#",
            "#.........#",
            "###########",
        };

        [Test]
        public void Radius_ByFlickers_HalvedByTiptoe()
        {
            Assert.AreEqual(4000, Lighting.RadiusUnits(3, false));
            Assert.AreEqual(3000, Lighting.RadiusUnits(2, false));
            Assert.AreEqual(2000, Lighting.RadiusUnits(1, false));
            Assert.AreEqual(0, Lighting.RadiusUnits(0, false));
            Assert.AreEqual(2000, Lighting.RadiusUnits(3, true));
            Assert.AreEqual(1500, Lighting.RadiusUnits(2, true));
            Assert.AreEqual(1000, Lighting.RadiusUnits(1, true));
        }

        [Test]
        public void LitAtTickZero()
        {
            var sim = new RaidSim(TestVaults.Parse(OpenRoom), 1, InputLog.Empty);
            Assert.IsTrue(sim.IsLit(new TilePos(5, 5)));
            Assert.IsTrue(sim.IsLit(new TilePos(5, 1)));
        }

        [Test]
        public void FullFlame_IsCircleOfRadiusFour()
        {
            LitMap lit = Light(OpenRoom, AtRest(5, 5, 3, false));
            AssertLit(lit, 5, 1, true);   // distance 4
            AssertLit(lit, 9, 5, true);   // distance 4
            AssertLit(lit, 8, 7, true);   // 3^2 + 2^2 = 13 <= 16
            AssertLit(lit, 8, 8, false);  // 3^2 + 3^2 = 18 > 16
            AssertLit(lit, 5, 0, false);  // wall at distance 5
        }

        [TestCase(2, false, 3)]
        [TestCase(1, false, 2)]
        [TestCase(3, true, 2)]
        [TestCase(1, true, 1)]
        public void Radius_ShrinksWithFlameAndTiptoe(int flickers, bool tiptoe, int reach)
        {
            LitMap lit = Light(OpenRoom, AtRest(5, 5, flickers, tiptoe));
            AssertLit(lit, 5, 5 - reach, true);
            AssertLit(lit, 5, 5 - reach - 1, false);
        }

        [Test]
        public void NoFlame_LightsNothing()
        {
            LitMap lit = Light(OpenRoom, AtRest(5, 5, 0, false));
            CollectionAssert.DoesNotContain(lit.Tiles, true);
        }

        [Test]
        public void Walls_BlockLight_ButAreLitThemselves()
        {
            string[] rooms =
            {
                "#########",
                "#...#...#",
                "#.E.#...#",
                "#...#...#",
                "#########",
            };
            LitMap lit = Light(rooms, AtRest(2, 2, 3, false));
            AssertLit(lit, 3, 2, true);
            AssertLit(lit, 4, 2, true, "the wall face is lit");
            AssertLit(lit, 5, 2, false, "behind the wall");
            AssertLit(lit, 5, 1, false, "behind the wall");
        }

        [Test]
        public void Light_DoesNotSqueezeThroughDiagonalWallCorner()
        {
            string[] map =
            {
                "######",
                "#.E#.#",
                "#.#..#",
                "#....#",
                "######",
            };
            // (3,1) and (2,2) are walls touching at a corner between (2,1) and (3,2).
            LitMap lit = Light(map, AtRest(2, 1, 3, false));
            AssertLit(lit, 3, 2, false);
            AssertLit(lit, 4, 1, false);
            AssertLit(lit, 1, 2, true);
        }

        [Test]
        public void Light_FollowsExactPositionWhileMoving()
        {
            var sim = new RaidSim(TestVaults.Parse(OpenRoom), 1, new InputLog(new[] { Command.Dash(0, Dir.N) }));
            Assert.IsTrue(sim.IsLit(new TilePos(5, 9)), "4 tiles behind, at rest");

            sim.Step();
            sim.Step();
            sim.Step(); // progress 600: Wick is at y = 4.4
            Assert.IsFalse(sim.IsLit(new TilePos(5, 9)), "now 4.6 tiles behind");
            Assert.IsTrue(sim.IsLit(new TilePos(5, 1)));
        }

        [Test]
        public void IsLit_OffGridIsFalse()
        {
            var sim = new RaidSim(TestVaults.Parse(OpenRoom), 1, InputLog.Empty);
            Assert.IsFalse(sim.IsLit(new TilePos(-1, 5)));
            Assert.IsFalse(sim.IsLit(new TilePos(5, 11)));
        }

        private static WickState AtRest(int x, int y, int flickers, bool tiptoe)
        {
            return new WickState { Tile = new TilePos(x, y), Flickers = flickers, Tiptoe = tiptoe };
        }

        private sealed class LitMap
        {
            public bool[] Tiles;
            public int Width;
        }

        private static LitMap Light(string[] map, WickState wick)
        {
            VaultDef vault = TestVaults.Parse(map);
            var lit = new LitMap { Tiles = new bool[vault.Width * vault.Height], Width = vault.Width };
            Lighting.Compute(vault, in wick, lit.Tiles);
            return lit;
        }

        private static void AssertLit(LitMap lit, int x, int y, bool expected, string why = "")
        {
            Assert.AreEqual(expected, lit.Tiles[y * lit.Width + x], "(" + x + "," + y + ") " + why);
        }
    }
}
