namespace Wick.Sim
{
    /// <summary>
    /// Immutable vault layout. Tiles are row-major from the top-left: index = y * Width + x.
    /// Objects are sorted by Id.
    /// </summary>
    public sealed class VaultDef
    {
        private readonly TileType[] _tiles;
        private readonly ObjectDef[] _objects;

        public VaultDef(int schema, string vaultId, int version, int vaultLevel, int width, int height,
            TileType[] tiles, TilePos entrance, ObjectDef[] objects)
        {
            Schema = schema;
            VaultId = vaultId;
            Version = version;
            VaultLevel = vaultLevel;
            Width = width;
            Height = height;
            _tiles = tiles;
            Entrance = entrance;
            _objects = objects;
        }

        public int Schema { get; }
        public string VaultId { get; }
        public int Version { get; }
        public int VaultLevel { get; }
        public int Width { get; }
        public int Height { get; }
        public TilePos Entrance { get; }

        public int ObjectCount => _objects.Length;

        public ObjectDef GetObject(int index)
        {
            return _objects[index];
        }

        public bool InBounds(TilePos p)
        {
            return p.X >= 0 && p.Y >= 0 && p.X < Width && p.Y < Height;
        }

        /// <summary>Tile at p; out-of-bounds positions read as Wall.</summary>
        public TileType TileAt(TilePos p)
        {
            return InBounds(p) ? _tiles[p.Y * Width + p.X] : TileType.Wall;
        }

        public bool IsWalkable(TilePos p)
        {
            // TODO M2: pits (fall = hit, step 5) and ice (sliding, step 2) are walkable floor for now.
            return TileAt(p) != TileType.Wall;
        }
    }
}
