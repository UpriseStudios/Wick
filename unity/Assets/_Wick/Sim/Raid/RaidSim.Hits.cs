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

                    case ObjectType.SwingAxe:
                        hits = AxeHits(State.Tick, o, _vault.LevelOf(o.Type), at);
                        break;

                    case ObjectType.Fan:
                        hits = false; // fans push (below); they never cost a flicker
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
                    w.Flickers = w.Flickers > HitCost(o.Type) ? w.Flickers - HitCost(o.Type) : 0;
                    w.InvulnTicks = SimTuning.InvulnTicks;
                    Emit(SimEventType.FlickerLost, o.Id, at, w.Flickers);
                    if (o.Type == ObjectType.MimicChest && DefenceStats.MimicSteals(_vault.LevelOf(o.Type)) && w.CarriedPiles > 0)
                    {
                        // Level 5: it snatches a pile back. Piles in a vault are worth the same, so take the average.
                        w.CarriedTreasure -= w.CarriedTreasure / w.CarriedPiles;
                        w.CarriedPiles--;
                    }

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
            else if (w.Flickers > 0 && w.KnockTiles == 0)
            {
                PushFromFans(at);
            }

            if (flickersBefore > 0 && w.Flickers <= 0)
            {
                Emit(SimEventType.Snuffed, 0, at, 0);
            }
        }

        /// <summary>
        /// Fan Maze: a blowing fan whose wind reaches Wick pushes him along it (a knockback, so it
        /// stops before walls, crates and bell plates). The first fan by id wins. No flicker lost.
        /// </summary>
        private void PushFromFans(TilePos at)
        {
            for (int i = 0; i < _vault.ObjectCount; i++)
            {
                ObjectDef o = _vault.GetObject(i);
                if (o.Type != ObjectType.Fan || !_trapActive[i])
                {
                    continue;
                }

                int level = _vault.LevelOf(o.Type);
                Dir facing = FanFacing(State.Tick, o, level);
                if (OnFanLine(o, facing, at))
                {
                    StartKnockback(facing, DefenceStats.FanPush(level));
                    return;
                }
            }
        }

        /// <summary>Flickers one hit costs: a Mimic Chest's bite takes two, everything else one.</summary>
        private static int HitCost(ObjectType type)
        {
            return type == ObjectType.MimicChest ? SimTuning.MimicBiteFlickers : 1;
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
                if (!IsWalkableNow(p) || IsArmedBellPlate(p))
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
