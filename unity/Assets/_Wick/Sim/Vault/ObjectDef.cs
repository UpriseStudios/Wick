namespace Wick.Sim
{
    /// <summary>A placed vault object (trap, guardian, treasure, brazier) as authored.</summary>
    public sealed class ObjectDef
    {
        public ObjectDef(int id, string type, TilePos pos, int rot, int cycle, int offset, int value, TilePos[] patrol)
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

        // TODO M2: map to an ObjectType enum once the slice entities exist.
        public string Type { get; }

        public TilePos Pos { get; }
        public int Rot { get; }
        public int Cycle { get; }
        public int Offset { get; }
        public int Value { get; }

        /// <summary>Patrol waypoints; empty when the object doesn't patrol.</summary>
        public TilePos[] Patrol { get; }
    }
}
