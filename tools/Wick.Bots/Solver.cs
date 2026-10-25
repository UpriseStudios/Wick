using System;
using System.Collections.Generic;
using System.Linq;
using Wick.Sim;

namespace Wick.Bots
{
    /// <summary>
    /// Finds an escape: the shortest path to the nearest hoard pile and back, walked one dash at a
    /// time, waiting before each step for the first moment it can be taken without losing a flicker.
    /// </summary>
    public static class Solver
    {
        private const int MaxWait = 150;

        public static InputLog Escape(VaultDef vault, int seed)
        {
            List<TilePos> treasure = new List<TilePos>();
            for (int i = 0; i < vault.ObjectCount; i++)
            {
                ObjectDef o = vault.GetObject(i);
                if (o.Type == ObjectType.Treasure && vault.Hoard.Contains(o.Pos))
                {
                    treasure.Add(o.Pos);
                }
            }

            List<TilePos> there = treasure.Select(p => Path(vault, vault.Entrance, p)).Where(p => p != null)
                .OrderBy(p => p.Count).FirstOrDefault();
            if (there == null)
            {
                return null;
            }

            List<TilePos> back = Path(vault, there[there.Count - 1], vault.Entrance);
            var route = new List<TilePos>(there);
            route.AddRange(back.Skip(1));

            var commands = new List<Command>();
            int tick = 0;
            for (int i = 1; i < route.Count; i++)
            {
                Dir dir = DirBetween(route[i - 1], route[i]);
                bool stepped = false;
                for (int wait = 0; wait <= MaxWait && !stepped; wait++)
                {
                    var trial = new List<Command>(commands) { Command.Dash(tick + wait, dir) };
                    int arrived = TryStep(vault, seed, trial, route[i], i == route.Count - 1);
                    if (arrived >= 0)
                    {
                        commands = trial;
                        tick = arrived;
                        stepped = true;
                    }
                }

                if (!stepped || tick >= RaidParams.Default.RaidTicks)
                {
                    return null;
                }
            }

            return new InputLog(commands.ToArray());
        }

        /// <summary>The tick Wick stands on target with every flicker he had, or -1.</summary>
        private static int TryStep(VaultDef vault, int seed, List<Command> commands, TilePos target, bool last)
        {
            var sim = new RaidSim(vault, seed, new InputLog(commands.ToArray()));
            int lastTick = commands[commands.Count - 1].Tick;
            int flickersBefore = -1;
            while (!sim.IsOver)
            {
                if (sim.State.Tick == lastTick)
                {
                    flickersBefore = sim.State.Wick.Flickers;
                }

                sim.Step();
                if (flickersBefore >= 0 && sim.State.Wick.Flickers < flickersBefore)
                {
                    return -1;
                }

                WickState w = sim.State.Wick;
                if (sim.State.Tick > lastTick && w.Tile == target && !w.Moving)
                {
                    // Must also survive a moment of standing there.
                    for (int i = 0; i < 6 && !sim.IsOver; i++)
                    {
                        sim.Step();
                        if (sim.State.Wick.Flickers < flickersBefore)
                        {
                            return -1;
                        }
                    }

                    return sim.State.Tick - 6;
                }

                if (sim.State.Tick > lastTick + 60)
                {
                    return -1;
                }
            }

            return last && sim.State.Outcome == Outcome.Escaped ? sim.State.Tick : -1;
        }

        private static List<TilePos> Path(VaultDef v, TilePos from, TilePos to)
        {
            var prev = new Dictionary<TilePos, TilePos>();
            var queue = new Queue<TilePos>();
            queue.Enqueue(from);
            prev[from] = from;
            while (queue.Count > 0)
            {
                TilePos p = queue.Dequeue();
                if (p == to)
                {
                    var path = new List<TilePos> { p };
                    while (p != from)
                    {
                        p = prev[p];
                        path.Add(p);
                    }

                    path.Reverse();
                    return path;
                }

                for (int d = 0; d < 4; d++)
                {
                    TilePos n = p.Step((Dir)d);
                    if (v.IsWalkable(n) && !prev.ContainsKey(n) && !IsBell(v, n) && v.TileAt(n) != TileType.Pit &&
                        !Bot.SlidesIntoPit(v, v.IsWalkable, n, (Dir)d))
                    {
                        prev[n] = p;
                        queue.Enqueue(n);
                    }
                }
            }

            return null;
        }

        private static bool IsBell(VaultDef v, TilePos p)
        {
            for (int i = 0; i < v.ObjectCount; i++)
            {
                if (v.GetObject(i).Type == ObjectType.SnufferBell && v.GetObject(i).Pos == p)
                {
                    return true;
                }
            }

            return false;
        }

        private static Dir DirBetween(TilePos a, TilePos b)
        {
            if (b.Y < a.Y) return Dir.N;
            if (b.X > a.X) return Dir.E;
            if (b.Y > a.Y) return Dir.S;
            return Dir.W;
        }
    }
}
