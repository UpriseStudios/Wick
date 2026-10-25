using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Rendering;
using Wick.Editor.ArtPipeline;
using Wick.Game;

namespace Wick.Editor
{
    /// <summary>
    /// Builds the vault themes. Each kit piece comes from Art/Incoming/tile/tile_&lt;theme&gt;_&lt;piece&gt;/
    /// when it exists (with the theme's shared atlas from tile_&lt;theme&gt;_atlas), otherwise from a
    /// stand-in made of bevel-free boxes in the theme's palette colours. Pieces face +Z.
    /// Menu: Wick > Art > Build Themes. Also run by Wick > Rebuild Placeholder Content.
    /// </summary>
    public static class ThemeBuilder
    {
        private const string Root = "Assets/_Wick";
        private const string ThemesDir = Root + "/Art/Themes";
        private const string StandInDir = Root + "/Art/StandIns/Themes";
        public const string CatalogPath = ThemesDir + "/ThemeCatalog.asset";

        private sealed class ThemeSpec
        {
            public string Name, Id, DisplayName;
            public Color Floor, FloorAlt, Wall, Trim, Door, Pit, Ice;
            public Color Sky, Equator, Ground;
        }

        // Colours from the Art Direction palette (Stone Blue, Shadow Violet, Treasure Gold, Trap Teal...).
        private static readonly ThemeSpec[] Specs =
        {
            new ThemeSpec
            {
                Name = "treasury", Id = ThemeCatalog.DefaultId, DisplayName = "Classic Treasury",
                Floor = new Color(0.17f, 0.14f, 0.12f), FloorAlt = new Color(0.20f, 0.165f, 0.135f), Wall = new Color(0.36f, 0.31f, 0.27f),
                Trim = new Color(0.42f, 0.09f, 0.11f), Door = new Color(0.34f, 0.21f, 0.11f), Pit = new Color(0.02f, 0.015f, 0.012f),
                Ice = new Color(0.46f, 0.62f, 0.66f), Sky = new Color(0.06f, 0.05f, 0.07f), Equator = new Color(0.035f, 0.03f, 0.035f),
                Ground = new Color(0.015f, 0.012f, 0.012f),
            },
            new ThemeSpec
            {
                Name = "crypt", Id = "theme_crypt", DisplayName = "Crypt",
                Floor = new Color(0.15f, 0.14f, 0.15f), FloorAlt = new Color(0.18f, 0.165f, 0.17f), Wall = new Color(0.54f, 0.50f, 0.44f),
                Trim = new Color(0.26f, 0.14f, 0.30f), Door = new Color(0.40f, 0.27f, 0.15f), Pit = new Color(0.015f, 0.012f, 0.015f),
                Ice = new Color(0.46f, 0.62f, 0.66f), Sky = new Color(0.06f, 0.05f, 0.08f), Equator = new Color(0.035f, 0.03f, 0.04f),
                Ground = new Color(0.015f, 0.012f, 0.015f),
            },
        };

        [MenuItem("Wick/Art/Build Themes")]
        public static void BuildMenu()
        {
            Build();
            AssetDatabase.SaveAssets();
            Debug.Log("ThemeBuilder: themes rebuilt.");
        }

        public static ThemeCatalog Build()
        {
            Directory.CreateDirectory(ThemesDir);
            var catalog = LoadOrCreate<ThemeCatalog>(CatalogPath);
            catalog.Themes.Clear();
            foreach (ThemeSpec spec in Specs)
            {
                ThemeDefinition theme = BuildTheme(spec);
                bool bundled = spec.Id == ThemeCatalog.DefaultId;
                catalog.Themes.Add(new ThemeListing
                {
                    Id = theme.Id,
                    DisplayName = theme.DisplayName,
                    StandIn = theme.StandIn,
                    Bundled = bundled ? theme : null,
                    Reference = bundled ? null : new AssetReferenceT<ThemeDefinition>(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(theme))),
                });
            }

            EditorUtility.SetDirty(catalog);
            return catalog;
        }

