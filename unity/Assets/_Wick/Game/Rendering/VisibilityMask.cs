using UnityEngine;
using Wick.Sim;

namespace Wick.Game
{
    /// <summary>
    /// Copies Wick.Sim's visibility into a one-texel-per-tile texture for Wick/StylisedLit and
    /// Wick/Flame (Art/Shaders/WickVisibility.hlsl), and is the single place views ask "is this
    /// tile visible?". During the scout it shows
    /// Lighting.ComputeScout; while the clock runs, the sim's lit set (RaidState.Lit).
    /// It never computes visibility itself.
    /// </summary>
    public sealed class VisibilityMask : MonoBehaviour
    {
        private static readonly int MaskId = Shader.PropertyToID("_WickVisMask");
        private static readonly int GridId = Shader.PropertyToID("_WickVisGrid");
        private static readonly int DarkId = Shader.PropertyToID("_WickVisDark");
        private static readonly int BoostId = Shader.PropertyToID("_WickLitBoost");

        [SerializeField] private RaidRunner _runner;
        [SerializeField] private RaidTuning _tuning;

        private Texture2D _texture;
        private byte[] _pixels;
        private bool[] _scout;
        private VaultDef _vault;
        private bool _wroteScout;
        private int _wroteTick = -1;

        public Texture2D Texture => _texture;

        public void Bind(RaidRunner runner, RaidTuning tuning)
        {
            _runner = runner;
            _tuning = tuning;
        }

        /// <summary>Whether tile p is shown as lit right now (exactly the mask's texel).</summary>
        public bool IsVisible(TilePos p)
        {
            if (_vault == null || !_vault.InBounds(p))
            {
                return false;
            }

            return _pixels[p.Y * _vault.Width + p.X] != 0;
        }

        private void LateUpdate()
        {
            Refresh();
        }

        private void OnDisable()
        {
            Shader.SetGlobalVector(GridId, Vector4.zero);
        }

        /// <summary>Rewrites the texture if the sim state or mode changed since the last write.</summary>
        public void Refresh()
        {
            if (_runner == null || _runner.Sim == null)
            {
                Shader.SetGlobalVector(GridId, Vector4.zero);
                _vault = null;
                return;
            }

            if (_vault != _runner.Vault)
            {
                Allocate(_runner.Vault);
            }

            bool scout = _runner.Mode == RaidMode.Scout;
            int tick = _runner.Sim.State.Tick;
            if (scout == _wroteScout && tick == _wroteTick)
            {
                return;
            }

            if (scout)
            {
                Lighting.ComputeScout(_vault, _scout);
                Write(_scout);
            }
            else
            {
                Write(_runner.Sim.State.Lit);
            }

            _wroteScout = scout;
            _wroteTick = tick;
        }

        private void Allocate(VaultDef vault)
        {
            _vault = vault;
            if (_texture != null)
            {
                DestroySafe(_texture);
            }

            _texture = new Texture2D(vault.Width, vault.Height, TextureFormat.R8, false, true)
            {
                name = "WickVisibilityMask",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            _pixels = new byte[vault.Width * vault.Height];
            _scout = new bool[vault.Width * vault.Height];
            _wroteTick = -1;

            Shader.SetGlobalTexture(MaskId, _texture);
            Shader.SetGlobalVector(GridId, new Vector4(vault.Width, vault.Height, 1f, 0f));
            ApplyTuning();
        }

        // Texel (x, y) is tile (x, y): row y of the data is texture row y.
        private void Write(bool[] lit)
        {
            for (int i = 0; i < _pixels.Length; i++)
            {
                _pixels[i] = lit[i] ? (byte)255 : (byte)0;
            }

            _texture.SetPixelData(_pixels, 0);
            _texture.Apply(false);
            ApplyTuning();
        }

        private void ApplyTuning()
        {
            Shader.SetGlobalFloat(DarkId, _tuning != null ? _tuning.UnlitBrightness : 0.04f);
            Shader.SetGlobalFloat(BoostId, _tuning != null ? _tuning.LitBoost : 0.2f);
        }

        private static void DestroySafe(Object o)
        {
            if (Application.isPlaying)
            {
                Object.Destroy(o);
            }
            else
            {
                DestroyImmediate(o);
            }
        }
    }
}
