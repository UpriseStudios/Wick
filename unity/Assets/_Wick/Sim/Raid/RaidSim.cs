using System;
using System.Collections.Generic;

namespace Wick.Sim
{
    /// <summary>
    /// One raid as a pure function of (vault, seed, input log, params). Step() runs the fixed tick
    /// order from the tech design; never reorder it (see CLAUDE.md). The steps live in the
    /// RaidSim.*.cs partial files.
    /// </summary>
    public sealed partial class RaidSim
    {
        /// <summary>PCG32 stream id for raid randomness ("WICK").</summary>
        public const ulong RngStream = 0x5749434BUL;

        private readonly VaultDef _vault;
        private readonly List<Command> _commands;
        private readonly List<SimEvent> _events = new List<SimEvent>(32);
        private int _nextCommand;
        private bool _arrivedThisTick;

        /// <summary>A live raid: commands arrive through Submit.</summary>
        public RaidSim(VaultDef vault, int seed)
            : this(vault, seed, InputLog.Empty)
        {
        }

        public RaidSim(VaultDef vault, int seed, InputLog log)
            : this(vault, seed, log, RaidParams.Default)
        {
        }

        public RaidSim(VaultDef vault, int seed, InputLog log, RaidParams raidParams)
        {
            _vault = vault ?? throw new ArgumentNullException(nameof(vault));
            if (raidParams.RaidTicks < RaidParams.MinRaidTicks)
            {
                throw new ArgumentException("Raid params are unset", nameof(raidParams));
            }

            Params = raidParams;
            if (log == null)
            {
                throw new ArgumentNullException(nameof(log));
            }

            _commands = new List<Command>(log.Count + 64);
            for (int i = 0; i < log.Count; i++)
            {
                _commands.Add(log[i]);
            }

            State = new RaidState();
            State.Rng = new Pcg32(unchecked((uint)seed), RngStream);
            State.Wick.Tile = vault.Entrance;
            State.Wick.Facing = Dir.N;
            State.Wick.Flickers = SimTuning.StartFlickers;
            State.Wick.DecoysLeft = raidParams.Decoys;
            State.Wick.LightBonus = raidParams.LightBonus;
            State.Spent = new bool[vault.ObjectCount];
            State.Carried = new bool[vault.ObjectCount];
            State.GateOpen = new int[vault.ObjectCount];
            State.BellFall = new int[vault.ObjectCount];
            State.Guardians = CreateGuardians(vault);
            State.Lit = new bool[vault.Width * vault.Height];
            _trapActive = new bool[vault.ObjectCount];
            InitGuardians();
            UpdateLight();
        }

        public RaidState State { get; }

        /// <summary>
        /// An independent copy at this exact tick: stepping either never affects the other, and both
        /// give the same results for the same commands. For bots and tools that try several moves
        /// from one moment (tools/balance); the game itself never branches a raid.
        /// </summary>
        public RaidSim Clone()
        {
            return new RaidSim(this);
        }

        private RaidSim(RaidSim from)
        {
            _vault = from._vault;
            Params = from.Params;
            _commands = new List<Command>(from._commands);
            _nextCommand = from._nextCommand;
            _arrivedThisTick = from._arrivedThisTick;
            _trapActive = (bool[])from._trapActive.Clone();
            _guardianOfObject = from._guardianOfObject; // fixed after construction
            _bfsDist = new int[from._bfsDist.Length];
            _bfsQueue = new int[from._bfsQueue.Length];
            State = from.State.Clone();
        }

        public RaidParams Params { get; }

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

        /// <summary>
        /// Adds a live command. It must be stamped for a tick that has not been simulated yet
        /// (normally State.Tick, "the next tick") and not before the last command.
        /// </summary>
        public void Submit(Command command)
        {
            if (IsOver)
            {
                throw new InvalidOperationException("The raid has ended");
            }

            if (command.Tick < State.Tick)
            {
                throw new ArgumentException("Command for tick " + command.Tick + " but tick " + State.Tick + " is next");
            }

            if (_commands.Count > 0 && command.Tick < _commands[_commands.Count - 1].Tick)
            {
                throw new ArgumentException("Command ticks must not go backwards");
            }

            if ((byte)command.Type > (byte)CommandType.Decoy || (byte)command.Dir > (byte)Dir.W)
            {
                throw new ArgumentException("Invalid command " + command);
            }

            _commands.Add(command);
        }

