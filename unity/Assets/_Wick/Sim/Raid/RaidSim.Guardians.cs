using System;

namespace Wick.Sim
{
    // Step 4: guardians, in entity-id order. Each one first perceives (decoy, Wick's light),
    // then moves one tick along a BFS shortest path toward its current target.
    public sealed partial class RaidSim
    {
        /// <summary>Object index to index in State.Guardians, or -1.</summary>
        private int[] _guardianOfObject;

        private int[] _bfsDist;
        private int[] _bfsQueue;

        private void InitGuardians()
        {
            _guardianOfObject = new int[_vault.ObjectCount];
            for (int i = 0; i < _guardianOfObject.Length; i++)
            {
                _guardianOfObject[i] = -1;
            }

            for (int g = 0; g < State.Guardians.Length; g++)
            {
                _guardianOfObject[State.Guardians[g].ObjectIndex] = g;
            }

            _bfsDist = new int[_vault.Width * _vault.Height];
            _bfsQueue = new int[_vault.Width * _vault.Height];
        }

        private void UpdateGuardians()
        {
            for (int g = 0; g < State.Guardians.Length; g++)
            {
                ref GuardianState guardian = ref State.Guardians[g];
                ObjectDef def = _vault.GetObject(guardian.ObjectIndex);
                int level = _vault.LevelOf(guardian.Kind);
                if (guardian.Kind == ObjectType.NightOwl)
                {
                    PerceiveOwl(ref guardian, def);
                    Advance(ref guardian, guardian.Mode == GuardianMode.Patrol
                        ? SimTuning.OwlPatrolSpeed
                        : DefenceStats.OwlChaseSpeed(level));
                    if (guardian.Mode == GuardianMode.Patrol)
                    {
                        AdvanceWaypoint(ref guardian, def);
                    }
                }
                else if (guardian.Kind == ObjectType.MimicChest)
                {
                    PerceiveMimic(ref guardian, def);
                }
                else if (guardian.Kind == ObjectType.SleepyTroll)
                {
                    UpdateTroll(ref guardian, def, level);
                }
                else
                {
                    PerceiveMoths(ref guardian);
                    Advance(ref guardian, DefenceStats.MothSpeed(level));
                }
            }
        }

        // ---- Night Owl ----------------------------------------------------------------

        private void PerceiveOwl(ref GuardianState owl, ObjectDef def)
        {
            WickState w = State.Wick;
            TilePos at = owl.Occupied;

            if (DecoyActive() && Senses(at, w.DecoyTile, SimTuning.OwlSightRange))
            {
                owl.Mode = GuardianMode.Distracted;
                owl.Target = w.DecoyTile;
                return;
            }

            if (owl.Mode == GuardianMode.Distracted)
            {
                owl.Mode = GuardianMode.Patrol;
                owl.Target = PatrolTarget(owl, def);
            }

            bool sees = OwlSeesWick(owl);
            if (owl.Mode == GuardianMode.Patrol)
            {
                if (sees)
                {
                    owl.Mode = GuardianMode.Chase;
                    owl.Target = w.Occupied;
                    owl.LostTicks = 0;
                    Emit(SimEventType.GuardianAlerted, def.Id, at, 0);
                }

                return;
            }

            // Chasing.
            if (sees)
            {
                owl.Target = w.Occupied;
                owl.LostTicks = 0;
            }
            else if (++owl.LostTicks >= SimTuning.OwlLoseTicks)
            {
                owl.Mode = GuardianMode.Patrol;
                owl.LostTicks = 0;
                owl.Target = PatrolTarget(owl, def);
            }
        }

        /// <summary>
        /// The owl spots Wick when his light reaches the owl, he is within sight range and its
        /// front cone, and it has a clear line of sight to him.
        /// </summary>
        public bool OwlSeesWick(in GuardianState owl)
        {
            TilePos from = owl.Occupied;
            TilePos wick = State.Wick.Occupied;
            if (!IsLit(from) || Motion.DistSq(from, wick) > SimTuning.OwlSightRange * SimTuning.OwlSightRange)
            {
                return false;
            }

            int dx = wick.X - from.X;
            int dy = wick.Y - from.Y;
            int forward = dx * owl.Facing.Dx() + dy * owl.Facing.Dy();
            int lateral = dx * owl.Facing.Dy() - dy * owl.Facing.Dx();
            if (forward <= 0 || 2 * Math.Abs(lateral) > forward * DefenceStats.OwlConeHalves(_vault.LevelOf(ObjectType.NightOwl)))
            {
                return false;
            }

            return Lighting.HasLineOfSight(_vault, from, wick);
        }

