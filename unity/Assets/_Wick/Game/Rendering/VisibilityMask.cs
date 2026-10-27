using UnityEngine;
using Wick.Sim;

namespace Wick.Game
{
    /// <summary>
    /// Copies Wick.Sim's visibility into a one-texel-per-tile texture for Wick/StylisedLit and
    /// Wick/Flame (Art/Shaders/WickVisibility.hlsl), and is the single place views ask "is this
    /// tile visible?". While the clock runs it shows the sim's lit set (RaidState.Lit). During the
    /// scout it shows Lighting.ComputeScout's outer rooms as Remembered (layout only: no tile is lit,
    /// so traps and guardians stay hidden). At Home (FullyLit) there is no mask. The vault editor's
    /// darkness preview hands it a still picture (ShowStill). It never computes visibility itself.
    ///
    /// Three tile states (docs/m10-2-lighting.md): R = lit now (255 or 0), G = seen, meaning lit now
    /// or, with the profile's Remembered on, lit earlier this raid (RaidRunner.EverLit). Lit tiles
    /// are Lit, seen ones Remembered, the rest Unseen. It also uploads the static warm-pool map
    /// (StaticLights) and the profile's darkness values as shader globals.
    /// </summary>
    public sealed class VisibilityMask : MonoBehaviour
    {
        private static readonly int MaskId = Shader.PropertyToID("_WickVisMask");
        private static readonly int GridId = Shader.PropertyToID("_WickVisGrid");
        private static readonly int EdgeId = Shader.PropertyToID("_WickVisEdge");
        private static readonly int RememberedId = Shader.PropertyToID("_WickRemembered");
        private static readonly int RememberedTintId = Shader.PropertyToID("_WickRememberedTint");
        private static readonly int UnseenId = Shader.PropertyToID("_WickUnseen");
        private static readonly int UnseenTintId = Shader.PropertyToID("_WickUnseenTint");
        private static readonly int BoostId = Shader.PropertyToID("_WickLitBoost");
        private static readonly int StaticMapId = Shader.PropertyToID("_WickStaticMap");
        private static readonly int StaticColorId = Shader.PropertyToID("_WickStaticColor");
        private static readonly int StaticDarkId = Shader.PropertyToID("_WickStaticDark");

        [SerializeField] private RaidRunner _runner;
        [SerializeField] private LightingProfile _profile;

        private Texture2D _texture;
        private Texture2D _static;
        private byte[] _pixels;
        private bool[] _scout;
        private bool[] _none;
        private VaultDef _vault;
        private bool _wroteScout;
        private int _wroteTick = -1;
        private bool _hasStatic;
        private bool _still;
        private bool _fullyLit;
        private LightingProfile _fallback;

        public Texture2D Texture => _texture;

        /// <summary>No mask at all (Home: the vault fully lit and cosy). Every tile counts as visible.</summary>
        public bool FullyLit
        {
            get => _fullyLit;
            set
            {
                if (_fullyLit != value)
                {
                    _fullyLit = value;
                    _wroteTick = -1;
                    SetGrid();
                }
            }
        }

        /// <summary>The profile in use (the theme's, or the Classic Treasury defaults).</summary>
        public LightingProfile Profile => _profile != null ? _profile : _fallback ??= LightingProfile.Default();

        public void Bind(RaidRunner runner, LightingProfile profile)
        {
            _runner = runner;
            _profile = profile;
        }

        /// <summary>Switches profile (a theme's) and rewrites everything that depends on it.</summary>
        public void SetProfile(LightingProfile profile)
        {
            _profile = profile;
            _wroteTick = -1;
            ApplyProfile();
        }

        /// <summary>Whether tile p is shown as lit right now (exactly the mask's R texel).</summary>
        public bool IsVisible(TilePos p)
        {
            if (_vault == null || !_vault.InBounds(p))
            {
                return false;
            }

            if (_fullyLit)
            {
                return true;
            }

            return _pixels[(p.Y * _vault.Width + p.X) * 2] != 0;
        }

        /// <summary>Whether tile p shows as Remembered or Lit (the mask's G texel).</summary>
        public bool IsSeen(TilePos p)
        {
            if (_vault == null || !_vault.InBounds(p))
            {
                return false;
            }

            return _fullyLit || _pixels[(p.Y * _vault.Width + p.X) * 2 + 1] != 0;
        }