        private static ThemeDefinition BuildTheme(ThemeSpec spec)
        {
            string dir = StandInDir + "/" + spec.Name;
            Directory.CreateDirectory(dir);
            var theme = LoadOrCreate<ThemeDefinition>(ThemesDir + "/" + spec.Id + ".asset");
            theme.Id = spec.Id;
            theme.DisplayName = spec.DisplayName;
            theme.AmbientSky = spec.Sky;
            theme.AmbientEquator = spec.Equator;
            theme.AmbientGround = spec.Ground;

            Material floor = Mat(dir, "floor", spec.Floor), floorAlt = Mat(dir, "floor_alt", spec.FloorAlt), wall = Mat(dir, "wall", spec.Wall);
            Material trim = Mat(dir, "trim", spec.Trim), door = Mat(dir, "door", spec.Door), pit = Mat(dir, "pit", spec.Pit), ice = Mat(dir, "ice", spec.Ice);
            Material atlas = RealAtlas(spec.Name);
            int real = 0;

            GameObject Piece(string name, System.Action<Transform> standIn)
            {
                string id = "tile_" + spec.Name + "_" + name;
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(ArtBudgets.IncomingRoot + "/tile/" + id + "/" + id + ".fbx");
                if (model != null)
                {
                    real++;
                    return Save(dir + "/" + name + ".prefab", root =>
                    {
                        GameObject m = (GameObject)PrefabUtility.InstantiatePrefab(model, root);
                        if (atlas != null)
                        {
                            foreach (Renderer r in m.GetComponentsInChildren<Renderer>())
                            {
                                r.sharedMaterial = atlas;
                            }
                        }
                    });
                }

                return Save(dir + "/" + name + ".prefab", standIn);
            }

            theme.Floor = Piece("floor", t => Box(t, new Vector3(0f, -0.05f, 0f), new Vector3(1f, 0.1f, 1f), floor));
            theme.FloorVariants = new[]
            {
                Piece("floor_b", t =>
                {
                    Box(t, new Vector3(0f, -0.05f, 0f), new Vector3(1f, 0.1f, 1f), floor);
                    Box(t, new Vector3(-0.2f, 0.005f, 0.15f), new Vector3(0.4f, 0.01f, 0.3f), floorAlt);
                }),
                Piece("floor_c", t =>
                {
                    Box(t, new Vector3(0f, -0.05f, 0f), new Vector3(1f, 0.1f, 1f), floor);
                    Box(t, new Vector3(0.22f, 0.005f, -0.2f), new Vector3(0.3f, 0.01f, 0.35f), floorAlt);
                }),
            };
            theme.WallFill = Piece("wall_fill", t => Box(t, new Vector3(0f, 0.5f, 0f), Vector3.one, wall));
            theme.WallStraight = Piece("wall_straight", t =>
            {
                Box(t, new Vector3(0f, 0.5f, 0f), Vector3.one, wall);
                Box(t, new Vector3(0f, 0.55f, 0.51f), new Vector3(0.3f, 0.6f, 0.02f), trim); // banner on the open face
            });
            theme.WallCorner = Piece("wall_corner", t =>
            {
                Box(t, new Vector3(0f, 0.5f, 0f), Vector3.one, wall);
                Box(t, new Vector3(0f, 0.85f, 0.51f), new Vector3(1f, 0.08f, 0.02f), trim);
                Box(t, new Vector3(0.51f, 0.85f, 0f), new Vector3(0.02f, 0.08f, 1f), trim);
            });
            theme.Door = Piece("door", t =>
            {
                Box(t, new Vector3(0f, -0.05f, 0f), new Vector3(1f, 0.1f, 1f), door);
                Box(t, new Vector3(-0.45f, 0.6f, 0f), new Vector3(0.1f, 1.2f, 0.3f), door);
                Box(t, new Vector3(0.45f, 0.6f, 0f), new Vector3(0.1f, 1.2f, 0.3f), door);
            });
            theme.Pit = Piece("pit", t => Box(t, new Vector3(0f, -0.3f, 0f), new Vector3(1f, 0.05f, 1f), pit));
            theme.Ice = Piece("ice", t => Box(t, new Vector3(0f, -0.05f, 0f), new Vector3(1f, 0.1f, 1f), ice));
            theme.EntranceArch = Piece("entrance", t =>
            {
                // Dark timber, like the doors: crimson stays for the banners.
                Box(t, new Vector3(-0.46f, 0.7f, 0f), new Vector3(0.08f, 1.4f, 0.2f), door);
                Box(t, new Vector3(0.46f, 0.7f, 0f), new Vector3(0.08f, 1.4f, 0.2f), door);
                Box(t, new Vector3(0f, 1.42f, 0f), new Vector3(1f, 0.1f, 0.2f), door);
            });
            // Lighting (docs/m10-2-lighting.md). Classic Treasury's profile is locked first; other themes
            // use its values until their own profiles are made (M10.2b). Created once, then tuned in place.
            theme.Lighting = spec.Id == ThemeCatalog.DefaultId
                ? LoadOrCreate<LightingProfile>(ThemesDir + "/lighting_" + spec.Name + ".asset")
                : null;
            Material bracket = Mat(dir, "sconce_bracket", new Color(0.16f, 0.13f, 0.11f));
            Material sconceFlame = FlameMat(dir, "sconce_flame");
            theme.Sconce = Piece("sconce", t =>
            {
                // Hangs on the wall face it's placed on, lighting the floor towards +Z.
                Box(t, new Vector3(0f, 0.95f, 0.04f), new Vector3(0.12f, 0.22f, 0.08f), bracket);
                Box(t, new Vector3(0f, 1.05f, 0.12f), new Vector3(0.16f, 0.05f, 0.16f), bracket);
                Sphere(t, new Vector3(0f, 1.16f, 0.12f), new Vector3(0.1f, 0.16f, 0.1f), sconceFlame);
            });
            Material pitGlow = GlowMat(dir, "pit_edge_glow");
            theme.PitEdge = Save(dir + "/pit_edge.prefab", t =>
            {
                // A faint strip along the pit's +Z edge; VaultView turns one to face each open side.
                Box(t, new Vector3(0f, 0.01f, 0.46f), new Vector3(1f, 0.03f, 0.08f), pitGlow);
                t.gameObject.AddComponent<ReadableGlow>().Kind = ReadableKind.PitEdge;
            });
            theme.EntranceGlow = Save(dir + "/entrance_glow.prefab", t =>
            {
                Quad(t, new Vector3(0f, 0.02f, 0f), Vector3.one, BlobMat(dir, "entrance_blob"));
                t.gameObject.AddComponent<ReadableGlow>().Kind = ReadableKind.Entrance;
            });
            theme.StandIn = real == 0;
            EditorUtility.SetDirty(theme);
            return theme;
        }

