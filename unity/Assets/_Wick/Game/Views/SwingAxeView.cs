using UnityEngine;
using Wick.Sim;

namespace Wick.Game
{
    /// <summary>
    /// Swing Axe: the blade's angle is a pure function of the sim's phase, so what the player sees
    /// is exactly when the blade is live (over its tile at phase 0 and half a cycle later, out in
    /// front at a quarter). The swing itself is the tell.
    /// </summary>
    public sealed class SwingAxeView : EntityView
    {
        [Tooltip("Rotates about its local X axis: the blade's pivot, hanging from the beam.")]
        [SerializeField] private Transform _pivot;

        private const float MaxAngle = 75f;

        protected override void OnInit()
        {
            transform.rotation = SimPose.Facing(Def.Facing);
        }

        public override void Sync()
        {
            SetVisible(IsVisible(Def.Pos));
            if (_pivot == null)
            {
                return;
            }

            int level = Ctx.Vault.LevelOf(Def.Type);
            float cycle = DefenceStats.Cycle(Def, level);
            float phase = RaidSim.Phase(Ctx.ShownTick, Def, level) + (Ctx.ClockRunning ? Ctx.Alpha : 0f);
            float angle = Mathf.Sin(phase / cycle * Mathf.PI * 2f) * MaxAngle;
            _pivot.localRotation = Quaternion.Euler(angle, 0f, 0f);
        }
    }
}
