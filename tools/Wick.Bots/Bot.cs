using System;
using System.Collections.Generic;
using Wick.Sim;

namespace Wick.Bots
{
    /// <summary>
    /// A heuristic raider for the balance simulator: a decent player, not a perfect one. It plays a
    /// live RaidSim and decides only when Wick is at rest. Each decision picks a goal (the nearest
    /// treasure left, hoard first; home once it has loot and time is short or it has enough; a lever
    /// when a closed gate is in the way), takes the first step of a shortest path that avoids known
    /// hazards, and times that step by trying it on clones after 0 to 45 ticks of waiting, taking the
    /// first that loses no flicker. Around sleeping guardians it tiptoes, it throws a decoy at a
    /// chasing guardian, and it freezes while Sir Whiskers stalks.
    /// </summary>
    public sealed class Bot
    {
        private const int MaxWait = 45;
        private const int StepHorizon = 24;

        public RaidResult Play(VaultDef vault, int seed, RaidParams raidParams, out InputLog log)
        {
            var sim = new RaidSim(vault, seed, InputLog.Empty, raidParams);
            int frozen = 0;
            while (!sim.IsOver)
            {
                WickState w = sim.State.Wick;
                if (w.Moving)
                {
                    sim.Step();
                    continue;
                }

                SetTiptoe(sim, vault, WantsTiptoe(sim, vault));
                if (w.DecoysLeft > 0 && w.DecoyTicksLeft == 0 && GuardianChasingNear(sim, 3))
                {
                    sim.Submit(Command.Decoy(sim.State.Tick));
                }

                // Sir Whiskers stalks only moving flames: hold still and he loses interest.
                if (frozen < 50 && WhiskersStalkingNear(sim, 3))
                {
                    frozen++;
                    sim.Step();
                    continue;
                }

                frozen = 0;
                TilePos goal = Goal(sim, vault, raidParams);
                Dir? dir = FirstStep(sim, vault, goal);
                if (dir == null)
                {
                    sim.Step(); // nowhere to go yet (a gate may open, a guardian may move)
                    continue;
                }

                int wait = SafeWait(sim, dir.Value);
                for (int i = 0; i < wait && !sim.IsOver; i++)
                {
                    sim.Step();
                }

                if (!sim.IsOver && !sim.State.Wick.Moving)
                {
                    sim.Submit(Command.Dash(sim.State.Tick, dir.Value));
                    sim.Step();
                }
            }

            log = sim.RecordedLog;
            return sim.GetResult();
        }

        // ---- Goals ----------------------------------------------------------------------------

        private static TilePos Goal(RaidSim sim, VaultDef vault, RaidParams p)
        {
            WickState w = sim.State.Wick;
            int left = p.RaidTicks - sim.State.Tick;
            int home = Distance(sim, vault, w.Tile, vault.Entrance);
            long total = Scoring.TotalTreasure(vault);
            bool enough = w.CarriedPiles > 0 && (w.CarriedTreasure * 2 >= total || left < home * 12 + 240);
            if (enough || left < home * 12 + 120)
            {
                return vault.Entrance;
            }

            TilePos best = vault.Entrance;
            int bestCost = int.MaxValue;
            for (int i = 0; i < vault.ObjectCount; i++)
            {
                ObjectDef o = vault.GetObject(i);
                if (o.Type != ObjectType.Treasure || sim.State.Spent[i])
                {
                    continue;
                }

                int cost = Distance(sim, vault, w.Tile, o.Pos) - (vault.Hoard.Contains(o.Pos) ? 4 : 0);
                if (cost < bestCost)
                {
                    bestCost = cost;
                    best = o.Pos;
                }
            }

            return best;
        }

        // ---- Paths ----------------------------------------------------------------------------