        /// <summary>Every command of this raid so far, for replays and server verification.</summary>
        public InputLog RecordedLog => new InputLog(_commands.ToArray());

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
                    Facing = o.Facing,
                    Mode = GuardianMode.Patrol,
                    Target = o.Patrol.Length > 0 ? o.Patrol[0] : o.Pos,
                };
            }

            return guardians;
        }

        public RaidResult GetResult()
        {
            return new RaidResult(State.Outcome, State.Tick, State.Hash.Value, State.Checkpoints.ToArray(),
                Scoring.Stars(_vault, State), Scoring.Loot(State), State.Wick.Flickers);
        }

        // ---- 1. Inputs -------------------------------------------------------------------

        private void ApplyInputs()
        {
            while (_nextCommand < _commands.Count && _commands[_nextCommand].Tick <= State.Tick)
            {
                Apply(_commands[_nextCommand]);
                _nextCommand++;
            }
        }

        private void Apply(Command c)
        {
            ref WickState w = ref State.Wick;
            if (w.Falling && (c.Type == CommandType.Dash || c.Type == CommandType.RunStart || c.Type == CommandType.Decoy))
            {
                return; // nothing to move or throw from the bottom of a pit
            }

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
                    if (w.DecoysLeft > 0 && w.DecoyTicksLeft == 0)
                    {
                        w.DecoysLeft--;
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

            if (w.ExitLockTicks > 0 && --w.ExitLockTicks == 0)
            {
                Emit(SimEventType.ExitUnlocked, 0, _vault.Entrance, 0);
            }

            if (w.Falling)
            {
                if (--w.FallTicks == 0)
                {
                    RespawnFromPit();
                }

                return;
            }

            if (!w.Moving)
            {
                // TODO M2: retry a blocked run if doors or other blockers can change.
                return;
            }

            int ice = _vault.LevelOf(ObjectType.IceTiles);
            if (w.Sliding && DefenceStats.IceDims(ice) && w.GustDimTicks < 2)
            {
                w.GustDimTicks = 2; // kept topped up while he slides; step 5 counts it down
            }

            w.Progress += w.KnockTiles > 0 ? SimTuning.KnockSpeed
                : w.Sliding ? DefenceStats.IceSlideSpeed(ice)
                : PuddleSlowed(WickSpeed(in w), in w);
            if (w.Progress < SubTile.PerTile)
            {
                return;
            }

            // Arrive at the next tile centre.
            int leftover = w.Progress - SubTile.PerTile;
            w.Tile = w.Tile.Step(w.MoveDir);
            w.Progress = 0;
            w.Moving = false;
            if (w.Tile != _vault.Entrance)
            {
                w.LeftEntrance = true;
            }

            if (_vault.TileAt(w.Tile) == TileType.Pit)
            {
                StartFall(); // running, sliding or knocked in: he drops
                return;
            }

            if (w.KnockTiles > 0)
            {
                // A knockback slide isn't an arrival: it can't count as escaping.
                if (--w.KnockTiles > 0)
                {
                    w.Moving = true;
                    w.Progress = leftover;
                    return;
                }

                leftover = 0;
            }
            else
            {
                _arrivedThisTick = true;

                // Ice: he slides on in the same direction until something stops him or the ice
                // ends. Swipes made meanwhile wait until he stops.
                if (_vault.TileAt(w.Tile) == TileType.Ice && IsWalkableNow(w.Tile.Step(w.MoveDir)))
                {
                    w.Sliding = true;
                    w.Moving = true;
                    w.Progress = leftover;
                    return;
                }

                w.Sliding = false;
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

        /// <summary>
        /// Pits: Wick has just arrived on one. He loses a flicker (unless invulnerable); if that was
        /// his last he is snuffed, otherwise he spends PitFallTicks in the pit and pops out at the
        /// entrance. A level 5 pit also puts every carried pile back where it came from.
        /// </summary>
        private void StartFall()
        {
            ref WickState w = ref State.Wick;
            int level = _vault.LevelOf(ObjectType.Pit);
            w.Moving = false;
            w.Progress = 0;
            w.Running = false;
            w.HasQueued = false;
            w.KnockTiles = 0;
            w.Sliding = false;
            if (w.InvulnTicks == 0)
            {
                w.Flickers--;
            }

            Emit(SimEventType.PitFall, 0, w.Tile, w.Flickers);
            if (w.Flickers <= 0)
            {
                Emit(SimEventType.Snuffed, 0, w.Tile, 0);
                return;
            }

            w.FallTicks = DefenceStats.PitFallTicks(level);
            if (DefenceStats.PitDropsLoot(level))
            {
                for (int i = 0; i < _vault.ObjectCount; i++)
                {
                    if (State.Carried[i])
                    {
                        ObjectDef pile = _vault.GetObject(i);
                        State.Carried[i] = false;
                        State.Spent[i] = false;
                        Emit(SimEventType.LootDropped, pile.Id, pile.Pos, pile.Value);
                    }
                }

                w.CarriedTreasure = 0;
                w.CarriedPiles = 0;
            }
        }

        /// <summary>Out of the pit: on the entrance facing in, briefly invulnerable, with the exit shut for a while.</summary>
        private void RespawnFromPit()
        {
            ref WickState w = ref State.Wick;
            w.Tile = _vault.Entrance;
            w.Facing = Dir.N;
            for (int d = 0; d < 4; d++)
            {
                if (_vault.IsWalkable(_vault.Entrance.Step((Dir)d)))
                {
                    w.Facing = (Dir)d;
                    break;
                }
            }

            w.InvulnTicks = SimTuning.InvulnTicks + 1; // step 5 counts it down this tick, as after a hit
            Emit(SimEventType.PitRespawn, 0, w.Tile, w.Flickers);
            w.ExitLockTicks = SimTuning.PitRespawnExitLockTicks;
            if (w.ExitLockTicks > 0)
            {
                Emit(SimEventType.ExitLocked, 0, w.Tile, w.ExitLockTicks);
            }
        }

        /// <summary>A Drip Pipe level 5 puddle under Wick slows him to tiptoe speed.</summary>
        private int PuddleSlowed(int speed, in WickState w)
        {
            return !w.Tiptoe && IsPuddle(w.Occupied) ? speed / SimTuning.TiptoeSpeedDivisor : speed;
        }

        private void UpdateLight()
        {
            Lighting.Compute(_vault, in State.Wick, State.Lit);
        }

        /// <summary>Walkable this tick: the vault's walls and crates, plus any closed gate.</summary>
        public bool IsWalkableNow(TilePos p)
        {
            return _vault.IsWalkable(p) && !IsClosedGate(p);
        }

        private bool TryStartMove(ref WickState w, Dir dir, bool isRun)
        {
            if (!IsWalkableNow(w.Tile.Step(dir)))
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
            else if (_arrivedThisTick && w.LeftEntrance && w.Tile == _vault.Entrance && w.ExitLockTicks == 0)
            {
                State.Outcome = Outcome.Escaped;
            }
            else if (State.Tick + 1 >= Params.RaidTicks)
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
