using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Wick.Game;

namespace Wick.Editor
{
    /// <summary>
    /// Stand-in prefabs for M10's new defences, made from primitives and the existing placeholder
    /// materials, and registered in the ViewRegistry. Touches nothing else (WickContentBuilder
    /// would also rebuild scenes and materials). Phase 2 art replaces these through the registry.
    /// Menu: Wick > Build M10 Stand-ins. Batch: -executeMethod Wick.Editor.M10StandIns.Build
    /// </summary>
    public static class M10StandIns
    {
        private const string MaterialsDir = "Assets/_Wick/Art/Materials";
        private const string PrefabsDir = "Assets/_Wick/Prefabs";
        private const string RegistryPath = "Assets/_Wick/Data/ViewRegistry.asset";

        [MenuItem("Wick/Build M10 Stand-ins")]
        public static void Build()
        {
            var registry = AssetDatabase.LoadAssetAtPath<ViewRegistry>(RegistryPath);
            if (registry == null)
            {
                Debug.LogError("No ViewRegistry at " + RegistryPath + "; run Wick > Rebuild Placeholder Content first");
                return;
            }

            Material metal = Mat("Metal");
            Material crate = Mat("Crate");

            registry.SwingAxe = Save("SwingAxe", root =>
            {
                Box(root, "Beam", new Vector3(0f, 2.2f, 0f), new Vector3(0.9f, 0.12f, 0.12f), crate);
                var pivot = new GameObject("Pivot").transform;
                pivot.SetParent(root, false);
                pivot.localPosition = new Vector3(0f, 2.15f, 0f);
                Box(pivot, "Chain", new Vector3(0f, -0.75f, 0f), new Vector3(0.06f, 1.5f, 0.06f), metal);
                Box(pivot, "Blade", new Vector3(0f, -1.6f, 0f), new Vector3(0.12f, 0.45f, 0.7f), metal);
                SwingAxeView view = root.gameObject.AddComponent<SwingAxeView>();
                Set(view, "_pivot", pivot);
            });

            Material gold = Mat("Gold");
            Material warning = Mat("Warning");
            registry.MimicChest = Save("MimicChest", root =>
            {
                // Dressed as a treasure pile: a gold heap in an open-topped chest.
                var body = new GameObject("Body").transform;
                body.SetParent(root, false);
                Box(body, "Chest", new Vector3(0f, 0.2f, 0f), new Vector3(0.6f, 0.4f, 0.45f), crate);
                LightingBuilder.AddContactShadow(root, 0.8f);
                Box(body, "Gold", new Vector3(0f, 0.42f, 0f), new Vector3(0.5f, 0.12f, 0.38f), gold);
                Box(body, "Tongue", new Vector3(0f, 0.38f, 0.1f), new Vector3(0.18f, 0.04f, 0.25f), warning);
                var lid = new GameObject("Lid").transform;
                lid.SetParent(root, false);
                lid.localPosition = new Vector3(0f, 0.42f, -0.22f);
                Box(lid, "LidBoard", new Vector3(0f, 0.03f, 0.22f), new Vector3(0.62f, 0.06f, 0.46f), crate);

                // Peeking eyes under the lid, readable in the dark while it winds up to bite.
                var eyes = new GameObject("Eyes").transform;
                eyes.SetParent(root, false);
                foreach (float x in new[] { -0.1f, 0.1f })
                {
                    Prim(PrimitiveType.Sphere, eyes, "Eye", new Vector3(x, 0.44f, 0.2f), Vector3.one * 0.07f, Mat("GuardianEye"));
                }

                eyes.gameObject.AddComponent<ReadableGlow>().Kind = ReadableKind.GuardianEyes;
                MimicView view = root.gameObject.AddComponent<MimicView>();
                Set(view, "_body", body);
                Set(view, "_lid", lid);
            });

            Material owl = Mat("Owl");
            Material eye = Mat("OwlEye");
            registry.SleepyTroll = Save("SleepyTroll", root =>
            {
                var body = new GameObject("Body").transform;
                body.SetParent(root, false);
                Box(body, "Torso", new Vector3(0f, 0.55f, 0f), new Vector3(0.9f, 1.1f, 0.7f), owl);
                LightingBuilder.AddContactShadow(root, 1.05f);
                Box(body, "Nightcap", new Vector3(0f, 1.25f, 0f), new Vector3(0.35f, 0.3f, 0.35f), warning);
                GameObject bubble = Prim(PrimitiveType.Sphere, root, "SnoreBubble", new Vector3(0.45f, 1.2f, 0.2f), Vector3.one * 0.22f, eye);
                TrollView view = root.gameObject.AddComponent<TrollView>();
                Set(view, "_body", body);
                Set(view, "_snore", bubble.transform);
            });

            Material stone = Mat("Plate");
            Material gust = Mat("Gust");
            registry.Fan = Save("Fan", root =>
            {
                Prim(PrimitiveType.Cylinder, root, "Base", new Vector3(0f, 0.1f, 0f), new Vector3(0.6f, 0.1f, 0.6f), stone);
                LightingBuilder.AddContactShadow(root, 0.8f);
                var head = new GameObject("Head").transform;
                head.SetParent(root, false);
                head.localPosition = new Vector3(0f, 0.55f, 0f);
                Box(head, "Housing", new Vector3(0f, 0f, -0.1f), new Vector3(0.35f, 0.35f, 0.25f), crate);
                var blades = new GameObject("Blades").transform;
                blades.SetParent(head, false);
                blades.localPosition = new Vector3(0f, 0f, 0.08f);
                Box(blades, "BladeA", Vector3.zero, new Vector3(0.6f, 0.1f, 0.03f), crate);
                Box(blades, "BladeB", Vector3.zero, new Vector3(0.1f, 0.6f, 0.03f), crate);
                GameObject wind = Box(head, "Wind", new Vector3(0f, -0.25f, 1.6f), new Vector3(0.3f, 0.08f, 3f), gust);
                FanView view = root.gameObject.AddComponent<FanView>();
                Set(view, "_head", head);
                Set(view, "_blades", blades);
                Set(view, "_wind", wind.transform);
            });

            registry.Gate = Save("Gate", root =>
            {
                var bars = new GameObject("Bars").transform;
                bars.SetParent(root, false);
                for (int i = 0; i < 4; i++)
                {
                    Box(bars, "Bar" + i, new Vector3(-0.36f + i * 0.24f, 0.7f, 0f), new Vector3(0.07f, 1.4f, 0.07f), metal);
                }

                Box(bars, "Rail", new Vector3(0f, 1.2f, 0f), new Vector3(0.9f, 0.08f, 0.08f), metal);
                GateView view = root.gameObject.AddComponent<GateView>();
                Set(view, "_bars", bars);
            });

            registry.Lever = Save("Lever", root =>
            {
                Box(root, "Base", new Vector3(0f, 0.05f, 0f), new Vector3(0.4f, 0.1f, 0.3f), stone);
                LightingBuilder.AddContactShadow(root, 0.5f);
                var handle = new GameObject("Handle").transform;
                handle.SetParent(root, false);
                handle.localPosition = new Vector3(0f, 0.1f, 0f);
                Box(handle, "Stick", new Vector3(0f, 0.25f, 0f), new Vector3(0.06f, 0.5f, 0.06f), metal);
                Prim(PrimitiveType.Sphere, handle, "Knob", new Vector3(0f, 0.52f, 0f), Vector3.one * 0.12f, warning);
                LeverView view = root.gameObject.AddComponent<LeverView>();
                Set(view, "_handle", handle);
            });

            registry.SirWhiskers = Save("SirWhiskers", root =>
            {
                // His cushion is a separate prop: it stays put when he gets up.
                Box(root, "Cushion", new Vector3(0f, 0.06f, 0f), new Vector3(0.8f, 0.12f, 0.8f), warning);
                var body = new GameObject("Body").transform;
                body.SetParent(root, false);
                Prim(PrimitiveType.Sphere, body, "Belly", new Vector3(0f, 0.4f, 0f), new Vector3(0.75f, 0.55f, 0.85f), gold);
                LightingBuilder.AddContactShadow(body, 0.95f); // moves with him off the cushion
                Prim(PrimitiveType.Sphere, body, "Head", new Vector3(0f, 0.75f, 0.35f), Vector3.one * 0.42f, gold);
                Box(body, "Tail", new Vector3(0f, 0.45f, -0.5f), new Vector3(0.1f, 0.1f, 0.45f), gold);

                // His eyes, readable in the dark whenever he's awake.
                var eyes = new GameObject("Eyes").transform;
                eyes.SetParent(body, false);
                foreach (float x in new[] { -0.09f, 0.09f })
                {
                    Prim(PrimitiveType.Sphere, eyes, "Eye", new Vector3(x, 0.8f, 0.55f), Vector3.one * 0.07f, Mat("GuardianEye"));
                }

                eyes.gameObject.AddComponent<ReadableGlow>().Kind = ReadableKind.GuardianEyes;
                WhiskersView view = root.gameObject.AddComponent<WhiskersView>();
                Set(view, "_body", body);
            });

            EditorUtility.SetDirty(registry);
            AssetDatabase.SaveAssets();
            Debug.Log("M10 stand-ins built");
        }

        private static Material Mat(string name)
        {
            return AssetDatabase.LoadAssetAtPath<Material>(MaterialsDir + "/" + name + ".mat");
        }

        private static GameObject Save(string name, System.Action<Transform> build)
        {
            var go = new GameObject(name);
            build(go.transform);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, PrefabsDir + "/" + name + ".prefab");
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
            so.FindProperty(field).objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
