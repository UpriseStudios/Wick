using UnityEngine;

namespace Wick.Game
{
    /// <summary>The things that stay readable in the dark (docs/m10-2-lighting.md, Light and darkness rules).</summary>
    public enum ReadableKind
    {
        PitEdge,
        TrapWarning,
        GuardianEyes,
        Brazier,
        Entrance,
        Treasure,
    }

    /// <summary>
    /// Every lighting value for one vault theme (docs/m10-2-lighting.md): ambient, the dim main light,
    /// the three darkness levels, Wick's flame, static warm pools (sconces, torches, the entrance),
    /// brazier light blobs, the always-readable glows, and the atmosphere and post values M10.2b uses.
    /// A new theme only changes data. Presentation only: what is lit is Wick.Sim's decision.
    /// </summary>
    /// <summary>What floats in a theme's air.</summary>
    public enum DustKind
    {
        Motes,
        Ash,
        Bubbles,
    }

    [CreateAssetMenu(menuName = "Wick/Lighting Profile", fileName = "LightingProfile")]
    public sealed class LightingProfile : ScriptableObject
    {
        [Header("Ambient (URP gradient)")]
        public Color AmbientSky = Hex(0x3A2F6B);
        public Color AmbientEquator = Hex(0x2A2550);
        public Color AmbientGround = Hex(0x1E1B3A);

        [Header("Main light (very dim, cool, from high above, no shadows)")]
        public Color MainLightColor = Hex(0x5B6B9A);
        public float MainLightIntensity = 0.15f;
        public Vector3 MainLightAngles = new Vector3(75f, 30f, 0f);

        [Header("Darkness levels")]
        [Tooltip("Flat light added to lit tiles so they always read brighter than unlit ones.")]
        [Range(0f, 1f)] public float LitBoost = 0.06f;

        [Tooltip("Tiles Wick has lit earlier in this raid stay faintly visible. Off: they go back to Unseen.")]
        public bool Remembered = true;

        [Range(0f, 1f)] public float RememberedBrightness = 0.35f;
        [Range(0f, 1f)] public float RememberedDesaturation = 0.6f;
        public Color RememberedTint = new Color(0.78f, 0.84f, 1f);

        [Tooltip("Never lit this raid: shapes readable, details hidden. Never pitch black.")]
        [Range(0f, 1f)] public float UnseenBrightness = 0.12f;
        [Range(0f, 1f)] public float UnseenDesaturation = 0.8f;
        public Color UnseenTint = new Color(0.72f, 0.78f, 1f);

        [Tooltip("Width of the soft edge between tile states, in tiles. Tile centres always show their own state.")]
        [Range(0f, 1f)] public float EdgeSoftnessTiles = 0.5f;

        [Header("Wick's flame (one realtime point light, no shadows)")]
        public Color FlameColor = Hex(0xFF9F1C);
        public Color FlameCoreColor = Hex(0xFFE29A);
        public float FlameIntensity = 2f;

        [Tooltip("Light range = the sim's light radius plus this many tiles.")]
        public float FlameRangeExtraTiles = 0.5f;

        [Tooltip("Seconds the pool takes to follow a radius change (tiptoe, gust).")]
        public float FlameEaseSeconds = 0.25f;

        [Range(0f, 0.5f)] public float FlickerAmount = 0.05f;
        public float FlickerHz = 6f;

        [Tooltip("Intensity flicker at 1 flicker left.")]
        [Range(0f, 0.5f)] public float FranticAmount = 0.15f;

        [Tooltip("Intensity boost on grabbing gold or using a brazier.")]
        public float FlareBoost = 0.4f;
        public float FlareSeconds = 0.4f;
        public float SnuffSeconds = 0.3f;

        [Header("Static warm pools (sconces, torch decorations, the entrance)")]
        public Color TorchColor = Hex(0xFF7A2E);
        public float TorchRadiusTiles = 1.5f;
        [Range(0f, 2f)] public float TorchIntensity = 0.8f;

        [Tooltip("How much of a pool still shows on tiles Wick hasn't lit (walls and floor only).")]
        [Range(0f, 1f)] public float StaticInDark = 0.5f;

        [Tooltip("Sconces are hung along walls at least this many tiles apart.")]
        public int SconceSpacingTiles = 5;

