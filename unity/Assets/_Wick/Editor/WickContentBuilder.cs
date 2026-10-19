using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.UIElements;
using Wick.Game;

namespace Wick.Editor
{
    /// <summary>
    /// Generates the placeholder (grey-box) content for the raid scene from code: materials,
    /// primitive prefabs, the ViewRegistry, tuning assets, Wick's skins and shared Animator
    /// controller (SkinBuilder), UI panel settings, the Raid scene, and build/player settings.
    /// Idempotent: run it again after
    /// changing it. Real art replaces the prefabs later through the ViewRegistry.
    /// Menu: Wick > Rebuild Placeholder Content. Batch: -executeMethod Wick.Editor.WickContentBuilder.Build
    /// </summary>
    public static class WickContentBuilder
    {
        private const string Root = "Assets/_Wick";
        private const string MaterialsDir = Root + "/Art/Materials";
        private const string AnimationDir = Root + "/Art/Animation";
        private const string PrefabsDir = Root + "/Prefabs";
        private const string DataDir = Root + "/Data";
        private const string UiDir = Root + "/Game/UI";
        private const string ScenePath = Root + "/Scenes/Raid.unity";
        private const string EditorScenePath = Root + "/Scenes/Editor.unity";
        private const string EditorUiDir = Root + "/Game/VaultEditor";

