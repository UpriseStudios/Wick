using UnityEngine;
using Wick.Sim;

namespace Wick.Game
{
    /// <summary>A wall brazier: a plaque on the wall face next to walkable floor; flame out once used.</summary>
    public sealed class BrazierView : EntityView
    {
        [SerializeField] private Transform _flame;

        protected override void OnInit()
        {
            // Face the first walkable neighbour (N, E, S, W), the side Wick uses it from.
            for (int d = 0; d < 4; d++)
            {
                var dir = (Dir)d;
                if (Ctx.Vault.IsWalkable(Def.Pos.Step(dir)))
                {
                    transform.rotation = SimPose.Facing(dir);
                    transform.position = SimPose.TileCenter(Def.Pos) + SimPose.DirVector(dir) * 0.5f;
                    break;
                }
            }
        }

        public override void Sync()
        {
            SetVisible(IsVisible(Def.Pos));
            if (_flame != null)
            {
                _flame.gameObject.SetActive(!Spent);
            }
        }
    }
}
