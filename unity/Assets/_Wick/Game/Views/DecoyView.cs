using UnityEngine;
using Wick.Sim;

namespace Wick.Game
{
    /// <summary>Wick's decoy wax drip: shown on its tile while it burns.</summary>
    public sealed class DecoyView : MonoBehaviour
    {
        private ViewContext _ctx;

        public void Init(ViewContext ctx)
        {
            _ctx = ctx;
            gameObject.SetActive(false);
        }

        public void Sync()
        {
            if (_ctx == null)
            {
                return;
            }

            WickState w = _ctx.Current.Wick;
            bool burning = w.DecoyTicksLeft > 0;
            if (gameObject.activeSelf != burning)
            {
                gameObject.SetActive(burning);
            }

            if (burning)
            {
                transform.position = SimPose.TileCenter(w.DecoyTile);
            }
        }
    }
}
