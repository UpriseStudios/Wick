using UnityEngine;
using Wick.Sim;

namespace Wick.Game
{
    /// <summary>
    /// Sets Wick's Animator parameters from sim state (tech design "Character reactions"):
    /// Fear (0-1), Flickers, Greed (treasure grabbed this second), Tiptoe and Outcome.
    /// One shared controller drives every skin.
    /// </summary>
    public sealed class ReactionDriver : MonoBehaviour
    {
        public static readonly int FearId = Animator.StringToHash("Fear");
        public static readonly int FlickersId = Animator.StringToHash("Flickers");
        public static readonly int GreedId = Animator.StringToHash("Greed");
        public static readonly int TiptoeId = Animator.StringToHash("Tiptoe");
        public static readonly int OutcomeId = Animator.StringToHash("Outcome");

        [SerializeField] private Animator _animator;

        private ViewContext _ctx;
        private float _greed;
        private float _fear;

        public float Fear => _fear;
        public float Greed => _greed;

        public void Init(ViewContext ctx)
        {
            _ctx = ctx;
            _greed = 0f;
            _fear = 0f;
            if (_animator != null)
            {
                _animator.Rebind();
            }
        }

        public void Sync()
        {
            if (_ctx == null || _animator == null)
            {
                return;
            }

            RaidSnapshot cur = _ctx.Current;
            float targetFear = Reactions.Fear(_ctx.Runner.Sim, _ctx.Vault, _ctx.ShownTick, in cur.Wick, cur.Guardians,
                _ctx.Tuning.FearRangeTiles);
            _fear = Mathf.MoveTowards(_fear, targetFear, Time.deltaTime * 4f);
            _greed = Mathf.MoveTowards(_greed, 0f, Time.deltaTime / Mathf.Max(0.01f, _ctx.Tuning.GreedDecaySeconds));

            _animator.SetFloat(FearId, _fear);
            _animator.SetInteger(FlickersId, cur.Wick.Flickers);
            _animator.SetFloat(GreedId, _greed);
            _animator.SetBool(TiptoeId, cur.Wick.Tiptoe);
            _animator.SetInteger(OutcomeId, (int)_ctx.Runner.Sim.State.Outcome);
        }

        public void OnSimEvent(in SimEvent e)
        {
            if (e.Type == SimEventType.TreasureTaken)
            {
                _greed = 1f;
            }
        }
    }
}