        private static TilePos PatrolTarget(in GuardianState owl, ObjectDef def)
        {
            return def.Patrol.Length == 0 ? def.Pos : def.Patrol[owl.Waypoint];
        }

        private void AdvanceWaypoint(ref GuardianState owl, ObjectDef def)
        {
            if (def.Patrol.Length == 0)
            {
                owl.Target = def.Pos;
                return;
            }

            if (!owl.Moving && owl.Tile == def.Patrol[owl.Waypoint])
            {
                owl.Waypoint = (owl.Waypoint + 1) % def.Patrol.Length;
            }

            owl.Target = def.Patrol[owl.Waypoint];
        }

        // ---- Wax Moths ----------------------------------------------------------------

        private void PerceiveMoths(ref GuardianState moths)
        {
            WickState w = State.Wick;
            TilePos at = moths.Occupied;

            int wickStrength = Lighting.RadiusUnits(in w);
            bool wickDraws = wickStrength > 0 && DrawsMoths(at, w.Occupied, wickStrength);
            bool decoyDraws = DecoyActive() && DrawsMoths(at, w.DecoyTile, SimTuning.DecoyLightStrength);

            if (decoyDraws && (!wickDraws || SimTuning.DecoyLightStrength >= wickStrength))
            {
                moths.Mode = GuardianMode.Distracted;
                moths.Target = w.DecoyTile;
            }
            else if (wickDraws)
            {
                moths.Mode = GuardianMode.Chase;
                moths.Target = w.Occupied;
            }
            else
            {
                moths.Mode = GuardianMode.Patrol;
                moths.Target = moths.Moving ? moths.Tile.Step(moths.MoveDir) : moths.Tile;
            }
        }

        /// <summary>A light draws moths from within its strength plus MothSenseBonus, in sight.</summary>
        private bool DrawsMoths(TilePos moths, TilePos light, int strength)
        {
            long reach = (long)strength + SimTuning.MothSenseBonus;
            long dist2 = (long)Motion.DistSq(moths, light) * SubTile.PerTile * SubTile.PerTile;
            return dist2 <= reach * reach && Lighting.HasLineOfSight(_vault, moths, light);
        }

        // ---- Mimic Chest ----------------------------------------------------------------

        /// <summary>
        /// A Mimic Chest never moves. While Wick is on its tile or beside it (not diagonally) it
        /// winds up, one tick at a time; stepping away resets it. The bite is in step 5.
        /// </summary>
        private void PerceiveMimic(ref GuardianState mimic, ObjectDef def)
        {
            if (!MimicInReach(mimic))
            {
                mimic.ContactTicks = 0;
                mimic.Mode = GuardianMode.Patrol;
                return;
            }

            if (mimic.ContactTicks == 0)
            {
                mimic.Mode = GuardianMode.Chase;
                Emit(SimEventType.GuardianAlerted, def.Id, mimic.Tile, 0);
            }

            mimic.ContactTicks++;
        }

        private bool MimicInReach(in GuardianState mimic)
        {
            TilePos wick = State.Wick.Occupied;
            return wick == mimic.Tile || Motion.DistSq(wick, mimic.Tile) == 1;
        }

        // ---- Sleepy Troll ---------------------------------------------------------------

        /// <summary>
        /// A Sleepy Troll never moves. A bright flame near it wakes it (Mode Chase); awake, it
        /// stomps on a rhythm and falls back asleep after TrollSleepTicks undisturbed. ContactTicks
        /// counts ticks awake; LostTicks counts ticks since it was last disturbed.
        /// </summary>
        private void UpdateTroll(ref GuardianState troll, ObjectDef def, int level)
        {
            WickState w = State.Wick;
            bool disturbed = Motion.Chebyshev(w.Occupied, troll.Tile) <= SimTuning.TrollWakeRange &&
                Lighting.RadiusUnits(in w) >= DefenceStats.TrollWakeLight(level);

            if (troll.Mode != GuardianMode.Chase)
            {
                if (disturbed)
                {
                    troll.Mode = GuardianMode.Chase;
                    troll.ContactTicks = 0;
                    troll.LostTicks = 0;
                    Emit(SimEventType.GuardianAlerted, def.Id, troll.Tile, 0);
                }

                return;
            }

            troll.ContactTicks++;
            troll.LostTicks = disturbed ? 0 : troll.LostTicks + 1;
            if (troll.LostTicks >= SimTuning.TrollSleepTicks)
            {
                troll.Mode = GuardianMode.Patrol;
                troll.ContactTicks = 0;
                troll.LostTicks = 0;
                return;
            }

            if (IsStompTick(troll))
            {
                Emit(SimEventType.TrapFired, def.Id, troll.Tile, 0);
            }
        }

