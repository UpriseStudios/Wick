using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Wick.Sim;

namespace Wick.Game.Tests
{
    /// <summary>
    /// Themes are presentation only: any vault JSON renders in any theme, every tile gets a kit
    /// piece, and floors, walls, doors, pits and ice stay just as readable in every theme.
    /// </summary>
    public class ThemeTests
    {
        private const string CatalogPath = "Assets/_Wick/Art/Themes/ThemeCatalog.asset";
        private const int PixelsPerTile = 12;
        private readonly List<UnityEngine.Object> _spawned = new List<UnityEngine.Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (UnityEngine.Object o in _spawned)
            {
                UnityEngine.Object.DestroyImmediate(o);
            }

            _spawned.Clear();
        }

        // ---- Piece choice ---------------------------------------------------------------------

        private static Func<TilePos, TileType> Grid(params string[] rows)
        {
            return p => rows[p.Y][p.X] switch
            {
                '#' => TileType.Wall,
                'D' => TileType.Door,
                'P' => TileType.Pit,
                'I' => TileType.Ice,
                _ => TileType.Floor,
            };
        }

        [Test]
        public void WallsFaceTheirOpenSide_AndTurnCorners()
        {
            Func<TilePos, TileType> g = Grid(
                "#####",
                "#...#",
                "#.#.#",
                "#...#",
                "#####");
            PieceChoice At(int x, int y) => ThemeKit.Choose(5, 5, g, new TilePos(2, 3), new TilePos(x, y));

            Assert.AreEqual(TilePiece.WallFill, At(0, 0).Piece, "outer corner of the vault: nothing open");
            Assert.AreEqual((TilePiece.WallStraight, Dir.S), (At(2, 0).Piece, At(2, 0).Facing), "top wall faces down into the room");
            Assert.AreEqual((TilePiece.WallStraight, Dir.E), (At(0, 2).Piece, At(0, 2).Facing));
            Assert.AreEqual(TilePiece.WallFill, At(2, 2).Piece, "a pillar open on all four sides");
            Assert.IsTrue(At(2, 3).Entrance);
            Assert.IsFalse(At(1, 1).Entrance);
        }

        [Test]
        public void CornersPointTheirTwoFaces()
        {
            Func<TilePos, TileType> g = Grid(
                "...",
                ".##",
                ".##");
            // (1,1) is open to the north and west only: a corner facing W (W then N clockwise).
            PieceChoice c = ThemeKit.Choose(3, 3, g, new TilePos(0, 0), new TilePos(1, 1));
            Assert.AreEqual((TilePiece.WallCorner, Dir.W), (c.Piece, c.Facing));
        }

        [Test]
        public void DoorsTurnWithTheirCorridor()
        {
            Func<TilePos, TileType> g = Grid(
                "#.#",
                ".D.",
                "#.#");
            Assert.AreEqual(Dir.E, ThemeKit.Choose(3, 3, g, default, new TilePos(1, 1)).Facing, "open east-west: the door spans north-south");
            Func<TilePos, TileType> corridor = Grid(
                "#.#",
                "#D#",
                "#.#");
            Assert.AreEqual(Dir.N, ThemeKit.Choose(3, 3, corridor, default, new TilePos(1, 1)).Facing);
        }

        [Test]
        public void ChoicesAreStable()
        {
            Func<TilePos, TileType> g = Grid("...", "...", "...");
            Assert.AreEqual(ThemeKit.Choose(3, 3, g, default, new TilePos(2, 1)), ThemeKit.Choose(3, 3, g, default, new TilePos(2, 1)));
        }

        // ---- Every theme, every vault -----------------------------------------------------------

        public static IEnumerable<string> ThemeIds()
        {
            foreach (ThemeDefinition t in AssetDatabase.LoadAssetAtPath<ThemeCatalog>(CatalogPath).Themes)
            {
                yield return t.Id;
            }
        }

        [TestCaseSource(nameof(ThemeIds))]
        public void EveryPieceFitsOneTile(string id)
        {
            ThemeDefinition theme = AssetDatabase.LoadAssetAtPath<ThemeCatalog>(CatalogPath).Find(id);
            foreach (TilePiece piece in Enum.GetValues(typeof(TilePiece)))
            {
                Assert.IsNotNull(theme.PrefabFor(new PieceChoice(piece, Dir.N, 0, false)), id + " has nothing for " + piece);
            }

            foreach (GameObject prefab in theme.Pieces())
            {
                var b = new Bounds();
                bool first = true;
                foreach (MeshFilter mf in prefab.GetComponentsInChildren<MeshFilter>())
                {
                    Bounds wb = TransformBounds(mf.sharedMesh.bounds, prefab.transform.worldToLocalMatrix * mf.transform.localToWorldMatrix);
                    if (first)
                    {
                        b = wb;
                        first = false;
                    }
                    else
                    {
                        b.Encapsulate(wb);
                    }
                }

                Assert.LessOrEqual(b.size.x, 1.06f, id + "/" + prefab.name + " is wider than a tile");
                Assert.LessOrEqual(b.size.z, 1.06f, id + "/" + prefab.name + " is deeper than a tile");
            }
        }

        /// <summary>
        /// Renders each dev vault from above in every theme under a neutral light, and checks the
        /// tile types stay distinct: walls against floors, pits darker than floors, ice and doors
        /// standing out from both. The same rules for every theme is what "identical readability" means.
        /// </summary>
        [TestCaseSource(nameof(ThemeIds))]
        public void EveryVaultReadsTheSameInEveryTheme(string id)
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            {
                Assert.Ignore("No GPU in this run");
            }

