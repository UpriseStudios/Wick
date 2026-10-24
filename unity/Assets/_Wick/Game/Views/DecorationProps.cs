using UnityEngine;
using Wick.Sim;

namespace Wick.Game
{
    /// <summary>
    /// Builds a decoration's model. Until phase 2 art supplies prop_banner, prop_statue,
    /// prop_trophy_* and friends, each is a stand-in made from an existing prop prefab, scaled
    /// and tinted so the kinds read apart. Presentation only.
    /// </summary>
    public static class DecorationProps
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly Color Cloth = new Color(0.75f, 0.18f, 0.16f);
        private static readonly Color Stone = new Color(0.62f, 0.6f, 0.58f);
        private static readonly Color Gold = new Color(1f, 0.8f, 0.25f);

        public static GameObject Spawn(DecorationDef d, VaultDef vault, ViewRegistry registry, Transform parent)
        {
            return Spawn(d.Kind, d.Pos, d.Rot, vault.TileAt, registry, parent);
        }

        public static GameObject Spawn(string kind, TilePos pos, int rot, System.Func<TilePos, TileType> tileAt, ViewRegistry registry,
            Transform parent)
        {
            GameObject prefab;
            Vector3 scale;
            Color? tint;
            switch (kind)
            {
                case Decorations.Torch:
                    prefab = registry.Brazier;
                    scale = new Vector3(0.6f, 0.9f, 0.6f);
                    tint = null;
                    break;
                case Decorations.GoldPile:
                    prefab = registry.Treasure;
                    scale = Vector3.one * 0.8f;
                    tint = null;
                    break;
                case Decorations.Banner:
                    prefab = registry.Crate;
                    scale = new Vector3(0.6f, 1.4f, 0.08f);
                    tint = Cloth;
                    break;
                case Decorations.Statue:
                    prefab = registry.Crate;
                    scale = new Vector3(0.45f, 1.5f, 0.45f);
                    tint = Stone;
                    break;
                default: // trophies
                    prefab = registry.Crate;
                    scale = new Vector3(0.4f, 0.7f, 0.4f);
                    tint = Gold;
                    break;
            }

            if (prefab == null)
            {
                return null;
            }

            GameObject go = Object.Instantiate(prefab, parent);
            go.name = kind + " (stand-in)";
            foreach (MonoBehaviour b in go.GetComponentsInChildren<MonoBehaviour>())
            {
                b.enabled = false; // a model, not a raid object
            }

            go.transform.localScale = scale;
            go.transform.position = SimPose.TileCenter(pos);
            go.transform.rotation = SimPose.Facing((Dir)rot);
            if (Decorations.IsWallMounted(kind))
            {
                // Hang it on the wall face that looks onto floor.
                for (int d = 0; d < 4; d++)
                {
                    var dir = (Dir)d;
                    if (tileAt(pos.Step(dir)) != TileType.Wall)
                    {
                        go.transform.rotation = SimPose.Facing(dir);
                        go.transform.position = SimPose.TileCenter(pos) + SimPose.DirVector(dir) * 0.55f + Vector3.up * 0.4f;
                        break;
                    }
                }
            }

            if (tint.HasValue)
            {
                var block = new MaterialPropertyBlock();
                foreach (Renderer r in go.GetComponentsInChildren<Renderer>())
                {
                    r.GetPropertyBlock(block);
                    block.SetColor(BaseColorId, tint.Value);
                    r.SetPropertyBlock(block);
                }
            }

            return go;
        }
    }

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