        [Header("Brazier light blobs")]
        public Color BlobColor = Hex(0xFF8A2E);
        public float BlobRadiusTiles = 1.5f;
        [Range(0f, 1f)] public float BlobOpacity = 0.6f;
        public float BlobFadeSeconds = 0.5f;

        [Header("Always readable in the dark")]
        public bool ReadablePitEdges = true;
        public bool ReadableTrapWarnings = true;
        public bool ReadableGuardianEyes = true;
        public bool ReadableBraziers = true;
        public bool ReadableEntrance = true;
        public bool ReadableTreasure = true;

        public Color PitEdgeColor = new Color(0.12f, 0.42f, 0.52f);
        public Color WarningRed = Hex(0xE5484D);
        public Color WarningTeal = Hex(0x3EC6E0);
        public Color TreasureGold = Hex(0xF2C14E);
        public Color EntranceColor = new Color(1f, 0.92f, 0.8f);

        [Tooltip("Entrance glow multiplier while Wick carries loot (head home).")]
        public float EntranceLootBoost = 1.8f;

        [Header("Atmosphere (placed from the layout, a few per room)")]
        public Color FogColor = new Color(0.36f, 0.3f, 0.55f);
        [Tooltip("How thick the low fog cards are (0 = none).")]
        [Range(0f, 1f)] public float FogDensity = 0.25f;
        public int FogCardsPerRoom = 2;
        public Color ShaftColor = new Color(0.85f, 0.9f, 1f);
        [Range(0f, 1f)] public float ShaftStrength = 0.35f;
        public int ShaftsPerRoom = 1;
        public DustKind Dust = DustKind.Motes;
        public Color DustColor = new Color(1f, 0.8f, 0.55f);
        [Range(0f, 1f)] public float DustAmount = 0.5f;

        [Header("Post-processing")]
        [Tooltip("Colour-grading LUT (a 1024 x 32 strip, Wick > Art > Build Lighting LUTs).")]
        public Texture ColorGradeLut;
        [Range(0f, 1f)] public float LutContribution = 1f;
        public float BloomIntensity = 0.55f;
        public float BloomThreshold = 1f;
        [Range(0f, 1f)] public float VignetteIntensity = 0.34f;
        public float PostExposure = 0.1f;
        public Color AccentColor = new Color(0.65f, 0.1f, 0.12f);

        [Header("Other screens")]
        [Tooltip("Blended in on the results screen after an escape.")]
        public Texture VictoryLut;
        [Tooltip("Blended in on the results screen after a snuff.")]
        public Texture DefeatLut;
        public float ResultsGradeSeconds = 0.6f;

        [Tooltip("Exposure lift for share clips, in stops (0.14 is about +10%), so they read on social feeds.")]
        public float ClipExposureLift = 0.14f;

        [Tooltip("Low quality has no bloom, so emissives are brightened by this much instead.")]
        public float LowEmissiveBoost = 0.6f;

        public bool IsReadable(ReadableKind kind)
        {
            switch (kind)
            {
                case ReadableKind.PitEdge: return ReadablePitEdges;
                case ReadableKind.TrapWarning: return ReadableTrapWarnings;
                case ReadableKind.GuardianEyes: return ReadableGuardianEyes;
                case ReadableKind.Brazier: return ReadableBraziers;
                case ReadableKind.Entrance: return ReadableEntrance;
                case ReadableKind.Treasure: return ReadableTreasure;
                default: return false;
            }
        }

        /// <summary>Wick's light range in world units (1 tile = 1 m) for a sim light radius in tiles.</summary>
        public float FlameRange(float radiusTiles)
        {
            return radiusTiles + FlameRangeExtraTiles;
        }

        /// <summary>Applies the ambient gradient to the scene.</summary>
        public void ApplyAmbient()
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = AmbientSky;
            RenderSettings.ambientEquatorColor = AmbientEquator;
            RenderSettings.ambientGroundColor = AmbientGround;
        }

        /// <summary>A profile with the Classic Treasury starting values (also the fallback when a theme has none).</summary>
        public static LightingProfile Default()
        {
            var p = CreateInstance<LightingProfile>();
            p.name = "Default lighting";
            p.hideFlags = HideFlags.DontSave;
            return p;
        }

        private static Color Hex(int rgb)
        {
            return new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f);
        }
    }
}
