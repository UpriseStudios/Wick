using System.Collections.Generic;

namespace Wick.Editor.ArtPipeline
{
    /// <summary>
    /// The handoff specs from docs/asset-production.md ("The look decoded", "Numbers to work to",
    /// the rig table and the animation lists), in one place for the import checks. Change a
    /// number here only when the doc changes.
    /// </summary>
    public static class ArtBudgets
    {
        /// <summary>Where finished assets land: Incoming/&lt;category&gt;/&lt;asset_id&gt;/.</summary>
        public const string IncomingRoot = "Assets/_Wick/Art/Incoming";

        public static readonly string[] Categories = { "char", "trap", "guard", "tile", "prop", "vfx", "ui", "sfx", "music" };

        /// <summary>Texture suffixes for 3D assets (UI sprites and VFX atlases are named freely after their id).</summary>
        public static readonly string[] TextureSuffixes = { "albedo", "mask", "emissive" };

        /// <summary>Wick's shared rig: every skin binds to exactly these bones.</summary>
        public static readonly string[] WickBones =
        {
            "root", "body", "head", "arm.L", "arm.R", "foot.L", "foot.R", "flame_base", "flame_mid", "flame_tip", "eye.L", "eye.R",
        };

        public static readonly string[] WickBlendShapes = { "fear", "greed", "smug", "wince", "blink" };

        public static readonly string[] WickClips =
        {
            "idle", "idle_fidget_pat", "idle_fidget_count", "dash", "run", "tiptoe", "hit", "low_health", "grab", "escape", "snuff", "victory",
        };

        public static readonly string[] TrapClips = { "idle", "warn", "fire", "reset" };

        public static readonly string[] GuardianClips = { "idle", "patrol", "alert", "chase", "sleep", "wake", "hit_player" };

        public static readonly string[] SirWhiskersClips =
        {
            "nap", "wake", "stalk", "lose_interest", "wiggle", "pounce", "swat", "lick_paw", "chase_decoy", "return_to_cushion",
        };

        public static Budget For(string category, string assetId)
        {
            switch (category)
            {
                case "char":
                    return new Budget { MaxTriangles = 5000, MaxTexture = 1024, MinHeight = 0.3f, MaxHeight = 1.0f, MaxFootprint = 1.0f, Clips = WickClips };
                case "trap":
                    return new Budget { MaxTriangles = 4000, MaxTexture = 512, MaxFootprint = 1.05f, Clips = TrapClips };
                case "guard":
                    // Guardians may overhang their tile; Sir Whiskers is the showpiece at the top of the range.
                    return assetId == "guard_sir_whiskers"
                        ? new Budget { MaxTriangles = 5000, MaxTexture = 512, Clips = SirWhiskersClips }
                        : new Budget { MaxTriangles = 4000, MaxTexture = 512, Clips = GuardianClips };
                case "tile":
                    return new Budget { MaxTriangles = 800, MaxTexture = 1024, MaxFootprint = 1.02f };
                case "prop":
                    // Props have no triangle line in the doc; held to the trap ceiling until they get one.
                    return new Budget { MaxTriangles = 4000, MaxTexture = 512 };
                case "vfx":
                    return new Budget { MaxTexture = 512 };
                case "ui":
                    return new Budget { MaxTexture = 2048, NonPowerOfTwoOk = true };
                default:
                    return new Budget();
            }
        }

        /// <summary>Whether the category is a 3D model family (pivot, scale and material rules apply).</summary>
        public static bool IsModel(string category)
        {
            return category == "char" || category == "trap" || category == "guard" || category == "tile" || category == "prop";
        }

        public static bool IsAudio(string category)
        {
            return category == "sfx" || category == "music";
        }

        public static bool Contains(IEnumerable<string> list, string value)
        {
            foreach (string s in list)
            {
                if (s == value)
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>One category's limits. Zero means "no limit in the spec".</summary>
    public sealed class Budget
    {
        public int MaxTriangles;
        public int MaxTexture;
        public float MinHeight;
        public float MaxHeight;

        /// <summary>Largest width or depth in metres (one tile = 1 m).</summary>
        public float MaxFootprint;

        public bool NonPowerOfTwoOk;

        /// <summary>The clip names this family may use (null: no clips expected).</summary>
        public string[] Clips;
    }
}
