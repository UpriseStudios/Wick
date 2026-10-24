using System;
using System.Collections.Generic;
using System.Linq;
using Wick.Sim;

namespace Wick.Bots
{
    /// <summary>
    /// Fills a room template with traps and guardians within a Vault Level's budget. Deterministic
    /// for a given Random, so a tool run with a fixed seed gives the same vaults every time.
    /// </summary>
    public static class VaultGen
    {
        public static VaultDef Fill(VaultDef t, int level, Random rng, string vaultId)
        {
            var tiles = new TileType[t.Width * t.Height];
            for (int i = 0; i < tiles.Length; i++)
            {
                tiles[i] = t.TileAtIndex(i);
            }

            var objects = new List<ObjectDef>();
            int id = 1;
            var used = new HashSet<TilePos>();

            // Treasure: the template's pile plus extra piles in the hoard.
            List<TilePos> hoard = Floor(t).Where(p => t.Hoard.Contains(p)).ToList();
            int piles = 1 + rng.Next(VaultRules.MaxTreasurePiles(level));
            foreach (TilePos p in Shuffle(hoard, rng).Take(piles))
            {
                objects.Add(Obj(id++, ObjectType.Treasure, p, 0, 0, VaultRules.TreasureValue(level)));
                used.Add(p);
            }

            if (rng.Next(2) == 0)
            {
                TilePos? wall = WallBesideFloor(t, rng);
                if (wall.HasValue)
                {
                    objects.Add(Obj(id++, ObjectType.Brazier, wall.Value, 0, 0, 0));
                }
            }

            int budget = VaultRules.Budget(level);
            int spent = 0;
            List<TilePos> floor = Floor(t).Where(p => p != t.Entrance && !used.Contains(p)).ToList();
            var kinds = new List<ObjectType> { ObjectType.DripPipe, ObjectType.SpikeFloor, ObjectType.Bellows, ObjectType.Crate };
            if (level >= 2)
            {
                kinds.Add(ObjectType.SnufferBell);
                kinds.Add(ObjectType.NightOwl);
            }

            if (level >= 3)
            {
                kinds.Add(ObjectType.WaxMoths);
            }

            int guardians = 0;
            for (int tries = 0; tries < 40 && floor.Count > 0; tries++)
            {
                ObjectType type = kinds[rng.Next(kinds.Count)];
                int cost = VaultRules.Cost(type);
                if (spent + cost > budget || (ObjectTypes.IsGuardian(type) && guardians >= 1))
                {
                    continue;
                }

                if (type == ObjectType.Bellows)
                {
                    TilePos? wall = WallBesideFloor(t, rng);
                    if (!wall.HasValue || objects.Any(o => o.Pos == wall.Value))
                    {
                        continue;
                    }

                    int gust = 60 + rng.Next(4) * 15;
                    objects.Add(new ObjectDef(id++, type, wall.Value, OpenSide(t, wall.Value), gust, rng.Next(gust), 0, new TilePos[0]));
                    spent += cost;
                    continue;
                }

                TilePos at = floor[rng.Next(floor.Count)];
                floor.Remove(at);
                int cycle = type == ObjectType.DripPipe ? 45 + rng.Next(3) * 15 : type == ObjectType.SpikeFloor ? 60 + rng.Next(3) * 15 : 0;
                TilePos[] patrol = type == ObjectType.NightOwl ? Patrol(t, at) : new TilePos[0];
                objects.Add(new ObjectDef(id++, type, at, rng.Next(4), cycle, cycle > 0 ? rng.Next(cycle) : 0, 0, patrol));
                spent += cost;
                guardians += ObjectTypes.IsGuardian(type) ? 1 : 0;
            }

            return new VaultDef(1, vaultId, 1, level, t.Width, t.Height, tiles, t.Entrance, objects.ToArray(), t.HasHoard, t.Hoard, t.Theme);
        }

        private static ObjectDef Obj(int id, ObjectType type, TilePos p, int rot, int cycle, int value)
        {
            return new ObjectDef(id, type, p, rot, cycle, 0, value, new TilePos[0]);
        }

        private static IEnumerable<TilePos> Floor(VaultDef v)
        {
            for (int y = 0; y < v.Height; y++)
            {
                for (int x = 0; x < v.Width; x++)
                {
                    var p = new TilePos(x, y);
                    if (v.TileAt(p) == TileType.Floor)
                    {
                        yield return p;
                    }
                }
            }
        }

        private static TilePos? WallBesideFloor(VaultDef v, Random rng)
        {
            var walls = new List<TilePos>();
            for (int y = 0; y < v.Height; y++)
            {
                for (int x = 0; x < v.Width; x++)
                {
                    var p = new TilePos(x, y);
                    if (v.TileAt(p) == TileType.Wall && OpenSide(v, p) >= 0)
                    {
                        walls.Add(p);
                    }
                }
            }

            return walls.Count == 0 ? (TilePos?)null : walls[rng.Next(walls.Count)];
        }

        private static int OpenSide(VaultDef v, TilePos p)
        {
            for (int d = 0; d < 4; d++)
            {
                if (v.TileAt(p.Step((Dir)d)) == TileType.Floor)
                {
                    return d;
                }
            }

            return -1;
        }

        /// <summary>A short back-and-forth along the row, on open floor.</summary>
        private static TilePos[] Patrol(VaultDef v, TilePos from)
        {
            for (int dx = 3; dx >= 1; dx--)
            {
                foreach (int sign in new[] { 1, -1 })
                {
                    var to = new TilePos(from.X + sign * dx, from.Y);
                    if (v.TileAt(to) == TileType.Floor)
                    {
                        return new[] { from, to };
                    }
                }
            }

            return new TilePos[0];
        }

        private static List<T> Shuffle<T>(List<T> items, Random rng)
        {
            var list = new List<T>(items);
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }

            return list;
        }
    }
}
