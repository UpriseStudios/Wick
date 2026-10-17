using System;

namespace Wick.Sim
{
    /// <summary>
    /// One raid as a pure function of (vault, seed, input log). Step() runs the fixed tick
    /// order from the tech design; never reorder it (see CLAUDE.md).
    /// </summary>
    public sealed class RaidSim
    {
        /// <summary>PCG32 stream id for raid randomness ("WICK").</summary>
        public const ulong RngStream = 0x5749434BUL;

        private readonly VaultDef _vault;
        private readonly InputLog _log;
        private int _nextCommand;
        private bool _arrivedThisTick;

        public RaidSim(VaultDef vault, int seed, InputLog log)
        {
            _vault = vault ?? throw new ArgumentNullException(nameof(vault));
            _log = log ?? throw new ArgumentNullException(nameof(log));

            State = new RaidState();
            State.Rng = new Pcg32(unchecked((uint)seed), RngStream);
            State.Wick.Tile = vault.Entrance;
            State.Wick.Facing = Dir.N;
            State.Wick.Flickers = SimTuning.StartFlickers;
            State.Lit = new bool[vault.Width * vault.Height];
            UpdateLight();
        }

        public RaidState State { get; }

        public bool IsOver => State.Outcome != Outcome.Running;

        /// <summary>Advances one tick. Does nothing once the raid has ended.</summary>
        public void Step()
        {
            if (IsOver)
            {
                return;
            }

            _arrivedThisTick = false;

            ApplyInputs();      // 1. apply inputs stamped for this tick
            MoveWick();         // 2. move Wick (and decoy timer),
            UpdateLight();      //    then recompute his light so steps 3-7 see this tick's lit tiles
            AdvanceTraps();     // 3. advance traps by tick
            UpdateGuardians();  // 4. update guardians, sorted by entity id
            ResolveHits();      // 5. resolve hits: flicker loss, snuff, invulnerability window
            ResolvePickups();   // 6. resolve pickups: treasure, braziers
            CheckEnd();         // 7. check end: escaped, snuffed, or timer at 0
            FoldHash();         // 8. every 30 ticks (and at the end): fold state into running hash

            State.Tick++;
        }

        /// <summary>Whether Wick's flame lights tile p this tick. Off-grid tiles are never lit.</summary>
        public bool IsLit(TilePos p)
        {
            return _vault.InBounds(p) && State.Lit[p.Y * _vault.Width + p.X];
        }

        public RaidResult GetResult()
        {
            return new RaidResult(State.Outcome, State.Tick, State.Hash.Value, State.Checkpoints.ToArray());
        }

        // ---- 1. Inputs -------------------------------------------------------------------

        private void ApplyInputs()
        {
            while (_nextCommand < _log.Count && _log[_nextCommand].Tick <= State.Tick)
            {
                Apply(_log[_nextCommand]);
                _nextCommand++;
            }
        }

        private void Apply(Command c)
        {
            ref WickState w = ref State.Wick;
            switch (c.Type)
            {
                case CommandType.Dash:
                    // A fresh swipe overrides any held run.
                    w.Running = false;
                    if (w.Moving)
                    {
                        w.HasQueued = true;
                        w.QueuedDir = c.Dir;
                    }
                    else
                    {
                        w.Facing = c.Dir;
                        TryStartMove(ref w, c.Dir, false);
                    }

                    break;

                case CommandType.RunStart:
                    w.Running = true;
                    w.RunDir = c.Dir;
                    w.HasQueued = false;
                    if (!w.Moving)
                    {
                        w.Facing = c.Dir;
                        TryStartMove(ref w, c.Dir, true);
                    }

                    break;

                case CommandType.RunStop:
                    // The current tile move still completes.
                    w.Running = false;
                    break;

                case CommandType.TiptoeOn:
                    w.Tiptoe = true;
                    break;

                case CommandType.TiptoeOff:
                    w.Tiptoe = false;
                    break;

                case CommandType.Decoy:
                    if (!w.DecoyUsed)
                    {
                        w.DecoyUsed = true;
                        w.DecoyTile = w.Tile;
                        w.DecoyTicksLeft = SimTuning.DecoyTicks;
                    }

                    break;
            }
        }

