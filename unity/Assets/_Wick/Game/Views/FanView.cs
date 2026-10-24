using UnityEngine;
using Wick.Sim;

namespace Wick.Game
{
    /// <summary>
    /// Fan Maze: the head turns to the sim's current facing (a quarter turn every quarter cycle,
    /// so the rotation can be learned), the blades spin, and the wind shows while it blows.
    /// </summary>
    public sealed class FanView : EntityView
    {
        [SerializeField] private Transform _head;
        [SerializeField] private Transform _blades;

        [Tooltip("Shown while the fan is blowing.")]
        [SerializeField] private Transform _wind;

        protected override void OnInit()
        {
            transform.rotation = Quaternion.identity;
        }

        public override void Sync()
        {
            SetVisible(IsVisible(Def.Pos));
            int level = Ctx.Vault.LevelOf(Def.Type);
            int tick = Ctx.ShownTick;
            if (_head != null)
            {
                Quaternion target = SimPose.Facing(RaidSim.FanFacing(tick, Def, level));
                _head.rotation = Quaternion.Slerp(_head.rotation, target, Time.deltaTime * 14f);
            }

            bool blowing = Ctx.ClockRunning && RaidSim.FanBlowing(tick, Def, level);
            if (_blades != null)
            {
                _blades.Rotate(0f, 0f, (blowing ? 900f : 120f) * Time.deltaTime, Space.Self);
            }

            if (_wind != null)
            {
                _wind.gameObject.SetActive(blowing);
            }
        }
    }
}
