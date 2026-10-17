using NUnit.Framework;

namespace Wick.Sim.Tests
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
