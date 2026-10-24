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
        private readonly DecorationDef[] _decorations;

        public VaultDef(int schema, string vaultId, int version, int vaultLevel, int width, int height,
            TileType[] tiles, TilePos entrance, ObjectDef[] objects, bool hasHoard, TileRect hoard, string theme = "crypt",
            DefenceLevels defenceLevels = null, DecorationDef[] decorations = null)
        {
            Theme = theme;
            DefenceLevels = defenceLevels ?? DefenceLevels.Base;
            _decorations = decorations ?? new DecorationDef[0];
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
        public string Theme { get; }
        public string VaultId { get; }
        public int Version { get; }
        public int VaultLevel { get; }
        public int Width { get; }
        public int Height { get; }
        public TilePos Entrance { get; }

        /// <summary>The owner's defence upgrade levels when the vault was published.</summary>
        public DefenceLevels DefenceLevels { get; }

        /// <summary>The upgrade level of a type in this vault.</summary>
        public int LevelOf(ObjectType type)
        {
            return DefenceLevels.Level(type);
        }

        /// <summary>A copy of this vault with other defence levels (Cloud Code stamps the owner's on publish).</summary>
        public VaultDef WithDefenceLevels(DefenceLevels levels)
        {
            return new VaultDef(Schema, VaultId, Version, VaultLevel, Width, Height, (TileType[])_tiles.Clone(), Entrance,
                (ObjectDef[])_objects.Clone(), HasHoard, Hoard, Theme, levels, (DecorationDef[])_decorations.Clone());
        }

        /// <summary>Cosmetic props. The raid sim never reads these.</summary>
        public int DecorationCount => _decorations.Length;

        public DecorationDef GetDecoration(int index)
        {
            return _decorations[index];
        }

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

        /// <summary>Raw tile type at row-major index i.</summary>
        public TileType TileAtIndex(int i)
        {
            return _tiles[i];
        }

        /// <summary>Tile at p; out-of-bounds positions read as Wall.</summary>
        public TileType TileAt(TilePos p)
        {
            return InBounds(p) ? _tiles[p.Y * Width + p.X] : TileType.Wall;
        }

        /// <summary>Walls, crates and off-grid block movement.</summary>
        public bool IsWalkable(TilePos p)
        {
            // Pits are walkable floor for now (no rules for them yet); ice is walkable but slippery.
            return InBounds(p) && !_blocked[p.Y * Width + p.X];
        }

        /// <summary>Walls, crates and off-grid stop a Bellows gust.</summary>
        public bool BlocksGust(TilePos p)
        {
            return !IsWalkable(p);
        }
    }
}
