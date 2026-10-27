using UnityEngine;

namespace Wick.Game
{
    /// <summary>The colour grades from the lighting reference sheet, plus the results screen's two.</summary>
    public enum LutGrade
    {
        Neutral,
        WarmInterior,
        CoolVaultDepth,
        MixedLighting,
        DramaticContrast,
        Victory,
        Defeat,
    }

    /// <summary>
    /// The maths behind the colour-grading LUTs (docs/m10-2-lighting.md): each grade maps a display
    /// colour (sRGB, 0 to 1) to a graded one. Wick > Art > Build Lighting LUTs bakes them into the
    /// 1024 x 32 strips URP's Color Lookup reads (32 blue slices side by side, red across each slice,
    /// green up). Neutral leaves every colour as it is.
    /// </summary>
    public static class LutGrades
    {
        public const int Size = 32;

        public static string FileName(LutGrade grade)
        {
            switch (grade)
            {
                case LutGrade.WarmInterior: return "lut_warm_interior";
                case LutGrade.CoolVaultDepth: return "lut_cool_vault_depth";
                case LutGrade.MixedLighting: return "lut_mixed_lighting";
                case LutGrade.DramaticContrast: return "lut_dramatic_contrast";
                case LutGrade.Victory: return "lut_victory";
                case LutGrade.Defeat: return "lut_defeat";
                default: return "lut_neutral";
            }
        }

        public static Color Apply(LutGrade grade, Color c)
        {
            switch (grade)
            {
                case LutGrade.WarmInterior:
                    // Candlelit: a warmer white balance, shadows lifted a touch towards violet.
                    c = Balance(c, 1.06f, 1f, 0.9f);
                    c = Lift(c, new Color(0.03f, 0.015f, 0.04f));
                    return Saturate(c, 1.05f);
                case LutGrade.CoolVaultDepth:
                    // Cold stone and moonlight: cooler, a little flatter in colour, firmer contrast.
                    c = Balance(c, 0.92f, 0.98f, 1.08f);
                    c = Contrast(c, 1.06f);
                    return Saturate(c, 0.88f);
                case LutGrade.MixedLighting:
                    // Cool shadows, warm highlights.
                    return SplitTone(c, new Color(0.9f, 0.95f, 1.1f), new Color(1.08f, 1f, 0.88f));
                case LutGrade.DramaticContrast:
                    c = Contrast(c, 1.25f);
                    return Saturate(c, 1.1f);
                case LutGrade.Victory:
                    // Warm and golden, a little brighter and richer.
                    c = Balance(c, 1.08f, 1.02f, 0.88f);
                    c = Gain(c, 1.08f);
                    return Saturate(c, 1.15f);
                case LutGrade.Defeat:
                    // Cool, drained and darker.
                    c = Saturate(c, 0.45f);
                    c = Balance(c, 0.9f, 0.95f, 1.08f);
                    return Gain(c, 0.8f);
                default:
                    return c;
            }
        }

        /// <summary>The strip's pixels, row by row from the bottom (green 0) as Texture2D.SetPixels wants.</summary>
        public static Color[] Strip(LutGrade grade)
        {
            var pixels = new Color[Size * Size * Size];
            for (int g = 0; g < Size; g++)
            {
                for (int b = 0; b < Size; b++)
                {
                    for (int r = 0; r < Size; r++)
                    {
                        var c = new Color(r / (Size - 1f), g / (Size - 1f), b / (Size - 1f), 1f);
                        Color o = Apply(grade, c);
                        pixels[g * Size * Size + b * Size + r] = new Color(Mathf.Clamp01(o.r), Mathf.Clamp01(o.g), Mathf.Clamp01(o.b), 1f);
                    }
                }
            }

            return pixels;
        }

        private static float Luma(Color c) => 0.299f * c.r + 0.587f * c.g + 0.114f * c.b;

        private static Color Balance(Color c, float r, float g, float b) => new Color(c.r * r, c.g * g, c.b * b, 1f);

        private static Color Gain(Color c, float k) => new Color(c.r * k, c.g * k, c.b * k, 1f);

        private static Color Lift(Color c, Color lift)
        {
            // Adds the lift fully at black, none at white.
            return new Color(c.r + lift.r * (1 - c.r), c.g + lift.g * (1 - c.g), c.b + lift.b * (1 - c.b), 1f);
        }

        private static Color Saturate(Color c, float k)
        {
            float l = Luma(c);
            return new Color(l + (c.r - l) * k, l + (c.g - l) * k, l + (c.b - l) * k, 1f);
        }

        private static Color Contrast(Color c, float k)
        {
            return new Color(0.5f + (c.r - 0.5f) * k, 0.5f + (c.g - 0.5f) * k, 0.5f + (c.b - 0.5f) * k, 1f);
        }

        private static Color SplitTone(Color c, Color shadows, Color highlights)
        {
            float l = Mathf.Clamp01(Luma(c));
            Color t = Color.Lerp(shadows, highlights, l);
            return new Color(c.r * t.r, c.g * t.g, c.b * t.b, 1f);
        }
    }
}