        /// <summary>
        /// Uploads the static warm-pool map for this vault (per-tile intensity, StaticLights.Compute).
        /// Null clears it.
        /// </summary>
        public void SetStaticLight(float[] perTile)
        {
            if (_vault == null || perTile == null)
            {
                SetGrid();
                return;
            }

            if (_static == null || _static.width != _vault.Width || _static.height != _vault.Height)
            {
                DestroySafe(_static);
                _static = new Texture2D(_vault.Width, _vault.Height, TextureFormat.R8, false, true)
                {
                    name = "WickStaticLight",
                    filterMode = FilterMode.Bilinear, // pools blend smoothly between tiles
                    wrapMode = TextureWrapMode.Clamp,
                };
            }

            var bytes = new byte[perTile.Length];
            for (int i = 0; i < bytes.Length; i++)
            {
                bytes[i] = (byte)Mathf.RoundToInt(Mathf.Clamp01(perTile[i]) * 255f);
            }

            _static.SetPixelData(bytes, 0);
            _static.Apply(false);
            Shader.SetGlobalTexture(StaticMapId, _static);
            _hasStatic = true;
            SetGrid();
        }

        private void LateUpdate()
        {
            Refresh();
        }

        private void OnEnable()
        {
            LightingQuality.Changed += OnQualityChanged;
        }

        private void OnDisable()
        {
            LightingQuality.Changed -= OnQualityChanged;
            Shader.SetGlobalVector(GridId, Vector4.zero);
        }

        private void OnQualityChanged(QualityTier tier)
        {
            ApplyProfile();
        }

        /// <summary>Rewrites the texture if the sim state or mode changed since the last write.</summary>
        public void Refresh()
        {
            if (_still)
            {
                return; // a still picture (the editor's darkness preview) stays until replaced
            }

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
                Write(_none, _scout);
            }
            else
            {
                bool[] lit = _runner.Sim.State.Lit;
                Write(lit, Profile.Remembered && _runner.EverLit.Length == lit.Length ? _runner.EverLit : lit);
            }

            _wroteScout = scout;
            _wroteTick = tick;
        }

        private void Allocate(VaultDef vault)
        {
            _vault = vault;
            DestroySafe(_texture);
            _hasStatic = false;
            _texture = new Texture2D(vault.Width, vault.Height, TextureFormat.RG16, false, true)
            {
                name = "WickVisibilityMask",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            _pixels = new byte[vault.Width * vault.Height * 2];
            _scout = new bool[vault.Width * vault.Height];
            _none = new bool[vault.Width * vault.Height];
            _wroteTick = -1;

            Shader.SetGlobalTexture(MaskId, _texture);
            SetGrid();
            ApplyProfile();
        }

        // Texel (x, y) is tile (x, y): row y of the data is texture row y.
        private void Write(bool[] lit, bool[] seen)
        {
            for (int i = 0; i < lit.Length; i++)
            {
                _pixels[i * 2] = lit[i] ? (byte)255 : (byte)0;
                _pixels[i * 2 + 1] = lit[i] || seen[i] ? (byte)255 : (byte)0;
            }

            _texture.SetPixelData(_pixels, 0);
            _texture.Apply(false);
            ApplyProfile();
        }

        /// <summary>
        /// Shows a fixed picture of vault, not following any raid: the vault editor's "Preview
        /// darkness" (what a raider sees when the clock starts). Null puts the mask away.
        /// </summary>
        public void ShowStill(VaultDef vault, bool[] lit, bool[] seen)
        {
            if (vault == null)
            {
                _still = false;
                _vault = null;
                Shader.SetGlobalVector(GridId, Vector4.zero);
                return;
            }

            _still = true;
            if (_vault != vault)
            {
                Allocate(vault);
            }

            Write(lit, seen);
        }

        private void SetGrid()
        {
            // z = 0 switches the mask off (Home); the static pools (w) still glow.
            Shader.SetGlobalVector(GridId, _vault == null
                ? Vector4.zero
                : new Vector4(_vault.Width, _vault.Height, _fullyLit ? 0f : 1f, _hasStatic ? 1f : 0f));
        }

        private void ApplyProfile()
        {
            LightingProfile p = Profile;
            QualityTier tier = LightingQuality.Tier;
            Shader.SetGlobalFloat(EdgeId, LightingQuality.SoftMaskEdge(tier) ? p.EdgeSoftnessTiles * 0.5f : 0f); // Low: per tile
            LightingQuality.ApplyEmissive(tier, p);
            Shader.SetGlobalVector(RememberedId, new Vector4(p.RememberedBrightness, p.RememberedDesaturation, 0f, 0f));
            Shader.SetGlobalColor(RememberedTintId, p.RememberedTint);
            Shader.SetGlobalVector(UnseenId, new Vector4(p.UnseenBrightness, p.UnseenDesaturation, 0f, 0f));
            Shader.SetGlobalColor(UnseenTintId, p.UnseenTint);
            Shader.SetGlobalFloat(BoostId, p.LitBoost);
            Shader.SetGlobalColor(StaticColorId, p.TorchColor * p.TorchIntensity);
            Shader.SetGlobalFloat(StaticDarkId, p.StaticInDark);
        }

        private static void DestroySafe(Object o)
        {
            if (o == null)
            {
                return;
            }

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
