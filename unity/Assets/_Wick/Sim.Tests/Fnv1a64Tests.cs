using NUnit.Framework;

namespace Wick.Sim.Tests
{
    public class Fnv1a64Tests
    {
        [Test]
        public void Empty_IsOffsetBasis()
        {
            Assert.AreEqual(0xcbf29ce484222325UL, new Fnv1a64().Value);
        }

        [Test]
        public void KnownVectors()
        {
            Assert.AreEqual(0xaf63dc4c8601ec8cUL, Hash("a"));
            Assert.AreEqual(0x85944171f73967e8UL, Hash("foobar"));
        }

        [Test]
        public void AddInt_IsLittleEndianBytes()
        {
            var a = new Fnv1a64();
            a.AddInt(0x04030201);
            var b = new Fnv1a64();
            b.AddByte(1);
            b.AddByte(2);
            b.AddByte(3);
            b.AddByte(4);
            Assert.AreEqual(b.Value, a.Value);
        }

        private static ulong Hash(string ascii)
        {
            var h = new Fnv1a64();
            foreach (char c in ascii)
            {
                h.AddByte((byte)c);
            }

            return h.Value;
        }
    }
}
