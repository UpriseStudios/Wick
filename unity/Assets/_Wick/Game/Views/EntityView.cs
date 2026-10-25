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

        /// <summary>The vault theme's lighting (always set: the mask falls back to the defaults).</summary>
        public LightingProfile Lighting => Mask.Profile;

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
        private ReadableGlow[] _glows;
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
            // Glows stay readable in the dark, so SetVisible leaves their renderers alone.
            _glows = GetComponentsInChildren<ReadableGlow>(true);
            var body = new System.Collections.Generic.List<Renderer>();
            foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
            {
                if (r.GetComponentInParent<ReadableGlow>(true) == null)
                {
                    body.Add(r);
                }
            }

            _renderers = body.ToArray();
            transform.position = SimPose.TileCenter(Def.Pos);
            OnInit();
            foreach (ReadableGlow g in _glows)
            {
                if (g.Kind != ReadableKind.GuardianEyes)
                {
                    g.SetColor(ReadableGlow.ColorOf(g.Kind, ctx.Lighting));
                }
            }
        }

        /// <summary>The tile whose light decides whether this view's glows count as lit.</summary>
        protected virtual TilePos GlowTile => Def.Pos;

        /// <summary>Shows or hides the always-readable parts; VaultView calls it after Sync.</summary>
        public void SyncGlows()
        {
            bool lit = IsVisible(GlowTile);
            foreach (ReadableGlow g in _glows)
            {
                g.Apply(lit, Ctx.Lighting);
            }
        }

        /// <summary>This view's glow of a kind, or null.</summary>
        protected ReadableGlow Glow(ReadableKind kind)
        {
            foreach (ReadableGlow g in _glows)
            {
                if (g.Kind == kind)
                {
                    return g;
                }
            }

            return null;
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
