using System.Collections.Generic;

namespace Wick.Sim
{
    public enum Outcome : byte
    {
        Running = 0,
        Escaped = 1,
        Snuffed = 2,
        TimedOut = 3,
    }

    /// <summary>Wick's full simulation state. Every field here is folded into the state hash.</summary>
    public struct WickState
    {
        public TilePos Tile;

        /// <summary>Progress toward Tile.Step(MoveDir) in sub-tile units; 0 when at rest.</summary>
        public int Progress;

        public Dir Facing;
        public int Flickers;
        public bool Tiptoe;
        public int CarriedTreasure;
        public bool DecoyUsed;

        // Movement
        public bool Moving;
        public Dir MoveDir;

        /// <summary>Whether the current move started as part of a run (sets its speed).</summary>
        public bool MoveIsRun;

        public bool Running;
        public Dir RunDir;
        public bool HasQueued;
        public Dir QueuedDir;
        public bool LeftEntrance;

        // Decoy
        public TilePos DecoyTile;
        public int DecoyTicksLeft;
    }

    /// <summary>Mutable state of one raid. Owned and advanced only by RaidSim.</summary>
    public sealed class RaidState
    {
        /// <summary>The tick about to be simulated; after the raid ends, the number of ticks simulated.</summary>
        public int Tick;

        public WickState Wick;
        public Outcome Outcome;
        public Pcg32 Rng;
        public Fnv1a64 Hash;

        /// <summary>Running hash after each fold (every 30 ticks and at the end).</summary>
        public readonly List<ulong> Checkpoints = new List<ulong>(SimTuning.RaidTicks / SimTuning.HashIntervalTicks + 1);
    }
}
