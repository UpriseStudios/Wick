using UnityEngine;
using Wick.Sim;

namespace Wick.Game
{
    /// <summary>What Wick is doing, as the reactions need it (from the sim, or set directly by tests).</summary>
    public struct ReactionInput
    {
        /// <summary>0 to 1, from Reactions.Fear.</summary>
        public float Fear;

        public int Flickers;
        public bool Tiptoe;

        /// <summary>(int)Outcome: 0 playing, 1 escaped, 2 snuffed.</summary>
        public int Outcome;

        /// <summary>0 standing, 1 a single step or dash, 2 running.</summary>
        public int Move;
    }

    /// <summary>
    /// Wick's reactions (tech design "Character reactions"). Sets the shared Animator controller's
    /// parameters (Fear, Flickers, Greed, Tiptoe, Outcome, Move, and the Hit, Grab and Fidget
    /// triggers) and the face blend shapes on the skin's body: fear, greed and smug, plus wince
    /// and blink. One controller and one set of shape names work for every skin.
    /// </summary>
    public sealed class ReactionDriver : MonoBehaviour
    {
        public static readonly int FearId = Animator.StringToHash("Fear");
        public static readonly int FlickersId = Animator.StringToHash("Flickers");
        public static readonly int GreedId = Animator.StringToHash("Greed");
        public static readonly int TiptoeId = Animator.StringToHash("Tiptoe");
        public static readonly int OutcomeId = Animator.StringToHash("Outcome");
        public static readonly int MoveId = Animator.StringToHash("Move");
        public static readonly int HitId = Animator.StringToHash("Hit");
        public static readonly int GrabId = Animator.StringToHash("Grab");
        public static readonly int FidgetId = Animator.StringToHash("Fidget");
        public static readonly int FidgetIndexId = Animator.StringToHash("FidgetIndex");

        public static readonly string[] FaceShapes = { "fear", "greed", "smug", "wince", "blink" };
        private const int Fear = 0, Greed = 1, Smug = 2, Wince = 3, Blink = 4;

        [SerializeField] private Animator _animator;
        [SerializeField] private SkinnedMeshRenderer _face;

        private readonly int[] _shape = { -1, -1, -1, -1, -1 };
        private readonly float[] _weight = new float[5];
        private ViewContext _ctx;
        private float _blinkIn = 3f;
        private float _idleFor;
        private float _fidgetIn = 6f;

        public float FearLevel => _weight[Fear];
        public float GreedLevel => _weight[Greed];
        public float SmugLevel => _weight[Smug];
        public Animator Animator => _animator;

        /// <summary>The skin's animator and face; WickSkin calls this whenever the skin changes.</summary>
        public void Bind(Animator animator, SkinnedMeshRenderer face)
        {
            _animator = animator;
            _face = face;
            for (int i = 0; i < FaceShapes.Length; i++)
            {
                _shape[i] = face != null && face.sharedMesh != null ? face.sharedMesh.GetBlendShapeIndex(FaceShapes[i]) : -1;
            }
        }

        public void Init(ViewContext ctx)
        {
            _ctx = ctx;
            System.Array.Clear(_weight, 0, _weight.Length);
            _idleFor = 0f;
            if (_animator != null)
            {
                _animator.Rebind();
            }
        }

        public void Sync()
        {
            if (_ctx == null)
            {
                return;
            }

            RaidSnapshot cur = _ctx.Current;
            WickState w = cur.Wick;
            Apply(new ReactionInput
            {
                Fear = Reactions.Fear(_ctx.Runner.Sim, _ctx.Vault, _ctx.ShownTick, in w, cur.Guardians, _ctx.Tuning.FearRangeTiles),
                Flickers = w.Flickers,
                Tiptoe = w.Tiptoe,
                Outcome = (int)_ctx.Runner.Sim.State.Outcome,
                Move = !w.Moving ? 0 : w.MoveIsRun ? 2 : 1,
            }, Time.deltaTime, _ctx.Tuning.GreedDecaySeconds);
        }

        /// <summary>Moves the face towards the input and sets the animator; dt in seconds.</summary>
        public void Apply(in ReactionInput input, float dt, float greedDecaySeconds = 1.5f)
        {
            _weight[Fear] = Mathf.MoveTowards(_weight[Fear], input.Fear, dt * 4f);
            _weight[Greed] = Mathf.MoveTowards(_weight[Greed], 0f, dt / Mathf.Max(0.01f, greedDecaySeconds));
            float smugTarget = input.Outcome == (int)Outcome.Escaped ? 1f : 0f;
            _weight[Smug] = Mathf.MoveTowards(_weight[Smug], Mathf.Max(smugTarget, _weight[Smug] - dt * 0.5f), dt * 3f);
            _weight[Wince] = Mathf.MoveTowards(_weight[Wince], 0f, dt * 2.5f);

            // Blinks are presentation only, so Unity's random is fine here (never the sim's).
            _blinkIn -= dt;
            if (_blinkIn <= 0f && input.Outcome != (int)Outcome.Snuffed)
            {
                _weight[Blink] = 1f;
                _blinkIn = Random.Range(2.5f, 5f);
            }

            _weight[Blink] = Mathf.MoveTowards(_weight[Blink], 0f, dt * 8f);

            if (_animator != null && _animator.runtimeAnimatorController != null)
            {
                _animator.SetFloat(FearId, _weight[Fear]);
                _animator.SetInteger(FlickersId, input.Flickers);
                _animator.SetFloat(GreedId, _weight[Greed]);
                _animator.SetBool(TiptoeId, input.Tiptoe);
                _animator.SetInteger(OutcomeId, input.Outcome);
                _animator.SetInteger(MoveId, input.Move);
                Fidget(input, dt);
            }

            if (_face != null)
            {
                for (int i = 0; i < _shape.Length; i++)
                {
                    if (_shape[i] >= 0)
                    {
                        _face.SetBlendShapeWeight(_shape[i], _weight[i] * 100f);
                    }
                }
            }
        }

        public void OnSimEvent(in SimEvent e)
        {
            switch (e.Type)
            {
                case SimEventType.TreasureTaken:
                    _weight[Greed] = 1f;
                    Trigger(GrabId);
                    break;
                case SimEventType.FlickerLost:
                    _weight[Wince] = 1f;
                    Trigger(HitId);
                    break;
                case SimEventType.NearMiss:
                    _weight[Smug] = Mathf.Max(_weight[Smug], 0.6f); // got away with it
                    break;
            }
        }

        /// <summary>After a few calm seconds standing still, pat the flame or count the loot.</summary>
        private void Fidget(in ReactionInput input, float dt)
        {
            bool calm = input.Move == 0 && !input.Tiptoe && input.Outcome == 0 && _weight[Fear] < 0.2f && input.Flickers > 1;
            _idleFor = calm ? _idleFor + dt : 0f;
            if (_idleFor < _fidgetIn)
            {
                return;
            }

            _animator.SetInteger(FidgetIndexId, Random.Range(0, 2));
            Trigger(FidgetId);
            _idleFor = 0f;
            _fidgetIn = Random.Range(6f, 10f);
        }

        private void Trigger(int id)
        {
            if (_animator != null && _animator.runtimeAnimatorController != null)
            {
                _animator.SetTrigger(id);
            }
        }
    }
}
