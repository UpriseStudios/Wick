namespace Wick.Sim
{
    /// <summary>
    /// What each defence upgrade level does (docs/m10-plan.md, "Defence levels"). Levels 2 to 4
    /// are a small step each; level 5 is a bigger step plus a perk. First-pass numbers, tuned from
    /// the balance simulator (tools/balance). Changing any of them is a sim rule change.
    /// </summary>
    public static class DefenceStats
    {
        /// <summary>Ticks a level takes off a timed trap's authored cycle: 0, 2, 4, 6, then 9.</summary>
        public static int CycleReduction(int level)
        {
            if (level <= 1)
            {
                return 0;
            }

            int small = (level >= 4 ? 4 : level) - 1;
            return 2 * small + (level >= 5 ? 3 : 0);
        }

        /// <summary>A timed trap's cycle at a level, never below its type's minimum.</summary>
        public static int Cycle(ObjectDef trap, int level)
        {
            int cycle = trap.Cycle - CycleReduction(level);
            int min = ObjectTypes.MinCycle(trap.Type);
            return cycle < min ? min : cycle;
        }

        /// <summary>Spike Floor's warning rumble: 15 ticks, shortened to the 10-tick floor at level 5.</summary>
        public static int SpikeWarnTicks(int level)
        {
            return level >= 5 ? MinTellTicks : SimTuning.SpikeWarnTicks;
        }

        /// <summary>No upgrade shortens a tell below this (a third of a second).</summary>
        public const int MinTellTicks = 10;

        /// <summary>
        /// Drip Pipe level 5: the wet tile slows Wick to tiptoe speed for half a second after each
        /// drip (short enough that a level 5 cycle of 36 ticks still has a dry gap).
        /// </summary>
        public static int DripPuddleTicks(int level)
        {
            return level >= 5 ? SimTuning.TicksPerSecond / 2 : 0;
        }

        /// <summary>Bellows level 5: a gust that hits knocks Wick this many tiles down the gust line.</summary>
        public static int BellowsKnockback(int level)
        {
            return level >= 5 ? 1 : 0;
        }

        /// <summary>
        /// Snuffer Bell: ticks after dropping before it lifts and re-arms. 0 means it stays spent
        /// (level 1). Level 2 re-arms after 10 s, levels 3 and 4 two seconds sooner each, level 5 after 4 s.
        /// </summary>
        public static int BellRearmTicks(int level)
        {
            switch (level)
            {
                case 2: return 10 * SimTuning.TicksPerSecond;
                case 3: return 8 * SimTuning.TicksPerSecond;
                case 4: return 6 * SimTuning.TicksPerSecond;
                case 5: return 4 * SimTuning.TicksPerSecond;
                default: return 0;
            }
        }

        /// <summary>Snuffer Bell level 5: the plate also covers the tile in front of it (its facing).</summary>
        public static bool BellWidePlate(int level)
        {
            return level >= 5;
        }

        /// <summary>Night Owl chase speed: 100, then +5 a level, and +10 at level 5.</summary>
        public static int OwlChaseSpeed(int level)
        {
            return SimTuning.OwlChaseSpeed + Steps(level, 5, 10);
        }

        /// <summary>
        /// Night Owl level 5: a wider sight cone. Wick is in front of the owl when his sideways
        /// offset is at most the forward distance times this over 2: 2 (a 90-degree cone), or 3
        /// at level 5 (about 112 degrees). Sight range itself never grows: the owl only sees Wick
        /// by his own light, which reaches 4 tiles.
        /// </summary>
        public static int OwlConeHalves(int level)
        {
            return level >= 5 ? 3 : 2;
        }

        /// <summary>Wax Moths drift speed: 40, then +3 a level, and +5 at level 5.</summary>
        public static int MothSpeed(int level)
        {
            return SimTuning.MothSpeed + Steps(level, 3, 5);
        }

        /// <summary>Wax Moths drain a flicker after this many ticks of contact: 60, or 45 at level 5.</summary>
        public static int MothDrainTicks(int level)
        {
            return level >= 5 ? 45 : SimTuning.MothDrainTicks;
        }

        /// <summary>Swing Axe level 5: the top of the arc also catches the tile in front of it.</summary>
        public static bool AxeWideArc(int level)
        {
            return level >= 5;
        }

        /// <summary>Mimic Chest: its wind-up before a bite: 18 ticks, 2 fewer a level, never below the tell floor.</summary>
        public static int MimicLungeTicks(int level)
        {
            int ticks = SimTuning.MimicLungeTicks - Steps(level, 2, 2);
            return ticks < MinTellTicks ? MinTellTicks : ticks;
        }

        /// <summary>Mimic Chest level 5: a bite also steals one treasure pile Wick is carrying.</summary>
        public static bool MimicSteals(int level)
        {
            return level >= 5;
        }

        /// <summary>Ice Tiles: sub-tile units per tick while sliding: 250, +10% a level, +15% more at level 5.</summary>
        public static int IceSlideSpeed(int level)
        {
            return SimTuning.IceSlideSpeed * (100 + Steps(level, 10, 15)) / 100;
        }

        /// <summary>Ice Tiles level 5: sliding dims the flame like a Bellows gust.</summary>
        public static bool IceDims(int level)
        {
            return level >= 5;
        }

        /// <summary>Levels 2 to 4 add `small` each; level 5 adds `big` on top.</summary>
        private static int Steps(int level, int small, int big)
        {
            if (level <= 1)
            {
                return 0;
            }

            int n = (level >= 4 ? 4 : level) - 1;
            return small * n + (level >= 5 ? big : 0);
        }
    }
}