        /// <summary>The first step toward goal, avoiding hazards if possible, or toward a lever if a gate blocks the way.</summary>
        private static Dir? FirstStep(RaidSim sim, VaultDef vault, TilePos goal)
        {
            WickState w = sim.State.Wick;
            bool[] avoid = Hazards(sim, vault);
            Dir? d = Bfs(sim, vault, w.Tile, goal, avoid) ?? Bfs(sim, vault, w.Tile, goal, null);
            if (d != null)
            {
                return d;
            }

            // Blocked: head for a lever whose gate is closed.
            for (int i = 0; i < vault.ObjectCount; i++)
            {
                ObjectDef o = vault.GetObject(i);
                if (o.Type != ObjectType.Lever)
                {
                    continue;
                }

                foreach (int id in o.Links)
                {
                    int g = vault.IndexOfObject(id);
                    if (g >= 0 && sim.State.GateOpen[g] == 0 && o.Pos != w.Tile)
                    {
                        Dir? toLever = Bfs(sim, vault, w.Tile, o.Pos, avoid) ?? Bfs(sim, vault, w.Tile, o.Pos, null);
                        if (toLever != null)
                        {
                            return toLever;
                        }
                    }
                }
            }

            return null;
        }

        private static int Distance(RaidSim sim, VaultDef vault, TilePos from, TilePos to)
        {
            int n = BfsLength(sim, vault, from, to);
            return n < 0 ? 999 : n;
        }

        private static Dir? Bfs(RaidSim sim, VaultDef vault, TilePos from, TilePos to, bool[] avoid)
        {
            if (from == to)
            {
                return null;
            }

            int[] prev = Search(sim, vault, to, from, avoid);
            if (prev == null)
            {
                return null;
            }

            // Searched from the goal back: the parent of `from` is its next tile.
            TilePos next = new TilePos(prev[from.Y * vault.Width + from.X] % vault.Width, prev[from.Y * vault.Width + from.X] / vault.Width);
            for (int d = 0; d < 4; d++)
            {
                if (from.Step((Dir)d) == next)
                {
                    return (Dir)d;
                }
            }

            return null;
        }

        private static int BfsLength(RaidSim sim, VaultDef vault, TilePos from, TilePos to)
        {
            if (from == to)
            {
                return 0;
            }

            int[] prev = Search(sim, vault, to, from, null);
            if (prev == null)
            {
                return -1;
            }

            int n = 0;
            int at = from.Y * vault.Width + from.X;
            int target = to.Y * vault.Width + to.X;
            while (at != target && n < 2000)
            {
                at = prev[at];
                n++;
            }

            return n;
        }

        /// <summary>BFS from start until goal; prev[i] = the neighbour of i one step closer to start. Null if unreachable.</summary>
        private static int[] Search(RaidSim sim, VaultDef vault, TilePos start, TilePos goal, bool[] avoid)
        {
            int w = vault.Width;
            var prev = new int[w * vault.Height];
            for (int i = 0; i < prev.Length; i++)
            {
                prev[i] = -1;
            }

            var queue = new Queue<TilePos>();
            queue.Enqueue(start);
            prev[start.Y * w + start.X] = start.Y * w + start.X;
            while (queue.Count > 0)
            {
                TilePos p = queue.Dequeue();
                if (p == goal)
                {
                    return prev;
                }

                for (int d = 0; d < 4; d++)
                {
                    TilePos n = p.Step((Dir)d);
                    // Searching back from the goal: the step taken is n to p. Never into a pit, nor onto
                    // ice that slides into one.
                    if (!sim.IsWalkableNow(n) || prev[n.Y * w + n.X] >= 0 || vault.TileAt(n) == TileType.Pit ||
                        SlidesIntoPit(vault, sim.IsWalkableNow, p, (Dir)((d + 2) % 4)))
                    {
                        continue;
                    }

                    if (avoid != null && avoid[n.Y * w + n.X] && n != goal)
                    {
                        continue;
                    }

                    prev[n.Y * w + n.X] = p.Y * w + p.X;
                    queue.Enqueue(n);
                }
            }

            return null;
        }

