using NUnit.Framework;
using Wick.Sim;

namespace Wick.Sim.Server.Tests
{
    public class SimInfoTests
    {
        [Test]
        public void Version_IsOne()
        {
            Assert.AreEqual(1, SimInfo.Version);
        }
    }
}
