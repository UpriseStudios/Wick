using System.Collections;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Wick.Game;

namespace Wick.Editor
{
    /// <summary>
    /// The lighting lab's Editor side (docs/m10-2-lighting.md, M10.2c): the tuning panel's Save writes the
    /// tuned values into the profile asset; Import Tuned Profile brings in a profile tuned on a phone;
    /// Build Lab Scene makes the editor-only LightingLab scene; Capture Sign-off writes the sign-off set,
    /// contact sheet and readability report to docs/art/lighting-signoff/&lt;date&gt;/.
    /// </summary>
    [InitializeOnLoad]
    public static class LightingLabTools
    {
        public const string LabScenePath = "Assets/_Wick/Scenes/LightingLab.unity";
        private const string RaidScenePath = "Assets/_Wick/Scenes/Raid.unity";

        static LightingLabTools()
        {
            LightingTuner.SaveToAsset = (tuned, asset) => CopyInto(JsonUtility.ToJson(tuned), asset);
        }

        /// <summary>
        /// Overwrites a profile asset's values from JSON, keeping its LUT textures (references don't
        /// survive a trip through a phone's JSON).
        /// </summary>
        public static void CopyInto(string json, LightingProfile asset)
        {
            Texture lut = asset.ColorGradeLut, victory = asset.VictoryLut, defeat = asset.DefeatLut;
            Undo.RecordObject(asset, "Tune lighting");
            string keepName = asset.name;
            JsonUtility.FromJsonOverwrite(json, asset);
            asset.name = keepName;
            asset.ColorGradeLut = lut;
            asset.VictoryLut = victory;
            asset.DefeatLut = defeat;
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
        }

        [MenuItem("Wick/Lighting/Import Tuned Profile...")]
        public static void ImportTuned()
        {
            string path = EditorUtility.OpenFilePanel("Tuned lighting profile (from the phone's lighting folder)", "", "json");
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            string name = Path.GetFileNameWithoutExtension(path);
            foreach (string guid in AssetDatabase.FindAssets("t:LightingProfile " + name))
            {
                var asset = AssetDatabase.LoadAssetAtPath<LightingProfile>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset != null && asset.name == name)
                {
                    CopyInto(File.ReadAllText(path), asset);
                    Debug.Log("Imported tuned lighting into " + AssetDatabase.GetAssetPath(asset));
                    return;
                }
            }

            EditorUtility.DisplayDialog("Import Tuned Profile", "No LightingProfile named " + name + " in the project.", "OK");
        }

        /// <summary>A copy of the Raid scene that starts the lab at once. Not added to Build Settings, so it never ships.</summary>
        [MenuItem("Wick/Lighting/Build Lab Scene")]
        public static void BuildLabScene()
        {
            AssetDatabase.DeleteAsset(LabScenePath);
            AssetDatabase.CopyAsset(RaidScenePath, LabScenePath);
            var scene = EditorSceneManager.OpenScene(LabScenePath, OpenSceneMode.Single);
            new GameObject("Lab Start").AddComponent<LabStart>();
            EditorSceneManager.SaveScene(scene);
            Debug.Log("LightingLabTools: " + LabScenePath + " built (open it and press Play).");
        }

        /// <summary>
        /// The sign-off set from the Raid scene in edit mode, plus the vault editor normally and with
        /// "Preview darkness", into docs/art/lighting-signoff/&lt;date&gt;/.
        /// Batch: -executeMethod Wick.Editor.LightingLabTools.CaptureSignoff
        /// </summary>
        [MenuItem("Wick/Lighting/Capture Sign-off")]
        public static void CaptureSignoff()
        {
            EditorSceneManager.OpenScene(RaidScenePath, OpenSceneMode.Single);
            var runner = Object.FindAnyObjectByType<RaidRunner>();
            var view = Object.FindAnyObjectByType<VaultView>();
            var mask = Object.FindAnyObjectByType<VisibilityMask>();
            var grade = Object.FindAnyObjectByType<Grade>();
            Camera camera = Object.FindAnyObjectByType<RaidCamera>().GetComponent<Camera>();
            var tuning = AssetDatabase.LoadAssetAtPath<RaidTuning>("Assets/_Wick/Data/RaidTuning.asset");
            var lab = new GameObject("Lab (capture)").AddComponent<LightingLab>();
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, "../../docs/art/lighting-signoff"));
            var capture = new SignoffCapture(runner, view, mask, grade, camera, tuning, lab, SignoffCapture.Folder(root));

            // Edit mode: run the capture to the end in one go.
            var stack = new System.Collections.Generic.Stack<IEnumerator>();
            stack.Push(capture.CaptureRaidShots(p => Debug.Log(p)));
            while (stack.Count > 0)
            {
                IEnumerator top = stack.Peek();
                if (!top.MoveNext())
                {
                    stack.Pop();
                }
                else if (top.Current is IEnumerator inner)
                {
                    stack.Push(inner);
                }
            }

            foreach ((string name, Texture2D image) in WickScreenshots.EditorShots(SignoffCapture.Width, SignoffCapture.Height))
            {
                capture.AddShot(name, image);
                Object.DestroyImmediate(image);
            }

            capture.Finish();
            Debug.Log("LightingLabTools: sign-off written to " + SignoffCapture.Folder(root) + " (" + capture.Shots.Count + " shots, " +
                capture.Flags + " flagged)");
        }
    }
}
