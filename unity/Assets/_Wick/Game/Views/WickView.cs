using UnityEngine;
using Wick.Sim;

namespace Wick.Game
{
    /// <summary>
    /// Wick: interpolated position and facing, and his flame. The flame's point light range and
    /// the flame mesh size follow the sim's light radius (Lighting.RadiusUnits), so tiptoe and
    /// a Bellows gust visibly dim him. The light is decoration only: visibility is the mask.
    /// </summary>
    public sealed class WickView : MonoBehaviour
    {
        [SerializeField] private Transform _body;
        [SerializeField] private Transform _flame;
        [SerializeField] private Light _flameLight;
        [SerializeField] private ReactionDriver _reactions;

        private ViewContext _ctx;
        private float _range;
        private float _rangeVelocity;
        private float _flare;

        public void Init(ViewContext ctx)
        {
            _ctx = ctx;
            transform.position = SimPose.Position(ctx.Current.Wick);
            transform.rotation = SimPose.Facing(ctx.Current.Wick.Facing);
            _range = TargetRange();
            if (_flameLight != null)
            {
                _flameLight.color = ctx.Tuning.FlameColor;
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

            if (_flame != null)
            {
                _flame.gameObject.SetActive(lit);
                float size = 0.15f + 0.06f * _range / _ctx.Tuning.FlameRangePerTile + _flare * 0.15f;
                _flame.localScale = Vector3.one * size;
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
        }

        private float TargetRange()
        {
            float tiles = Lighting.RadiusUnits(in _ctx.Current.Wick) / (float)SubTile.PerTile;
            return tiles * _ctx.Tuning.FlameRangePerTile;
        }
    }
}