        [MenuItem("Wick/Rebuild Placeholder Content")]
        public static void Build()
        {
            foreach (string dir in new[] { MaterialsDir, AnimationDir, PrefabsDir, DataDir })
            {
                Directory.CreateDirectory(dir);
            }

            // Anything that imports assets runs before the references the scene keeps are loaded:
            // an import can replace freshly created objects and leave stale references behind.
            BuildPanelSettings();
            Materials m = BuildMaterials();
            (SkinCatalog skins, AnimatorController controller) = SkinBuilder.Build();
            AudioBuilder.Build();
            BuildPrefabs(m, skins, controller);
            LoadOrCreate<RaidTuning>(DataDir + "/RaidTuning.asset");
            LoadOrCreate<InputTuning>(DataDir + "/InputTuning.asset");
            AssetDatabase.SaveAssets();
            BuildScene();
            BuildEditorContent();
            var scenes = new List<EditorBuildSettingsScene>();
            if (File.Exists(BootSceneBuilder.ScenePath))
            {
                scenes.Add(new EditorBuildSettingsScene(BootSceneBuilder.ScenePath, true)); // Boot stays the first scene
            }

            scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            scenes.Add(new EditorBuildSettingsScene(EditorScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            RemoveTemplateLeftovers();
            ApplyProjectSettings();
            AssetDatabase.SaveAssets();
            Debug.Log("WickContentBuilder: placeholder content rebuilt.");
        }

        // ---- Materials --------------------------------------------------------------------

        private sealed class Materials
        {
            public Material Flame, Decoy;
            public Material Metal, Water, Gust, Spikes, Warning, Bell, Plate;
            public Material Owl, OwlEye, Moths, Gold, Brazier, BrazierFlame, Crate;
            public Material Overlay;
        }

        private static Materials BuildMaterials()
        {
            return new Materials
            {
                Flame = FlameMaterial("Flame", ignoreMask: true),
                Decoy = Lit("Decoy", new Color(1f, 0.85f, 0.5f), new Color(2f, 1.4f, 0.5f), ignoreMask: true),
                Metal = Lit("Metal", new Color(0.40f, 0.40f, 0.46f)),
                Water = Lit("Water", new Color(0.30f, 0.55f, 1f), new Color(0.1f, 0.25f, 0.6f)),
                Gust = Lit("Gust", new Color(0.60f, 0.72f, 0.88f), new Color(0.12f, 0.16f, 0.24f)),
                Spikes = Lit("Spikes", new Color(0.80f, 0.80f, 0.86f)),
                Warning = Lit("Warning", new Color(1f, 0.3f, 0.2f), new Color(1.5f, 0.3f, 0.1f)),
                Bell = Lit("Bell", new Color(0.72f, 0.56f, 0.24f)),
                Plate = Lit("Plate", new Color(0.52f, 0.46f, 0.36f)),
                Owl = Lit("Owl", new Color(0.45f, 0.35f, 0.25f)),
                OwlEye = Lit("OwlEye", Color.white, new Color(0.6f, 0.6f, 0.5f)),
                Moths = Lit("Moths", new Color(0.88f, 0.84f, 0.62f), new Color(0.3f, 0.28f, 0.15f)),
                Gold = Lit("Gold", new Color(1f, 0.8f, 0.2f), new Color(0.4f, 0.3f, 0.05f)),
                Brazier = Lit("Brazier", new Color(0.30f, 0.25f, 0.20f)),
                BrazierFlame = FlameMaterial("BrazierFlame", ignoreMask: false),
                Crate = Lit("Crate", new Color(0.56f, 0.38f, 0.20f)),
                Overlay = MaterialAsset("Overlay", Shader.Find("Wick/Overlay")),
            };
        }

        private static Material Lit(string name, Color baseColor, Color? emission = null, bool ignoreMask = false)
        {
            Material mat = MaterialAsset(name, Shader.Find("Wick/StylisedLit"));
            mat.SetColor("_BaseColor", baseColor);
            mat.SetColor("_EmissionColor", emission ?? Color.black);
            mat.SetFloat("_IgnoreMask", ignoreMask ? 1f : 0f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Material FlameMaterial(string name, bool ignoreMask)
        {
            Material mat = MaterialAsset(name, Shader.Find("Wick/Flame"));
            mat.SetFloat("_IgnoreMask", ignoreMask ? 1f : 0f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Material MaterialAsset(string name, Shader shader)
        {
            string path = MaterialsDir + "/" + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }

            mat.shader = shader;
            return mat;
        }

        // ---- Prefabs ---------------------------------------------------------------------

        private static ViewRegistry BuildPrefabs(Materials m, SkinCatalog skins, AnimatorController controller)
        {
            ViewRegistry registry = LoadOrCreate<ViewRegistry>(DataDir + "/ViewRegistry.asset");

            registry.Themes = ThemeBuilder.Build(); // tiles come from the themes' kits

            // Wick is a holder: WickSkin puts the equipped skin's model in "Skin" when a raid loads.
            registry.Wick = Save("Wick", root =>
            {
                var body = new GameObject("Skin").transform;
                body.SetParent(root, false);
                WickSkin skin = root.gameObject.AddComponent<WickSkin>();
                Set(skin, "_catalog", skins);
                Set(skin, "_controller", controller);
                Set(skin, "_holder", body);

                var lightGo = new GameObject("FlameLight");
                lightGo.transform.SetParent(root, false);
                lightGo.transform.localPosition = new Vector3(0f, 1.1f, 0f);
                Light light = lightGo.AddComponent<Light>();
                light.type = LightType.Point;
                light.shadows = LightShadows.None;
                light.range = 5f;

                ReactionDriver reactions = root.gameObject.AddComponent<ReactionDriver>();
                WickView view = root.gameObject.AddComponent<WickView>();
                Set(view, "_body", body);
                Set(view, "_flameLight", light);
                Set(view, "_reactions", reactions);
                Set(view, "_skin", skin);
            });

            registry.Decoy = Save("Decoy", root =>
            {
                Prim(PrimitiveType.Sphere, root, "Drip", new Vector3(0f, 0.15f, 0f), new Vector3(0.3f, 0.2f, 0.3f), m.Decoy);
                root.gameObject.AddComponent<DecoyView>();
            });

            registry.DripPipe = Save("DripPipe", root =>
            {
                Prim(PrimitiveType.Cylinder, root, "Pipe", new Vector3(0f, 1.7f, 0f), new Vector3(0.22f, 0.4f, 0.22f), m.Metal);
                Prim(PrimitiveType.Cylinder, root, "Puddle", new Vector3(0f, 0.01f, 0f), new Vector3(0.6f, 0.01f, 0.6f), m.Water);
                GameObject drip = Prim(PrimitiveType.Cylinder, root, "Drip", new Vector3(0f, 0.65f, 0f), new Vector3(0.12f, 0.65f, 0.12f), m.Water);
                TimedTrapView view = root.gameObject.AddComponent<TimedTrapView>();
                Set(view, "_active", drip.transform);
            });

            registry.Bellows = Save("Bellows", root =>
            {
                Box(root, "Body", new Vector3(0f, 0.3f, -0.1f), new Vector3(0.7f, 0.6f, 0.5f), m.Metal);
                Box(root, "Nozzle", new Vector3(0f, 0.3f, 0.3f), new Vector3(0.25f, 0.25f, 0.3f), m.Metal);
                GameObject gust = Box(root, "Gust", new Vector3(0f, 0.3f, 1f), new Vector3(0.3f, 0.12f, 1f), m.Gust);
                TimedTrapView view = root.gameObject.AddComponent<TimedTrapView>();
                Set(view, "_gust", gust.transform);
            });

            registry.SpikeFloor = Save("SpikeFloor", root =>
            {
                Box(root, "Plate", new Vector3(0f, 0.02f, 0f), new Vector3(0.9f, 0.04f, 0.9f), m.Metal);
                GameObject warning = Box(root, "Warning", new Vector3(0f, 0.045f, 0f), new Vector3(0.92f, 0.01f, 0.92f), m.Warning);
                var spikes = new GameObject("Spikes").transform;
                spikes.SetParent(root, false);
                for (int i = 0; i < 4; i++)
                {
                    float x = (i % 2 == 0 ? -1 : 1) * 0.2f;
                    float z = (i < 2 ? -1 : 1) * 0.2f;
                    Box(spikes, "Spike" + i, new Vector3(x, 0.3f, z), new Vector3(0.12f, 0.55f, 0.12f), m.Spikes);
                }

                TimedTrapView view = root.gameObject.AddComponent<TimedTrapView>();
                Set(view, "_active", spikes);
                Set(view, "_warning", warning.transform);
            });

            registry.SnufferBell = Save("SnufferBell", root =>
            {
                Box(root, "Plate", new Vector3(0f, 0.03f, 0f), new Vector3(0.6f, 0.06f, 0.6f), m.Plate);
                GameObject bell = Prim(PrimitiveType.Cylinder, root, "Bell", new Vector3(0f, 2.2f, 0f), new Vector3(0.7f, 0.35f, 0.7f), m.Bell);
                BellView view = root.gameObject.AddComponent<BellView>();
                Set(view, "_bell", bell.transform);
            });

            registry.NightOwl = Save("NightOwl", root =>
            {
                Box(root, "Body", new Vector3(0f, 0.4f, 0f), new Vector3(0.5f, 0.8f, 0.4f), m.Owl);
                GameObject eye = Prim(PrimitiveType.Sphere, root, "Eye", new Vector3(0f, 0.65f, 0.22f), Vector3.one * 0.2f, m.OwlEye);
                GuardianView view = root.gameObject.AddComponent<GuardianView>();
                Set(view, "_modeIndicator", eye.GetComponent<Renderer>());
            });

            registry.WaxMoths = Save("WaxMoths", root =>
            {
                GameObject first = null;
                for (int i = 0; i < 5; i++)
                {
                    float a = i * Mathf.PI * 2f / 5f;
                    GameObject moth = Prim(PrimitiveType.Sphere, root, "Moth" + i,
                        new Vector3(Mathf.Cos(a) * 0.22f, 0.5f + (i % 2) * 0.15f, Mathf.Sin(a) * 0.22f), Vector3.one * 0.12f, m.Moths);
                    first = first != null ? first : moth;
                }

                GuardianView view = root.gameObject.AddComponent<GuardianView>();
                Set(view, "_modeIndicator", first.GetComponent<Renderer>());
            });

            registry.Treasure = Save("Treasure", root =>
            {
                GameObject pile = Box(root, "Pile", new Vector3(0f, 0.25f, 0f), new Vector3(0.35f, 0.25f, 0.35f), m.Gold);
                pile.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
                TreasureView view = root.gameObject.AddComponent<TreasureView>();
                Set(view, "_pile", pile.transform);
            });

            registry.Brazier = Save("Brazier", root =>
            {
                Box(root, "Plaque", new Vector3(0f, 0.8f, 0.04f), new Vector3(0.4f, 0.4f, 0.08f), m.Brazier);
                GameObject flame = Prim(PrimitiveType.Sphere, root, "Flame", new Vector3(0f, 1.05f, 0.14f), Vector3.one * 0.22f, m.BrazierFlame);
                BrazierView view = root.gameObject.AddComponent<BrazierView>();
                Set(view, "_flame", flame.transform);
            });

            registry.Crate = Save("Crate", root =>
            {
                Box(root, "Box", new Vector3(0f, 0.425f, 0f), Vector3.one * 0.85f, m.Crate);
                root.gameObject.AddComponent<CrateView>();
            });

            EditorUtility.SetDirty(registry);
            return registry;
        }

        private static GameObject Save(string name, System.Action<Transform> build)
        {
            var go = new GameObject(name);
            build(go.transform);
            string path = PrefabsDir + "/" + name + ".prefab";
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }

        private static GameObject Box(Transform parent, string name, Vector3 position, Vector3 scale, Material material)
        {
            return Prim(PrimitiveType.Cube, parent, name, position, scale, material);
        }

        private static GameObject Prim(PrimitiveType type, Transform parent, string name, Vector3 position, Vector3 scale, Material material)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return go;
        }

        private static void Set(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            SerializedProperty p = so.FindProperty(field);
            if (p == null)
            {
                throw new System.InvalidOperationException(target.GetType().Name + " has no serialized field " + field);
            }

            p.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---- UI ----------------------------------------------------------------------------

        private static PanelSettings BuildPanelSettings()
        {
            string themePath = UiDir + "/RaidTheme.tss";
            if (!File.Exists(themePath))
            {
                File.WriteAllText(themePath, "@import url(\"unity-theme://default\");\n");
                AssetDatabase.ImportAsset(themePath);
            }

            PanelSettings panel = LoadOrCreate<PanelSettings>(UiDir + "/RaidPanelSettings.asset");
            panel.themeStyleSheet = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(themePath);
            panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panel.referenceResolution = new Vector2Int(1080, 1920);
            panel.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            panel.match = 0.5f;
            EditorUtility.SetDirty(panel);
            return panel;
        }

        // ---- Scene ------------------------------------------------------------------------

        private static void BuildScene()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Load after NewScene: switching scenes unloads unused assets, which would leave
            // references taken earlier pointing at destroyed objects (saved as null).
            var registry = AssetDatabase.LoadAssetAtPath<ViewRegistry>(DataDir + "/ViewRegistry.asset");
            var raidTuning = AssetDatabase.LoadAssetAtPath<RaidTuning>(DataDir + "/RaidTuning.asset");
            var inputTuning = AssetDatabase.LoadAssetAtPath<InputTuning>(DataDir + "/InputTuning.asset");
            var panel = AssetDatabase.LoadAssetAtPath<PanelSettings>(UiDir + "/RaidPanelSettings.asset");
            var overlayMaterial = AssetDatabase.LoadAssetAtPath<Material>(MaterialsDir + "/Overlay.mat");

            // Dark vault: gradient ambient only, no directional light; Wick's flame is the light.
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.11f, 0.10f, 0.14f);
            RenderSettings.ambientEquatorColor = new Color(0.07f, 0.06f, 0.08f);
            RenderSettings.ambientGroundColor = new Color(0.03f, 0.03f, 0.04f);
            RenderSettings.skybox = null;
            RenderSettings.fog = false;

            var raid = new GameObject("Raid");
            RaidRunner runner = raid.AddComponent<RaidRunner>();
            VisibilityMask mask = raid.AddComponent<VisibilityMask>();
            Set(mask, "_runner", runner);
            Set(mask, "_tuning", raidTuning);
            VaultView vaultView = raid.AddComponent<VaultView>();
            Set(vaultView, "_runner", runner);
            Set(vaultView, "_registry", registry);
            Set(vaultView, "_mask", mask);
            Set(vaultView, "_tuning", raidTuning);
            RaidInput input = raid.AddComponent<RaidInput>();
            Set(input, "_runner", runner);
            Set(input, "_tuning", inputTuning);
            Set(input, "_raidTuning", raidTuning);
            DevOverlay overlay = raid.AddComponent<DevOverlay>();
            Set(overlay, "_runner", runner);
            Set(overlay, "_material", overlayMaterial);

            var audioGo = new GameObject("Audio");
            AudioDirector audio = audioGo.AddComponent<AudioDirector>();
            Set(audio, "_mixer", AssetDatabase.LoadAssetAtPath<AudioMixer>(AudioBuilder.MixerPath));
            Set(audio, "_bank", AssetDatabase.LoadAssetAtPath<SfxBank>(AudioBuilder.BankPath));
            Set(audio, "_vaultView", vaultView);

            var cameraGo = new GameObject("Main Camera") { tag = "MainCamera" };
            Camera camera = cameraGo.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.fieldOfView = 40f;
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = 60f;
            RaidCamera raidCamera = cameraGo.AddComponent<RaidCamera>();
            Set(raidCamera, "_vaultView", vaultView);
            Set(raidCamera, "_tuning", raidTuning);
            RaidCamera.Place(cameraGo.transform, new Vector3(4f, 0f, -11f), raidTuning);

            var uiGo = new GameObject("UI");
            UIDocument doc = uiGo.AddComponent<UIDocument>();
            doc.panelSettings = panel;
            doc.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UiDir + "/RaidUI.uxml");
            RaidUI ui = uiGo.AddComponent<RaidUI>();
            Set(ui, "_runner", runner);
            Set(ui, "_input", input);
            Set(ui, "_overlay", overlay);

            RaidFlow flow = raid.AddComponent<RaidFlow>();
            Set(flow, "_runner", runner);
            Set(flow, "_ui", ui);
            Set(flow, "_camera", raidCamera);
            Set(flow, "_tuning", raidTuning);

            RaidFeel feel = raid.AddComponent<RaidFeel>();
            Set(feel, "_vaultView", vaultView);
            Set(feel, "_ui", ui);
            Set(feel, "_camera", camera);

            var eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<InputSystemUIInputModule>();

            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        // ---- Project ------------------------------------------------------------------------

        // ---- Vault editor ----------------------------------------------------------------------

        private static void BuildEditorContent()
        {
            Material entrance = Lit("EditorEntrance", new Color(0.3f, 1f, 0.5f), new Color(0.2f, 0.8f, 0.3f), ignoreMask: true);
            Material hoard = Lit("EditorHoard", new Color(1f, 0.8f, 0.2f), new Color(0.9f, 0.6f, 0.1f), ignoreMask: true);
            Material waypoint = Lit("EditorWaypoint", new Color(0.4f, 0.8f, 1f), new Color(0.2f, 0.5f, 0.9f), ignoreMask: true);
            Material line = MaterialAsset("EditorPatrolLine", Shader.Find("Wick/Overlay"));
            line.SetColor("_Color", new Color(0.4f, 0.8f, 1f, 0.85f));
            EditorUtility.SetDirty(line);

            EditorAssets assets = LoadOrCreate<EditorAssets>(DataDir + "/EditorAssets.asset");
            assets.Registry = AssetDatabase.LoadAssetAtPath<ViewRegistry>(DataDir + "/ViewRegistry.asset");
            assets.Tuning = LoadOrCreate<EditorTuning>(DataDir + "/EditorTuning.asset");
            assets.EntranceMarker = Save("EditorEntrance", root =>
            {
                Box(root, "Base", new Vector3(0f, 0.03f, 0f), new Vector3(0.8f, 0.04f, 0.8f), entrance);
                Box(root, "Arrow", new Vector3(0f, 0.06f, 0.15f), new Vector3(0.2f, 0.04f, 0.5f), entrance);
            });
            assets.HoardEdge = Save("EditorHoardEdge", root => Box(root, "Edge", Vector3.zero, Vector3.one, hoard));
            assets.Waypoint = Save("EditorWaypoint", root =>
                Prim(PrimitiveType.Cylinder, root, "Point", new Vector3(0f, 0.05f, 0f), new Vector3(0.3f, 0.04f, 0.3f), waypoint));
            assets.CornerMarker = Save("EditorCorner", root =>
                Prim(PrimitiveType.Cylinder, root, "Corner", new Vector3(0f, 0.08f, 0f), new Vector3(0.5f, 0.06f, 0.5f), hoard));
            assets.PatrolLine = line;
            EditorUtility.SetDirty(assets);
            AssetDatabase.SaveAssets();

            BuildEditorScene();
        }

        private static void BuildEditorScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var assets = AssetDatabase.LoadAssetAtPath<EditorAssets>(DataDir + "/EditorAssets.asset");
            var panel = AssetDatabase.LoadAssetAtPath<PanelSettings>(UiDir + "/RaidPanelSettings.asset");

            // Building wants to see the whole vault: brighter ambient and a soft key light.
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.32f, 0.30f, 0.36f);
            RenderSettings.ambientEquatorColor = new Color(0.22f, 0.20f, 0.24f);
            RenderSettings.ambientGroundColor = new Color(0.10f, 0.10f, 0.12f);
            RenderSettings.skybox = null;
            RenderSettings.fog = false;

            var lightGo = new GameObject("Key Light");
            Light key = lightGo.AddComponent<Light>();
            key.type = LightType.Directional;
            key.intensity = 0.8f;
            key.color = new Color(1f, 0.95f, 0.88f);
            key.shadows = LightShadows.None;
            lightGo.transform.rotation = Quaternion.Euler(55f, -30f, 0f);

            var cameraGo = new GameObject("Editor Camera") { tag = "MainCamera" };
            Camera camera = cameraGo.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.05f, 0.05f, 0.07f);
            camera.fieldOfView = 40f;
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = 80f;
            EditorCamera editorCamera = cameraGo.AddComponent<EditorCamera>();
            Set(editorCamera, "_tuning", assets.Tuning);

            var viewGo = new GameObject("Vault View");
            EditorView view = viewGo.AddComponent<EditorView>();
            Set(view, "_assets", assets);

            var root = new GameObject("Vault Editor");
            EditorInput input = root.AddComponent<EditorInput>();
            Set(input, "_tuning", assets.Tuning);
            VaultEditorController controller = root.AddComponent<VaultEditorController>();

            var uiGo = new GameObject("UI");
            UIDocument doc = uiGo.AddComponent<UIDocument>();
            doc.panelSettings = panel;
            doc.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(EditorUiDir + "/EditorUI.uxml");
            EditorUI ui = uiGo.AddComponent<EditorUI>();
            Set(ui, "_controller", controller);
            Set(ui, "_input", input);

            var eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<InputSystemUIInputModule>();

            Set(controller, "_assets", assets);
            Set(controller, "_view", view);
            Set(controller, "_camera", editorCamera);
            Set(controller, "_input", input);
            Set(controller, "_ui", ui);
            var so = new SerializedObject(controller);
            SerializedProperty hide = so.FindProperty("_hideDuringTest");
            GameObject[] hidden = { cameraGo, lightGo, viewGo, uiGo, eventSystem };
            hide.arraySize = hidden.Length;
            for (int i = 0; i < hidden.Length; i++)
            {
                hide.GetArrayElementAtIndex(i).objectReferenceValue = hidden[i];
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.SaveScene(scene, EditorScenePath);
        }

        private static void RemoveTemplateLeftovers()
        {
            // The URP template registered its sample actions as project-wide input actions.
            EditorBuildSettings.RemoveConfigObject("com.unity.input.settings.actions");
            AssetDatabase.DeleteAsset("Assets/InputSystem_Actions.inputactions");
            AssetDatabase.DeleteAsset("Assets/Scenes/SampleScene.unity");
            if (AssetDatabase.IsValidFolder("Assets/Scenes") && AssetDatabase.FindAssets("", new[] { "Assets/Scenes" }).Length == 0)
            {
                AssetDatabase.DeleteAsset("Assets/Scenes");
            }
        }

        private static void ApplyProjectSettings()
        {
            PlayerSettings.companyName = "Uprise Studios";
            PlayerSettings.productName = "Wax Bandits";
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.uprisestudios.wick");
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        }

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<T>(), path);
                AssetDatabase.SaveAssets();
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                asset = AssetDatabase.LoadAssetAtPath<T>(path);
            }

            return asset;
        }
    }
}
