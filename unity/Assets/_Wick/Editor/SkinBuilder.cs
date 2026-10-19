using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Rendering;
using Wick.Editor.ArtPipeline;
using Wick.Game;

namespace Wick.Editor
{
    /// <summary>
    /// Builds Wick's skins, clips and shared Animator controller. A skin whose model is in
    /// Art/Incoming/char/&lt;asset_id&gt;/ uses it (with a StylisedLit material from its _albedo and
    /// _mask textures); every other skin gets a generated stand-in on the same rig: the shared
    /// bone names, the face blend shapes and a flame mesh, so reactions and tests work before
    /// the art arrives. Clips come from char_wick_default when it exists, stand-ins otherwise.
    /// Menu: Wick > Art > Build Skins. Also run by Wick > Rebuild Placeholder Content.
    /// </summary>
    public static class SkinBuilder
    {
        private const string Root = "Assets/_Wick";
        private const string SkinsDir = Root + "/Art/Skins";
        private const string StandInDir = Root + "/Art/StandIns";
        private const string ClipDir = Root + "/Art/Animation/StandIn";
        public const string ControllerPath = Root + "/Art/Animation/WickShared.controller";
        public const string CatalogPath = SkinsDir + "/SkinCatalog.asset";

        /// <summary>The skins in the art packs. Cosmetic ids match the economy's grants.</summary>
        private static readonly SkinSpec[] Specs =
        {
            new SkinSpec("char_wick_default", SkinCatalog.DefaultId, "Wick", new Color(0.96f, 0.91f, 0.85f)),
            new SkinSpec("char_drippy", "skin_drippy", "Drippy", new Color(0.98f, 0.86f, 0.62f)), // honey wax: the differentiator is still open
            new SkinSpec("char_cinder", "skin_cinder", "Cinder", new Color(0.93f, 0.89f, 0.83f)) { Mask = true },
            new SkinSpec("char_pip", "skin_pip", "Pip", new Color(0.96f, 0.93f, 0.86f)) { Height = 0.5f, Flame = 2f, Satchel = false },
            new SkinSpec("char_twist", "skin_twist", "Twist", new Color(0.90f, 0.82f, 0.90f)) { Satchel = false, Twist = true },
            new SkinSpec("char_ember", "skin_ember", "Ember", new Color(0.95f, 0.80f, 0.70f)) { Height = 0.7f, Width = 1.3f, Flame = 1.4f },

            // Heist Pass variants: Twist's model, repainted (or with a different flame).
            new SkinSpec("char_twist_gilded", "skin_twist_gilded", "Gilded Twist", new Color(0.95f, 0.76f, 0.30f)) { Satchel = false, Twist = true, Base = "char_twist" },
            new SkinSpec("char_twist_flame", "skin_twist_flame", "Blazing Twist", new Color(0.90f, 0.82f, 0.90f))
                { Satchel = false, Twist = true, Base = "char_twist", FlameCore = new Color(1f, 1f, 0.95f), FlameOuter = new Color(1f, 0.75f, 0.2f) },
        };

        private sealed class SkinSpec
        {
            public readonly string AssetId, Id, Name;
            public readonly Color Wax;
            public float Height = 1f, Width = 1f, Flame = 1f;
            public bool Satchel = true, Mask, Twist;

            /// <summary>For a variant: the skin whose model it repaints.</summary>
            public string Base;

            /// <summary>A variant flame colour (null: the standard flame).</summary>
            public Color? FlameCore, FlameOuter;

            public SkinSpec(string assetId, string id, string name, Color wax)
            {
                AssetId = assetId;
                Id = id;
                Name = name;
                Wax = wax;
            }
        }

        [MenuItem("Wick/Art/Build Skins")]
        public static void BuildMenu()
        {
            Build();
            AssetDatabase.SaveAssets();
            Debug.Log("SkinBuilder: skins, clips and the shared controller rebuilt.");
        }

