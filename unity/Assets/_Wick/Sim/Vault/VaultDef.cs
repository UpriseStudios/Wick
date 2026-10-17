namespace Wick.Sim
{
    /// <summary>Inclusive tile rectangle.</summary>
    public readonly struct TileRect
    {
        public readonly int MinX;
        public readonly int MinY;
        public readonly int MaxX;
        public readonly int MaxY;

        public TileRect(int minX, int minY, int maxX, int maxY)
        {
            MinX = minX;
            MinY = minY;
            MaxX = maxX;
            MaxY = maxY;
        }

        public bool Contains(TilePos p)
        {
            return p.X >= MinX && p.X <= MaxX && p.Y >= MinY && p.Y <= MaxY;
        }
    }

    /// <summary>
    /// Immutable vault layout. Tiles are row-major from the top-left: index = y * Width + x.
    /// Objects are sorted by Id.
    /// </summary>
    public sealed class VaultDef
    {
        private readonly TileType[] _tiles;
        private readonly ObjectDef[] _objects;
        private readonly bool[] _blocked;

        public VaultDef(int schema, string vaultId, int version, int vaultLevel, int width, int height,
            TileType[] tiles, TilePos entrance, ObjectDef[] objects, bool hasHoard, TileRect hoard)
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
            HasHoard = hasHoard;
            Hoard = hoard;

            _blocked = new bool[tiles.Length];
            for (int i = 0; i < tiles.Length; i++)
            {
                _blocked[i] = tiles[i] == TileType.Wall;
            }

            foreach (ObjectDef o in objects)
            {
                if (o.Type == ObjectType.Crate && InBounds(o.Pos))
                {
                    _blocked[o.Pos.Y * width + o.Pos.X] = true;
                }
            }
        }

        public int Schema { get; }
        public string VaultId { get; }
        public int Version { get; }
        public int VaultLevel { get; }
        public int Width { get; }
        public int Height { get; }
        public TilePos Entrance { get; }

        /// <summary>Whether the vault marks a hoard room (star 1).</summary>
        public bool HasHoard { get; }

        public TileRect Hoard { get; }

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

        /// <summary>Walls, crates and off-grid block movement.</summary>
        public bool IsWalkable(TilePos p)
        {
            // Pits and ice are walkable floor in the vertical slice (no rules for them yet).
            return InBounds(p) && !_blocked[p.Y * Width + p.X];
        }

        /// <summary>Walls, crates and off-grid stop a Bellows gust.</summary>
        public bool BlocksGust(TilePos p)
        {
            return !IsWalkable(p);
        }
    }
}
