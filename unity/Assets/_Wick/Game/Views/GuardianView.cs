using UnityEngine;
using Wick.Sim;

namespace Wick.Game
{
    /// <summary>Night Owl or Wax Moths: interpolated position, facing and a mode tint.</summary>
    public sealed class GuardianView : EntityView
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        [Tooltip("Tinted by mode: patrol, chase, distracted.")]
        [SerializeField] private Renderer _modeIndicator;

        [SerializeField] private Color _patrol = new Color(0.8f, 0.8f, 0.7f);
        [SerializeField] private Color _chase = new Color(1f, 0.15f, 0.1f);
        [SerializeField] private Color _distracted = new Color(1f, 0.85f, 0.2f);

        private int _guardian = -1;
        private MaterialPropertyBlock _block;

        // The owl's lantern eyes stay readable in the dark wherever the owl is.
        protected override TilePos GlowTile => _guardian >= 0 ? Ctx.Current.Guardians[_guardian].Occupied : Def.Pos;
        private GuardianMode _shownMode = (GuardianMode)255;

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

            _block = new MaterialPropertyBlock();
            transform.rotation = SimPose.Facing(Def.Facing);
        }

        public override void Sync()
        {
            if (_guardian < 0)
            {
                return;
            }

            GuardianState prev = Ctx.Previous.Guardians[_guardian];
            GuardianState cur = Ctx.Current.Guardians[_guardian];
            transform.position = Vector3.Lerp(SimPose.Position(prev), SimPose.Position(cur), Ctx.Alpha);
            transform.rotation = Quaternion.Slerp(transform.rotation, SimPose.Facing(cur.Facing), Time.deltaTime * 12f);
            SetVisible(IsVisible(cur.Occupied));

            if (_modeIndicator != null && cur.Mode != _shownMode)
            {
                _shownMode = cur.Mode;
                Color c = cur.Mode == GuardianMode.Chase ? _chase : cur.Mode == GuardianMode.Distracted ? _distracted : _patrol;
                _modeIndicator.GetPropertyBlock(_block);
                _block.SetColor(BaseColorId, c);
                _block.SetColor(EmissionId, c * 0.6f);
                _modeIndicator.SetPropertyBlock(_block);
            }
        }
    }
}
