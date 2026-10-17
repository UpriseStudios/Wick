using NUnit.Framework;

namespace Wick.Sim.Tests
{
    public class SimInfoTests
    {
        [Test]
        public void Version_IsThree()
        {
            Assert.AreEqual(3, SimInfo.Version);
        }
    }
}
