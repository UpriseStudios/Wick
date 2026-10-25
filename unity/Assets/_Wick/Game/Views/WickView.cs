using UnityEngine;
using Wick.Sim;

namespace Wick.Game
{
    /// <summary>
    /// Wick: interpolated position and facing, his skin and his flame. The flame's point light
    /// (docs/m10-2-lighting.md) reaches the sim's light radius (Lighting.RadiusUnits) plus half a
    /// tile, easing over the profile's FlameEaseSeconds, so tiptoe and a Bellows gust visibly shrink
    /// the pool. It flickers gently, frantically at 1 flicker, flares on gold and braziers, and fades
    /// out on a snuff; Wick/Flame grows frantic as flickers run out. The light is decoration only:
    /// visibility is the mask.
    /// </summary>
    public sealed class WickView : MonoBehaviour
    {
        private static readonly int FlickersId = Shader.PropertyToID("_Flickers");
        private static readonly int FlareId = Shader.PropertyToID("_Flare");

        [SerializeField] private Transform _body;
        [SerializeField] private Light _flameLight;
        [SerializeField] private ReactionDriver _reactions;
        [SerializeField] private WickSkin _skin;

        private ViewContext _ctx;
        private float _range;
        private float _baseRange;
        private float _rangeVelocity;
        private float _flare;
        private float _snuff = 1f;
        private MaterialPropertyBlock _flameProps;

        public WickSkin Skin => _skin;
        public ReactionDriver Reactions => _reactions;

        public void Init(ViewContext ctx)
        {
            _ctx = ctx;
            transform.position = SimPose.Position(ctx.Current.Wick);
            transform.rotation = SimPose.Facing(ctx.Current.Wick.Facing);
            _range = TargetRange();
            _baseRange = Mathf.Max(0.01f, _range); // the flame's rest size is a full, calm flame
            _flameProps ??= new MaterialPropertyBlock();
            _snuff = ctx.Current.Wick.Flickers > 0 ? 1f : 0f;
            if (_flameLight != null)
            {
                _flameLight.color = ctx.Lighting.FlameColor;
                _flameLight.shadows = LightShadows.None;
            }

            if (_skin != null)
            {
                _skin.ApplyEquipped();
                _reactions?.Bind(_skin.Animator, _skin.Face);
            }

            if (_reactions != null)
            {
                _reactions.Init(ctx);
            }
        }

        public void Sync()
        {
            if (_ctx == null)
            {
                return;
            }

            WickState prev = _ctx.Previous.Wick;
            WickState cur = _ctx.Current.Wick;
            transform.position = Vector3.Lerp(SimPose.Position(prev), SimPose.Position(cur), _ctx.Alpha);
            transform.rotation = Quaternion.Slerp(transform.rotation, SimPose.Facing(cur.Facing), Time.deltaTime * 15f);

            LightingProfile p = _ctx.Lighting;
            _range = Mathf.SmoothDamp(_range, TargetRange(), ref _rangeVelocity, p.FlameEaseSeconds * 0.5f); // settles in about FlameEaseSeconds
            _flare = Mathf.MoveTowards(_flare, 0f, Time.deltaTime / Mathf.Max(0.01f, p.FlareSeconds));

            bool lit = cur.Flickers > 0;
            _snuff = lit ? 1f : Mathf.MoveTowards(_snuff, 0f, Time.deltaTime / Mathf.Max(0.01f, p.SnuffSeconds));
            if (_flameLight != null)
            {
                _flameLight.enabled = _snuff > 0f;
                _flameLight.range = _range;
                _flameLight.intensity = FlameIntensity(p, cur.Flickers, _flare, Time.time) * _snuff;
            }

            if (_skin != null && _skin.Flame != null)
            {
                float scale = Mathf.Clamp(_range / _baseRange, 0.4f, 1.4f) * (1f + _flare * 0.3f);
                _skin.Flame.localScale = Vector3.one * scale;
            }

            Renderer flame = _skin != null ? _skin.FlameRenderer : null;
            if (flame != null)
            {
                flame.enabled = lit;
                flame.GetPropertyBlock(_flameProps);
                _flameProps.SetFloat(FlickersId, cur.Flickers);
                _flameProps.SetFloat(FlareId, _flare);
                flame.SetPropertyBlock(_flameProps);
            }

            if (_skin != null && _skin.FlameTip != null)
            {
                foreach (Transform vfx in _skin.FlameTip)
                {
                    vfx.gameObject.SetActive(lit);
                }
            }

            if (_reactions != null)
            {
                _reactions.Sync();
            }
        }

        public void OnSimEvent(in SimEvent e)
        {
            if (_reactions != null)
            {
                _reactions.OnSimEvent(e);
            }

            if (e.Type == SimEventType.TreasureTaken || e.Type == SimEventType.BrazierUsed)
            {
                _flare = 1f;
            }

            if (e.Type == SimEventType.Snuffed && _skin?.Current?.SnuffVfx != null)
            {
                Transform at = _skin.FlameTip != null ? _skin.FlameTip : transform;
                GameObject puff = Instantiate(_skin.Current.SnuffVfx, at.position, Quaternion.identity, transform.parent);
                Destroy(puff, 4f);
            }
        }

        private float TargetRange()
        {
            return FlameRange(_ctx.Lighting, in _ctx.Current.Wick);
        }

        /// <summary>The light's range for this Wick: the sim's light radius in tiles plus the profile's extra half tile.</summary>
        public static float FlameRange(LightingProfile p, in WickState wick)
        {
            return p.FlameRange(Lighting.RadiusUnits(in wick) / (float)SubTile.PerTile);
        }

        /// <summary>
        /// Intensity at time t: a gentle flicker (FlickerAmount at FlickerHz), frantic (FranticAmount,
        /// faster) at 1 flicker, and FlareBoost times the flare (1 just after grabbing gold, easing to 0).
        /// </summary>
        public static float FlameIntensity(LightingProfile p, int flickers, float flare, float t)
        {
            float amount = flickers <= 1 ? p.FranticAmount : p.FlickerAmount;
            float hz = flickers <= 1 ? p.FlickerHz * 2.2f : p.FlickerHz;
            float w = t * hz * Mathf.PI * 2f;
            float wobble = 0.6f * Mathf.Sin(w) + 0.4f * Mathf.Sin(w * 2.7f + 1.3f); // stays within -1..1
            return p.FlameIntensity * (1f + amount * wobble) * (1f + p.FlareBoost * flare);
        }
    }
}
