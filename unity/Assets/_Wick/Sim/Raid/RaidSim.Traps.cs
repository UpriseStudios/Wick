namespace Wick.Sim
{
    // Step 3: traps. Timed traps are a pure function of the tick, cycle and offset, so they
    // carry no state and never drift; only the Snuffer Bell's one-shot flag is stored (Spent).
    public sealed partial class RaidSim
    {
        /// <summary>Per object: whether its timed trap is in its damaging phase this tick.</summary>
        private bool[] _trapActive;

        public static int Phase(int tick, ObjectDef trap)
        {
            return (tick + trap.Offset) % trap.Cycle;
        }

        /// <summary>Whether a timed trap is in its damaging phase on this tick.</summary>
        public static bool IsTrapActive(int tick, ObjectDef trap)
        {
            int phase = Phase(tick, trap);
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

                _trapActive[i] = IsTrapActive(State.Tick, o);
                int phase = Phase(State.Tick, o);

                if (o.Type == ObjectType.SpikeFloor && phase == o.Cycle - SimTuning.SpikeWarnTicks)
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