        public static bool IsStompTick(in GuardianState troll)
        {
            int since = troll.ContactTicks - SimTuning.TrollStompDelay;
            return troll.Mode == GuardianMode.Chase && since >= 0 && since % SimTuning.TrollStompCycle == 0;
        }

        // ---- Shared --------------------------------------------------------------------

        private bool DecoyActive()
        {
            return State.Wick.DecoyTicksLeft > 0;
        }

        private bool Senses(TilePos from, TilePos to, int rangeTiles)
        {
            return Motion.DistSq(from, to) <= rangeTiles * rangeTiles && Lighting.HasLineOfSight(_vault, from, to);
        }

        /// <summary>Moves one tick toward Target; at a tile centre, picks the next BFS step.</summary>
        private void Advance(ref GuardianState g, int speed)
        {
            if (!g.Moving)
            {
                if (g.Tile == g.Target || !NextStep(g.Tile, g.Target, out Dir dir))
                {
                    return;
                }

                g.Moving = true;
                g.MoveDir = dir;
                g.Facing = dir;
                g.Progress = 0;
            }

            g.Progress += speed;
            if (g.Progress >= SubTile.PerTile)
            {
                g.Tile = g.Tile.Step(g.MoveDir);
                g.Progress = 0;
                g.Moving = false;
            }
        }

        /// <summary>
        /// First step from 'from' on a shortest walkable path to 'to'. BFS runs outward from
        /// the target; ties between neighbours break in N, E, S, W order.
        /// </summary>
        private bool NextStep(TilePos from, TilePos to, out Dir dir)
        {
            dir = Dir.N;
            if (!IsWalkableNow(to))
            {
                return false;
            }

            int w = _vault.Width;
            for (int i = 0; i < _bfsDist.Length; i++)
            {
                _bfsDist[i] = -1;
            }

            int head = 0;
            int tail = 0;
            _bfsDist[to.Y * w + to.X] = 0;
            _bfsQueue[tail++] = to.Y * w + to.X;
            int fromIndex = from.Y * w + from.X;

            while (head < tail && _bfsDist[fromIndex] < 0)
            {
                int cur = _bfsQueue[head++];
                var p = new TilePos(cur % w, cur / w);
                for (int d = 0; d < 4; d++)
                {
                    TilePos n = p.Step((Dir)d);
                    if (!IsWalkableNow(n))
                    {
                        continue;
                    }

                    int ni = n.Y * w + n.X;
                    if (_bfsDist[ni] < 0)
                    {
                        _bfsDist[ni] = _bfsDist[cur] + 1;
                        _bfsQueue[tail++] = ni;
                    }
                }
            }

            int here = _bfsDist[fromIndex];
            if (here <= 0)
            {
                return false;
            }

            for (int d = 0; d < 4; d++)
            {
                TilePos n = from.Step((Dir)d);
                if (IsWalkableNow(n) && _bfsDist[n.Y * w + n.X] == here - 1)
                {
                    dir = (Dir)d;
                    return true;
                }
            }

            return false;
        }

        // ---- Step 5 contact ------------------------------------------------------------

        /// <summary>Whether the guardian at object index i damages Wick on tile 'at' this tick.</summary>
        private bool GuardianHits(int objectIndex, TilePos at)
        {
            int gi = _guardianOfObject[objectIndex];
            if (gi < 0)
            {
                return false;
            }

            ref GuardianState g = ref State.Guardians[gi];
            if (g.Kind == ObjectType.MimicChest)
            {
                // It bites once its wind-up is done (whether or not Wick is invulnerable), then starts over.
                if (g.ContactTicks >= DefenceStats.MimicLungeTicks(_vault.LevelOf(g.Kind)) && MimicInReach(g))
                {
                    g.ContactTicks = 0;
                    return true;
                }

                return false;
            }

            if (g.Kind == ObjectType.SleepyTroll)
            {
                if (!IsStompTick(g))
                {
                    return false;
                }

                return at == g.Tile || (DefenceStats.TrollStompsAround(_vault.LevelOf(g.Kind)) && Motion.DistSq(at, g.Tile) == 1);
            }

            bool touching = g.Occupied == at;
            if (g.Kind == ObjectType.NightOwl)
            {
                return touching;
            }

            // Wax Moths drain after every MothDrainTicks of continuous contact.
            if (!touching)
            {
                g.ContactTicks = 0;
                return false;
            }

            if (++g.ContactTicks >= DefenceStats.MothDrainTicks(_vault.LevelOf(g.Kind)))
            {
                g.ContactTicks = 0;
                return true;
            }

            return false;
        }
    }
}