            ThemeDefinition theme = AssetDatabase.LoadAssetAtPath<ThemeCatalog>(CatalogPath).Find(id);
            Shader.SetGlobalVector("_WickVisGrid", Vector4.zero); // no mask: judge the art itself
            Shader.SetGlobalFloat("_WickLitBoost", 0f);
            var light = new GameObject("Neutral light (test)").AddComponent<Light>();
            _spawned.Add(light.gameObject);
            light.type = LightType.Directional;
            light.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            light.color = Color.white;
            light.intensity = 1f;

            foreach (TextAsset text in Resources.LoadAll<TextAsset>("DevVaults"))
            {
                VaultDef vault = VaultJson.Parse(text.text);
                var root = new GameObject("Vault (test)");
                _spawned.Add(root);
                Func<TilePos, TileType> tileAt = vault.TileAt;
                for (int y = 0; y < vault.Height; y++)
                {
                    for (int x = 0; x < vault.Width; x++)
                    {
                        var p = new TilePos(x, y);
                        theme.Spawn(ThemeKit.Choose(vault.Width, vault.Height, tileAt, vault.Entrance, p), SimPose.TileCenter(p), root.transform);
                    }
                }

                Texture2D image = Render(vault);
                AssertReadable(id + "/" + text.name, vault, image);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void AssertReadable(string what, VaultDef vault, Texture2D image)
        {
            var sum = new Dictionary<TileType, Vector3>();
            var count = new Dictionary<TileType, int>();
            for (int y = 0; y < vault.Height; y++)
            {
                for (int x = 0; x < vault.Width; x++)
                {
                    TileType t = vault.TileAt(new TilePos(x, y));
                    Color c = Centre(image, x, vault.Height - 1 - y);
                    sum[t] = (sum.TryGetValue(t, out Vector3 s) ? s : Vector3.zero) + new Vector3(c.r, c.g, c.b);
                    count[t] = (count.TryGetValue(t, out int n) ? n : 0) + 1;
                }
            }

            Vector3 Mean(TileType t) => sum[t] / count[t];
            float Lum(Vector3 c) => 0.2126f * c.x + 0.7152f * c.y + 0.0722f * c.z;

            Vector3 floor = Mean(TileType.Floor);
            Assert.IsTrue(sum.ContainsKey(TileType.Wall), what + " has no walls?");
            Vector3 wall = Mean(TileType.Wall);
            Assert.Greater(Mathf.Abs(Lum(wall) - Lum(floor)), 0.08f, what + ": walls and floors too alike (" + wall + " vs " + floor + ")");
            if (sum.ContainsKey(TileType.Pit))
            {
                Assert.Less(Lum(Mean(TileType.Pit)), Lum(floor) - 0.05f, what + ": pits don't read darker than floor");
            }

            foreach (TileType t in new[] { TileType.Ice, TileType.Door })
            {
                if (sum.ContainsKey(t))
                {
                    Assert.Greater((Mean(t) - floor).magnitude, 0.08f, what + ": " + t + " too close to floor");
                    Assert.Greater((Mean(t) - wall).magnitude, 0.08f, what + ": " + t + " too close to walls");
                }
            }
        }

        /// <summary>The average of the middle third of a tile, so a variant's small detail doesn't decide it.</summary>
        private static Color Centre(Texture2D image, int tx, int ty)
        {
            int from = PixelsPerTile / 3, to = PixelsPerTile - from;
            Color c = Color.clear;
            int n = 0;
            for (int y = from; y < to; y++)
            {
                for (int x = from; x < to; x++)
                {
                    c += image.GetPixel(tx * PixelsPerTile + x, ty * PixelsPerTile + y);
                    n++;
                }
            }

            return c / n;
        }

        private Texture2D Render(VaultDef vault)
        {
            var camGo = new GameObject("Camera (test)");
            _spawned.Add(camGo);
            Camera camera = camGo.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = vault.Height * 0.5f;
            camera.aspect = (float)vault.Width / vault.Height;
            camera.transform.position = new Vector3((vault.Width - 1) * 0.5f, 20f, -(vault.Height - 1) * 0.5f);
            camera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.nearClipPlane = 1f;
            camera.farClipPlane = 40f;

            var rt = new RenderTexture(vault.Width * PixelsPerTile, vault.Height * PixelsPerTile, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            var request = new RenderPipeline.StandardRequest { destination = rt };
            if (RenderPipeline.SupportsRenderRequest(camera, request))
            {
                RenderPipeline.SubmitRenderRequest(camera, request);
            }
            else
            {
                camera.targetTexture = rt;
                camera.Render();
                camera.targetTexture = null;
            }

            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false, true);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = previous;
            rt.Release();
            UnityEngine.Object.DestroyImmediate(rt);
            UnityEngine.Object.DestroyImmediate(camGo);
            _spawned.Remove(camGo);
            _spawned.Add(tex);
            return tex;
        }

        private static Bounds TransformBounds(Bounds b, Matrix4x4 m)
        {
            var result = new Bounds(m.MultiplyPoint3x4(b.center), Vector3.zero);
            for (int i = 0; i < 8; i++)
            {
                result.Encapsulate(m.MultiplyPoint3x4(new Vector3((i & 1) == 0 ? b.min.x : b.max.x, (i & 2) == 0 ? b.min.y : b.max.y,
                    (i & 4) == 0 ? b.min.z : b.max.z)));
            }

            return result;
        }
    }
}
