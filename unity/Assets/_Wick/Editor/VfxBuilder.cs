using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Wick.Editor.ArtPipeline;
using Wick.Game;

namespace Wick.Editor
{
    /// <summary>
    /// Builds the VFX library: each effect from Art/Incoming/vfx/&lt;id&gt;/&lt;id&gt;.prefab when supplied,
    /// otherwise a stand-in particle system made to the art pack's rules (few, large particles;
    /// flame colours for flame, red or teal for danger) on a soft round particle, or on the
    /// vfx_atlas texture once it exists.
    /// Menu: Wick > Art > Build VFX. Also run by Wick > Rebuild Placeholder Content.
    /// </summary>
    public static class VfxBuilder
    {
        private const string Root = "Assets/_Wick";
        public const string LibraryPath = Root + "/Art/Vfx/VfxLibrary.asset";
        private const string StandInDir = Root + "/Art/StandIns/Vfx";

        // Palette (docs/art-direction.md).
        private static readonly Color CandleFlame = Hex(0xFF9F1C), FlameCore = Hex(0xFFE29A), Gold = Hex(0xF2C14E);
        private static readonly Color DangerRed = Hex(0xE5484D), TrapTeal = Hex(0x3EC6E0), Smoke = new Color(0.62f, 0.62f, 0.68f);

        [MenuItem("Wick/Art/Build VFX")]
        public static void BuildMenu()
        {
            Build();
            AssetDatabase.SaveAssets();
            Debug.Log("VfxBuilder: VFX library rebuilt.");
        }

        public static VfxLibrary Build()
        {
            Directory.CreateDirectory(Root + "/Art/Vfx");
            Directory.CreateDirectory(StandInDir);
            Texture2D particle = ParticleTexture();
            Material add = Mat("vfx_additive", particle, BlendMode.One, BlendMode.One);
            Material blend = Mat("vfx_alpha", particle, BlendMode.One, BlendMode.OneMinusSrcAlpha);

            var library = AssetDatabase.LoadAssetAtPath<VfxLibrary>(LibraryPath);
            if (library == null)
            {
                library = ScriptableObject.CreateInstance<VfxLibrary>();
                AssetDatabase.CreateAsset(library, LibraryPath);
            }

            library.Entries.Clear();
            Add(library, VfxLibrary.CoinBurst, VfxRole.Neutral, () => Burst(blend, 8, 0.2f, 0.7f, 3.5f, 1.2f, Gold, Gold, 0.15f));
            Add(library, VfxLibrary.FlameFlare, VfxRole.Flame, () => Burst(add, 6, 0.3f, 0.6f, 1.6f, -0.3f, CandleFlame, FlameCore, 0.1f));
            Add(library, VfxLibrary.FlameSputter, VfxRole.Flame, () => Burst(add, 5, 0.16f, 0.4f, 2.2f, 0.4f, CandleFlame, FlameCore, 0.35f));
            Add(library, VfxLibrary.FlameSparks, VfxRole.Flame, () => Loop(add, 3, 2f, 0.14f, 0.8f, 0.5f, CandleFlame, FlameCore));
            Add(library, VfxLibrary.SnuffPuff, VfxRole.Neutral, () => Burst(blend, 6, 0.35f, 1.2f, 0.6f, -0.15f, Smoke, Color.white, 0.2f, grow: 2.2f));
            Add(library, VfxLibrary.WaterSplash, VfxRole.Danger, () => Burst(blend, 7, 0.14f, 0.6f, 2.6f, 1.5f, TrapTeal, Color.Lerp(TrapTeal, Color.white, 0.5f), 0.25f));
            Add(library, VfxLibrary.GustSwirl, VfxRole.Danger, () => Burst(blend, 2, 0.45f, 0.5f, 3f, 0f, Color.Lerp(TrapTeal, Color.white, 0.6f), Color.white, 0f, grow: 1.6f, forward: true));
            Add(library, VfxLibrary.WarningGlow, VfxRole.Danger, () => Burst(add, 1, 0.9f, 0.6f, 0f, 0f, DangerRed, DangerRed, 0f, grow: 1.3f));
            EditorUtility.SetDirty(library);
            return library;
        }

