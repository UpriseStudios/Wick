namespace Wick.Sim
{
    // Step 3: traps. Timed traps are a pure function of the tick, cycle and offset, so they
    // carry no state and never drift; only the Snuffer Bell's one-shot flag is stored (Spent).
    public sealed partial class RaidSim
    {
        /// <summary>Per object: whether its timed trap is in its damaging phase this tick.</summary>
        private bool[] _trapActive;

        /// <summary>Where a timed trap is in its cycle; upgrade levels shorten the cycle (DefenceStats).</summary>
        public static int Phase(int tick, ObjectDef trap, int level)
        {
            return (tick + trap.Offset) % DefenceStats.Cycle(trap, level);
        }

        /// <summary>Whether a timed trap at this upgrade level is in its damaging phase on this tick.</summary>
        public static bool IsTrapActive(int tick, ObjectDef trap, int level)
        {
            int phase = Phase(tick, trap, level);
            switch (trap.Type)
            {
                case ObjectType.DripPipe: return phase < SimTuning.DripWindowTicks;
                case ObjectType.Bellows: return phase < SimTuning.GustTicks;
                case ObjectType.SpikeFloor: return phase < SimTuning.SpikeRaisedTicks;
                case ObjectType.SwingAxe: return AxeOverCentre(phase, DefenceStats.Cycle(trap, level));
                case ObjectType.Fan: return FanBlowing(tick, trap, level);
                default: return false;
            }
        }

        /// <summary>A fan turns a quarter clockwise every quarter of its cycle, starting at its placed facing.</summary>
        public static Dir FanFacing(int tick, ObjectDef fan, int level)
        {
            int quarter = DefenceStats.Cycle(fan, level) / 4;
            return (Dir)((fan.Rot + Phase(tick, fan, level) / quarter) % 4);
        }

        /// <summary>Whether a fan is blowing: the first FanGustTicks of each quarter turn.</summary>
        public static bool FanBlowing(int tick, ObjectDef fan, int level)
        {
            int quarter = DefenceStats.Cycle(fan, level) / 4;
            return Phase(tick, fan, level) % quarter < SimTuning.FanGustTicks;
        }

        /// <summary>Whether tile p is in a fan's wind: up to FanRange tiles along its facing, stopping at walls and crates.</summary>
        public bool OnFanLine(ObjectDef fan, Dir facing, TilePos p)
        {
            TilePos t = fan.Pos;
            for (int i = 0; i < SimTuning.FanRange; i++)
            {
                t = t.Step(facing);
                if (_vault.BlocksGust(t))
                {
                    return false;
                }

                if (t == p)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// A Swing Axe's blade passes over its own tile twice a cycle: on the downswing (phase 0)
        /// and on the return (half a cycle later).
        /// </summary>
        private static bool AxeOverCentre(int phase, int cycle)
        {
            int half = cycle / 2;
            return phase < SimTuning.AxeSweepTicks || (phase >= half && phase < half + SimTuning.AxeSweepTicks);
        }

        /// <summary>Whether a Swing Axe's blade is over tile p this tick (level 5 reaches the tile in front at the top of the arc).</summary>
        public static bool AxeHits(int tick, ObjectDef axe, int level, TilePos p)
        {
            int cycle = DefenceStats.Cycle(axe, level);
            int phase = Phase(tick, axe, level);
            if (p == axe.Pos)
            {
                return AxeOverCentre(phase, cycle);
            }

            int quarter = cycle / 4;
            return DefenceStats.AxeWideArc(level) && p == axe.Pos.Step(axe.Facing) &&
                phase >= quarter && phase < quarter + SimTuning.AxeSweepTicks;
        }

        /// <summary>
        /// Whether tile p is on a bellows' gust line: up to GustRange tiles along its facing,
        /// stopping before the first wall, crate or grid edge. The bellows' own tile is not.
        /// </summary>
        public bool OnGustLine(ObjectDef bellows, TilePos p)
        {
            TilePos t = bellows.Pos;
            for (int i = 0; i < SimTuning.GustRange; i++)
            {
                t = t.Step(bellows.Facing);
                if (_vault.BlocksGust(t))
                {
                    return false;
                }

                if (t == p)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Drip Pipe level 5: for a moment after each drip its tile is a puddle that slows Wick to
        /// tiptoe speed.
        /// </summary>
        public bool IsPuddle(TilePos p)
        {
            for (int i = 0; i < _vault.ObjectCount; i++)
            {
                ObjectDef o = _vault.GetObject(i);
                if (o.Type != ObjectType.DripPipe || o.Pos != p)
                {
                    continue;
                }

                int level = _vault.LevelOf(o.Type);
                int phase = Phase(State.Tick, o, level);
                if (phase >= SimTuning.DripWindowTicks && phase < SimTuning.DripWindowTicks + DefenceStats.DripPuddleTicks(level))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Whether p is the plate of a Snuffer Bell that hasn't dropped (two tiles at level 5).</summary>
        public bool IsArmedBellPlate(TilePos p)
        {
            return ArmedBellAt(p) >= 0;
        }

        /// <summary>The object index of an armed Snuffer Bell whose plate covers p, or -1.</summary>
        private int ArmedBellAt(TilePos p)
        {
            for (int i = 0; i < _vault.ObjectCount; i++)
            {
                ObjectDef o = _vault.GetObject(i);
                if (o.Type == ObjectType.SnufferBell && !State.Spent[i] && BellPlateCovers(o, p))
                {
                    return i;
                }
            }

            return -1;
        }

        private bool BellPlateCovers(ObjectDef bell, TilePos p)
        {
            if (bell.Pos == p)
            {
                return true;
            }

            if (!DefenceStats.BellWidePlate(_vault.LevelOf(bell.Type)))
            {
                return false;
            }

            TilePos second = bell.Pos.Step(bell.Facing);
            return second == p && _vault.IsWalkable(second);
        }

        /// <summary>Whether a closed gate stands on p this tick.</summary>
        public bool IsClosedGate(TilePos p)
        {
            for (int i = 0; i < _vault.ObjectCount; i++)
            {
                ObjectDef o = _vault.GetObject(i);
                if (o.Type == ObjectType.Gate && o.Pos == p && State.GateOpen[i] == 0)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Lever Doors. Wick on a lever holds its gates open for GateOpenTicks; time counts down
        /// once he steps off. A gate whose time is up stays open while Wick or a guardian is in it,
        /// and closes the first tick it is clear.
        /// </summary>
        private void AdvanceGates(TilePos wick)
        {
            int open = DefenceStats.GateOpenTicks(_vault.LevelOf(ObjectType.Gate));
            for (int i = 0; i < _vault.ObjectCount; i++)
            {
                ObjectDef o = _vault.GetObject(i);
                if (o.Type == ObjectType.Lever && o.Pos == wick)
                {
                    foreach (int id in o.Links)
                    {
                        int g = _vault.IndexOfObject(id);
                        if (State.GateOpen[g] == 0)
                        {
                            Emit(SimEventType.TrapFired, id, _vault.GetObject(g).Pos, 1); // opens
                        }

                        State.GateOpen[g] = open + 1; // counted down below, so it holds at `open` while he stays
                    }
                }
            }

            for (int i = 0; i < _vault.ObjectCount; i++)
            {
                ObjectDef o = _vault.GetObject(i);
                if (o.Type != ObjectType.Gate || State.GateOpen[i] == 0)
                {
                    continue;
                }

                if (State.GateOpen[i] > 1)
                {
                    State.GateOpen[i]--;
                }
                else if (!SomeoneIn(o.Pos))
                {
                    State.GateOpen[i] = 0;
                    Emit(SimEventType.TrapFired, o.Id, o.Pos, 0); // closes
                }
            }
        }

        private bool SomeoneIn(TilePos p)
        {
            WickState w = State.Wick;
            if (w.Tile == p || w.Occupied == p || (w.Moving && w.Tile.Step(w.MoveDir) == p))
            {
                return true;
            }

            foreach (GuardianState g in State.Guardians)
            {
                if (g.Tile == p || g.Occupied == p || (g.Moving && g.Tile.Step(g.MoveDir) == p))
                {
                    return true;
                }
            }

            return false;
        }

        private void AdvanceTraps()
        {
            TilePos wick = State.Wick.Occupied;
            AdvanceGates(wick);
            for (int i = 0; i < _vault.ObjectCount; i++)
            {
                ObjectDef o = _vault.GetObject(i);
                if (!ObjectTypes.IsTimedTrap(o.Type))
                {
                    continue;
                }

                int level = _vault.LevelOf(o.Type);
                _trapActive[i] = IsTrapActive(State.Tick, o, level);
                int phase = Phase(State.Tick, o, level);

                if (o.Type == ObjectType.SpikeFloor && phase == DefenceStats.Cycle(o, level) - DefenceStats.SpikeWarnTicks(level))
                {
                    Emit(SimEventType.TrapWarning, o.Id, o.Pos, 0);
                }

                bool swing = o.Type == ObjectType.SwingAxe && phase == DefenceStats.Cycle(o, level) / 2;
                bool turn = o.Type == ObjectType.Fan && phase % (DefenceStats.Cycle(o, level) / 4) == 0;
                if (phase == 0 || swing || turn)
                {
                    Emit(SimEventType.TrapFired, o.Id, o.Pos, 0);
                    if (IsNearMiss(o, wick))
                    {
                        Emit(SimEventType.NearMiss, o.Id, wick, 0);
                    }
                }
            }
        }

        private bool HitsTile(ObjectDef trap, TilePos p)
        {
            if (trap.Type == ObjectType.Fan)
            {
                return OnFanLine(trap, FanFacing(State.Tick, trap, _vault.LevelOf(trap.Type)), p);
            }

            if (trap.Type == ObjectType.SwingAxe)
            {
                return trap.Pos == p || (DefenceStats.AxeWideArc(_vault.LevelOf(trap.Type)) && trap.Pos.Step(trap.Facing) == p);
            }

            return trap.Type == ObjectType.Bellows ? OnGustLine(trap, p) : trap.Pos == p;
        }

        /// <summary>Wick is right next to where the trap fired, but not in it.</summary>
        private bool IsNearMiss(ObjectDef trap, TilePos wick)
        {
            if (HitsTile(trap, wick))
            {
                return false;
            }

            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    if ((dx != 0 || dy != 0) && HitsTile(trap, new TilePos(wick.X + dx, wick.Y + dy)))
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