        /// <summary>The theme's shared 1024 px atlas material, when tile_&lt;theme&gt;_atlas has been supplied.</summary>
        private static Material RealAtlas(string theme)
        {
            string id = "tile_" + theme + "_atlas";
            string folder = ArtBudgets.IncomingRoot + "/tile/" + id;
            var albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(folder + "/" + id + "_albedo.png");
            if (albedo == null)
            {
                return null;
            }

            string path = ThemesDir + "/" + id + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Wick/StylisedLit"));
                AssetDatabase.CreateAsset(mat, path);
            }

            mat.SetTexture("_BaseMap", albedo);
            Texture2D mask = AssetDatabase.LoadAssetAtPath<Texture2D>(folder + "/" + id + "_mask.png");
            mat.SetTexture("_MaskMap", mask != null ? mask : Texture2D.whiteTexture);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Material Mat(string dir, string name, Color colour)
        {
            string path = dir + "/" + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Wick/StylisedLit"));
                AssetDatabase.CreateAsset(mat, path);
            }

            mat.SetColor("_BaseColor", colour);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>A flame that follows the visibility mask (a sconce isn't on the always-readable list).</summary>
        private static Material FlameMat(string dir, string name)
        {
            Material mat = MatWith(dir, name, Shader.Find("Wick/Flame"));
            mat.SetFloat("_IgnoreMask", 0f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>An always-readable glow: near-black body, emission (set per renderer by ReadableGlow), ignores the mask.</summary>
        public static Material GlowMat(string dir, string name)
        {
            Material mat = MatWith(dir, name, Shader.Find("Wick/StylisedLit"));
            mat.SetColor("_BaseColor", new Color(0.02f, 0.02f, 0.02f));
            mat.SetColor("_EmissionColor", Color.white);
            mat.SetFloat("_IgnoreMask", 1f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>A soft additive floor pool (Wick/LightBlob).</summary>
        public static Material BlobMat(string dir, string name)
        {
            return MatWith(dir, name, Shader.Find("Wick/LightBlob"));
        }

        private static Material MatWith(string dir, string name, Shader shader)
        {
            string path = dir + "/" + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }

            mat.shader = shader;
            return mat;
        }

        private static void Sphere(Transform parent, Vector3 position, Vector3 scale, Material material)
        {
            Prim(PrimitiveType.Sphere, parent, position, scale, material);
        }

        /// <summary>A quad lying flat on the floor (facing up).</summary>
        public static void Quad(Transform parent, Vector3 position, Vector3 scale, Material material)
        {
            GameObject go = Prim(PrimitiveType.Quad, parent, position, scale, material);
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        }

        private static void Box(Transform parent, Vector3 position, Vector3 scale, Material material)
        {
            Prim(PrimitiveType.Cube, parent, position, scale, material);
        }

        private static GameObject Prim(PrimitiveType type, Transform parent, Vector3 position, Vector3 scale, Material material)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            return go;
        }

        private static GameObject Save(string path, System.Action<Transform> build)
        {
            var go = new GameObject(Path.GetFileNameWithoutExtension(path));
            build(go.transform);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<T>();
                AssetDatabase.CreateAsset(asset, path);
            }

            return asset;
        }
    }
}
