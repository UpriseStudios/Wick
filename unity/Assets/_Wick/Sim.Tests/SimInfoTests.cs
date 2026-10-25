using NUnit.Framework;

namespace Wick.Sim.Tests
{
    public class SimInfoTests
    {
        [Test]
        public void Version_IsFive()
        {
            Assert.AreEqual(5, SimInfo.Version);
        }
    }
}
