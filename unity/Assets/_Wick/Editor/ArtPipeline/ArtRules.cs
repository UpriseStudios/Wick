using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Wick.Editor.ArtPipeline
{
    /// <summary>What an import measured about an asset; the rules judge only this, so they test without files.</summary>
    public sealed class AssetFacts
    {
        public string Path;

        // Models
        public int Triangles;
        public int Materials;
        public bool HasFlameMesh;
        public float MinY;
        public float Height;
        public float Width;
        public float Depth;
        public float CentreX;
        public float CentreZ;
        public List<string> Bones = new List<string>();
        public List<string> BlendShapes = new List<string>();
        public List<string> Clips = new List<string>();

        // Textures
        public int TextureWidth;
        public int TextureHeight;

        // Audio
        public int Channels;
    }

    public sealed class Violation
    {
        public string Path;
        public string Rule;
        public string Detail;

        public override string ToString()
        {
            return Path + ": " + Rule + " - " + Detail;
        }
    }

    /// <summary>
    /// The handoff rules. Every check reports; none of them changes the asset. Naming, budgets,
    /// pivot and scale, the shared Wick rig and the clip names come from docs/asset-production.md.
    /// </summary>
    public static class ArtRules
    {
        private static readonly Regex AssetId = new Regex("^(char|trap|guard|tile|prop|vfx|ui|sfx|music)_[a-z0-9]+(_[a-z0-9]+)*$");

        private const float PivotTolerance = 0.02f;
        private const float CentreTolerance = 0.1f;

        /// <summary>
        /// Splits an Incoming path into category and asset id, or explains why it can't.
        /// Files inside a "source" or "previews" folder are working files and are skipped (null id, no violation).
        /// </summary>
        public static bool TryParsePath(string path, out string category, out string assetId, out string fileStem, List<Violation> violations)
        {
            category = null;
            assetId = null;
            fileStem = null;
            string rel = path.Replace('\\', '/');
            if (!rel.StartsWith(ArtBudgets.IncomingRoot + "/"))
            {
                return false;
            }

            if (rel.EndsWith(".md") || rel.EndsWith(".txt"))
            {
                return false; // notes and licence files travel with assets
            }

            string[] parts = rel.Substring(ArtBudgets.IncomingRoot.Length + 1).Split('/');
            foreach (string p in parts)
            {
                if (p == "source" || p == "previews")
                {
                    return false;
                }
            }

            if (parts.Length < 3)
            {
                Add(violations, path, "Location", "Put files in Incoming/<category>/<asset_id>/");
                return false;
            }

            category = parts[0];
            assetId = parts[1];
            string file = parts[parts.Length - 1];
            int dot = file.LastIndexOf('.');
            fileStem = dot > 0 ? file.Substring(0, dot) : file;

            if (!ArtBudgets.Contains(ArtBudgets.Categories, category))
            {
                Add(violations, path, "Category", "'" + category + "' isn't one of " + string.Join(", ", ArtBudgets.Categories));
                return false;
            }

            if (!AssetId.IsMatch(assetId))
            {
                Add(violations, path, "Naming", "asset id '" + assetId + "' must be <category>_<name>_<variant>, lower case, digits and underscores");
                return false;
            }

            if (!assetId.StartsWith(category + "_"))
            {
                Add(violations, path, "Naming", "asset id '" + assetId + "' is in the '" + category + "' folder but doesn't start with '" + category + "_'");
                return false;
            }

            return true;
        }

        public static List<Violation> CheckModel(string category, string assetId, string fileStem, AssetFacts f)
        {
            var v = new List<Violation>();
            Budget b = ArtBudgets.For(category, assetId);

            // Animations may come in their own files: char_wick_default@run.fbx.
            string stem = fileStem.Contains("@") ? fileStem.Substring(0, fileStem.IndexOf('@')) : fileStem;
            if (stem != assetId)
            {
                Add(v, f.Path, "Naming", "model file should be named " + assetId + " (or " + assetId + "@<clip>), not " + fileStem);
            }

            bool animationOnly = fileStem.Contains("@") && f.Triangles == 0;
            if (!animationOnly)
            {
                if (b.MaxTriangles > 0 && f.Triangles > b.MaxTriangles)
                {
                    Add(v, f.Path, "Triangles", f.Triangles + " triangles; the " + category + " budget is " + b.MaxTriangles);
                }

                int allowedMaterials = 1 + (f.HasFlameMesh ? 1 : 0); // the flame is its own Wick/Flame mesh
                if (f.Materials > allowedMaterials)
                {
                    Add(v, f.Path, "Materials", f.Materials + " materials; atlas everything onto one" + (f.HasFlameMesh ? " (plus the flame)" : ""));
                }

                if (System.Math.Abs(f.MinY) > PivotTolerance)
                {
                    Add(v, f.Path, "Pivot", "the base sits at y = " + Metres(f.MinY) + "; the pivot belongs at the base (y = 0)");
                }

                if (System.Math.Abs(f.CentreX) > CentreTolerance || System.Math.Abs(f.CentreZ) > CentreTolerance)
                {
                    Add(v, f.Path, "Pivot", "the pivot is " + Metres(f.CentreX) + ", " + Metres(f.CentreZ) + " off the base centre");
                }

                if (b.MaxHeight > 0 && (f.Height < b.MinHeight || f.Height > b.MaxHeight))
                {
                    Add(v, f.Path, "Scale", "height " + Metres(f.Height) + "; expected " + Metres(b.MinHeight) + " to " + Metres(b.MaxHeight) +
                        " (Wick is 0.6 m, flame tip about 0.8 m)");
                }

                if (b.MaxFootprint > 0 && (f.Width > b.MaxFootprint || f.Depth > b.MaxFootprint))
                {
                    Add(v, f.Path, "Scale", Metres(f.Width) + " x " + Metres(f.Depth) + " footprint; it must fit " + Metres(b.MaxFootprint) +
                        " (one tile is 1 m)");
                }

                if (category == "char")
                {
                    foreach (string bone in ArtBudgets.WickBones)
                    {
                        if (!f.Bones.Contains(bone))
                        {
                            Add(v, f.Path, "Rig", "missing shared bone '" + bone + "'");
                        }
                    }

                    foreach (string shape in ArtBudgets.WickBlendShapes)
                    {
                        if (!f.BlendShapes.Contains(shape))
                        {
                            Add(v, f.Path, "Blend shapes", "missing face blend shape '" + shape + "'");
                        }
                    }
                }
            }

            foreach (string clip in f.Clips)
            {
                if (b.Clips != null && !ArtBudgets.Contains(b.Clips, clip))
                {
                    Add(v, f.Path, "Clips", "clip '" + clip + "' isn't in the " + category + " list (" + string.Join(", ", b.Clips) + ")");
                }
            }

            return v;
        }

        public static List<Violation> CheckTexture(string category, string assetId, string fileStem, AssetFacts f)
        {
            var v = new List<Violation>();
            Budget b = ArtBudgets.For(category, assetId);

            if (ArtBudgets.IsModel(category))
            {
                bool named = false;
                foreach (string suffix in ArtBudgets.TextureSuffixes)
                {
                    named |= fileStem == assetId + "_" + suffix;
                }

                if (!named)
                {
                    Add(v, f.Path, "Naming", "texture should be " + assetId + "_albedo, _mask or _emissive, not " + fileStem);
                }
            }
            else if (!fileStem.StartsWith(assetId))
            {
                Add(v, f.Path, "Naming", "file name should start with " + assetId);
            }

            int largest = System.Math.Max(f.TextureWidth, f.TextureHeight);
            if (b.MaxTexture > 0 && largest > b.MaxTexture)
            {
                Add(v, f.Path, "Texture size", f.TextureWidth + " x " + f.TextureHeight + "; the " + category + " limit is " + b.MaxTexture + " px");
            }

            if (!b.NonPowerOfTwoOk && (!PowerOfTwo(f.TextureWidth) || !PowerOfTwo(f.TextureHeight)))
            {
                Add(v, f.Path, "Texture size", f.TextureWidth + " x " + f.TextureHeight + " isn't a power of two");
            }

            return v;
        }

        public static List<Violation> CheckAudio(string category, string assetId, string fileStem, AssetFacts f)
        {
            var v = new List<Violation>();
            if (fileStem != assetId)
            {
                Add(v, f.Path, "Naming", "audio file should be named " + assetId + ", not " + fileStem);
            }

            if (category == "sfx" && f.Channels > 1)
            {
                Add(v, f.Path, "Audio", "sound effects are mono; this has " + f.Channels + " channels");
            }

            return v;
        }

        private static bool PowerOfTwo(int n)
        {
            return n > 0 && (n & (n - 1)) == 0;
        }

        private static string Metres(float m)
        {
            return m.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + " m";
        }

        private static void Add(List<Violation> list, string path, string rule, string detail)
        {
            list?.Add(new Violation { Path = path, Rule = rule, Detail = detail });
        }
    }
}
