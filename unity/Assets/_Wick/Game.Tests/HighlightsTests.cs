using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Wick.Sim;

namespace Wick.Game.Tests
{
    public class HighlightsTests
    {
        private static TimedEvent At(int seconds, SimEventType type, int value = 0)
        {
            return new TimedEvent(seconds * SimTuning.TicksPerSecond, new SimEvent(type, 1, new TilePos(0, 0), value));
        }

        private const int Raid = SimTuning.RaidTicks;

        [Test]
        public void Scores_OnlyTheMomentsWorthAClip()
        {
            Assert.AreEqual(Highlights.SnuffScore, Highlights.Score(At(0, SimEventType.Snuffed).Event, 300));
            Assert.AreEqual(Highlights.NearMissScore, Highlights.Score(At(0, SimEventType.NearMiss).Event, 300));
            Assert.AreEqual(Highlights.BigGrabScore, Highlights.Score(At(0, SimEventType.TreasureTaken, 300).Event, 300));
            Assert.AreEqual(0, Highlights.Score(At(0, SimEventType.TreasureTaken, 100).Event, 300), "a small pile");
            Assert.AreEqual(0, Highlights.Score(At(0, SimEventType.TrapFired).Event, 300));
        }

        [Test]
        public void PicksTheBusiestStretch_WithinTenToFifteenSeconds()
        {
            var events = new List<TimedEvent>
            {
                At(5, SimEventType.NearMiss),
                At(30, SimEventType.NearMiss), At(33, SimEventType.FlickerLost), At(36, SimEventType.NearMiss),
                At(38, SimEventType.Snuffed),
            };

            ClipWindow w = Highlights.Pick(events, 38 * 30, Outcome.Snuffed, 300);

            Assert.That(w.Ticks, Is.InRange(Highlights.MinTicks, Highlights.MaxTicks));
            Assert.LessOrEqual(w.StartTick, 30 * 30);
            Assert.GreaterOrEqual(w.EndTick, 38 * 30);
            Assert.AreEqual(38 * 30, w.KeyTick, "the snuff is the key moment");
            Assert.AreEqual(3 + 4 + 3 + 10, w.Score);
        }

        [Test]
        public void ALastSecondEscape_IsTheKeyMoment()
        {
            var events = new List<TimedEvent> { At(10, SimEventType.NearMiss), At(57, SimEventType.NearMiss) };
            ClipWindow w = Highlights.Pick(events, Raid - 30, Outcome.Escaped, 300);
            Assert.AreEqual(Raid - 30, w.KeyTick);
            Assert.AreEqual(Raid - 30, w.EndTick);
        }

        [Test]
        public void TheKeyMomentGetsLeadUp()
        {
            var events = new List<TimedEvent> { At(20, SimEventType.Snuffed) };
            ClipWindow w = Highlights.Pick(events, 20 * 30, Outcome.Snuffed, 300);
            Assert.GreaterOrEqual(w.KeyTick - w.StartTick, Highlights.LeadTicks);
        }

        [Test]
        public void AShortRaid_IsShownWhole()
        {
            ClipWindow w = Highlights.Pick(new List<TimedEvent> { At(4, SimEventType.Snuffed) }, 8 * 30, Outcome.Snuffed, 300);
            Assert.AreEqual(0, w.StartTick);
            Assert.AreEqual(8 * 30, w.EndTick);
        }

        [Test]
        public void ANothingRaid_StillGetsAWindow()
        {
            ClipWindow w = Highlights.Pick(new List<TimedEvent>(), Raid, Outcome.TimedOut, 300);
            Assert.AreEqual(Highlights.MinTicks, w.Ticks);
            Assert.AreEqual(0, w.Score);
        }

        [Test]
        public void IsDeterministic_OnARealRaid()
        {
            TextAsset text = Resources.Load<TextAsset>("DevGoldens/full-heist");
            RaidFile raid = RaidFile.Parse(text.text);
            List<TimedEvent> a = Highlights.Collect(raid.Vault, raid.Seed, raid.Log, out RaidResult r1);
            List<TimedEvent> b = Highlights.Collect(raid.Vault, raid.Seed, raid.Log, out RaidResult r2);
            Assert.AreEqual(r1.HashHex, raid.ExpectedHash, "collecting events doesn't change the raid");

            ClipWindow w1 = Highlights.Pick(a, r1.Ticks, r1.Outcome, Highlights.BigTreasure(raid.Vault));
            ClipWindow w2 = Highlights.Pick(b, r2.Ticks, r2.Outcome, Highlights.BigTreasure(raid.Vault));
            Assert.AreEqual(w1.StartTick, w2.StartTick);
            Assert.AreEqual(w1.KeyTick, w2.KeyTick);
            Assert.Greater(w1.Score, 0, "full-heist has a drip hit and a big grab");
        }
    }
}
