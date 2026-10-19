using UnityEngine;
using Wick.Sim;

namespace Wick.Game
{
    /// <summary>
    /// Plays effects from sim events: gold bursting from a grab, a brazier's flare, the flame
    /// sputtering on a lost flicker, a teal splash under a drip, swirls along a Bellows gust and a
    /// red glow on a spike warning. Effects on tiles the player can't see are skipped, so VFX never
    /// reveal anything the visibility mask hides. Presentation only.
    /// </summary>
    public sealed class VfxDirector : MonoBehaviour
    {
        [SerializeField] private VfxLibrary _library;
        [SerializeField] private VaultView _vaultView;

        public void Bind(VfxLibrary library, VaultView vaultView)
        {
            _library = library;
            _vaultView = vaultView;
        }

        private void OnEnable()
        {
            _vaultView.SimEventRaised += OnSimEvent;
        }

        private void OnDisable()
        {
            _vaultView.SimEventRaised -= OnSimEvent;
        }

        private void OnSimEvent(SimEvent e)
        {
            ViewContext ctx = _vaultView.Context;
            if (ctx == null || _library == null)
            {
                return;
            }

            Vector3 wick = _vaultView.Wick != null ? _vaultView.Wick.transform.position : SimPose.TileCenter(e.Tile);
            switch (e.Type)
            {
                case SimEventType.TreasureTaken:
                    Spawn(VfxLibrary.CoinBurst, wick + Vector3.up * 0.4f, e.Tile, ctx);
                    break;
                case SimEventType.BrazierUsed:
                    Spawn(VfxLibrary.FlameFlare, wick + Vector3.up * 0.7f, e.Tile, ctx);
                    break;
                case SimEventType.FlickerLost:
                    Spawn(VfxLibrary.FlameSputter, wick + Vector3.up * 0.7f, e.Tile, ctx);
                    break;
                case SimEventType.TrapWarning:
                    Spawn(VfxLibrary.WarningGlow, SimPose.TileCenter(e.Tile) + Vector3.up * 0.05f, e.Tile, ctx);
                    break;
                case SimEventType.TrapFired:
                    ObjectDef source = Find(ctx.Vault, e.EntityId);
                    if (source != null && source.Type == ObjectType.DripPipe)
                    {
                        Spawn(VfxLibrary.WaterSplash, SimPose.TileCenter(e.Tile) + Vector3.up * 0.05f, e.Tile, ctx);
                    }
                    else if (source != null && source.Type == ObjectType.Bellows)
                    {
                        // Swirls along the gust line, facing the way the air goes.
                        ObjectDef b = source;
                        for (int i = 1; i <= SimTuning.GustRange; i++)
                        {
                            TilePos p = b.Pos;
                            for (int s = 0; s < i; s++)
                            {
                                p = p.Step(b.Facing);
                            }

                            if (!ctx.Vault.InBounds(p) || !ctx.Vault.IsWalkable(p))
                            {
                                break;
                            }

                            Spawn(VfxLibrary.GustSwirl, SimPose.TileCenter(p) + Vector3.up * 0.4f, p, ctx, SimPose.Facing(b.Facing));
                        }
                    }

                    break;
            }
        }

        private void Spawn(string id, Vector3 at, TilePos tile, ViewContext ctx, Quaternion? facing = null)
        {
            VfxEntry entry = _library.Find(id);
            if (entry?.Prefab == null || !ctx.Mask.IsVisible(tile))
            {
                return;
            }

            GameObject fx = Instantiate(entry.Prefab, at, facing ?? Quaternion.identity, _vaultView.transform);
            Destroy(fx, 3f);
        }

        private static ObjectDef Find(VaultDef vault, int id)
        {
            for (int i = 0; i < vault.ObjectCount; i++)
            {
                if (vault.GetObject(i).Id == id)
                {
                    return vault.GetObject(i);
                }
            }

            return null;
        }
    }
}
