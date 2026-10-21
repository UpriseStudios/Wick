using System;
using UnityEngine;

namespace Wick.Editor
{
    /// <summary>
    /// A tiny signed-distance painter for the UI kit's stand-in sprites and icons: shapes are
    /// distance functions (negative inside), painted with a heavy ink outline, a vertical gradient
    /// fill and anti-aliased edges, the chunky look of the Wax Bandits logo.
    /// </summary>
    public sealed class UiPainter
    {
        public static readonly Color Ink = Hex(0x1A0F08);

        private readonly int _size;
        private readonly Color[] _px;

        public UiPainter(int size)
        {
            _size = size;
            _px = new Color[size * size];
        }

        public int Size => _size;

        /// <summary>Fills shape `sdf` (pixel units, y up) with a top-to-bottom gradient, outlined in ink.</summary>
        public UiPainter Fill(Func<float, float, float> sdf, Color top, Color bottom, float outline = 0f, Color? outlineColour = null)
        {
            Color ink = outlineColour ?? Ink;
            for (int y = 0; y < _size; y++)
            {
                for (int x = 0; x < _size; x++)
                {
                    float d = sdf(x + 0.5f, y + 0.5f);
                    float cover = Mathf.Clamp01(0.5f - d);
                    if (cover <= 0f)
                    {
                        continue;
                    }

                    Color fill = Color.Lerp(bottom, top, y / (float)(_size - 1));
                    Color c = outline > 0f ? Color.Lerp(fill, ink, Mathf.Clamp01(d + outline + 0.5f)) : fill;
                    Blend(x, y, c, cover);
                }
            }

            return this;
        }

        /// <summary>
        /// Wood grain inside a shape: soft streaks along x, wobbling with value noise, darkening up to
        /// `strength`. Gives plaques and round buttons a carved-timber feel instead of a flat fill.
        /// </summary>
        public UiPainter Grain(Func<float, float, float> sdf, Color dark, float strength, float spacing = 9f)
        {
            for (int y = 0; y < _size; y++)
            {
                for (int x = 0; x < _size; x++)
                {
                    float d = sdf(x + 0.5f, y + 0.5f);
                    if (d > 0f)
                    {
                        continue;
                    }

                    float wobble = Noise(x * 0.035f, y * 0.11f) * 6f;
                    float streak = 0.5f + 0.5f * Mathf.Sin((y + wobble) / spacing * Mathf.PI * 2f);
                    float a = strength * Mathf.Pow(streak, 6f) * Mathf.Clamp01(-d / 3f);
                    Blend(x, y, dark, a);
                }
            }

            return this;
        }

        /// <summary>Smooth value noise in 0..1 (deterministic, for texture only).</summary>
        public static float Noise(float x, float y)
        {
            int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y);
            float xf = x - xi, yf = y - yi;
            float u = xf * xf * (3 - 2 * xf), v = yf * yf * (3 - 2 * yf);
            float a = Hash(xi, yi), b = Hash(xi + 1, yi), c = Hash(xi, yi + 1), d = Hash(xi + 1, yi + 1);
            return Mathf.Lerp(Mathf.Lerp(a, b, u), Mathf.Lerp(c, d, u), v);
        }

        private static float Hash(int x, int y)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263);
                h = (h ^ (h >> 13)) * 1274126177u;
                return (h ^ (h >> 16)) / (float)uint.MaxValue;
            }
        }

        /// <summary>A flat-coloured shape, no outline.</summary>
        public UiPainter Solid(Func<float, float, float> sdf, Color colour)
        {
            return Fill(sdf, colour, colour);
        }

        private void Blend(int x, int y, Color c, float a)
        {
            int i = y * _size + x;
            Color under = _px[i];
            float alpha = a * c.a;
            float outA = alpha + under.a * (1f - alpha);
            if (outA <= 0f)
            {
                return;
            }

            Color rgb = (c * alpha + under * under.a * (1f - alpha)) / outA;
            _px[i] = new Color(rgb.r, rgb.g, rgb.b, outA);
        }

        public Texture2D ToTexture()
        {
            var tex = new Texture2D(_size, _size, TextureFormat.RGBA32, false);
            tex.SetPixels(_px);
            tex.Apply();
            return tex;
        }

        // ---- Shapes (pixel space, y up) ------------------------------------------------------

        public static Func<float, float, float> RoundRect(float cx, float cy, float hw, float hh, float r)
        {
            return (x, y) =>
            {
                float qx = Mathf.Abs(x - cx) - (hw - r);
                float qy = Mathf.Abs(y - cy) - (hh - r);
                return new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
            };
        }

        public static Func<float, float, float> Circle(float cx, float cy, float r)
        {
            return (x, y) => new Vector2(x - cx, y - cy).magnitude - r;
        }

        public static Func<float, float, float> Ellipse(float cx, float cy, float rx, float ry)
        {
            return (x, y) =>
            {
                float k = new Vector2((x - cx) / rx, (y - cy) / ry).magnitude;
                return (k - 1f) * Mathf.Min(rx, ry);
            };
        }

        /// <summary>A capsule (thick line) from a to b.</summary>
        public static Func<float, float, float> Line(Vector2 a, Vector2 b, float r)
        {
            return (x, y) =>
            {
                Vector2 p = new Vector2(x, y), ab = b - a;
                float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
                return (p - (a + ab * t)).magnitude - r;
            };
        }

        /// <summary>A convex or concave polygon (even-odd), distance to its edges.</summary>
        public static Func<float, float, float> Polygon(params Vector2[] pts)
        {
            return (x, y) =>
            {
                var p = new Vector2(x, y);
                float d = float.MaxValue;
                bool inside = false;
                for (int i = 0, j = pts.Length - 1; i < pts.Length; j = i++)
                {
                    Vector2 a = pts[j], b = pts[i], ab = b - a;
                    float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
                    d = Mathf.Min(d, (p - (a + ab * t)).magnitude);
                    if ((a.y > y) != (b.y > y) && x < (b.x - a.x) * (y - a.y) / (b.y - a.y) + a.x)
                    {
                        inside = !inside;
                    }
                }

                return inside ? -d : d;
            };
        }

        /// <summary>A candle-flame / drop shape pointing up, base circle at (cx, cy).</summary>
        public static Func<float, float, float> Drop(float cx, float cy, float r, float height)
        {
            Func<float, float, float> bulb = Circle(cx, cy, r);
            Func<float, float, float> tip = Polygon(new Vector2(cx - r * 0.92f, cy + r * 0.35f), new Vector2(cx, cy + height),
                new Vector2(cx + r * 0.92f, cy + r * 0.35f));
            return Union(bulb, tip);
        }

        public static Func<float, float, float> Union(params Func<float, float, float>[] shapes)
        {
            return (x, y) =>
            {
                float d = float.MaxValue;
                foreach (Func<float, float, float> s in shapes)
                {
                    d = Mathf.Min(d, s(x, y));
                }

                return d;
            };
        }

        public static Func<float, float, float> Subtract(Func<float, float, float> a, Func<float, float, float> b)
        {
            return (x, y) => Mathf.Max(a(x, y), -b(x, y));
        }

        public static Func<float, float, float> Inset(Func<float, float, float> s, float by)
        {
            return (x, y) => s(x, y) + by;
        }

        public static Color Hex(int rgb, float a = 1f)
        {
            return new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f, a);
        }
    }
}
