using UnityEngine;
using Wick.Sim;

namespace Wick.Game
{
    /// <summary>What a view needs to read the raid. Views never change the sim.</summary>
    public sealed class ViewContext
    {
        public RaidRunner Runner;
        public VaultDef Vault;
        public VisibilityMask Mask;
        public RaidTuning Tuning;

        public RaidSnapshot Previous => Runner.Previous;
        public RaidSnapshot Current => Runner.Current;
        public float Alpha => Runner.Alpha;

        /// <summary>The tick whose state Current shows (the last one simulated), or 0 before any.</summary>
        public int ShownTick => Current.Tick > 0 ? Current.Tick - 1 : 0;

        public bool ClockRunning => Runner.Mode == RaidMode.Live || Runner.Mode == RaidMode.Replay || Runner.Mode == RaidMode.Ended;
    }

    /// <summary>Base for the view of one vault object (trap, guardian, pickup, crate).</summary>
    public abstract class EntityView : MonoBehaviour
    {
        private Renderer[] _renderers;
        private bool _visible = true;

        protected ViewContext Ctx { get; private set; }
        protected int ObjectIndex { get; private set; }
        protected ObjectDef Def { get; private set; }

        protected bool Spent => Ctx.Runner.Sim.State.Spent[ObjectIndex];

        public int EntityId => Def.Id;

        public void Init(ViewContext ctx, int objectIndex)
        {
            Ctx = ctx;
            ObjectIndex = objectIndex;
            Def = ctx.Vault.GetObject(objectIndex);
            _renderers = GetComponentsInChildren<Renderer>(true);
            transform.position = SimPose.TileCenter(Def.Pos);
            OnInit();
        }

        protected virtual void OnInit()
        {
        }

        /// <summary>Called every frame after the runner has stepped.</summary>
        public abstract void Sync();

        public virtual void OnSimEvent(in SimEvent e)
        {
        }

        protected bool IsVisible(TilePos p)
        {
            return Ctx.Mask.IsVisible(p);
        }

        protected void SetVisible(bool visible)
        {
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
