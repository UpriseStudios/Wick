using System;
using System.Collections.Generic;
using System.Linq;
using Wick.Sim;

namespace Wick.Bots
{
    /// <summary>
    /// Worst-case vaults for the balance simulator: a LayoutGen layout on a Vault Level's full grid,
    /// filled to most of its build budget with the defences that level unlocks, every one at the
    /// highest upgrade level that Vault Level allows. Only vaults that pass VaultValidator are
    /// returned, so every one is something a player could publish (if they could beat it).
    /// </summary>
    public static class BalanceGen
    {
        private static readonly ObjectType[] Defences =
        {
            ObjectType.DripPipe, ObjectType.SpikeFloor, ObjectType.Bellows, ObjectType.Crate, ObjectType.SnufferBell,
            ObjectType.NightOwl, ObjectType.WaxMoths, ObjectType.SwingAxe, ObjectType.MimicChest, ObjectType.SleepyTroll,
            ObjectType.Fan, ObjectType.Gate, ObjectType.IceTiles, ObjectType.Pit, ObjectType.SirWhiskers,
        };

        /// <summary>A valid worst-case vault for this level, or null after too many tries.</summary>
        public static VaultDef Make(int level, Random rng, string vaultId)
        {
            for (int attempt = 0; attempt < 200; attempt++)
            {
                VaultDef v = TryMake(level, rng, vaultId);
                if (v != null && VaultValidator.Validate(v).Count == 0)
                {
                    return v;
                }
            }

            return null;
        }

        private static VaultDef TryMake(int level, Random rng, string vaultId)
        {
            LayoutGen.Layout layout = LayoutGen.ForLevel(level, rng);
            var objects = new List<ObjectDef>();
            var used = new HashSet<TilePos> { layout.Entrance, layout.Entrance.Step(Dir.N) };
            int id = 1;

            // Treasure in the hoard, braziers on walls.
            List<TilePos> hoard = FloorIn(layout, layout.Hoard);
            int piles = 1 + rng.Next(VaultRules.MaxTreasurePiles(level));
            foreach (TilePos p in hoard.OrderBy(_ => rng.Next()).Take(piles))
            {
                objects.Add(new ObjectDef(id++, ObjectType.Treasure, p, 0, 0, 0, VaultRules.TreasureValue(level), new TilePos[0]));
                used.Add(p);
            }

            for (int b = rng.Next(VaultRules.BrazierLimit(level) + 1); b > 0; b--)
            {
                TilePos? wall = WallBesideFloor(layout, rng, used);
                if (wall.HasValue)
                {
                    objects.Add(new ObjectDef(id++, ObjectType.Brazier, wall.Value, 0, 0, 0, 0, new TilePos[0]));
                    used.Add(wall.Value);
                }
            }

            // Builders guard the way in: most defences go on or beside the shortest route to the hoard.
            HashSet<TilePos> route = RouteTiles(layout);
            ObjectType[] kinds = Defences.Where(k => VaultRules.RequiredLevel(k) <= level).ToArray();
            int budget = VaultRules.Budget(level);
            int spent = 0;
            bool cat = false;
            for (int tries = 0; tries < 120 && spent < budget * 9 / 10; tries++)
            {
                ObjectType type = kinds[rng.Next(kinds.Length)];
                if (type == ObjectType.SirWhiskers && cat)
                {
                    continue;
                }

                int cost = type == ObjectType.IceTiles || type == ObjectType.Pit ? 0 : VaultRules.Cost(type);
                if (spent + cost > budget)
                {
                    continue;
                }

                int before = objects.Count;
                spent += Place(type, layout, objects, used, route, ref id, rng, level, budget - spent);
                cat |= type == ObjectType.SirWhiskers && objects.Count > before;
            }

            DefenceLevels levels = DefenceLevels.Base;
            int cap = DefenceLevels.Cap(level);
            foreach (ObjectType k in kinds)
            {
                if (ObjectTypes.IsUpgradable(k) && cap > 1)
                {
                    levels = levels.With(k, cap);
                }
            }

            objects.Sort((a, b) => a.Id.CompareTo(b.Id));
            return new VaultDef(1, vaultId, 1, level, layout.Width, layout.Height, (TileType[])layout.Tiles.Clone(), layout.Entrance,
                objects.ToArray(), true, layout.Hoard, "crypt", levels);
        }

