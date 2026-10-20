using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using Wick.Sim;
using Wick.Sim.Tests;

namespace Wick.Game.Tests
{
    /// <summary>
    /// What the player sees must be exactly what Wick.Sim decided. VisibilityMaskTests prove the
    /// texture matches the sim; this renders a floor through Wick/StylisedLit from straight above
    /// and checks every tile's pixel is bright exactly where the sim says the tile is lit, so the
    /// shader's world-to-tile lookup can't drift from SimPose.
    /// </summary>
    public class ShaderVisibilityTests
    {
        private const int PixelsPerTile = 16;
        private readonly List<Object> _spawned = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (Object o in _spawned)
            {
                Object.DestroyImmediate(o);
            }

            _spawned.Clear();
        }

        [TestCaseSource(typeof(GoldenReplayTests), nameof(GoldenReplayTests.GoldenFiles))]
        public void RenderedFloorMatchesSimLitSet(string file)
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            {
                Assert.Ignore("No GPU in this run");
            }

            RaidFile raid = RaidFile.Parse(File.ReadAllText(Path.Combine(GoldenReplayTests.GoldenDirectory(), file)));
            var rig = new GameObject("Shader rig (test)");
            _spawned.Add(rig);
            RaidRunner runner = rig.AddComponent<RaidRunner>();
            VisibilityMask mask = rig.AddComponent<VisibilityMask>();
            var tuning = ScriptableObject.CreateInstance<RaidTuning>();
            tuning.LitBoost = 0.3f; // a clear lit/dark gap: this checks the tile mapping, not the look
            _spawned.Add(tuning);
            mask.Bind(runner, tuning);

            VaultDef vault = raid.Vault;
            Camera camera = Floor(vault);
            runner.LoadReplay(vault, raid.Seed, raid.Log);
            int checkedFrames = 0;
            while (runner.Mode == RaidMode.Replay)
            {
                runner.Advance(TickAccumulator.TickSeconds);
                if (runner.Sim.State.Tick % 45 != 0)
                {
                    continue;
                }

                mask.Refresh();
                AssertPixelsMatch(runner, vault, Render(camera, vault), "tick " + runner.Sim.State.Tick);
                checkedFrames++;
            }

            Assert.Greater(checkedFrames, 0);
        }

        private Camera Floor(VaultDef vault)
        {
            // One upward-facing quad per tile, tile (x, y) centred on world (x, 0, -y) as in SimPose.
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var triangles = new List<int>();
            for (int y = 0; y < vault.Height; y++)
            {
                for (int x = 0; x < vault.Width; x++)
                {
                    int i = vertices.Count;
                    vertices.Add(new Vector3(x - 0.5f, 0, -y + 0.5f));
                    vertices.Add(new Vector3(x + 0.5f, 0, -y + 0.5f));
                    vertices.Add(new Vector3(x + 0.5f, 0, -y - 0.5f));
                    vertices.Add(new Vector3(x - 0.5f, 0, -y - 0.5f));
                    normals.AddRange(new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up });
                    triangles.AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3 });
                }
            }

            var mesh = new Mesh { name = "Floor (test)", indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetTriangles(triangles, 0);
            _spawned.Add(mesh);

            var material = new Material(Shader.Find("Wick/StylisedLit"));
            material.SetColor("_BaseColor", Color.white);
            _spawned.Add(material);

            var floor = new GameObject("Floor (test)");
            _spawned.Add(floor);
            floor.AddComponent<MeshFilter>().sharedMesh = mesh;
            floor.AddComponent<MeshRenderer>().sharedMaterial = material;

            var camGo = new GameObject("Camera (test)");
            _spawned.Add(camGo);
            Camera camera = camGo.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = vault.Height * 0.5f;
            camera.transform.position = new Vector3((vault.Width - 1) * 0.5f, 10f, -(vault.Height - 1) * 0.5f);
            camera.transform.rotation = Quaternion.Euler(90f, 0f, 0f); // looking down, screen up = -tile y
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.nearClipPlane = 1f;
            camera.farClipPlane = 20f;
            return camera;
        }

        private Texture2D Render(Camera camera, VaultDef vault)
        {
            var rt = new RenderTexture(vault.Width * PixelsPerTile, vault.Height * PixelsPerTile, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            camera.aspect = (float)vault.Width / vault.Height;
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
            Object.DestroyImmediate(rt);
            _spawned.Add(tex);
            return tex;
        }

        private static void AssertPixelsMatch(RaidRunner runner, VaultDef vault, Texture2D image, string when)
        {
            // Lit tiles get at least the lit boost (0.3 here); unlit ones a flat 0.04.
            const float threshold = 0.12f;
            for (int y = 0; y < vault.Height; y++)
            {
                for (int x = 0; x < vault.Width; x++)
                {
                    int px = x * PixelsPerTile + PixelsPerTile / 2;
                    int py = (vault.Height - 1 - y) * PixelsPerTile + PixelsPerTile / 2; // image rows run bottom-up
                    bool bright = image.GetPixel(px, py).r > threshold;
                    bool lit = runner.Sim.IsLit(new TilePos(x, y));
                    if (bright != lit)
                    {
                        Assert.Fail(when + ": tile (" + x + "," + y + ") renders " + (bright ? "lit" : "dark") + " but the sim says " +
                            (lit ? "lit" : "dark") + " (pixel " + image.GetPixel(px, py) + ")");
                    }
                }
            }
        }
    }
}
