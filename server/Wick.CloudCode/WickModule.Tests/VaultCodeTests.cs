using System.Threading.Tasks;
using NUnit.Framework;
using Wick.Sim;

namespace Wick.CloudCode.Tests
{
    public class VaultCodeTests
    {
        [Test]
        public void Codes_AreEightUnambiguousCharacters_DerivedFromTheVaultId()
        {
            string code = VaultCodes.Derive("v_player123", 0);
            Assert.AreEqual(8, code.Length);
            Assert.AreEqual(code, VaultCodes.Derive("v_player123", 0), "stable across republishes");
            Assert.AreNotEqual(code, VaultCodes.Derive("v_player124", 0));
            foreach (char c in code)
            {
                StringAssert.Contains(c.ToString(), VaultCodes.Alphabet);
            }

            StringAssert.DoesNotContain("0", VaultCodes.Alphabet);
            StringAssert.DoesNotContain("O", VaultCodes.Alphabet);
            StringAssert.DoesNotContain("I", VaultCodes.Alphabet);
            StringAssert.DoesNotContain("1", VaultCodes.Alphabet);
        }

        [TestCase("abcd-efgh", "ABCDEFGH")]
        [TestCase(" WXYZ 2345 ", "WXYZ2345")]
        [TestCase("ABCDEFG", null)]
        [TestCase("ABCDEFGH9", null)]
        [TestCase("ABCD0FGH", null)]
        [TestCase("v_player123", null)]
        public void Normalize(string text, string expected)
        {
            Assert.AreEqual(expected, VaultCodes.Normalize(text));
        }

        [Test]
        public async Task AClash_MovesOnToTheNextSalt()
        {
            var store = new FakeStore();
            string taken = VaultCodes.Derive("v_mine", 0);
            await store.SetPrivateCustomAsync("code_" + taken, "vault", Json.Write("v_someone_else"));

            string code = await VaultCodes.AssignAsync(store, "v_mine");

            Assert.AreEqual(VaultCodes.Derive("v_mine", 1), code);
            Assert.AreEqual("v_mine", await VaultCodes.ResolveAsync(store, code));
            Assert.AreEqual(code, await VaultCodes.AssignAsync(store, "v_mine"), "found again, not reassigned");
        }

        [Test]
        public async Task PublishingGivesTheVaultACode_ThatRaidsAndCardsUse()
        {
            var w = new World();
            PublishResult published = await new PublishService(w.Store, w.Clock).PublishAsync("owner", "req-pub-0001",
                TestVaults.PublishDocument(TestVaults.Halls()));
            Assert.IsNotNull(published.Code);

            RaidStartResult byCode = await w.Raids.StartFriendRaidAsync("raider", published.Code.ToLowerInvariant(), false);
            Assert.IsTrue(byCode.Ok, byCode.Reason);
            Assert.AreEqual("v_owner", byCode.VaultId);

            VaultCard card = await w.Defence.GetVaultCardAsync(published.Code);
            Assert.IsTrue(card.Ok);
            Assert.AreEqual(1, card.VaultLevel);
            Assert.IsFalse((await w.Defence.GetVaultCardAsync("ZZZZZZZZ")).Ok);
        }

        [Test]
        public async Task SeedVaultsGetCodesToo()
        {
            var w = new World();
            SeedResult r = await w.Defence.SeedVaultAsync("crypt", TestVaults.PublishDocument(TestVaults.Halls()), 0);
            Assert.IsTrue((await w.Defence.GetVaultCardAsync(r.Code)).Ok);
        }
    }
}
