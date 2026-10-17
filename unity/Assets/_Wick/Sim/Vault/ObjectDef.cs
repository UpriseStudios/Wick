namespace Wick.Sim
{
    /// <summary>A placed vault object (trap, guardian, treasure, brazier, crate) as authored.</summary>
    public sealed class ObjectDef
    {
        public ObjectDef(int id, ObjectType type, TilePos pos, int rot, int cycle, int offset, int value, TilePos[] patrol)
        {
            Id = id;
            Type = type;
            Pos = pos;
            Rot = rot;
            Cycle = cycle;
            Offset = offset;
            Value = value;
            Patrol = patrol;
        }

        public int Id { get; }
        public ObjectType Type { get; }
        public TilePos Pos { get; }

        /// <summary>Facing as a Dir value (0 = N, 1 = E, 2 = S, 3 = W).</summary>
        public int Rot { get; }

        public int Cycle { get; }
        public int Offset { get; }
        public int Value { get; }

        /// <summary>Patrol waypoints; empty when the object doesn't patrol.</summary>
        public TilePos[] Patrol { get; }

        public Dir Facing => (Dir)Rot;
    }
}
