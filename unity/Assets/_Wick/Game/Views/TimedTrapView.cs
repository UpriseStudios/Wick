using UnityEngine;
using Wick.Sim;

namespace Wick.Game
{
    /// <summary>
    /// Drip Pipe, Bellows and Spike Floor. Whether the trap is in its damaging phase comes from
    /// RaidSim.IsTrapActive (a pure function of tick, cycle and offset), never from a local timer.
    /// </summary>
    public sealed class TimedTrapView : EntityView
    {
        [Tooltip("Shown while the trap is in its damaging phase (drip, gust, raised spikes).")]
        [SerializeField] private Transform _active;

        [Tooltip("Flashes during the warning before the trap fires (spikes).")]
        [SerializeField] private Transform _warning;

        [Tooltip("Bellows only: stretched along the gust line.")]
        [SerializeField] private Transform _gust;

        private float _warnUntil;

        protected override void OnInit()
        {
            transform.rotation = SimPose.Facing(Def.Facing);

            // A bellows is built into a wall: sit it on the wall face it blows from.
            float face = Ctx.Vault.TileAt(Def.Pos) == TileType.Wall ? 0.5f : 0f;
            transform.position = SimPose.TileCenter(Def.Pos) + SimPose.DirVector(Def.Facing) * face;
            if (_gust != null)
            {
                int length = 0;
                TilePos t = Def.Pos;
                while (true)
                {
                    t = t.Step(Def.Facing);
                    if (!Ctx.Runner.Sim.OnGustLine(Def, t))
                    {
                        break;
                    }

                    length++;
                }

                _gust.localScale = new Vector3(_gust.localScale.x, _gust.localScale.y, Mathf.Max(length, 0.01f));
                _gust.localPosition = new Vector3(0f, _gust.localPosition.y, 0.5f - face + length * 0.5f);
            }
        }

        public override void Sync()
        {
            SetVisible(IsVisible(Def.Pos));
            bool active = Ctx.ClockRunning && RaidSim.IsTrapActive(Ctx.ShownTick, Def, Ctx.Vault.LevelOf(Def.Type));
            if (_active != null)
            {
                _active.gameObject.SetActive(active);
            }

            if (_gust != null)
            {
                _gust.gameObject.SetActive(active);
            }

            if (_warning != null)
            {
                bool warning = Time.time < _warnUntil;
                _warning.gameObject.SetActive(warning && Mathf.Repeat(Time.time * 8f, 1f) < 0.5f);
            }
        }

        public override void OnSimEvent(in SimEvent e)
        {
            if (e.Type == SimEventType.TrapWarning)
            {
                _warnUntil = Time.time + SimTuning.SpikeWarnTicks / (float)SimTuning.TicksPerSecond;
            }
        }
    }
}