        /// <summary>Builds everything and returns the catalog and the shared controller.</summary>
        public static (SkinCatalog, AnimatorController) Build()
        {
            foreach (string dir in new[] { SkinsDir, StandInDir, StandInDir + "/Meshes", ClipDir })
            {
                Directory.CreateDirectory(dir);
            }

            AnimatorController controller = BuildController(ResolveClips());
            var catalog = LoadOrCreate<SkinCatalog>(CatalogPath);
            catalog.Skins.Clear();
            foreach (SkinSpec spec in Specs)
            {
                catalog.Skins.Add(BuildSkin(spec));
            }

            EditorUtility.SetDirty(catalog);
            return (catalog, controller);
        }

        // ---- Skins -----------------------------------------------------------------------------

        private static SkinDefinition BuildSkin(SkinSpec spec)
        {
            var skin = LoadOrCreate<SkinDefinition>(SkinsDir + "/" + spec.AssetId + ".asset");
            skin.Id = spec.Id;
            skin.DisplayName = spec.Name;
            skin.FlameMaterial = spec.FlameCore.HasValue ? VariantFlame(spec) : AssetDatabase.LoadAssetAtPath<Material>(Root + "/Art/Materials/Flame.mat");
            var vfx = AssetDatabase.LoadAssetAtPath<VfxLibrary>(VfxBuilder.LibraryPath);
            skin.SnuffVfx = vfx?.Find(VfxLibrary.SnuffPuff)?.Prefab;
            skin.FlameVfx = vfx?.Find(VfxLibrary.FlameSparks)?.Prefab;

            // A variant uses its base skin's model with its own paint (when that art exists).
            string modelId = spec.Base ?? spec.AssetId;
            string folder = ArtBudgets.IncomingRoot + "/char/" + spec.AssetId;
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ArtBudgets.IncomingRoot + "/char/" + modelId + "/" + modelId + ".fbx");
            if (model != null)
            {
                skin.Model = model;
                bool ownPaint = File.Exists(folder + "/" + spec.AssetId + "_albedo.png");
                skin.BodyMaterial = RealMaterial(ownPaint ? spec.AssetId : modelId, ownPaint ? folder : ArtBudgets.IncomingRoot + "/char/" + modelId);
                skin.Icon = AssetDatabase.LoadAssetAtPath<Sprite>(ArtBudgets.IncomingRoot + "/ui/ui_icon_" + spec.AssetId.Substring(5) + "/ui_icon_" +
                    spec.AssetId.Substring(5) + ".png");
                skin.StandIn = false;
            }
            else
            {
                skin.Model = StandInModel(spec);
                skin.BodyMaterial = null; // the stand-in carries its own tinted materials
                skin.StandIn = true;
            }

            EditorUtility.SetDirty(skin);
            return skin;
        }

