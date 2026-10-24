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

            // Snuffer Bells: instant snuff, ignoring invulnerability. Each drops once. A knockback
            // never ends on a plate (see StartKnockback), so being knocked can't set one off.
            for (int i = 0; i < _vault.ObjectCount; i++)
            {
                ObjectDef o = _vault.GetObject(i);
                if (o.Type == ObjectType.SnufferBell && !State.Spent[i] && BellPlateCovers(o, at))
                {
                    State.Spent[i] = true;
                    w.Flickers = 0;
                    Emit(SimEventType.TrapFired, o.Id, o.Pos, 0);
                }
            }

            bool hitLanded = false;
            Dir knockDir = Dir.N;
            int knockTiles = 0;
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
                    if (o.Type == ObjectType.Bellows)
                    {
                        knockDir = o.Facing;
                        knockTiles = DefenceStats.BellowsKnockback(_vault.LevelOf(o.Type));
                    }
                }
            }

            if (knockTiles > 0 && w.Flickers > 0)
            {
                StartKnockback(knockDir, knockTiles);
            }

            if (flickersBefore > 0 && w.Flickers <= 0)
            {
                Emit(SimEventType.Snuffed, 0, at, 0);
            }
        }

        /// <summary>
        /// Pushes Wick up to `tiles` tiles in `dir` from the tile he occupies. He stops on the last
        /// free tile before a wall, crate, the grid edge or an armed Snuffer Bell plate, so a
        /// knockback can never set off an instant-snuff trap. The slide is a forced move at
        /// KnockSpeed: inputs during it queue up as usual, and it can't end the raid by escaping.
        /// </summary>
        private void StartKnockback(Dir dir, int tiles)
        {
            ref WickState w = ref State.Wick;
            TilePos from = w.Occupied;
            int free = 0;
            TilePos p = from;
            while (free < tiles)
            {
                p = p.Step(dir);
                if (!_vault.IsWalkable(p) || IsArmedBellPlate(p))
                {
                    break;
                }

                free++;
            }

            w.Tile = from;
            w.Progress = 0;
            w.Running = false;
            w.HasQueued = false;
            w.MoveIsRun = false;
            w.Moving = free > 0;
            w.MoveDir = dir;
            w.KnockTiles = free;
            w.Sliding = false;
            Emit(SimEventType.KnockedBack, 0, from, free);
        }
    }
}
