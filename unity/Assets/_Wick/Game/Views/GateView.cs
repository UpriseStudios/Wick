using UnityEngine;
using Wick.Sim;

namespace Wick.Game
{
    /// <summary>Lever Doors: the gate's bars rise while the sim has it open and drop when it closes.</summary>
    public sealed class GateView : EntityView
    {
        [SerializeField] private Transform _bars;

        private const float OpenHeight = 1.3f;

        protected override void OnInit()
        {
            // Bars run across the doorway: along the axis whose neighbours are walls.
            bool wallsEastWest = Ctx.Vault.TileAt(Def.Pos.Step(Dir.E)) == TileType.Wall;
            transform.rotation = Quaternion.Euler(0f, wallsEastWest ? 0f : 90f, 0f);
        }

        public override void Sync()
        {
            SetVisible(IsVisible(Def.Pos));
            if (_bars == null)
            {
                return;
            }

            bool open = Ctx.Runner.Sim.State.GateOpen[ObjectIndex] > 0;
            Vector3 target = new Vector3(0f, open ? OpenHeight : 0f, 0f);
            _bars.localPosition = Vector3.MoveTowards(_bars.localPosition, target, Time.deltaTime * 6f);
        }
    }
}
