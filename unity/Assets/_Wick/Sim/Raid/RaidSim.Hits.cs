namespace Wick.Sim
{
    // Step 5: hits. Order is fixed: invulnerability and dim timers, Snuffer Bells, then every
    // other damage source in entity-id order. Only the first hit while vulnerable lands.
    public sealed partial class RaidSim
    {
        private void ResolveHits()
        {
            ref WickState w = ref State.Wick;
            TilePos at = w.Occupied;

            bool invulnerable = w.InvulnTicks > 0;
            if (invulnerable)
            {
                w.InvulnTicks--;
            }

            if (w.GustDimTicks > 0)
            {
                w.GustDimTicks--;
            }

            int flickersBefore = w.Flickers;

            // Snuffer Bells: instant snuff, ignoring invulnerability. Each drops once.
            for (int i = 0; i < _vault.ObjectCount; i++)
            {
                ObjectDef o = _vault.GetObject(i);
                if (o.Type == ObjectType.SnufferBell && !State.Spent[i] && o.Pos == at)
                {
                    State.Spent[i] = true;
                    w.Flickers = 0;
                    Emit(SimEventType.TrapFired, o.Id, o.Pos, 0);
                }
            }

            bool hitLanded = false;
            for (int i = 0; i < _vault.ObjectCount && w.Flickers > 0; i++)
            {
                ObjectDef o = _vault.GetObject(i);
                bool hits;
                switch (o.Type)
                {
                    case ObjectType.DripPipe:
                    case ObjectType.SpikeFloor:
                        hits = _trapActive[i] && o.Pos == at;
                        break;

                    case ObjectType.Bellows:
                        hits = _trapActive[i] && OnGustLine(o, at);
                        if (hits)
                        {
                            // The gust shrinks the flame even through invulnerability.
                            w.GustDimTicks = SimTuning.GustDimTicks;
                        }

                        break;

                    default:
                        hits = GuardianHits(i, at);
                        break;
                }

                if (hits && !invulnerable && !hitLanded)
                {
                    hitLanded = true;
                    w.Flickers--;
                    w.InvulnTicks = SimTuning.InvulnTicks;
                    Emit(SimEventType.FlickerLost, o.Id, at, w.Flickers);
                }
            }

            if (flickersBefore > 0 && w.Flickers <= 0)
            {
                Emit(SimEventType.Snuffed, 0, at, 0);
            }
        }
    }
}
