using System.Collections.Generic;
using UnityEngine;

namespace Wick.Game
{
    /// <summary>
    /// The Art Direction pack's VFX rules as checks: few, large particles; flame effects in Candle
    /// Flame and Flame Core colours; danger effects in red or teal, never flame orange, so danger
    /// never reads as the player's own light. Used by the tests on every effect.
    /// </summary>
    public static class VfxRules
    {
        public const int MaxParticles = 16;
        public const float MinStartSize = 0.12f;

        /// <summary>Flame orange to yellow: hue 20-55 degrees, clearly saturated.</summary>
        public static bool IsFlameColour(Color c)
        {
            Color.RGBToHSV(c, out float h, out float s, out float v);
            return h * 360f >= 20f && h * 360f <= 55f && s >= 0.35f && v >= 0.4f;
        }

        public static List<string> Check(string id, VfxRole role, GameObject prefab)
        {
            var problems = new List<string>();
            foreach (ParticleSystem ps in prefab.GetComponentsInChildren<ParticleSystem>(true))
            {
                ParticleSystem.MainModule main = ps.main;
                if (main.maxParticles > MaxParticles)
                {
                    problems.Add(id + "/" + ps.name + ": " + main.maxParticles + " particles; keep to " + MaxParticles + " big ones");
                }

                if (main.startSize.mode == ParticleSystemCurveMode.Constant && main.startSize.constant < MinStartSize ||
                    main.startSize.mode == ParticleSystemCurveMode.TwoConstants && main.startSize.constantMax < MinStartSize)
                {
                    problems.Add(id + "/" + ps.name + ": particles under " + MinStartSize + " m read as noise on a phone");
                }

                foreach (Color c in Colours(ps))
                {
                    if (c.a < 0.05f)
                    {
                        continue;
                    }

                    bool flame = IsFlameColour(c);
                    if (role == VfxRole.Danger && flame)
                    {
                        problems.Add(id + "/" + ps.name + ": " + role + " effect uses flame orange " + ColorUtility.ToHtmlStringRGB(c));
                    }

                    if (role == VfxRole.Flame && !flame && !IsFlameCore(c))
                    {
                        problems.Add(id + "/" + ps.name + ": flame effect uses " + ColorUtility.ToHtmlStringRGB(c) + ", not Candle Flame or Flame Core");
                    }
                }
            }

            return problems;
        }

        /// <summary>Flame Core: pale warm yellow-white (#FFE29A and lighter).</summary>
        private static bool IsFlameCore(Color c)
        {
            Color.RGBToHSV(c, out float h, out float s, out float v);
            return v >= 0.85f && s <= 0.45f && (s < 0.1f || h * 360f >= 30f && h * 360f <= 60f);
        }

        /// <summary>Every colour a particle system can start with or fade through.</summary>
        private static IEnumerable<Color> Colours(ParticleSystem ps)
        {
            ParticleSystem.MinMaxGradient start = ps.main.startColor;
            foreach (Color c in Gradient(start))
            {
                yield return c;
            }

            if (ps.colorOverLifetime.enabled)
            {
                foreach (Color c in Gradient(ps.colorOverLifetime.color))
                {
                    // Over-lifetime colours multiply the start colour.
                    foreach (Color s in Gradient(start))
                    {
                        yield return c * s;
                    }
                }
            }
        }

        private static IEnumerable<Color> Gradient(ParticleSystem.MinMaxGradient g)
        {
            switch (g.mode)
            {
                case ParticleSystemGradientMode.Color:
                    yield return g.color;
                    break;
                case ParticleSystemGradientMode.TwoColors:
                    yield return g.colorMin;
                    yield return g.colorMax;
                    break;
                case ParticleSystemGradientMode.Gradient:
                case ParticleSystemGradientMode.RandomColor:
                    foreach (Color c in Keys(g.gradient))
                    {
                        yield return c;
                    }

                    break;
                case ParticleSystemGradientMode.TwoGradients:
                    foreach (Color c in Keys(g.gradientMin))
                    {
                        yield return c;
                    }

                    foreach (Color c in Keys(g.gradientMax))
                    {
                        yield return c;
                    }

                    break;
            }
        }

        private static IEnumerable<Color> Keys(Gradient g)
        {
            if (g == null)
            {
                yield break;
            }

            foreach (GradientColorKey k in g.colorKeys)
            {
                yield return new Color(k.color.r, k.color.g, k.color.b, g.Evaluate(k.time).a);
            }
        }
    }
}
