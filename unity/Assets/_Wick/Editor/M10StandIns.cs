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
                Box(body, "Gold", new Vector3(0f, 0.42f, 0f), new Vector3(0.5f, 0.12f, 0.38f), gold);
                Box(body, "Tongue", new Vector3(0f, 0.38f, 0.1f), new Vector3(0.18f, 0.04f, 0.25f), warning);
                var lid = new GameObject("Lid").transform;
                lid.SetParent(root, false);
                lid.localPosition = new Vector3(0f, 0.42f, -0.22f);
                Box(lid, "LidBoard", new Vector3(0f, 0.03f, 0.22f), new Vector3(0.62f, 0.06f, 0.46f), crate);
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
                Box(body, "Nightcap", new Vector3(0f, 1.25f, 0f), new Vector3(0.35f, 0.3f, 0.35f), warning);
                GameObject bubble = Prim(PrimitiveType.Sphere, root, "SnoreBubble", new Vector3(0.45f, 1.2f, 0.2f), Vector3.one * 0.22f, eye);
                TrollView view = root.gameObject.AddComponent<TrollView>();
                Set(view, "_body", body);
                Set(view, "_snore", bubble.transform);
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
