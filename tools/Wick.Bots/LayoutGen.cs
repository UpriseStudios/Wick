using System;
using System.Collections.Generic;
using Wick.Sim;

namespace Wick.Bots
{
    /// <summary>
    /// Procedural vault layouts for any grid in the Vault Level table: rooms in horizontal bands,
    /// the entrance in the middle of the bottom wall, the hoard in the top band. Each band is one or
    /// two rooms (a door in the wall between), and bands connect through one or two doors. Every
    /// floor tile is reachable from the entrance. Deterministic for a given Random.
    /// </summary>
    public static class LayoutGen
    {
        public sealed class Layout
        {
            public int Width;
            public int Height;
            public TileType[] Tiles;
            public TilePos Entrance;
            public TileRect Hoard;

            /// <summary>Each room's floor rectangle, bottom (entrance) room first, hoard last.</summary>
            public List<TileRect> Rooms = new List<TileRect>();

            public TileType At(TilePos p) => p.X >= 0 && p.Y >= 0 && p.X < Width && p.Y < Height ? Tiles[p.Y * Width + p.X] : TileType.Wall;

            public void Set(TilePos p, TileType t) => Tiles[p.Y * Width + p.X] = t;
        }

        /// <summary>A layout for a vault at this Vault Level, on that level's full grid.</summary>
        public static Layout ForLevel(int level, Random rng)
        {
            int width = VaultRules.GridWidth(level);
            int height = VaultRules.GridHeight(level);

            // Rooms (guide) from the table: 2-3 at level 1, up to 6-8 at level 10, as 2-4 bands.
            int bands = Math.Min(4, 2 + (level - 1) / 3);
            var layout = new Layout { Width = width, Height = height, Tiles = new TileType[width * height] };
            for (int i = 0; i < layout.Tiles.Length; i++)
            {
                layout.Tiles[i] = TileType.Wall;
            }

            // Interior rows 1 .. height-2 (the bottom row holds the entrance), split into bands with a
            // one-tile wall between them.
            int interior = height - 2;
            int bandHeight = (interior - (bands - 1)) / bands;
            int y = height - 2;
            var bandRooms = new List<List<TileRect>>();
            for (int b = 0; b < bands; b++)
            {
                int maxY = y;
                int minY = b == bands - 1 ? 1 : y - bandHeight + 1;
                var rooms = new List<TileRect>();
                bool split = b > 0 && b < bands - 1 ? rng.Next(3) > 0 : b == 0 && rng.Next(3) == 0;
                if (split)
                {
                    int wallX = width / 2 + rng.Next(-1, 2);
                    rooms.Add(new TileRect(1, minY, wallX - 1, maxY));
                    rooms.Add(new TileRect(wallX + 1, minY, width - 2, maxY));
                }
                else
                {
                    rooms.Add(new TileRect(1, minY, width - 2, maxY));
                }

                foreach (TileRect r in rooms)
                {
                    Carve(layout, r);
                    layout.Rooms.Add(r);
                }

                if (split)
                {
                    // A door in the wall between the two rooms.
                    int doorY = rng.Next(minY, maxY + 1);
                    layout.Set(new TilePos(rooms[0].MaxX + 1, doorY), TileType.Door);
                }

                bandRooms.Add(rooms);
                y = minY - 2;
            }

            // Doors between bands: each room in the upper band gets one into the band below.
            for (int b = 1; b < bands; b++)
            {
                List<TileRect> below = bandRooms[b - 1];
                foreach (TileRect upper in bandRooms[b])
                {
                    int wallY = upper.MaxY + 1;
                    for (int tries = 0; tries < 30; tries++)
                    {
                        int x = rng.Next(upper.MinX, upper.MaxX + 1);
                        if (below.Exists(r => x >= r.MinX && x <= r.MaxX))
                        {
                            layout.Set(new TilePos(x, wallY), TileType.Door);
                            break;
                        }
                    }
                }
            }

            layout.Entrance = new TilePos(width / 2, height - 1);
            layout.Set(layout.Entrance, TileType.Floor);
            if (layout.At(layout.Entrance.Step(Dir.N)) == TileType.Wall)
            {
                layout.Set(layout.Entrance.Step(Dir.N), TileType.Floor);
            }

            layout.Hoard = bandRooms[bands - 1][rng.Next(bandRooms[bands - 1].Count)];
            return layout;
        }

        private static void Carve(Layout layout, TileRect r)
        {
            for (int y = r.MinY; y <= r.MaxY; y++)
            {
                for (int x = r.MinX; x <= r.MaxX; x++)
                {
                    layout.Set(new TilePos(x, y), TileType.Floor);
                }
            }
        }
    }
}
