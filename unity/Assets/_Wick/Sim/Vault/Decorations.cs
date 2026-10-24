using System;
using System.Collections.Generic;

namespace Wick.Sim
{
    /// <summary>
    /// A cosmetic prop placed in a vault: banners, torches, gold piles, statues and achievement
    /// trophies. The raid sim never reads decorations: they don't block, they aren't hashed and
    /// they aren't part of the layout hash, so redecorating never needs a new clear-check run.
    /// </summary>
    public sealed class DecorationDef
    {
        public DecorationDef(int id, string kind, TilePos pos, int rot)
        {
            Id = id;
            Kind = kind ?? throw new ArgumentNullException(nameof(kind));
            Pos = pos;
            Rot = rot;
        }

        public int Id { get; }

        /// <summary>The prop's asset id, e.g. "prop_banner" or "prop_trophy_vault_snuffer_gold".</summary>
        public string Kind { get; }

        public TilePos Pos { get; }

        /// <summary>Facing as a Dir value (0 = N, 1 = E, 2 = S, 3 = W).</summary>
        public int Rot { get; }
    }

    /// <summary>The decoration catalogue and its placement rules.</summary>
    public static class Decorations
    {
        public const string Banner = "prop_banner";
        public const string Torch = "prop_torch";
        public const string GoldPile = "prop_gold_pile";
        public const string Statue = "prop_statue";

        /// <summary>Trophies are "prop_" plus the trophy cosmetic an achievement tier grants.</summary>
        public const string TrophyPrefix = "prop_trophy_";

        /// <summary>Decorations every player can place. Trophies are earned; more come with the gem shop (M12).</summary>
        public static IReadOnlyList<string> Free { get; } = new[] { Banner, Torch, GoldPile, Statue };

        public static bool IsTrophy(string kind)
        {
            return kind != null && kind.StartsWith(TrophyPrefix, StringComparison.Ordinal) && kind.Length > TrophyPrefix.Length;
        }

        public static bool IsKnown(string kind)
        {
            for (int i = 0; i < Free.Count; i++)
            {
                if (Free[i] == kind)
                {
                    return true;
                }
            }

            return IsTrophy(kind);
        }

        /// <summary>Banners and torches hang on a wall beside open floor; everything else stands on floor.</summary>
        public static bool IsWallMounted(string kind)
        {
            return kind == Banner || kind == Torch;
        }

        /// <summary>The trophy prop a trophy cosmetic ("trophy_...") unlocks.</summary>
        public static string TrophyKind(string cosmetic)
        {
            return "prop_" + cosmetic;
        }

        /// <summary>The cosmetic that unlocks a trophy prop, or null if the kind isn't a trophy.</summary>
        public static string CosmeticOf(string kind)
        {
            return IsTrophy(kind) ? kind.Substring("prop_".Length) : null;
        }
    }
}
