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
    /// plays out identically for a player who owns everything and one who owns nothing but has
    /// raised the same levels (Wick's upgrades follow Vault Level, which money can't skip).
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
            // Wick's upgrades (Lantern, extra decoy) are earned through Vault Level, which needs raid
            // stars: the M10 rule is that a raid plays the same for everyone at the same progression,
            // whatever they have bought. So the free player here has raised the same levels.
            var progression = new Progression { VaultLevel = 10, Lantern = 5, Decoys = 1 };
            await w.Set("whale", EconomyKeys.Upgrades, new Upgrades { Levels = progression });
            await w.Set("grinder", EconomyKeys.Upgrades, new Upgrades { Levels = new Progression { VaultLevel = 10, Lantern = 5, Decoys = 1 } });

            SubmitResult whale = await w.RaidVault("whale", "v_owner", TestVaults.Escape);
            w.Clock.Advance(Rules.ShieldMs(TestEconomy.Loot) + 1);
            SubmitResult free = await w.RaidVault("grinder", "v_owner", TestVaults.Escape);

            Assert.AreEqual(free.Hash, whale.Hash, "same vault, seed, inputs and progression: same raid");
            Assert.AreEqual(free.Stars, whale.Stars);
            Assert.AreEqual(free.Loot, whale.Loot);
            Assert.AreEqual(free.Ticks, whale.Ticks);
        }

        [Test]
        public void WickUpgrades_AreCappedByVaultLevel_SoBuyingCantGetAhead()
        {
            foreach (LanternStep l in TestEconomy.Default.Lantern)
            {
                Assert.Greater(l.MinVaultLevel, 1, "every Lantern level waits for a Vault Level");
            }

            foreach (DecoyStep d in TestEconomy.Default.Decoys)
            {
                Assert.Greater(d.MinVaultLevel, 1, "every extra decoy waits for a Vault Level");
            }
        }
    }
}