        private static Material VariantFlame(SkinSpec spec)
        {
            string path = SkinsDir + "/" + spec.AssetId + "_flame.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Wick/Flame"));
                AssetDatabase.CreateAsset(mat, path);
            }

            mat.SetColor("_CoreColor", spec.FlameCore.Value);
            mat.SetColor("_OuterColor", spec.FlameOuter ?? spec.FlameCore.Value);
            mat.SetFloat("_IgnoreMask", 1f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>A StylisedLit material from the asset's painted albedo and mask.</summary>
        private static Material RealMaterial(string assetId, string folder)
        {
            string path = SkinsDir + "/" + assetId + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Wick/StylisedLit"));
                AssetDatabase.CreateAsset(mat, path);
            }

            mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(folder + "/" + assetId + "_albedo.png"));
            Texture2D mask = AssetDatabase.LoadAssetAtPath<Texture2D>(folder + "/" + assetId + "_mask.png");
            mat.SetTexture("_MaskMap", mask != null ? mask : Texture2D.whiteTexture);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // ---- Stand-in models (the shared rig, simple shapes) -------------------------------------

        private static GameObject StandInModel(SkinSpec spec)
        {
            float h = spec.Height, w = spec.Width;
            Material wax = TintedMaterial(spec.AssetId + "_wax", spec.Wax);
            Material dark = TintedMaterial("standin_dark", new Color(0.12f, 0.08f, 0.06f));
            Material leather = TintedMaterial("standin_leather", new Color(0.45f, 0.28f, 0.15f));

            var model = new GameObject(spec.AssetId);
            Transform root = Bone("root", model.transform, Vector3.zero);
            Transform body = Bone("body", root, new Vector3(0f, 0.04f, 0f));
            Transform head = Bone("head", body, new Vector3(0f, 0.26f * h, 0f));
            Transform eyeL = Bone("eye.L", head, new Vector3(-0.055f * w, 0.06f, 0.15f * w));
            Transform eyeR = Bone("eye.R", head, new Vector3(0.055f * w, 0.06f, 0.15f * w));
            Transform flameBase = Bone("flame_base", head, new Vector3(0f, 0.2f * h + 0.02f, 0f));
            Transform flameMid = Bone("flame_mid", flameBase, new Vector3(0f, 0.07f * spec.Flame, 0f));
            Transform flameTip = Bone("flame_tip", flameMid, new Vector3(0f, 0.07f * spec.Flame, 0f));
            Transform armL = Bone("arm.L", body, new Vector3(-0.17f * w, 0.18f * h, 0f));
            Transform armR = Bone("arm.R", body, new Vector3(0.17f * w, 0.18f * h, 0f));
            Transform footL = Bone("foot.L", root, new Vector3(-0.07f * w, 0.03f, 0.02f));
            Transform footR = Bone("foot.R", root, new Vector3(0.07f * w, 0.03f, 0.02f));

            // Body: a waxy pillar skinned to body and head, with the five face shapes.
            Transform[] bodyBones = { root, body, head };
            Mesh bodyMesh = Lathe(spec.AssetId + "_body", 0.04f, 0.44f * h, 18, 10, t => (0.16f - 0.02f * t * t) * w * (spec.Twist ? 1f + 0.08f * Mathf.Sin(t * 18f) : 1f),
                t => t < 0.45f ? 1 : 2, model.transform, bodyBones);
            AddFaceShapes(bodyMesh, 0.04f + 0.44f * h);
            Skinned("body_mesh", model.transform, bodyMesh, bodyBones, body, wax);

            // Flame: its own mesh (Wick/Flame), skinned to the three flame bones, UV v from base to tip.
            Transform[] flameBones = { flameBase, flameMid, flameTip };
            float flameHeight = 0.2f * spec.Flame;
            Vector3 flameOrigin = model.transform.InverseTransformPoint(flameBase.position);
            Mesh flameMesh = Lathe(spec.AssetId + "_flame", flameOrigin.y, flameHeight, 12, 8, t => 0.06f * spec.Flame * Mathf.Sin(Mathf.PI * Mathf.Lerp(0.15f, 1f, t)) * (1f - 0.6f * t),
                t => t < 0.33f ? 0 : t < 0.66f ? 1 : 2, model.transform, flameBones);
            Skinned("flame_mesh", model.transform, flameMesh, flameBones, flameBase, AssetDatabase.LoadAssetAtPath<Material>(Root + "/Art/Materials/Flame.mat"));

            // Rigid parts ride their bones.
            Part(PrimitiveType.Sphere, eyeL, Vector3.zero, Vector3.one * 0.06f, dark);
            Part(PrimitiveType.Sphere, eyeR, Vector3.zero, Vector3.one * 0.06f, dark);
            Part(PrimitiveType.Capsule, armL, new Vector3(-0.03f, -0.05f, 0f), new Vector3(0.06f, 0.07f, 0.06f), wax);
            Part(PrimitiveType.Capsule, armR, new Vector3(0.03f, -0.05f, 0f), new Vector3(0.06f, 0.07f, 0.06f), wax);
            Part(PrimitiveType.Sphere, footL, Vector3.zero, new Vector3(0.08f, 0.05f, 0.11f), wax);
            Part(PrimitiveType.Sphere, footR, Vector3.zero, new Vector3(0.08f, 0.05f, 0.11f), wax);
            if (spec.Satchel)
            {
                Part(PrimitiveType.Cube, body, new Vector3(-0.17f * w, 0.1f * h, 0.04f), new Vector3(0.1f, 0.1f, 0.06f), leather);
            }

            if (spec.Mask)
            {
                Part(PrimitiveType.Cube, head, new Vector3(0f, 0.06f, 0.13f * w), new Vector3(0.3f * w, 0.06f, 0.06f), dark);
            }

            if (spec.Height < 0.6f)
            {
                Part(PrimitiveType.Cylinder, root, new Vector3(0f, 0.04f, 0f), new Vector3(0.38f * w, 0.04f, 0.38f * w),
                    TintedMaterial("standin_tin", new Color(0.72f, 0.74f, 0.78f))); // Pip's tealight cup
            }

            string path = StandInDir + "/" + spec.AssetId + "_standin.prefab";
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(model, path);
            Object.DestroyImmediate(model);
            return prefab;
        }

        private static Transform Bone(string name, Transform parent, Vector3 position)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = position;
            return t;
        }

        /// <summary>
        /// A surface of revolution from y0 up `height`, radius(t) for t 0..1, UV v = t, each ring
        /// weighted fully to bones[bone(t)]. Vertices are in the model root's space.
        /// </summary>
        private static Mesh Lathe(string name, float y0, float height, int segments, int rings, System.Func<float, float> radius,
            System.Func<float, int> bone, Transform space, Transform[] bones)
        {
            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            var weights = new List<BoneWeight>();
            var triangles = new List<int>();
            for (int r = 0; r <= rings; r++)
            {
                float t = r / (float)rings;
                for (int s = 0; s <= segments; s++)
                {
                    float a = s / (float)segments * Mathf.PI * 2f;
                    float rad = r == rings ? 0f : radius(t); // close the top
                    vertices.Add(new Vector3(Mathf.Sin(a) * rad, y0 + t * height, Mathf.Cos(a) * rad));
                    uvs.Add(new Vector2(s / (float)segments, t));
                    weights.Add(new BoneWeight { boneIndex0 = bone(t), weight0 = 1f });
                }
            }

            for (int r = 0; r < rings; r++)
            {
                for (int s = 0; s < segments; s++)
                {
                    int a = r * (segments + 1) + s, b = a + segments + 1;
                    triangles.AddRange(new[] { a, a + 1, b, a + 1, b + 1, b }); // clockwise from outside: faces point out
                }
            }

            var mesh = new Mesh { name = name };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.boneWeights = weights.ToArray();
            var bindposes = new Matrix4x4[bones.Length];
            for (int i = 0; i < bones.Length; i++)
            {
                bindposes[i] = bones[i].worldToLocalMatrix * space.localToWorldMatrix;
            }

            mesh.bindposes = bindposes;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>fear, greed, smug, wince, blink: small, visible deltas on the face area so every shape moves something.</summary>
        private static void AddFaceShapes(Mesh mesh, float top)
        {
            Vector3[] v = mesh.vertices;
            var zero = new Vector3[v.Length];
            foreach (string shape in ReactionDriver.FaceShapes)
            {
                var d = new Vector3[v.Length];
                for (int i = 0; i < v.Length; i++)
                {
                    bool face = v[i].z > 0.05f && v[i].y > top - 0.2f;
                    if (!face)
                    {
                        continue;
                    }

                    switch (shape)
                    {
                        case "fear": d[i] = new Vector3(v[i].x * 0.12f, 0.015f, 0f); break;     // eyes wide, brows up
                        case "greed": d[i] = new Vector3(0f, 0f, 0.02f); break;                  // leaning in, grinning
                        case "smug": d[i] = new Vector3(0f, v[i].x * 0.08f, 0f); break;          // one side up
                        case "wince": d[i] = new Vector3(-v[i].x * 0.1f, -0.015f, 0f); break;    // squeezed
                        case "blink": d[i] = new Vector3(0f, -0.01f, -0.005f); break;
                    }
                }

                mesh.AddBlendShapeFrame(shape, 100f, d, zero, zero);
            }
        }

        private static void Skinned(string name, Transform parent, Mesh mesh, Transform[] bones, Transform rootBone, Material material)
        {
            string path = StandInDir + "/Meshes/" + mesh.name + ".asset";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(mesh, path);

            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var smr = go.AddComponent<SkinnedMeshRenderer>();
            smr.sharedMesh = mesh;
            smr.bones = bones;
            smr.rootBone = rootBone;
            smr.sharedMaterial = material;
            smr.updateWhenOffscreen = true;
            smr.shadowCastingMode = ShadowCastingMode.Off;
        }

        private static void Part(PrimitiveType type, Transform bone, Vector3 position, Vector3 scale, Material material)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = bone.name + "_part";
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(bone, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = ShadowCastingMode.Off;
        }

        private static Material TintedMaterial(string name, Color colour)
        {
            string path = StandInDir + "/" + name + ".mat";
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

        // ---- Clips -------------------------------------------------------------------------------

        /// <summary>Each clip in the handoff list: from char_wick_default when the art has it, else a stand-in.</summary>
        private static Dictionary<string, AnimationClip> ResolveClips()
        {
            var clips = new Dictionary<string, AnimationClip>();
            string folder = ArtBudgets.IncomingRoot + "/char/char_wick_default";
            if (AssetDatabase.IsValidFolder(folder))
            {
                foreach (string guid in AssetDatabase.FindAssets("t:AnimationClip", new[] { folder }))
                {
                    foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GUIDToAssetPath(guid)))
                    {
                        if (o is AnimationClip c && !c.name.StartsWith("__preview__") && ArtBudgets.Contains(ArtBudgets.WickClips, c.name))
                        {
                            clips[c.name] = c;
                        }
                    }
                }
            }

            foreach (string name in ArtBudgets.WickClips)
            {
                if (!clips.ContainsKey(name))
                {
                    clips[name] = StandInClip(name);
                }
            }

            return clips;
        }

        // Clips never touch flame_base's scale: WickView sets it from the sim's light radius.
        private const string Body = "root/body", Head = "root/body/head";
        private const string ArmL = "root/body/arm.L", ArmR = "root/body/arm.R", FootL = "root/foot.L", FootR = "root/foot.R";

        private static AnimationClip StandInClip(string name)
        {
            var clip = new AnimationClip { name = name };
            bool loop = name == "idle" || name == "run" || name == "tiptoe" || name == "low_health";
            switch (name)
            {
                case "idle":
                    Scale(clip, Body, Keys(0f, 1f, 1f, 1.03f, 2f, 1f), Keys(0f, 1f, 1f, 0.98f, 2f, 1f));
                    break;
                case "idle_fidget_pat":
                    Rotate(clip, ArmR, "z", Keys(0f, 0f, 0.4f, 150f, 1f, 150f, 1.5f, 0f));
                    Rotate(clip, Head, "z", Keys(0f, 0f, 0.5f, -8f, 1f, -8f, 1.5f, 0f));
                    break;
                case "idle_fidget_count":
                    Rotate(clip, ArmL, "x", Keys(0f, 0f, 0.5f, -60f, 1.5f, -60f, 2f, 0f));
                    Rotate(clip, Head, "x", Keys(0f, 0f, 0.5f, 20f, 1.5f, 20f, 2f, 0f));
                    break;
                case "dash":
                    Scale(clip, Body, Keys(0f, 1f, 0.08f, 0.8f, 0.16f, 1.2f, 0.27f, 1f), Keys(0f, 1f, 0.08f, 1.15f, 0.16f, 0.9f, 0.27f, 1f));
                    break;
                case "run":
                    Rotate(clip, Body, "x", Keys(0f, 12f, 0.53f, 12f));
                    Move(clip, FootL, "z", Keys(0f, 0.08f, 0.27f, -0.04f, 0.53f, 0.08f));
                    Move(clip, FootR, "z", Keys(0f, -0.04f, 0.27f, 0.08f, 0.53f, -0.04f));
                    break;
                case "tiptoe":
                    Scale(clip, Body, Keys(0f, 0.9f, 0.8f, 0.9f), null);
                    Move(clip, FootL, "y", Keys(0f, 0.03f, 0.4f, 0.07f, 0.8f, 0.03f));
                    Move(clip, FootR, "y", Keys(0f, 0.07f, 0.4f, 0.03f, 0.8f, 0.07f));
                    break;
                case "hit":
                    Scale(clip, Body, Keys(0f, 1f, 0.1f, 0.75f, 0.4f, 1f), Keys(0f, 1f, 0.1f, 1.2f, 0.4f, 1f));
                    break;
                case "low_health":
                    Rotate(clip, Body, "z", Keys(0f, -4f, 0.17f, 4f, 0.33f, 4f, 0.5f, -4f, 0.67f, -4f));
                    break;
                case "grab":
                    Rotate(clip, ArmL, "x", Keys(0f, 0f, 0.2f, -90f, 0.6f, -40f));
                    Rotate(clip, ArmR, "x", Keys(0f, 0f, 0.2f, -90f, 0.6f, -40f));
                    break;
                case "escape":
                    Move(clip, Body, "y", Keys(0f, 0.04f, 0.3f, 0.2f, 0.6f, 0.04f, 0.8f, 0.04f));
                    break;
                case "snuff":
                    Rotate(clip, Body, "z", Keys(0f, 0f, 0.5f, 10f, 0.7f, -6f, 1f, 4f));
                    Scale(clip, Body, Keys(0f, 1f, 0.6f, 0.85f, 1f, 0.9f), null);
                    break;
                case "victory":
                    Rotate(clip, ArmL, "z", Keys(0f, 0f, 0.3f, -150f, 1.6f, -150f));
                    Rotate(clip, ArmR, "z", Keys(0f, 0f, 0.3f, 150f, 1.6f, 150f));
                    Move(clip, Body, "y", Keys(0f, 0.04f, 0.4f, 0.15f, 0.8f, 0.04f, 1.6f, 0.04f));
                    break;
            }

            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = loop;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            string path = ClipDir + "/" + name + ".anim";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(clip, path);
            return clip;
        }

        private static AnimationCurve Keys(params float[] timeValue)
        {
            var curve = new AnimationCurve();
            for (int i = 0; i < timeValue.Length; i += 2)
            {
                curve.AddKey(timeValue[i], timeValue[i + 1]);
            }

            return curve;
        }

        /// <summary>Squash and stretch: y from `y`, x and z from `xz` (or volume-preserving from y).</summary>
        private static void Scale(AnimationClip clip, string path, AnimationCurve y, AnimationCurve xz)
        {
            if (xz == null)
            {
                xz = new AnimationCurve();
                foreach (Keyframe k in y.keys)
                {
                    xz.AddKey(k.time, 1f / Mathf.Sqrt(Mathf.Max(0.05f, k.value)));
                }
            }

            clip.SetCurve(path, typeof(Transform), "m_LocalScale.x", xz);
            clip.SetCurve(path, typeof(Transform), "m_LocalScale.y", y);
            clip.SetCurve(path, typeof(Transform), "m_LocalScale.z", xz);
        }

        /// <summary>A single-axis rotation, keyed as quaternion curves (what generic clips store).</summary>
        private static void Rotate(AnimationClip clip, string path, string axis, AnimationCurve degrees)
        {
            Vector3 unit = axis == "x" ? Vector3.right : axis == "y" ? Vector3.up : Vector3.forward;
            var x = new AnimationCurve();
            var y = new AnimationCurve();
            var z = new AnimationCurve();
            var w = new AnimationCurve();
            foreach (Keyframe k in degrees.keys)
            {
                Quaternion q = Quaternion.AngleAxis(k.value, unit);
                x.AddKey(k.time, q.x);
                y.AddKey(k.time, q.y);
                z.AddKey(k.time, q.z);
                w.AddKey(k.time, q.w);
            }

            clip.SetCurve(path, typeof(Transform), "m_LocalRotation.x", x);
            clip.SetCurve(path, typeof(Transform), "m_LocalRotation.y", y);
            clip.SetCurve(path, typeof(Transform), "m_LocalRotation.z", z);
            clip.SetCurve(path, typeof(Transform), "m_LocalRotation.w", w);
        }

        private static void Move(AnimationClip clip, string path, string axis, AnimationCurve value)
        {
            clip.SetCurve(path, typeof(Transform), "m_LocalPosition." + axis, value);
        }

        // ---- The shared controller -------------------------------------------------------------

        /// <summary>
        /// Layer "Body": locomotion (idle, step/dash, run, tiptoe, low health) and the end poses
        /// (escape then victory, snuff). Layer "Actions": one-shots over it (hit, grab, fidgets),
        /// returning to an empty state that writes nothing.
        /// </summary>
        private static AnimatorController BuildController(Dictionary<string, AnimationClip> clips)
        {
            AssetDatabase.DeleteAsset(ControllerPath);
            AnimatorController c = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            c.AddParameter("Fear", AnimatorControllerParameterType.Float);
            c.AddParameter("Flickers", AnimatorControllerParameterType.Int);
            c.AddParameter("Greed", AnimatorControllerParameterType.Float);
            c.AddParameter("Tiptoe", AnimatorControllerParameterType.Bool);
            c.AddParameter("Outcome", AnimatorControllerParameterType.Int);
            c.AddParameter("Move", AnimatorControllerParameterType.Int);
            c.AddParameter("Hit", AnimatorControllerParameterType.Trigger);
            c.AddParameter("Grab", AnimatorControllerParameterType.Trigger);
            c.AddParameter("Fidget", AnimatorControllerParameterType.Trigger);
            c.AddParameter("FidgetIndex", AnimatorControllerParameterType.Int);
            AnimatorControllerLayer[] layers = c.layers;
            layers[0].name = "Body";
            c.layers = layers;

            AnimatorStateMachine body = c.layers[0].stateMachine;
            AnimatorState idle = State(body, "Idle", clips["idle"]);
            AnimatorState step = State(body, "Dash", clips["dash"]);
            AnimatorState run = State(body, "Run", clips["run"]);
            AnimatorState tiptoe = State(body, "Tiptoe", clips["tiptoe"]);
            AnimatorState low = State(body, "LowHealth", clips["low_health"]);
            AnimatorState escape = State(body, "Escape", clips["escape"]);
            AnimatorState victory = State(body, "Victory", clips["victory"]);
            AnimatorState snuff = State(body, "Snuff", clips["snuff"]);
            body.defaultState = idle;

            // While playing, the state follows the sim each frame; the outcome states hold.
            Any(body, run, ("Outcome", AnimatorConditionMode.Equals, 0), ("Move", AnimatorConditionMode.Equals, 2));
            Any(body, tiptoe, ("Outcome", AnimatorConditionMode.Equals, 0), ("Move", AnimatorConditionMode.NotEqual, 2), ("Tiptoe", AnimatorConditionMode.If, 0));
            Any(body, step, ("Outcome", AnimatorConditionMode.Equals, 0), ("Move", AnimatorConditionMode.Equals, 1), ("Tiptoe", AnimatorConditionMode.IfNot, 0));
            Any(body, low, ("Outcome", AnimatorConditionMode.Equals, 0), ("Move", AnimatorConditionMode.Equals, 0), ("Tiptoe", AnimatorConditionMode.IfNot, 0),
                ("Flickers", AnimatorConditionMode.Less, 2));
            Any(body, idle, ("Outcome", AnimatorConditionMode.Equals, 0), ("Move", AnimatorConditionMode.Equals, 0), ("Tiptoe", AnimatorConditionMode.IfNot, 0),
                ("Flickers", AnimatorConditionMode.Greater, 1));
            Any(body, escape, ("Outcome", AnimatorConditionMode.Equals, 1));
            Any(body, snuff, ("Outcome", AnimatorConditionMode.Equals, 2));
            AnimatorStateTransition win = escape.AddTransition(victory);
            win.hasExitTime = true;
            win.exitTime = 1f;
            win.hasFixedDuration = true;
            win.duration = 0.1f;

            c.AddLayer("Actions");
            layers = c.layers;
            layers[1].defaultWeight = 1f;
            layers[1].blendingMode = AnimatorLayerBlendingMode.Override;
            c.layers = layers;
            AnimatorStateMachine actions = c.layers[1].stateMachine;
            AnimatorState empty = actions.AddState("Empty");
            empty.writeDefaultValues = false; // contributes nothing, so the body layer shows through
            actions.defaultState = empty;
            OneShot(actions, empty, State(actions, "Hit", clips["hit"], false), ("Hit", AnimatorConditionMode.If, 0));
            OneShot(actions, empty, State(actions, "Grab", clips["grab"], false), ("Grab", AnimatorConditionMode.If, 0));
            OneShot(actions, empty, State(actions, "FidgetPat", clips["idle_fidget_pat"], false), ("Fidget", AnimatorConditionMode.If, 0),
                ("FidgetIndex", AnimatorConditionMode.Equals, 0));
            OneShot(actions, empty, State(actions, "FidgetCount", clips["idle_fidget_count"], false), ("Fidget", AnimatorConditionMode.If, 0),
                ("FidgetIndex", AnimatorConditionMode.Equals, 1));

            EditorUtility.SetDirty(c);
            return c;
        }

        /// <summary>Body states write defaults (so a pose never sticks after its clip); action states don't (so the body shows through).</summary>
        private static AnimatorState State(AnimatorStateMachine sm, string name, Motion motion, bool writeDefaults = true)
        {
            AnimatorState s = sm.AddState(name);
            s.motion = motion;
            s.writeDefaultValues = writeDefaults;
            return s;
        }

        private static void Any(AnimatorStateMachine sm, AnimatorState to, params (string param, AnimatorConditionMode mode, float value)[] conditions)
        {
            AnimatorStateTransition t = sm.AddAnyStateTransition(to);
            t.canTransitionToSelf = false;
            t.hasFixedDuration = true; // seconds, not a fraction of the (possibly empty) source state
            t.duration = 0.12f;
            t.hasExitTime = false;
            foreach ((string param, AnimatorConditionMode mode, float value) in conditions)
            {
                t.AddCondition(mode, value, param);
            }
        }

        private static void OneShot(AnimatorStateMachine sm, AnimatorState empty, AnimatorState shot, params (string param, AnimatorConditionMode mode, float value)[] conditions)
        {
            AnimatorStateTransition into = sm.AddAnyStateTransition(shot);
            into.canTransitionToSelf = false;
            into.hasFixedDuration = true;
            into.duration = 0.05f;
            into.hasExitTime = false;
            foreach ((string param, AnimatorConditionMode mode, float value) in conditions)
            {
                into.AddCondition(mode, value, param);
            }

            AnimatorStateTransition back = shot.AddTransition(empty);
            back.hasExitTime = true;
            back.exitTime = 1f;
            back.hasFixedDuration = true;
            back.duration = 0.1f;
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
