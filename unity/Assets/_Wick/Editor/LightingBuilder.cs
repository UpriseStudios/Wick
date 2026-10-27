using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Wick.Game;

namespace Wick.Editor
{
    /// <summary>
    /// M10.2b lighting assets (docs/m10-2-lighting.md): the colour-grading LUT strips (LutGrades), the
    /// per-theme LightingProfiles (Classic Treasury's created by ThemeBuilder; Crypt and the Sunken
    /// Ship placeholder here, with the spec's direction), and the shared atmosphere and contact-shadow
    /// pieces. Profiles are created once and then tuned in place: rebuilding never overwrites tuning,
    /// it only fills in missing LUTs. Menu: Wick > Art > Build Lighting. Also run by ThemeBuilder.
    /// </summary>
    public static class LightingBuilder
    {
        public const string Dir = "Assets/_Wick/Art/Lighting";
        public const string LutDir = Dir + "/Luts";

        [MenuItem("Wick/Art/Build Lighting")]
        public static void BuildMenu()
        {
            BuildLuts();
            AssetDatabase.SaveAssets();
            Debug.Log("LightingBuilder: LUTs built.");
        }

        /// <summary>Writes every grade's strip as a PNG and imports it the way Color Lookup needs.</summary>
        public static void BuildLuts()
        {
            Directory.CreateDirectory(LutDir);
            foreach (LutGrade grade in System.Enum.GetValues(typeof(LutGrade)))
            {
                string path = LutDir + "/" + LutGrades.FileName(grade) + ".png";
                var tex = new Texture2D(LutGrades.Size * LutGrades.Size, LutGrades.Size, TextureFormat.RGBA32, false, true);
                tex.SetPixels(LutGrades.Strip(grade));
                tex.Apply();
                File.WriteAllBytes(path, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = false; // the strip holds display colours as data
                importer.mipmapEnabled = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Bilinear;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.SaveAndReimport();
            }
        }

        public static Texture Lut(LutGrade grade)
        {
            return AssetDatabase.LoadAssetAtPath<Texture>(LutDir + "/" + LutGrades.FileName(grade) + ".png");
        }

        /// <summary>
        /// Fills a profile's missing LUTs (the theme's main grade, victory and defeat). Tuned values and
        /// LUTs someone has already chosen are left alone.
        /// </summary>
        public static void FillLuts(LightingProfile p, LutGrade main)
        {
            p.ColorGradeLut = p.ColorGradeLut != null ? p.ColorGradeLut : Lut(main);
            p.VictoryLut = p.VictoryLut != null ? p.VictoryLut : Lut(LutGrade.Victory);
            p.DefeatLut = p.DefeatLut != null ? p.DefeatLut : Lut(LutGrade.Defeat);
            EditorUtility.SetDirty(p);
        }

        /// <summary>
        /// A placeholder theme profile with the spec's direction (docs/m10-2-lighting.md, Theme
        /// profiles), created only if missing so lab tuning survives rebuilds.
        /// </summary>
        public static LightingProfile Placeholder(string id)
        {
            string path = Dir + "/lighting_" + id + ".asset";
            var p = AssetDatabase.LoadAssetAtPath<LightingProfile>(path);
            if (p != null)
            {
                return p;
            }

            Directory.CreateDirectory(Dir);
            p = ScriptableObject.CreateInstance<LightingProfile>();
            if (id == "crypt")
            {
                // Purple-grey, darker ground; pale grey fog; few pale moonlight shafts; grey ash; cool grade.
                p.AmbientSky = new Color(0.27f, 0.24f, 0.33f);
                p.AmbientEquator = new Color(0.19f, 0.17f, 0.24f);
                p.AmbientGround = new Color(0.09f, 0.08f, 0.11f);
                p.FogColor = new Color(0.6f, 0.6f, 0.64f);
                p.FogDensity = 0.35f;
                p.ShaftColor = new Color(0.78f, 0.82f, 0.95f);
                p.ShaftsPerRoom = 1;
                p.ShaftStrength = 0.25f;
                p.Dust = DustKind.Ash;
                p.DustColor = new Color(0.55f, 0.55f, 0.55f);
                p.AccentColor = new Color(0.5f, 0.3f, 0.65f);
                FillLuts(p, LutGrade.CoolVaultDepth);
            }
            else if (id == "sunken_ship")
            {
                // Teal-blue with a green tint; heavier teal fog; many wavy shafts; bubbles; cool green grade.
                p.AmbientSky = new Color(0.16f, 0.36f, 0.4f);
                p.AmbientEquator = new Color(0.1f, 0.26f, 0.28f);
                p.AmbientGround = new Color(0.05f, 0.14f, 0.15f);
                p.FogColor = new Color(0.2f, 0.5f, 0.52f);
                p.FogDensity = 0.5f;
                p.FogCardsPerRoom = 3;
                p.ShaftColor = new Color(0.45f, 0.85f, 0.85f);
                p.ShaftsPerRoom = 3;
                p.ShaftStrength = 0.4f;
                p.Dust = DustKind.Bubbles;
                p.DustColor = new Color(0.7f, 0.95f, 1f);
                p.AccentColor = new Color(0.25f, 0.8f, 0.75f);
                FillLuts(p, LutGrade.MixedLighting);
            }

            AssetDatabase.CreateAsset(p, path);
            return p;
        }

        // ---- Shared pieces ----------------------------------------------------------------------

        public static Material AtmosphereMat(string name, float shape, BlendMode src, BlendMode dst)
        {
            string path = Dir + "/" + name + ".mat";
            Directory.CreateDirectory(Dir);
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Wick/Atmosphere"));
                AssetDatabase.CreateAsset(mat, path);
            }

            mat.shader = Shader.Find("Wick/Atmosphere");
            mat.SetFloat("_Shape", shape);
            mat.SetFloat("_SrcBlend", (float)src);
            mat.SetFloat("_DstBlend", (float)dst);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        public static Material ShadowMat()
        {
            string path = Dir + "/ContactShadow.mat";
            Directory.CreateDirectory(Dir);
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Wick/ShadowBlob"));
                AssetDatabase.CreateAsset(mat, path);
            }

            mat.shader = Shader.Find("Wick/ShadowBlob");
            EditorUtility.SetDirty(mat);
            return mat;
        }

        public static Material DustMat()
        {
            string path = Dir + "/Dust.mat";
            Directory.CreateDirectory(Dir);
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Wick/Particle"));
                AssetDatabase.CreateAsset(mat, path);
            }

            mat.shader = Shader.Find("Wick/Particle");
            mat.SetFloat("_SrcBlend", (float)BlendMode.One);
            mat.SetFloat("_DstBlend", (float)BlendMode.One); // additive motes
            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>A soft contact shadow under a character, guardian or prop: a flat quad `size` tiles across.</summary>
        public static void AddContactShadow(Transform parent, float size)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "ContactShadow";
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, 0.015f, 0f);
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            go.transform.localScale = new Vector3(size, size, 1f);
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = ShadowMat();
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
        }

        /// <summary>Dust motes: a small particle system; VaultView sets colour, amount and drift from the profile.</summary>
        public static void BuildDust(Transform root)
        {
            var ps = root.gameObject.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = ps.main;
            main.playOnAwake = false;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(3f, 5f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.05f);
            main.maxParticles = 60;
            ParticleSystem.ShapeModule shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 1f;
            ParticleSystem.ColorOverLifetimeModule fade = ps.colorOverLifetime;
            fade.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.8f, 0.3f), new GradientAlphaKey(0f, 1f) });
            fade.color = g;
            var renderer = root.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = DustMat();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }
    }
}