        /// <summary>Places one defence (or an ice patch, a pit line, or a gate and its lever); returns the build points spent.</summary>
        private static int Place(ObjectType type, LayoutGen.Layout layout, List<ObjectDef> objects, HashSet<TilePos> used,
            HashSet<TilePos> route, ref int id, Random rng, int level, int left)
        {
            switch (type)
            {
                case ObjectType.Bellows:
                {
                    TilePos? wall = WallBesideFloor(layout, rng, used);
                    if (!wall.HasValue)
                    {
                        return 0;
                    }

                    int cycle = ObjectTypes.MinCycle(type) + 45 + rng.Next(4) * 15;
                    objects.Add(new ObjectDef(id++, type, wall.Value, OpenSide(layout, wall.Value), cycle, rng.Next(cycle), 0, new TilePos[0]));
                    used.Add(wall.Value);
                    return VaultRules.Cost(type);
                }

                case ObjectType.IceTiles:
                {
                    // A short patch in a row of some room: 2 to 4 tiles at 3 points each.
                    TileRect room = layout.Rooms[rng.Next(layout.Rooms.Count)];
                    int length = 2 + rng.Next(3);
                    int y = rng.Next(room.MinY, room.MaxY + 1);
                    int x0 = rng.Next(room.MinX, Math.Max(room.MinX + 1, room.MaxX - length + 2));
                    int cost = 0;
                    for (int x = x0; x < x0 + length && x <= room.MaxX; x++)
                    {
                        var p = new TilePos(x, y);
                        if (layout.At(p) == TileType.Floor && !used.Contains(p) && cost + 3 <= left)
                        {
                            layout.Set(p, TileType.Ice);
                            cost += 3;
                        }
                    }

                    return cost;
                }

                case ObjectType.Pit:
                {
                    // A line of 2 to 4 pits across a room outside the hoard, mostly on or beside the route,
                    // as a maze wall: never in or beside a doorway, and kept only if the pit-free walk to
                    // the hoard is still there and long enough. Walls of pits are what players build.
                    var rooms = layout.Rooms.Where(r => !(r.MinX <= layout.Hoard.MaxX && r.MaxX >= layout.Hoard.MinX &&
                        r.MinY <= layout.Hoard.MaxY && r.MaxY >= layout.Hoard.MinY)).ToList();
                    if (rooms.Count == 0)
                    {
                        return 0;
                    }

                    TileRect room = rooms[rng.Next(rooms.Count)];
                    bool across = rng.Next(2) == 0;
                    int length = 2 + rng.Next(3);
                    var start = new TilePos(rng.Next(room.MinX, room.MaxX + 1), rng.Next(room.MinY, room.MaxY + 1));
                    VaultDef doorways = Trial(layout, level);
                    var dug = new List<TilePos>();
                    for (int i = 0; i < length && (dug.Count + 1) * VaultRules.Cost(type) <= left; i++)
                    {
                        var p = across ? new TilePos(start.X + i, start.Y) : new TilePos(start.X, start.Y + i);
                        if (room.Contains(p) && layout.At(p) == TileType.Floor && !used.Contains(p) &&
                            !VaultValidator.InOrBesideDoorway(doorways, p))
                        {
                            dug.Add(p);
                        }
                    }

                    if (dug.Count == 0 || (!dug.Any(route.Contains) && rng.Next(4) > 0))
                    {
                        return 0;
                    }

                    foreach (TilePos p in dug)
                    {
                        layout.Set(p, TileType.Pit);
                    }

                    if (VaultValidator.RouteTiles(Trial(layout, level)) < VaultRules.MinRouteTiles(layout.Width, layout.Height))
                    {
                        foreach (TilePos p in dug)
                        {
                            layout.Set(p, TileType.Floor);
                        }

                        return 0;
                    }

                    foreach (TilePos p in dug)
                    {
                        used.Add(p);
                    }

                    return dug.Count * VaultRules.Cost(type);
                }

                case ObjectType.Gate:
                {
                    // A gate in a door between bands, with a lever near it on each side: gates close
                    // behind Wick, and the owner's clear-check has to get back out (levers are free).
                    List<TilePos> doors = Tiles(layout, TileType.Door).Where(d => !used.Contains(d) &&
                        layout.At(d.Step(Dir.N)) != TileType.Wall && layout.At(d.Step(Dir.S)) != TileType.Wall).ToList();
                    if (doors.Count == 0)
                    {
                        return 0;
                    }

                    TilePos door = doors[rng.Next(doors.Count)];
                    List<TilePos> Near(Func<TileRect, bool> side) => layout.Rooms.Where(side).SelectMany(r => FloorIn(layout, r))
                        .Where(p => !used.Contains(p) && Math.Abs(p.X - door.X) + Math.Abs(p.Y - door.Y) <= 6).ToList();
                    List<TilePos> below = Near(r => r.MinY > door.Y);
                    List<TilePos> above = Near(r => r.MaxY < door.Y);
                    if (below.Count == 0 || above.Count == 0)
                    {
                        return 0;
                    }

                    int gate = id++;
                    objects.Add(new ObjectDef(gate, ObjectType.Gate, door, 0, 0, 0, 0, new TilePos[0]));
                    used.Add(door);
                    foreach (List<TilePos> side in new[] { below, above })
                    {
                        TilePos lever = side[rng.Next(side.Count)];
                        objects.Add(new ObjectDef(id++, ObjectType.Lever, lever, 0, 0, 0, 0, new TilePos[0], new[] { gate }));
                        used.Add(lever);
                    }

                    return VaultRules.Cost(ObjectType.Gate);
                }
            }

            // Instant-snuff bells and mimics stay clear of doorways (so a vault never forces one); the
            // rest may guard them. Three times in four a defence goes on the route.
            bool clearOfDoors = type == ObjectType.SnufferBell || type == ObjectType.MimicChest;
            List<TilePos> floor = AllFloor(layout).Where(p => !used.Contains(p) && !(clearOfDoors && NextToDoor(layout, p))).ToList();
            List<TilePos> onRoute = floor.Where(route.Contains).ToList();
            if (onRoute.Count > 0 && rng.Next(4) > 0)
            {
                floor = onRoute;
            }

            if (type == ObjectType.SleepyTroll)
            {
                // Trolls block doorways: tiptoe past.
                List<TilePos> doors = Tiles(layout, TileType.Door).Where(d => !used.Contains(d)).ToList();
                floor = doors.Count > 0 ? doors : floor;
            }

            if (floor.Count == 0)
            {
                return 0;
            }

            TilePos at = floor[rng.Next(floor.Count)];
            int c = ObjectTypes.IsTimedTrap(type) ? ObjectTypes.MinCycle(type) + 30 + rng.Next(4) * 15 : 0;
            TilePos[] patrol = type == ObjectType.NightOwl ? Patrol(layout, at, rng) : new TilePos[0];
            objects.Add(new ObjectDef(id++, type, at, rng.Next(4), c, c > 0 ? rng.Next(c) : 0, 0, patrol));
            used.Add(at);
            return VaultRules.Cost(type);
        }

