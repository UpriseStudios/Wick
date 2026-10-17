using NUnit.Framework;

namespace Wick.Sim.Tests
{
    public class SimInfoTests
    {
        [Test]
        public void Version_IsTwo()
        {
            Assert.AreEqual(2, SimInfo.Version);
        }
    }
}
