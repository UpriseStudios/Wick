using NUnit.Framework;
using Wick.Services;

namespace Wick.Game.Tests
{
    public class VaultLinkTests
    {
        [Test]
        public void BuildsTheShareLink()
        {
            Assert.AreEqual("https://wick.uprisestudios.co.uk/v/ABCD2345", VaultLink.Build("ABCD2345"));
        }

        [TestCase("https://wick.uprisestudios.co.uk/v/ABCD2345", "ABCD2345")]
        [TestCase("https://wick.uprisestudios.co.uk/v/abcd-2345?utm=x", "ABCD2345")]
        [TestCase("utm_source=google-play&vault=WXYZ6789", "WXYZ6789")]
        [TestCase("vault%3DWXYZ6789", "WXYZ6789")]
        [TestCase("Hey, Wick vault code: QRST2345 good luck", "QRST2345")]
        [TestCase("qrst 2345", "QRST2345")]
        [TestCase("https://wick.uprisestudios.co.uk/", null)]
        [TestCase("ABCD0123", null)]
        [TestCase("", null)]
        [TestCase(null, null)]
        public void FindsTheCode(string text, string code)
        {
            Assert.AreEqual(code, VaultLink.FindCode(text));
        }

        [Test]
        public void TheStoreLinkCarriesTheCodeInTheReferrer()
        {
            Assert.AreEqual("ABCD2345", VaultLink.FindCode(VaultLink.Store("ABCD2345")));
        }
    }
}