        /// <summary>The layout alone (no objects) as a vault, for tile rules such as doorways and the route.</summary>
        private static VaultDef Trial(LayoutGen.Layout layout, int level)
        {
            return new VaultDef(1, "trial", 1, level, layout.Width, layout.Height, (TileType[])layout.Tiles.Clone(), layout.Entrance,
                new ObjectDef[0], true, layout.Hoard);
        }

        /// <summary>The shortest walk from the entrance to the hoard, and every tile beside it.</summary>
        private static HashSet<TilePos> RouteTiles(LayoutGen.Layout layout)
        {
            var prev = new Dictionary<TilePos, TilePos> { [layout.Entrance] = layout.Entrance };
            var queue = new Queue<TilePos>();
            queue.Enqueue(layout.Entrance);
            TilePos end = layout.Entrance;
            while (queue.Count > 0)
            {
                TilePos p = queue.Dequeue();
                if (layout.Hoard.Contains(p))
                {
                    end = p;
                    break;
                }

                for (int d = 0; d < 4; d++)
                {
                    TilePos n = p.Step((Dir)d);
                    if (layout.At(n) != TileType.Wall && !prev.ContainsKey(n))
                    {
                        prev[n] = p;
                        queue.Enqueue(n);
                    }
                }
            }

            var route = new HashSet<TilePos>();
            for (TilePos p = end; ; p = prev[p])
            {
                route.Add(p);
                for (int d = 0; d < 4; d++)
                {
                    route.Add(p.Step((Dir)d));
                }

                if (p == layout.Entrance)
                {
                    break;
                }
            }

            return route;
        }

