using System;
using NUnit.Framework;

namespace Wick.Sim.Tests
{
    public class Pcg32Tests
    {
        [Test]
        public void MatchesReferenceSequence_Seed42_Stream54()
        {
            // First outputs of the reference pcg32-demo (pcg-c-basic) for seed 42, stream 54.
            uint[] expected = { 0xa15c02b7, 0x7b47f409, 0xba1d3330, 0x83d2f293, 0xbfa4784b, 0xcbed606e };
            var rng = new Pcg32(42, 54);
            for (int i = 0; i < expected.Length; i++)
            {
                Assert.AreEqual(expected[i], rng.NextUInt(), "output " + i);
            }
        }

        [Test]
        public void SameSeedSameSequence()
        {
            var a = new Pcg32(99120, 1);
            var b = new Pcg32(99120, 1);
            for (int i = 0; i < 1000; i++)
            {
                Assert.AreEqual(a.NextUInt(), b.NextUInt());
            }
        }

        [Test]
        public void NextInt_StaysInRangeAndHitsEveryValue()
        {
            var rng = new Pcg32(7, 3);
            var seen = new bool[6];
            for (int i = 0; i < 10000; i++)
            {
                int v = rng.NextInt(6);
                Assert.That(v, Is.InRange(0, 5));
                seen[v] = true;
            }

            CollectionAssert.DoesNotContain(seen, false);
        }

        [Test]
        public void NextInt_RejectsNonPositiveBound()
        {
            var rng = new Pcg32(1, 1);
            Assert.Throws<ArgumentOutOfRangeException>(() => rng.NextInt(0));
        }
    }
}
