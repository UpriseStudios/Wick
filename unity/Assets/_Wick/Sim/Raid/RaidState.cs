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

        /// <summary>Decoys still to throw (RaidParams.Decoys at the start).</summary>
        public int DecoysLeft;

        /// <summary>Lantern: light radius added for this raid (RaidParams.LightBonus).</summary>
        public int LightBonus;

        public bool DecoyUsed => DecoysLeft == 0;

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

        // Hits, pickups, scoring
        public int InvulnTicks;
        public int CarriedPiles;
        public int GustDimTicks;
        public bool ReachedHoard;

        /// <summary>Tiles of knockback still to slide (Moving is set and MoveDir is the push).</summary>
        public int KnockTiles;

        /// <summary>Sliding on ice: he keeps going in MoveDir until something stops him or the ice ends.</summary>
        public bool Sliding;

        /// <summary>The tile Wick counts as standing on for hits, contacts and pickups.</summary>
        public TilePos Occupied => Motion.OccupiedTile(Tile, Moving, MoveDir, Progress);
    }

    public enum GuardianMode : byte
    {
        /// <summary>Owl walking its waypoints; moths hovering with nothing to chase.</summary>
        Patrol = 0,

        /// <summary>Owl hunting Wick; moths drifting toward Wick's light.</summary>
        Chase = 1,

        /// <summary>Drawn to an active decoy.</summary>
        Distracted = 2,
    }

    /// <summary>A Night Owl or Wax Moths swarm. Every field is folded into the state hash.</summary>
    public struct GuardianState
    {
        /// <summary>Index into the vault's objects (which are sorted by id).</summary>
        public int ObjectIndex;

        public ObjectType Kind;
        public TilePos Tile;
        public int Progress;
        public bool Moving;
        public Dir MoveDir;
        public Dir Facing;
        public GuardianMode Mode;

        /// <summary>Owl: index of the waypoint it is heading for.</summary>
        public int Waypoint;

        /// <summary>Where the guardian is heading when chasing or distracted.</summary>
        public TilePos Target;

        /// <summary>Owl: ticks since it last saw Wick while chasing.</summary>
        public int LostTicks;

        /// <summary>Moths: consecutive ticks in contact with Wick.</summary>
        public int ContactTicks;

        public TilePos Occupied => Motion.OccupiedTile(Tile, Moving, MoveDir, Progress);
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

        /// <summary>
        /// Per vault object (same order as the vault's objects, i.e. by id): a Snuffer Bell has
        /// triggered, a treasure pile was taken, or a brazier was used.
        /// </summary>
        public bool[] Spent;

        /// <summary>Night Owls and Wax Moths, sorted by entity id.</summary>
        public GuardianState[] Guardians;

        /// <summary>
        /// Per vault object: a gate's open ticks left (0 = closed, unless someone stands in it).
        /// Always 0 for other objects.
        /// </summary>
        public int[] GateOpen;

        /// <summary>
        /// Tiles lit by Wick's flame this tick, row-major (y * vault width + x). Derived from
        /// Wick's state and the vault, so it is not hashed separately. Read it with RaidSim.IsLit.
        /// </summary>
        public bool[] Lit;

        /// <summary>Running hash after each fold (every 30 ticks and at the end).</summary>
        public readonly List<ulong> Checkpoints = new List<ulong>(SimTuning.RaidTicks / SimTuning.HashIntervalTicks + 1);

        /// <summary>A deep copy (RaidSim.Clone).</summary>
        public RaidState Clone()
        {
            var c = new RaidState
            {
                Tick = Tick,
                Wick = Wick,
                Outcome = Outcome,
                Rng = Rng,
                Hash = Hash,
                Spent = (bool[])Spent.Clone(),
                Guardians = (GuardianState[])Guardians.Clone(),
                GateOpen = (int[])GateOpen.Clone(),
                Lit = (bool[])Lit.Clone(),
            };
            c.Checkpoints.AddRange(Checkpoints);
            return c;
        }
    }
}