        /// <summary>Whether stepping onto `onto` heading dir ends in a pit: ice keeps Wick sliding while the next tile is walkable.</summary>
        public static bool SlidesIntoPit(VaultDef vault, Func<TilePos, bool> walkable, TilePos onto, Dir dir)
        {
            for (TilePos t = onto; vault.TileAt(t) == TileType.Ice;)
            {
                t = t.Step(dir);
                if (!walkable(t))
                {
                    return false;
                }

                if (vault.TileAt(t) == TileType.Pit)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Tiles a careful player steps around: armed bell plates, a mimic and its reach, an awake troll.</summary>
        private static bool[] Hazards(RaidSim sim, VaultDef vault)
        {
            var avoid = new bool[vault.Width * vault.Height];
            void Mark(TilePos p)
            {
                if (vault.InBounds(p))
                {
                    avoid[p.Y * vault.Width + p.X] = true;
                }
            }

            for (int y = 0; y < vault.Height; y++)
            {
                for (int x = 0; x < vault.Width; x++)
                {
                    if (sim.IsArmedBellPlate(new TilePos(x, y)))
                    {
                        Mark(new TilePos(x, y));
                    }
                }
            }

            foreach (GuardianState g in sim.State.Guardians)
            {
                if (g.Kind == ObjectType.MimicChest)
                {
                    Mark(g.Tile);
                    for (int d = 0; d < 4; d++)
                    {
                        Mark(g.Tile.Step((Dir)d));
                    }
                }
                else if (g.Kind == ObjectType.SleepyTroll && g.Mode == GuardianMode.Chase)
                {
                    Mark(g.Tile);
                }
            }

            return avoid;
        }

        // ---- Timing ---------------------------------------------------------------------------

        /// <summary>The shortest wait (0 to MaxWait ticks) after which a dash in dir loses no flicker; 0 if none does.</summary>
        private static int SafeWait(RaidSim sim, Dir dir)
        {
            int flickers = sim.State.Wick.Flickers;
            RaidSim waiting = sim.Clone();
            for (int wait = 0; wait <= MaxWait; wait++)
            {
                if (waiting.IsOver)
                {
                    return 0;
                }

                if (waiting.State.Wick.Flickers < flickers)
                {
                    return 0; // standing still is hurting too: just go
                }

                RaidSim trial = waiting.Clone();
                trial.Submit(Command.Dash(trial.State.Tick, dir));
                bool hurt = false;
                for (int t = 0; t < StepHorizon && !trial.IsOver; t++)
                {
                    trial.Step();
                    if (trial.State.Wick.Flickers < flickers)
                    {
                        hurt = true;
                        break;
                    }

                    if (t > 2 && !trial.State.Wick.Moving)
                    {
                        break;
                    }
                }

                if (!hurt)
                {
                    return wait;
                }

                waiting.Step();
            }

            return 0;
        }

        // ---- Tactics --------------------------------------------------------------------------

        private static bool WantsTiptoe(RaidSim sim, VaultDef vault)
        {
            TilePos at = sim.State.Wick.Occupied;
            foreach (GuardianState g in sim.State.Guardians)
            {
                bool asleep = g.Mode == GuardianMode.Patrol;
                int near = Motion.Chebyshev(at, g.Tile);
                if ((g.Kind == ObjectType.SleepyTroll && asleep && near <= 3) ||
                    (g.Kind == ObjectType.SirWhiskers && asleep && near <= 4) ||
                    (g.Kind == ObjectType.NightOwl && asleep && near <= 5))
                {
                    return true;
                }
            }

            return false;
        }

        private static void SetTiptoe(RaidSim sim, VaultDef vault, bool on)
        {
            if (sim.State.Wick.Tiptoe != on)
            {
                sim.Submit(on ? Command.TiptoeOn(sim.State.Tick) : Command.TiptoeOff(sim.State.Tick));
            }
        }

        private static bool GuardianChasingNear(RaidSim sim, int range)
        {
            TilePos at = sim.State.Wick.Occupied;
            foreach (GuardianState g in sim.State.Guardians)
            {
                bool chasing = g.Mode == GuardianMode.Chase && g.Kind != ObjectType.MimicChest && g.Kind != ObjectType.SleepyTroll;
                if (chasing && Motion.Chebyshev(at, g.Occupied) <= range)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool WhiskersStalkingNear(RaidSim sim, int range)
        {
            TilePos at = sim.State.Wick.Occupied;
            foreach (GuardianState g in sim.State.Guardians)
            {
                if (g.Kind == ObjectType.SirWhiskers && g.Mode == GuardianMode.Chase && g.Waypoint == RaidSim.WhiskersStalking &&
                    Motion.Chebyshev(at, g.Occupied) <= range && Motion.Chebyshev(at, g.Occupied) > 1)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