        // ---- 2. Movement -----------------------------------------------------------------

        private void MoveWick()
        {
            ref WickState w = ref State.Wick;

            if (w.DecoyTicksLeft > 0)
            {
                w.DecoyTicksLeft--;
            }

            if (!w.Moving)
            {
                // TODO M2: retry a blocked run if doors or other blockers can change.
                return;
            }

            // TODO M2: carried treasure slows Wick; ice tiles slide.
            int speed = w.MoveIsRun ? SimTuning.RunSpeed : SimTuning.DashSpeed;
            if (w.Tiptoe)
            {
                speed /= SimTuning.TiptoeSpeedDivisor;
            }

            w.Progress += speed;
            if (w.Progress < SubTile.PerTile)
            {
                return;
            }

            // Arrive at the next tile centre.
            int leftover = w.Progress - SubTile.PerTile;
            w.Tile = w.Tile.Step(w.MoveDir);
            w.Progress = 0;
            w.Moving = false;
            _arrivedThisTick = true;
            if (w.Tile != _vault.Entrance)
            {
                w.LeftEntrance = true;
            }

            // A queued dash wins over continuing a run.
            if (w.HasQueued)
            {
                w.HasQueued = false;
                w.Facing = w.QueuedDir;
                TryStartMove(ref w, w.QueuedDir, false);
            }
            else if (w.Running)
            {
                w.Facing = w.RunDir;
                if (TryStartMove(ref w, w.RunDir, true))
                {
                    w.Progress = leftover;
                }
            }
        }

        private void UpdateLight()
        {
            Lighting.Compute(_vault, in State.Wick, State.Lit);
        }

        private bool TryStartMove(ref WickState w, Dir dir, bool isRun)
        {
            if (!_vault.IsWalkable(w.Tile.Step(dir)))
            {
                return false;
            }

            w.Moving = true;
            w.MoveDir = dir;
            w.MoveIsRun = isRun;
            w.Progress = 0;
            return true;
        }

        // ---- 3-6. Not yet implemented -----------------------------------------------------

        private void AdvanceTraps()
        {
            // TODO M2: timed traps are a pure function of State.Tick (cycle length, phase offset).
        }

        private void UpdateGuardians()
        {
            // TODO M2: patrol, alert and chase, iterating guardians sorted by entity id;
            // sight only against tiles inside Wick's light; decoy distraction.
        }

        private void ResolveHits()
        {
            // TODO M2: trap and guardian hits, pits; flicker loss, snuff, invulnerability window.
        }

        private void ResolvePickups()
        {
            // TODO M2: treasure piles (weight) and braziers (relight 1 flicker, once each).
        }

        // ---- 7. End checks ---------------------------------------------------------------

        private void CheckEnd()
        {
            WickState w = State.Wick;
            if (_arrivedThisTick && w.LeftEntrance && w.Tile == _vault.Entrance)
            {
                State.Outcome = Outcome.Escaped;
            }
            else if (w.Flickers <= 0)
            {
                State.Outcome = Outcome.Snuffed;
            }
            else if (State.Tick + 1 >= SimTuning.RaidTicks)
            {
                State.Outcome = Outcome.TimedOut;
            }
        }

        // ---- 8. Hash ---------------------------------------------------------------------

        private void FoldHash()
        {
            bool interval = (State.Tick + 1) % SimTuning.HashIntervalTicks == 0;
            if (interval || IsOver)
            {
                StateHasher.Fold(ref State.Hash, State);
                State.Checkpoints.Add(State.Hash.Value);
            }
        }
    }
}
