using NUnit.Framework;
using Wick.Services;

namespace Wick.Game.Tests
{
    public class BackoffTests
    {
        [Test]
        public void DoublesFromOneSecond_UnlessTheServiceSaysWhen()
        {
            Assert.AreEqual(1000, Backoff.DelayMs(1));
            Assert.AreEqual(2000, Backoff.DelayMs(2));
            Assert.AreEqual(4000, Backoff.DelayMs(3));
            Assert.AreEqual(16000, Backoff.DelayMs(99), "capped");
            Assert.AreEqual(2500, Backoff.DelayMs(1, 2.5f), "Retry-After wins");
        }

        [Test]
        public void RequestIds_AreUnique_AndAcceptedByTheServer()
        {
            string a = Backoff.NewRequestId();
            Assert.AreNotEqual(a, Backoff.NewRequestId());
            Assert.That(a, Does.Match("^[a-f0-9]{32}$"), "matches the server's 8-64 char [A-Za-z0-9_-] rule");
        }
    }
}
