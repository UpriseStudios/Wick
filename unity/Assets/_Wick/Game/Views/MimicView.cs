using UnityEngine;
using Wick.Sim;

namespace Wick.Game
{
    /// <summary>
    /// Mimic Chest: it looks like a treasure pile. Its tells are a slow breath (always, when
    /// lit) and the lid creaking open as it winds up to bite, read from the sim's wind-up count.
    /// </summary>
    public sealed class MimicView : EntityView
    {
        [Tooltip("Scaled gently up and down: the breath.")]
        [SerializeField] private Transform _body;

        [Tooltip("Rotates about its local X axis as the mimic winds up.")]
        [SerializeField] private Transform _lid;

        private const float BreathDepth = 0.05f;
        private const float BreathSeconds = 2.2f;
        private const float OpenAngle = -55f;

        private int _guardian = -1;
        private Vector3 _bodyScale;

        protected override void OnInit()
        {
            GuardianState[] all = Ctx.Runner.Sim.State.Guardians;
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].ObjectIndex == ObjectIndex)
                {
                    _guardian = i;
                }
            }

            transform.rotation = SimPose.Facing(Def.Facing);
            _bodyScale = _body != null ? _body.localScale : Vector3.one;
        }

        public override void Sync()
        {
            SetVisible(IsVisible(Def.Pos));
            if (_guardian < 0)
            {
                return;
            }

            if (_body != null)
            {
                float breath = 1f + Mathf.Sin(Time.time * Mathf.PI * 2f / BreathSeconds) * BreathDepth;
                _body.localScale = new Vector3(_bodyScale.x, _bodyScale.y * breath, _bodyScale.z);
            }

            if (_lid != null)
            {
                GuardianState cur = Ctx.Current.Guardians[_guardian];
                int lunge = DefenceStats.MimicLungeTicks(Ctx.Vault.LevelOf(Def.Type));
                float open = Mathf.Clamp01(cur.ContactTicks / (float)lunge);
                _lid.localRotation = Quaternion.Euler(open * OpenAngle, 0f, 0f);
            }
        }
    }
}
