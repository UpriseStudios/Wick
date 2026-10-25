using UnityEngine;
using Wick.Sim;

namespace Wick.Game
{
    /// <summary>
    /// A wall brazier: a plaque on the wall face next to walkable floor. While lit, its flame and a
    /// light blob on the floor in front (docs/m10-2-lighting.md) stay readable in the dark; once
    /// used, both fade out over the profile's BlobFadeSeconds and that corner goes dark.
    /// </summary>
    public sealed class BrazierView : EntityView
    {
        [SerializeField] private Transform _flame;

        [Tooltip("The light blob: a quad on the floor in front of the brazier (Wick/LightBlob).")]
        [SerializeField] private Transform _blob;

        private float _fade = 1f;
        private ReadableGlow _flameGlow;
        private ReadableGlow _blobGlow;

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

            _flameGlow = _flame != null ? _flame.GetComponent<ReadableGlow>() : null;
            _blobGlow = _blob != null ? _blob.GetComponent<ReadableGlow>() : null;
            if (_blob != null)
            {
                float size = Ctx.Lighting.BlobRadiusTiles * 2f;
                _blob.localScale = new Vector3(size, size, 1f);
            }
        }

        // The blob lies on the floor tile in front, which is where its light reads as lit or not.
        protected override TilePos GlowTile => Def.Pos.Step(Facing());

        public override void Sync()
        {
            SetVisible(IsVisible(Def.Pos));
            float target = Spent ? 0f : 1f;
            _fade = Mathf.MoveTowards(_fade, target, Time.deltaTime / Mathf.Max(0.01f, Ctx.Lighting.BlobFadeSeconds));
            LightingProfile p = Ctx.Lighting;
            if (_blobGlow != null)
            {
                _blobGlow.Active = _fade > 0f;
                Color c = p.BlobColor;
                c.a = p.BlobOpacity;
                _blobGlow.SetColor(c, _fade);
            }

            if (_flameGlow != null)
            {
                _flameGlow.Active = _fade > 0f;
                _flame.localScale = Vector3.one * (0.22f * Mathf.Max(0.05f, _fade));
            }
            else if (_flame != null)
            {
                _flame.gameObject.SetActive(!Spent);
            }
        }

        private Dir Facing()
        {
            for (int d = 0; d < 4; d++)
            {
                if (Ctx.Vault.IsWalkable(Def.Pos.Step((Dir)d)))
                {
                    return (Dir)d;
                }
            }

            return Dir.S;
        }
    }
}
