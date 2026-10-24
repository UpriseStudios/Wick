using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using Wick.Economy;
using Wick.Sim;

namespace Wick.CloudCode.Tests
{
    /// <summary>M10 decorations on the server: trophies must be earned, and props survive publishing.</summary>
    public class DecorationPublishTests
    {
        private World _w;

        [SetUp]
        public void SetUp()
        {
            _w = new World();
        }

        /// <summary>Three Halls with decorations (its hoard is rows 1-3, x 1-10).</summary>
        private static VaultDef Decorated(params DecorationDef[] decor)
        {
            VaultDef halls = TestVaults.Halls();
            var tiles = new TileType[halls.Width * halls.Height];
            for (int i = 0; i < tiles.Length; i++)
            {
                tiles[i] = halls.TileAtIndex(i);
            }

            var objects = new ObjectDef[halls.ObjectCount];
            for (int i = 0; i < objects.Length; i++)
            {
                objects[i] = halls.GetObject(i);
            }

            return new VaultDef(halls.Schema, halls.VaultId, halls.Version, halls.VaultLevel, halls.Width, halls.Height, tiles,
                halls.Entrance, objects, halls.HasHoard, halls.Hoard, halls.Theme, null, decor);
        }

        private Task<PublishResult> Publish(VaultDef vault)
        {
            return new PublishService(_w.Store, _w.Clock).PublishAsync("p1", _w.NextRequest(), TestVaults.PublishDocument(vault));
        }

        [Test]
        public async Task FreeDecorations_Publish_AndStayOnTheLiveVault()
        {
            PublishResult r = await Publish(Decorated(new DecorationDef(50, Decorations.Statue, new TilePos(2, 6), 0)));
            Assert.IsTrue(r.Ok, r.Reason);
            VaultDef live = VaultJson.Parse(_w.Get<LiveVault>("p1", Keys.VaultLive).Document);
            Assert.AreEqual(1, live.DecorationCount);
            Assert.AreEqual(Decorations.Statue, live.GetDecoration(0).Kind);
        }

        [Test]
        public async Task ATrophy_NeedsTheAchievementThatAwardsIt()
        {
            VaultDef withTrophy = Decorated(new DecorationDef(50, "prop_trophy_raider_bronze", new TilePos(2, 2), 0));
            PublishResult refused = await Publish(withTrophy);
            Assert.IsFalse(refused.Ok);
            StringAssert.Contains("haven't earned", refused.Reason);

            await _w.Set("p1", EconomyKeys.Inventory, new Inventory { Cosmetics = new List<string> { "trophy_raider_bronze" } });
            PublishResult ok = await Publish(withTrophy);
            Assert.IsTrue(ok.Ok, ok.Reason);
        }

        [Test]
        public void EveryAchievementTier_AwardsATrophy()
        {
            foreach (Achievement a in TestEconomy.Default.Achievements)
            {
                string[] tiers = { "bronze", "silver", "gold" };
                for (int i = 0; i < a.Grants.Count; i++)
                {
                    CollectionAssert.Contains(a.Grants[i].Cosmetics, "trophy_" + a.Id + "_" + tiers[i]);
                }
            }
        }
    }
}
