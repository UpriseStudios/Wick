using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Wick.Editor
{
    /// <summary>
    /// Copies tests/golden/*.json into Resources/DevGoldens so the dev menu can replay them on a
    /// device. tests/golden stays the source of truth; the copy is gitignored and refreshed when
    /// the Editor loads and before every build.
    /// </summary>
    [InitializeOnLoad]
    public static class GoldenSync
    {
        public const string TargetFolder = "Assets/_Wick/Resources/DevGoldens";

        static GoldenSync()
        {
            EditorApplication.delayCall += () => Sync(false);
        }

        [MenuItem("Wick/Sync Golden Replays")]
        public static void SyncFromMenu()
        {
            Sync(true);
        }

        public static void Sync(bool log)
        {
            string source = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "tests", "golden"));
            if (!Directory.Exists(source))
            {
                Debug.LogWarning("GoldenSync: no golden folder at " + source);
                return;
            }

            string target = Path.GetFullPath(Path.Combine(Application.dataPath, "..", TargetFolder));
            Directory.CreateDirectory(target);

            bool changed = false;
            foreach (string file in Directory.GetFiles(source, "*.json"))
            {
                string dest = Path.Combine(target, Path.GetFileName(file));
                string text = File.ReadAllText(file);
                if (!File.Exists(dest) || File.ReadAllText(dest) != text)
                {
                    File.WriteAllText(dest, text);
                    changed = true;
                }
            }

            foreach (string dest in Directory.GetFiles(target, "*.json"))
            {
                if (!File.Exists(Path.Combine(source, Path.GetFileName(dest))))
                {
                    File.Delete(dest);
                    File.Delete(dest + ".meta");
                    changed = true;
                }
            }

            if (changed)
            {
                AssetDatabase.Refresh();
            }

            if (log)
            {
                Debug.Log("GoldenSync: " + (changed ? "updated " : "already up to date in ") + TargetFolder);
            }
        }

        private sealed class BeforeBuild : IPreprocessBuildWithReport
        {
            public int callbackOrder => 0;

            public void OnPreprocessBuild(BuildReport report)
            {
                Sync(true);
            }
        }
    }
}
