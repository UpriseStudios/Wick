using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using NUnit.Framework;
using Wick.Economy;
using Wick.Sim;

namespace Wick.CloudCode.Tests
{
    /// <summary>
    /// Acceptance: nothing bought or earned from ads changes a value the raid sim reads (game spec,
    /// fairness rules). Grants are limited to currency, cosmetics, slots and access; and a raid
    /// plays out identically for a player who owns everything and one who owns nothing.
    /// </summary>
    public class FairnessTests
    {
        /// <summary>Changing this list means re-checking that the new kind of grant can't reach a raid.</summary>
        private static readonly string[] AllowedGrantFields = { "Gold", "Wax", "Gems", "Workshops", "Cosmetics", "PassPremium", "Club", "IsEmpty" };

        [Test]
        public void Grants_OnlyHoldCurrencyCosmeticsSlotsAndAccess()
        {
            string[] fields = typeof(Grant).GetProperties(BindingFlags.Public | BindingFlags.Instance).Select(p => p.Name).ToArray();
            CollectionAssert.AreEquivalent(AllowedGrantFields, fields);
        }

        private static IEnumerable<Grant> EveryGrant(EconomyConfig c)
        {
            foreach (Product p in c.Products) yield return p.Grant;
            foreach (AdPlacement a in c.Ads.Placements.Where(a => a.Grant != null)) yield return a.Grant;
            foreach (PassReward r in c.Pass.Rewards)
            {
                yield return r.Free;
                yield return r.Premium;
            }

            foreach (Achievement a in c.Achievements)
            {
                foreach (Grant g in a.Grants) yield return g;
            }
        }

        [Test]
        public void NoPaidOrAdReward_IsARaidItem()
        {
            foreach (Grant g in EveryGrant(TestEconomy.Default))
            {
                foreach (string cosmetic in g.Cosmetics ?? new List<string>())
                {
                    Assert.That(cosmetic, Does.Match("^(skin|theme|trapskin|emote|taunt|snuff_effect|flame_trim|trophy|title|clip_frame)_"),
                        "cosmetics only: " + cosmetic);
                }
            }
        }

        [Test]
        public async Task ARaidPlaysTheSame_ForAPlayerWhoOwnsEverything()
        {
            var w = new World();
            await w.Publish("owner");
            var everything = new Grant { Gold = 9_999_999, Wax = 9_999_999, Gems = 9_999_999, Workshops = 2, PassPremium = "s1", Club = true };
            var wallet = new Wallet();
            var inventory = new Inventory();
            var pass = new PassState();
            foreach (Grant g in EveryGrant(TestEconomy.Default).Append(everything))
            {
                Grants.Apply(g, TestEconomy.Default, wallet, inventory, pass);
            }

            inventory.ClubUntilMs = long.MaxValue;
            await w.Set("whale", Keys.Wallet, wallet);
            await w.Set("whale", EconomyKeys.Inventory, inventory);
            await w.Set("whale", EconomyKeys.Pass, pass);
            await w.Set("whale", EconomyKeys.Upgrades, new Upgrades { Levels = new Progression { VaultLevel = 10, Lantern = 5 } });

            SubmitResult whale = await w.RaidVault("whale", "v_owner", TestVaults.Escape);
            w.Clock.Advance(Rules.ShieldMs(TestEconomy.Loot) + 1);
            SubmitResult free = await w.RaidVault("newcomer", "v_owner", TestVaults.Escape);

            Assert.AreEqual(free.Hash, whale.Hash, "same vault, seed and inputs: same raid");
            Assert.AreEqual(free.Stars, whale.Stars);
            Assert.AreEqual(free.Loot, whale.Loot);
            Assert.AreEqual(free.Ticks, whale.Ticks);
        }
    }
}