        private static void Add(VfxLibrary library, string id, VfxRole role, System.Func<GameObject> standIn)
        {
            var real = AssetDatabase.LoadAssetAtPath<GameObject>(ArtBudgets.IncomingRoot + "/vfx/" + id + "/" + id + ".prefab");
            GameObject prefab = real;
            if (prefab == null)
            {
                GameObject go = standIn();
                go.name = id;
                prefab = PrefabUtility.SaveAsPrefabAsset(go, StandInDir + "/" + id + ".prefab");
                Object.DestroyImmediate(go);
            }

            library.Entries.Add(new VfxEntry { Id = id, Role = role, Prefab = prefab, StandIn = real == null });
        }

        /// <summary>A one-shot burst of a few big particles that fade (and optionally grow) over their life.</summary>
        private static GameObject Burst(Material mat, int count, float size, float life, float speed, float gravity, Color from, Color to,
            float spread, float grow = 1f, bool forward = false)
        {
            var go = new GameObject("vfx");
            var ps = go.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = ps.main;
            main.duration = life;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(life * 0.8f, life);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.7f, speed);
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.85f, size);
            main.startColor = new ParticleSystem.MinMaxGradient(from, to);
            main.gravityModifier = gravity;
            main.maxParticles = count;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.playOnAwake = true;
            main.stopAction = ParticleSystemStopAction.Destroy;

            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });

            ParticleSystem.ShapeModule shape = ps.shape;
            shape.shapeType = forward ? ParticleSystemShapeType.Cone : ParticleSystemShapeType.Hemisphere;
            shape.angle = forward ? 8f : 0f;
            shape.radius = Mathf.Max(0.01f, spread);
            if (!forward)
            {
                go.transform.rotation = Quaternion.Euler(-90f, 0f, 0f); // hemisphere opens upwards
            }

            Fade(ps, grow);
            Renderer(ps, mat);
            return go;
        }

        /// <summary>A gentle looping trickle (flame sparks on the flame tip).</summary>
        private static GameObject Loop(Material mat, int max, float rate, float size, float life, float speed, Color from, Color to)
        {
            var go = new GameObject("vfx");
            var ps = go.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = ps.main;
            main.loop = true;
            main.startLifetime = life;
            main.startSpeed = speed;
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.9f, size);
            main.startColor = new ParticleSystem.MinMaxGradient(from, to);
            main.gravityModifier = -0.1f;
            main.maxParticles = max;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = rate;
            ParticleSystem.ShapeModule shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.03f;
            Fade(ps, 0.6f);
            Renderer(ps, mat);
            return go;
        }

        private static void Fade(ParticleSystem ps, float grow)
        {
            ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.9f, 0.6f), new GradientAlphaKey(0f, 1f) });
            col.color = g;

            ParticleSystem.SizeOverLifetimeModule sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, grow));
        }

        private static void Renderer(ParticleSystem ps, Material mat)
        {
            var r = ps.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
        }

        private static Material Mat(string name, Texture2D texture, BlendMode src, BlendMode dst)
        {
            string path = StandInDir + "/" + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Wick/Particle"));
                AssetDatabase.CreateAsset(mat, path);
            }

            mat.SetTexture("_MainTex", texture);
            mat.SetFloat("_SrcBlend", (float)src);
            mat.SetFloat("_DstBlend", (float)dst);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>The vfx_atlas when supplied; otherwise a soft round blob (a hand-shaped look comes with the atlas).</summary>
        private static Texture2D ParticleTexture()
        {
            var atlas = AssetDatabase.LoadAssetAtPath<Texture2D>(ArtBudgets.IncomingRoot + "/vfx/vfx_atlas/vfx_atlas_albedo.png");
            if (atlas != null)
            {
                return atlas;
            }

            string path = StandInDir + "/vfx_soft_circle.png";
            if (!File.Exists(path))
            {
                const int n = 64;
                var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
                for (int y = 0; y < n; y++)
                {
                    for (int x = 0; x < n; x++)
                    {
                        float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n / 2f, n / 2f)) / (n / 2f);
                        float a = Mathf.Clamp01(1f - Mathf.SmoothStep(0.55f, 1f, d));
                        tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                    }
                }

                File.WriteAllBytes(path, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static Color Hex(int rgb)
        {
            return new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f);
        }
    }
}
