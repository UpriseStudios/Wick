using System.Collections.Generic;
using UnityEngine;
using Wick.Sim;

namespace Wick.Game
{
    /// <summary>
    /// Where the atmosphere goes (docs/m10-2-lighting.md, layer 7): a few fog cards and light shafts
    /// per room, placed from the layout alone so no vault needs hand-placed markers. A room is a
    /// connected floor area (pits and ice included) split at door tiles. Deterministic.
    /// Presentation only.
    /// </summary>
    public static class Atmosphere
    {
        /// <summary>The vault's rooms, each a list of tiles in row-major order; rooms ordered by their first tile.</summary>
        public static List<List<TilePos>> Rooms(VaultDef vault)
        {
            var room = new int[vault.Width * vault.Height];
            var rooms = new List<List<TilePos>>();
            for (int i = 0; i < room.Length; i++)
            {
                room[i] = -1;
            }

            for (int y = 0; y < vault.Height; y++)
            {
                for (int x = 0; x < vault.Width; x++)
                {
                    var start = new TilePos(x, y);
                    if (!InRoom(vault, start) || room[y * vault.Width + x] >= 0)
                    {
                        continue;
                    }

                    var tiles = new List<TilePos>();
                    var queue = new Queue<TilePos>();
                    room[y * vault.Width + x] = rooms.Count;
                    queue.Enqueue(start);
                    while (queue.Count > 0)
                    {
                        TilePos p = queue.Dequeue();
                        tiles.Add(p);
                        for (int d = 0; d < 4; d++)
                        {
                            TilePos n = p.Step((Dir)d);
                            if (InRoom(vault, n) && room[n.Y * vault.Width + n.X] < 0)
                            {
                                room[n.Y * vault.Width + n.X] = rooms.Count;
                                queue.Enqueue(n);
                            }
                        }
                    }

                    tiles.Sort((a, b) => a.Y != b.Y ? a.Y.CompareTo(b.Y) : a.X.CompareTo(b.X));
                    rooms.Add(tiles);
                }
            }

            return rooms;
        }

        /// <summary>
        /// Up to `perRoom` fog spots in each room: its pits first (fog pools in them), then its far
        /// corners (floor with walls on two or more sides), farthest from the entrance first.
        /// </summary>
        public static List<TilePos> FogSpots(VaultDef vault, List<List<TilePos>> rooms, int perRoom)
        {
            var spots = new List<TilePos>();
            if (perRoom <= 0)
            {
                return spots;
            }

            foreach (List<TilePos> tiles in rooms)
            {
                var candidates = new List<TilePos>();
                foreach (TilePos p in tiles)
                {
                    if (vault.TileAt(p) == TileType.Pit)
                    {
                        candidates.Add(p);
                    }
                }

                var corners = new List<TilePos>();
                foreach (TilePos p in tiles)
                {
                    if (vault.TileAt(p) == TileType.Floor && p != vault.Entrance && WallsAround(vault, p) >= 2)
                    {
                        corners.Add(p);
                    }
                }

                corners.Sort((a, b) =>
                {
                    int byDistance = Far(vault, b).CompareTo(Far(vault, a));
                    return byDistance != 0 ? byDistance : a.Y != b.Y ? a.Y.CompareTo(b.Y) : a.X.CompareTo(b.X);
                });
                candidates.AddRange(corners);
                for (int i = 0; i < candidates.Count && i < perRoom; i++)
                {
                    spots.Add(candidates[i]);
                }
            }

            return spots;
        }

        /// <summary>
        /// Light shafts: up to `perRoom` in each room, falling from a high grate in the room's back
        /// (north) wall onto the floor below it, spread across the wall from its middle out. Each is
        /// the wall tile with the grate; the shaft lands on the tile south of it.
        /// </summary>
        public static List<TilePos> ShaftWalls(VaultDef vault, List<List<TilePos>> rooms, int perRoom)
        {
            var walls = new List<TilePos>();
            if (perRoom <= 0)
            {
                return walls;
            }

            foreach (List<TilePos> tiles in rooms)
            {
                var back = new List<TilePos>();
                int minX = int.MaxValue, maxX = int.MinValue;
                foreach (TilePos p in tiles)
                {
                    minX = Mathf.Min(minX, p.X);
                    maxX = Mathf.Max(maxX, p.X);
                    TilePos above = p.Step(Dir.N);
                    if (vault.TileAt(p) == TileType.Floor && vault.TileAt(above) == TileType.Wall && vault.InBounds(above))
                    {
                        back.Add(above);
                    }
                }

                float middle = (minX + maxX) * 0.5f;
                back.Sort((a, b) =>
                {
                    int byMiddle = Mathf.Abs(a.X - middle).CompareTo(Mathf.Abs(b.X - middle));
                    return byMiddle != 0 ? byMiddle : a.Y != b.Y ? a.Y.CompareTo(b.Y) : a.X.CompareTo(b.X);
                });

                var chosen = new List<TilePos>();
                foreach (TilePos w in back)
                {
                    if (chosen.Count >= perRoom)
                    {
                        break;
                    }

                    bool spaced = true;
                    foreach (TilePos c in chosen)
                    {
                        spaced &= Mathf.Abs(c.X - w.X) >= 3;
                    }

                    if (spaced)
                    {
                        chosen.Add(w);
                    }
                }

                walls.AddRange(chosen);
            }

            return walls;
        }

        /// <summary>How many of a profile's per-room count a quality tier keeps (rounding, at least 1 when any).</summary>
        public static int Scaled(int perRoom, float share)
        {
            return share <= 0f || perRoom <= 0 ? 0 : Mathf.Max(1, Mathf.RoundToInt(perRoom * share));
        }

        private static bool InRoom(VaultDef vault, TilePos p)
        {
            TileType t = vault.TileAt(p);
            return vault.InBounds(p) && t != TileType.Wall && t != TileType.Door;
        }

        private static int WallsAround(VaultDef vault, TilePos p)
        {
            int n = 0;
            for (int d = 0; d < 4; d++)
            {
                n += vault.TileAt(p.Step((Dir)d)) == TileType.Wall ? 1 : 0;
            }

            return n;
        }

        private static int Far(VaultDef vault, TilePos p)
        {
            return Mathf.Abs(p.X - vault.Entrance.X) + Mathf.Abs(p.Y - vault.Entrance.Y);
        }
    }
}
