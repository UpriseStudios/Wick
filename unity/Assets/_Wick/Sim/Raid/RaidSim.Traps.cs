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
                default: return false;
            }
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

        private void AdvanceTraps()
        {
            TilePos wick = State.Wick.Occupied;
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

                if (phase == 0)
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