        private static TilePos[] Patrol(LayoutGen.Layout layout, TilePos from, Random rng)
        {
            TileRect room = layout.Rooms.FirstOrDefault(r => r.Contains(from));
            List<TilePos> tiles = FloorIn(layout, room);
            return tiles.Count == 0 ? new TilePos[0] : new[] { from, tiles[rng.Next(tiles.Count)] };
        }

        private static bool NextToDoor(LayoutGen.Layout layout, TilePos p)
        {
            for (int d = 0; d < 4; d++)
            {
                if (layout.At(p.Step((Dir)d)) == TileType.Door)
                {
                    return true;
                }
            }

            return layout.At(p) == TileType.Door;
        }

        private static List<TilePos> FloorIn(LayoutGen.Layout layout, TileRect r)
        {
            var tiles = new List<TilePos>();
            for (int y = r.MinY; y <= r.MaxY; y++)
            {
                for (int x = r.MinX; x <= r.MaxX; x++)
                {
                    var p = new TilePos(x, y);
                    if (layout.At(p) == TileType.Floor)
                    {
                        tiles.Add(p);
                    }
                }
            }

            return tiles;
        }

        private static IEnumerable<TilePos> AllFloor(LayoutGen.Layout layout) => Tiles(layout, TileType.Floor);

        private static List<TilePos> Tiles(LayoutGen.Layout layout, TileType type)
        {
            var tiles = new List<TilePos>();
            for (int y = 0; y < layout.Height; y++)
            {
                for (int x = 0; x < layout.Width; x++)
                {
                    if (layout.At(new TilePos(x, y)) == type)
                    {
                        tiles.Add(new TilePos(x, y));
                    }
                }
            }

            return tiles;
        }

        private static TilePos? WallBesideFloor(LayoutGen.Layout layout, Random rng, HashSet<TilePos> used)
        {
            List<TilePos> walls = Tiles(layout, TileType.Wall).Where(w => !used.Contains(w) && OpenSide(layout, w) >= 0).ToList();
            return walls.Count == 0 ? (TilePos?)null : walls[rng.Next(walls.Count)];
        }

        private static int OpenSide(LayoutGen.Layout layout, TilePos p)
        {
            for (int d = 0; d < 4; d++)
            {
                if (layout.At(p.Step((Dir)d)) == TileType.Floor)
                {
                    return d;
                }
            }

            return -1;
        }
    }
}
