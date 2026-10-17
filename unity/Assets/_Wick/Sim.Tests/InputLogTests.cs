using System;
using NUnit.Framework;

namespace Wick.Sim.Tests
{
    public class InputLogTests
    {
        [Test]
        public void EmptyLog_EncodesToNothing()
        {
            Assert.AreEqual(0, InputLog.Empty.Encode().Length);
            Assert.AreEqual(0, InputLog.Decode(new byte[0]).Count);
        }

        [Test]
        public void KnownEncoding()
        {
            var log = new InputLog(new[]
            {
                Command.Dash(3, Dir.E),       // delta 3, type 0 | E(1) << 3
                Command.TiptoeOn(3),          // delta 0, type 3
                Command.RunStart(200, Dir.W), // delta 197 = 0xC5 0x01, type 1 | W(3) << 3
            });
            CollectionAssert.AreEqual(
                new byte[] { 0x03, 0x08, 0x00, 0x03, 0xC5, 0x01, 0x19 },
                log.Encode());
        }

        [Test]
        public void RoundTrips_EveryTypeAndDirection()
        {
            var commands = new[]
            {
                Command.Dash(0, Dir.N),
                Command.Dash(0, Dir.E),
                Command.Dash(1, Dir.S),
                Command.Dash(2, Dir.W),
                Command.RunStart(5, Dir.N),
                Command.RunStart(5, Dir.W),
                Command.RunStop(9),
                Command.TiptoeOn(10),
                Command.TiptoeOff(127),
                Command.Decoy(128),
                Command.Dash(16511, Dir.S),       // delta 16383: largest 2-byte varint
                Command.Dash(16512 + 16384, Dir.E), // 3-byte varint
                Command.RunStop(int.MaxValue),
            };
            AssertRoundTrip(new InputLog(commands));
        }

        [Test]
        public void RoundTrips_RandomLogs()
        {
            var rng = new Pcg32(2026, 10);
            for (int n = 0; n < 200; n++)
            {
                var commands = new Command[rng.NextInt(60)];
                int tick = 0;
                for (int i = 0; i < commands.Length; i++)
                {
                    tick += rng.NextInt(4) == 0 ? 0 : rng.NextInt(400);
                    commands[i] = new Command(tick, (CommandType)rng.NextInt(6), (Dir)rng.NextInt(4));
                }

                AssertRoundTrip(new InputLog(commands));
            }
        }

        [Test]
        public void Base64_RoundTrips()
        {
            var log = new InputLog(new[] { Command.RunStart(0, Dir.N), Command.RunStop(31), Command.Decoy(900) });
            AssertSame(log, InputLog.FromBase64(log.ToBase64()));
        }

        [Test]
        public void NonDirectionalCommands_IgnoreDirection()
        {
            Assert.AreEqual(Dir.N, new Command(0, CommandType.Decoy, Dir.W).Dir);
        }

        [Test]
        public void Constructor_RejectsDecreasingTicks()
        {
            Assert.Throws<ArgumentException>(() => new InputLog(new[] { Command.Dash(5, Dir.N), Command.Dash(4, Dir.N) }));
            Assert.Throws<ArgumentException>(() => new InputLog(new[] { Command.Dash(-1, Dir.N) }));
        }

        [TestCase(new byte[] { 0x05 }, TestName = "MissingCommandByte")]
        [TestCase(new byte[] { 0x80 }, TestName = "TruncatedVarint")]
        [TestCase(new byte[] { 0x00, 0x06 }, TestName = "UnknownType")]
        [TestCase(new byte[] { 0x00, 0x20 }, TestName = "ReservedBitSet")]
        [TestCase(new byte[] { 0x00, 0x0A }, TestName = "DirectionOnRunStop")]
        [TestCase(new byte[] { 0x80, 0x00, 0x00 }, TestName = "NonCanonicalVarint")]
        [TestCase(new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0x7F, 0x00 }, TestName = "VarintTooLarge")]
        [TestCase(new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0x07, 0x00, 0x01, 0x00 }, TestName = "TickOverflow")]
        public void Decode_RejectsMalformed(byte[] data)
        {
            Assert.Throws<FormatException>(() => InputLog.Decode(data));
        }

        [Test]
        public void FromBase64_RejectsGarbage()
        {
            Assert.Throws<FormatException>(() => InputLog.FromBase64("not base64!"));
        }

        private static void AssertRoundTrip(InputLog log)
        {
            byte[] bytes = log.Encode();
            InputLog decoded = InputLog.Decode(bytes);
            AssertSame(log, decoded);
            CollectionAssert.AreEqual(bytes, decoded.Encode(), "re-encoding must be byte-identical");
        }

        private static void AssertSame(InputLog expected, InputLog actual)
        {
            Assert.AreEqual(expected.Count, actual.Count);
            for (int i = 0; i < expected.Count; i++)
            {
                Assert.AreEqual(expected[i].Tick, actual[i].Tick, "tick " + i);
                Assert.AreEqual(expected[i].Type, actual[i].Type, "type " + i);
                Assert.AreEqual(expected[i].Dir, actual[i].Dir, "dir " + i);
            }
        }
    }
}
