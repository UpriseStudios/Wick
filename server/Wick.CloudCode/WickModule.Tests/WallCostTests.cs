using System.Threading.Tasks;
using NUnit.Framework;
using Wick.Economy;
using Wick.Sim;

namespace Wick.CloudCode.Tests
{
    /// <summary>M10: walls cost gold, per wall tile added since the last published version.</summary>
    public class WallCostTests
    {
        private World _w;

        [SetUp]
        public void SetUp()
        {
            _w = new World();
        }

        private static VaultDef WithWall(VaultDef v, params TilePos[] walls)
        {
            var tiles = new TileType[v.Width * v.Height];
            for (int i = 0; i < tiles.Length; i++)
            {
                tiles[i] = v.TileAtIndex(i);
            }

            foreach (TilePos p in walls)
            {
                tiles[p.Y * v.Width + p.X] = TileType.Wall;
            }

            var objects = new ObjectDef[v.ObjectCount];
            for (int i = 0; i < objects.Length; i++)
            {
                objects[i] = v.GetObject(i);
            }

            return new VaultDef(v.Schema, v.VaultId, v.Version, v.VaultLevel, v.Width, v.Height, tiles, v.Entrance, objects,
                v.HasHoard, v.Hoard, v.Theme);
        }

        private Task<PublishResult> Publish(VaultDef v)
        {
            return new PublishService(_w.Store, _w.Clock).PublishAsync("p1", _w.NextRequest(), TestVaults.PublishDocument(v));
        }

        [Test]
        public void OnlyWallsOnOnceOpenTiles_Count()
        {
            VaultDef halls = TestVaults.Halls();
            Assert.AreEqual(0, WallCost.WallsAdded(null, halls), "the first publish is free");
            Assert.AreEqual(0, WallCost.WallsAdded(halls, halls));
            Assert.AreEqual(2, WallCost.WallsAdded(halls, WithWall(halls, new TilePos(8, 6), new TilePos(9, 6))));
            Assert.AreEqual(0, WallCost.WallsAdded(WithWall(halls, new TilePos(8, 6)), halls), "knocking walls down is free");
        }

        [Test]
        public async Task PublishingNewWalls_SpendsGold_AndIsRefusedWithoutIt()
        {
            VaultDef halls = TestVaults.Halls();
            Assert.IsTrue((await Publish(halls)).Ok);
            VaultDef walled = WithWall(halls, new TilePos(8, 6), new TilePos(9, 6));

            PublishResult poor = await Publish(walled);
            Assert.IsFalse(poor.Ok);
            StringAssert.Contains("new walls cost 30 gold", poor.Reason, "2 walls x 15 x Vault Level 1");

            await _w.Set("p1", Keys.Wallet, new Wallet { Gold = 100 });
            PublishResult paid = await Publish(walled);
            Assert.IsTrue(paid.Ok, paid.Reason);
            Assert.AreEqual(2, paid.WallsAdded);
            Assert.AreEqual(30, paid.GoldSpent);
            Assert.AreEqual(70, _w.Get<Wallet>("p1", Keys.Wallet).Gold);
        }
    }
}
