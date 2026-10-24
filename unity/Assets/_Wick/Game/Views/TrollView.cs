using UnityEngine;
using Wick.Sim;

namespace Wick.Game
{
    /// <summary>
    /// Sleepy Troll: slumped and snoring while asleep (the snore bubble is the tell that it's safe
    /// to tiptoe past), standing when awake, and squashing down on each stomp tick from the sim.
    /// </summary>
    public sealed class TrollView : EntityView
    {
        [SerializeField] private Transform _body;

        [Tooltip("Pulses while it sleeps; hidden once it wakes.")]
        [SerializeField] private Transform _snore;

        private int _guardian = -1;
        private Vector3 _bodyScale;
        private float _stompUntil;

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

        public override void OnSimEvent(in SimEvent e)
        {
            if (e.Type == SimEventType.TrapFired)
            {
                _stompUntil = Time.time + 0.15f;
            }
        }

        public override void Sync()
        {
            SetVisible(IsVisible(Def.Pos));
            if (_guardian < 0)
            {
                return;
            }

            bool awake = Ctx.Current.Guardians[_guardian].Mode == GuardianMode.Chase;
            if (_body != null)
            {
                float height = awake ? (Time.time < _stompUntil ? 0.8f : 1f) : 0.6f;
                _body.localScale = new Vector3(_bodyScale.x, _bodyScale.y * height, _bodyScale.z);
            }

            if (_snore != null)
            {
                _snore.gameObject.SetActive(!awake);
                _snore.localScale = Vector3.one * (0.18f + Mathf.PingPong(Time.time * 0.12f, 0.1f));
            }
        }
    }
}
