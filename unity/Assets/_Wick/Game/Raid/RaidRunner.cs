using System;
using UnityEngine;
using Wick.Sim;

namespace Wick.Game
{
    public enum RaidMode
    {
        /// <summary>No raid loaded.</summary>
        Idle,

        /// <summary>Raid loaded, clock not started (scout flyover).</summary>
        Scout,

        /// <summary>Clock running, player input accepted.</summary>
        Live,

        /// <summary>Clock running from a recorded log; input ignored.</summary>
        Replay,

        Ended,
    }

    /// <summary>
    /// Steps Wick.Sim at a fixed 30 Hz from frame time and keeps the previous and current
    /// snapshots so views can interpolate. Views read state from here and consume sim events;
    /// they never change the sim.
    /// </summary>
    public sealed class RaidRunner : MonoBehaviour
    {
        private readonly TickAccumulator _accumulator = new TickAccumulator();
        private InputLog _replayLog;

        public RaidMode Mode { get; private set; } = RaidMode.Idle;
        public RaidSim Sim { get; private set; }
        public VaultDef Vault { get; private set; }
        public int Seed { get; private set; }

        public RaidSnapshot Previous { get; private set; } = new RaidSnapshot();
        public RaidSnapshot Current { get; private set; } = new RaidSnapshot();

        /// <summary>Fraction of the way from Previous to Current, for interpolation.</summary>
        public float Alpha { get; private set; } = 1f;

        /// <summary>A raid was loaded (live or replay); views rebuild.</summary>
        public event Action<RaidRunner> Loaded;

        /// <summary>One sim event from the tick that just ran.</summary>
        public event Action<SimEvent> SimEventRaised;

        /// <summary>A tick finished; Current is the new state.</summary>
        public event Action<RaidRunner> Ticked;

        public event Action<RaidRunner, RaidResult> Ended;

        public bool IsLive => Mode == RaidMode.Live;

        /// <summary>Replays only: holds the clock without unloading.</summary>
        public bool Paused { get; set; }

        /// <summary>Loads a raid for the player, paused for the scout flyover.</summary>
        public void LoadLive(VaultDef vault, int seed)
        {
            _replayLog = null;
            Load(new RaidSim(vault, seed), vault, seed, RaidMode.Scout);
        }

        /// <summary>Loads a recorded raid; it starts playing straight away.</summary>
        public void LoadReplay(VaultDef vault, int seed, InputLog log)
        {
            _replayLog = log;
            Paused = false;
            Load(new RaidSim(vault, seed, log), vault, seed, RaidMode.Replay);
        }

        /// <summary>
        /// Jumps a replay to a tick by re-simulating from tick 0 without raising events (a whole
        /// raid takes about a millisecond), then carries on from there. Seeking to the end doesn't
        /// raise Ended; playing on into it does.
        /// </summary>
        public void SeekReplay(int tick)
        {
            if (_replayLog == null || Vault == null || (Mode != RaidMode.Replay && Mode != RaidMode.Ended))
            {
                return;
            }

            var sim = new RaidSim(Vault, Seed, _replayLog);
            while (!sim.IsOver && sim.State.Tick < tick)
            {
                sim.Step();
            }

            Load(sim, Vault, Seed, RaidMode.Replay);
            if (sim.IsOver)
            {
                Mode = RaidMode.Ended;
            }
        }

        /// <summary>Starts the raid clock after the scout.</summary>
        public void StartClock()
        {
            if (Mode == RaidMode.Scout)
            {
                _accumulator.Reset();
                Mode = RaidMode.Live;
            }
        }

        public void Unload()
        {
            Sim = null;
            _replayLog = null;
            Paused = false;
            Mode = RaidMode.Idle;
        }

        /// <summary>Queues a player command for the next tick. Ignored unless live.</summary>
        public bool Enqueue(CommandType type, Dir dir = Dir.N)
        {
            if (Mode != RaidMode.Live)
            {
                return false;
            }

            Sim.Submit(new Command(Sim.State.Tick, type, dir));
            return true;
        }

        private void Update()
        {
            Advance(Time.deltaTime);
        }

        /// <summary>Feeds frame time; steps the sim when the clock is running.</summary>
        public void Advance(double deltaSeconds)
        {
            if ((Mode != RaidMode.Live && Mode != RaidMode.Replay) || (Mode == RaidMode.Replay && Paused))
            {
                return;
            }

            int steps = _accumulator.Advance(deltaSeconds);
            for (int i = 0; i < steps && !Sim.IsOver; i++)
            {
                StepOnce();
            }

            Alpha = Mode == RaidMode.Ended ? 1f : _accumulator.Alpha;
        }

        private void StepOnce()
        {
            RaidSnapshot spare = Previous;
            Previous = Current;
            Current = spare;

            Sim.Step();
            Current.CopyFrom(Sim.State);

            for (int i = 0; i < Sim.Events.Count; i++)
            {
                SimEventRaised?.Invoke(Sim.Events[i]);
            }

            Ticked?.Invoke(this);

            if (Sim.IsOver)
            {
                Mode = RaidMode.Ended;
                Ended?.Invoke(this, Sim.GetResult());
            }
        }

        private void Load(RaidSim sim, VaultDef vault, int seed, RaidMode mode)
        {
            Sim = sim;
            Vault = vault;
            Seed = seed;
            Previous.CopyFrom(sim.State);
            Current.CopyFrom(sim.State);
            Alpha = 1f;
            _accumulator.Reset();
            Mode = mode;
            Loaded?.Invoke(this);
        }
    }
}
