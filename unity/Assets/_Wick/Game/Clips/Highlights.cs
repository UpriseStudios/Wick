using System.Collections.Generic;
using Wick.Sim;

namespace Wick.Game
{
    /// <summary>A sim event and the tick it happened on.</summary>
    public readonly struct TimedEvent
    {
        public readonly int Tick;
        public readonly SimEvent Event;

        public TimedEvent(int tick, SimEvent e)
        {
            Tick = tick;
            Event = e;
        }
    }

    /// <summary>The part of a raid a clip shows, and the moment it slows down for.</summary>
    public readonly struct ClipWindow
    {
        public readonly int StartTick;
        public readonly int EndTick;
        public readonly int KeyTick;
        public readonly int Score;

        public ClipWindow(int startTick, int endTick, int keyTick, int score)
        {
            StartTick = startTick;
            EndTick = endTick;
            KeyTick = keyTick;
            Score = score;
        }

        public int Ticks => EndTick - StartTick;
    }

    /// <summary>
    /// Picks the best 10-15 s of a raid for its share clip from the sim's events: near misses,
    /// lost flickers, a snuff, a big grab and a last-second escape. Pure: the same raid always
    /// picks the same window. Scores live here so they can be tuned without touching the sim.
    /// </summary>
    public static class Highlights
    {
        public const int MinTicks = 10 * SimTuning.TicksPerSecond;
        public const int MaxTicks = 15 * SimTuning.TicksPerSecond;
        public const int StepTicks = SimTuning.TicksPerSecond / 2;

        /// <summary>An escape with this little time left counts as last-second.</summary>
        public const int LastSecondTicks = 5 * SimTuning.TicksPerSecond;

        /// <summary>The key moment should have this much lead-up in the window where possible.</summary>
        public const int LeadTicks = 2 * SimTuning.TicksPerSecond;

        public const int NearMissScore = 3;
        public const int FlickerLostScore = 4;
        public const int BigGrabScore = 5;
        public const int LastSecondEscapeScore = 8;
        public const int SnuffScore = 10;

        /// <summary>How much an event is worth in a clip; 0 for events that aren't highlights.</summary>
        /// <param name="bigTreasure">Pile value that counts as a big grab (the vault's largest pile).</param>
        public static int Score(SimEvent e, int bigTreasure)
        {
            switch (e.Type)
            {
                case SimEventType.NearMiss: return NearMissScore;
                case SimEventType.FlickerLost: return FlickerLostScore;
                case SimEventType.Snuffed: return SnuffScore;
                case SimEventType.TreasureTaken: return bigTreasure > 0 && e.Value >= bigTreasure ? BigGrabScore : 0;
                default: return 0;
            }
        }

        /// <summary>
        /// The best window: the 10-15 s span with the highest total score (ties go to the shorter,
        /// then the earlier one). Its key moment is its highest-scoring event (ties go to the later).
        /// A raid shorter than 10 s is shown whole.
        /// </summary>
        /// <param name="endTick">The tick the raid ended on.</param>
        public static ClipWindow Pick(IReadOnlyList<TimedEvent> events, int endTick, Outcome outcome, int bigTreasure)
        {
            var scored = new List<(int Tick, int Score)>();
            foreach (TimedEvent t in events)
            {
                int s = Score(t.Event, bigTreasure);
                if (s > 0)
                {
                    scored.Add((t.Tick, s));
                }
            }

            if (outcome == Outcome.Escaped && endTick >= SimTuning.RaidTicks - LastSecondTicks)
            {
                scored.Add((endTick, LastSecondEscapeScore));
            }

            if (endTick <= MinTicks)
            {
                return Window(scored, 0, endTick);
            }

            ClipWindow best = default;
            bool found = false;
            for (int length = MinTicks; length <= MaxTicks; length += StepTicks)
            {
                for (int start = 0; start + length <= endTick + StepTicks; start += StepTicks)
                {
                    int end = start + length > endTick ? endTick : start + length;
                    ClipWindow w = Window(scored, end - length < 0 ? 0 : end - length, end);
                    if (!found || w.Score > best.Score)
                    {
                        best = w;
                        found = true;
                    }
                }
            }

            // Give the key moment some lead-up when the best window opens right on it.
            if (best.KeyTick - best.StartTick < LeadTicks && best.StartTick > 0)
            {
                int shift = LeadTicks - (best.KeyTick - best.StartTick);
                if (shift > best.StartTick)
                {
                    shift = best.StartTick;
                }

                best = Window(scored, best.StartTick - shift, best.EndTick - shift);
            }

            return best;
        }

        private static ClipWindow Window(List<(int Tick, int Score)> scored, int start, int end)
        {
            int total = 0;
            int keyTick = start;
            int keyScore = 0;
            foreach ((int tick, int score) in scored)
            {
                if (tick < start || tick > end)
                {
                    continue;
                }

                total += score;
                if (score >= keyScore)
                {
                    keyScore = score;
                    keyTick = tick;
                }
            }

            if (keyScore == 0)
            {
                keyTick = start + (end - start) / 2; // nothing happened: slow the middle
            }

            return new ClipWindow(start, end, keyTick, total);
        }

        /// <summary>Re-simulates a raid and keeps its events with their ticks (milliseconds).</summary>
        public static List<TimedEvent> Collect(VaultDef vault, int seed, InputLog log, out RaidResult result)
        {
            var events = new List<TimedEvent>();
            var sim = new RaidSim(vault, seed, log);
            while (!sim.IsOver)
            {
                int tick = sim.State.Tick;
                sim.Step();
                foreach (SimEvent e in sim.Events)
                {
                    events.Add(new TimedEvent(tick, e));
                }
            }

            result = sim.GetResult();
            return events;
        }

        /// <summary>The vault's largest treasure pile, the threshold for a big grab.</summary>
        public static int BigTreasure(VaultDef vault)
        {
            int max = 0;
            for (int i = 0; i < vault.ObjectCount; i++)
            {
                ObjectDef o = vault.GetObject(i);
                if (o.Type == ObjectType.Treasure && o.Value > max)
                {
                    max = o.Value;
                }
            }

            return max;
        }
    }
}
