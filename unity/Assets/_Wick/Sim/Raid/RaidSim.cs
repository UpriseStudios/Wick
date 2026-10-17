using System;
using System.Collections.Generic;

namespace Wick.Sim
{
    /// <summary>
    /// One raid as a pure function of (vault, seed, input log). Step() runs the fixed tick
    /// order from the tech design; never reorder it (see CLAUDE.md). The steps live in the
    /// RaidSim.*.cs partial files.
    /// </summary>
    public sealed partial class RaidSim
    {
        /// <summary>PCG32 stream id for raid randomness ("WICK").</summary>
        public const ulong RngStream = 0x5749434BUL;

        private readonly VaultDef _vault;
        private readonly InputLog _log;
        private readonly List<SimEvent> _events = new List<SimEvent>(32);
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
            State.Spent = new bool[vault.ObjectCount];
            State.Guardians = CreateGuardians(vault);
            State.Lit = new bool[vault.Width * vault.Height];
            _trapActive = new bool[vault.ObjectCount];
            UpdateLight();
        }

        public RaidState State { get; }

        public bool IsOver => State.Outcome != Outcome.Running;

        /// <summary>
        /// What happened during the last Step(), for views, audio and clip highlights.
        /// Cleared at the start of every step. The sim never reads these.
        /// </summary>
        public IReadOnlyList<SimEvent> Events => _events;

        /// <summary>Advances one tick. Does nothing once the raid has ended.</summary>
        public void Step()
        {
            if (IsOver)
            {
                return;
            }

            _arrivedThisTick = false;
            _events.Clear();

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

        private void Emit(SimEventType type, int entityId, TilePos tile, int value)
        {
            _events.Add(new SimEvent(type, entityId, tile, value));
        }

        private static GuardianState[] CreateGuardians(VaultDef vault)
        {
            int count = 0;
            for (int i = 0; i < vault.ObjectCount; i++)
            {
                if (ObjectTypes.IsGuardian(vault.GetObject(i).Type))
                {
                    count++;
                }
            }

            var guardians = new GuardianState[count];
            int n = 0;
            for (int i = 0; i < vault.ObjectCount; i++)
            {
                ObjectDef o = vault.GetObject(i);
                if (!ObjectTypes.IsGuardian(o.Type))
                {
                    continue;
                }

                guardians[n++] = new GuardianState
                {
                    ObjectIndex = i,
                    Kind = o.Type,
                    Tile = o.Pos,
                    Facing = Dir.N,
                    Mode = GuardianMode.Patrol,
                    Target = o.Pos,
                };
            }

            return guardians;
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

        // Steps 3-6 live in RaidSim.Traps.cs, RaidSim.Guardians.cs, RaidSim.Hits.cs and
        // RaidSim.Pickups.cs.

        // ---- 7. End checks ---------------------------------------------------------------

        private void CheckEnd()
        {
            WickState w = State.Wick;

            // Snuffed wins: a flame that goes out on the entrance tile did not escape.
            if (w.Flickers <= 0)
            {
                State.Outcome = Outcome.Snuffed;
            }
            else if (_arrivedThisTick && w.LeftEntrance && w.Tile == _vault.Entrance)
            {
                State.Outcome = Outcome.Escaped;
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
