using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Wick.Editor.ArtPipeline
{
    /// <summary>Measures imported assets into AssetFacts for ArtRules.</summary>
    public static class ArtFacts
    {
        public static AssetFacts Texture(string path, TextureImporter importer)
        {
            importer.GetSourceTextureWidthAndHeight(out int width, out int height);
            return new AssetFacts { Path = path, TextureWidth = width, TextureHeight = height };
        }

        public static AssetFacts Model(string path, GameObject root, ModelImporter importer)
        {
            var f = new AssetFacts { Path = path };
            var materials = new HashSet<string>();
            var min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            var max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
            Matrix4x4 toRoot = root.transform.worldToLocalMatrix;

            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                f.Bones.Add(t.name);
            }

            foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
            {
                Mesh mesh = r is SkinnedMeshRenderer skinned ? skinned.sharedMesh : r.GetComponent<MeshFilter>()?.sharedMesh;
                if (mesh == null)
                {
                    continue;
                }

                for (int i = 0; i < mesh.subMeshCount; i++)
                {
                    if (mesh.GetTopology(i) == MeshTopology.Triangles)
                    {
                        f.Triangles += (int)(mesh.GetIndexCount(i) / 3);
                    }
                }

                for (int i = 0; i < mesh.blendShapeCount; i++)
                {
                    f.BlendShapes.Add(mesh.GetBlendShapeName(i));
                }

                bool flame = r.name.ToLowerInvariant().Contains("flame");
                f.HasFlameMesh |= flame;
                if (!flame)
                {
                    foreach (Material m in r.sharedMaterials)
                    {
                        materials.Add(m != null ? m.name : "(none)");
                    }
                }

                Matrix4x4 m2r = toRoot * r.transform.localToWorldMatrix;
                Bounds b = mesh.bounds;
                for (int c = 0; c < 8; c++)
                {
                    var corner = new Vector3((c & 1) == 0 ? b.min.x : b.max.x, (c & 2) == 0 ? b.min.y : b.max.y, (c & 4) == 0 ? b.min.z : b.max.z);
                    Vector3 p = m2r.MultiplyPoint3x4(corner);
                    min = Vector3.Min(min, p);
                    max = Vector3.Max(max, p);
                }
            }

            f.Materials = materials.Count;
            if (f.Triangles > 0)
            {
                f.MinY = min.y;
                f.Height = max.y - min.y;
                f.Width = max.x - min.x;
                f.Depth = max.z - min.z;
                f.CentreX = (min.x + max.x) * 0.5f;
                f.CentreZ = (min.z + max.z) * 0.5f;
            }

            string file = System.IO.Path.GetFileNameWithoutExtension(path);
            if (file.Contains("@"))
            {
                f.Clips.Add(file.Substring(file.IndexOf('@') + 1));
            }
            else
            {
                ModelImporterClipAnimation[] clips = importer.clipAnimations.Length > 0 ? importer.clipAnimations : importer.defaultClipAnimations;
                foreach (ModelImporterClipAnimation c in clips)
                {
                    f.Clips.Add(c.name);
                }
            }

            return f;
        }
    }
}
