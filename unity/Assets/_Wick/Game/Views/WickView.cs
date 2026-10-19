using UnityEngine;
using Wick.Sim;

namespace Wick.Game
{
    /// <summary>
    /// Wick: interpolated position and facing, his skin and his flame. The flame's point light
    /// range and the flame bone's scale follow the sim's light radius (Lighting.RadiusUnits), so
    /// tiptoe and a Bellows gust visibly dim him, and Wick/Flame grows frantic as flickers run
    /// out. The light is decoration only: visibility is the mask.
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
            if (_flameLight != null)
            {
                _flameLight.color = ctx.Tuning.FlameColor;
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

            _range = Mathf.SmoothDamp(_range, TargetRange(), ref _rangeVelocity, _ctx.Tuning.FlameSmoothTime);
            _flare = Mathf.MoveTowards(_flare, 0f, Time.deltaTime * 2f);

            bool lit = cur.Flickers > 0;
            if (_flameLight != null)
            {
                _flameLight.enabled = lit;
                _flameLight.range = _range;
                _flameLight.intensity = _ctx.Tuning.FlameIntensity * (1f + _flare);
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
            float tiles = Lighting.RadiusUnits(in _ctx.Current.Wick) / (float)SubTile.PerTile;
            return tiles * _ctx.Tuning.FlameRangePerTile;
        }
    }
}
