using UnityEngine;
using Wick.Sim;

namespace Wick.Game
{
    /// <summary>
    /// A decoration in a raid: shown where Wick's light reaches, like everything else, and a trophy
    /// only once he has reached the hoard (so trophies are what a good raid gets to see).
    /// </summary>
    public sealed class DecorView : MonoBehaviour
    {
        private ViewContext _ctx;
        private TilePos _pos;
        private bool _trophy;
        private Renderer[] _renderers;
        private bool _visible = true;

        public void Init(ViewContext ctx, DecorationDef d)
        {
            _ctx = ctx;
            _pos = d.Pos;
            _trophy = Decorations.IsTrophy(d.Kind);
            _renderers = GetComponentsInChildren<Renderer>(true);
        }

        public void Sync()
        {
            bool visible = _ctx.Mask.IsVisible(_pos) && (!_trophy || _ctx.Current.Wick.ReachedHoard);
            if (visible == _visible)
            {
                return;
            }

            _visible = visible;
            foreach (Renderer r in _renderers)
            {
                r.enabled = visible;
            }
        }
    }
}
