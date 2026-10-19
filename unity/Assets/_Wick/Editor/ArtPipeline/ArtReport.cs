using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Wick.Editor.ArtPipeline
{
    /// <summary>
    /// Checks everything in Art/Incoming at once: the menu window for people, a batch method for
    /// CI. Also writes Logs/art-import-report.md.
    /// Batch: Unity.exe -batchmode -quit -projectPath unity -executeMethod Wick.Editor.ArtPipeline.ArtReport.CheckBatch
    /// </summary>
    public sealed class ArtReport : EditorWindow
    {
        public const string ReportPath = "Logs/art-import-report.md";

        private List<Violation> _violations = new List<Violation>();
        private int _checked;
        private Vector2 _scroll;

        [MenuItem("Wick/Art/Check Incoming Assets")]
        public static void Open()
        {
            var window = GetWindow<ArtReport>("Art Import Report");
            window.Run();
        }

        /// <summary>Exits 1 if any Incoming asset breaks the handoff specs.</summary>
        public static void CheckBatch()
        {
            List<Violation> violations = CheckAll(out int count);
            Debug.Log("Art check: " + count + " files, " + violations.Count + " violations. Report: " + ReportPath);
            foreach (Violation v in violations)
            {
                Debug.LogWarning("[Art] " + v);
            }

            EditorApplication.Exit(violations.Count > 0 ? 1 : 0);
        }

        public static List<Violation> CheckAll(out int fileCount)
        {
            var violations = new List<Violation>();
            fileCount = 0;
            if (!AssetDatabase.IsValidFolder(ArtBudgets.IncomingRoot))
            {
                Write(violations, 0);
                return violations;
            }

            foreach (string guid in AssetDatabase.FindAssets("", new[] { ArtBudgets.IncomingRoot }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetDatabase.IsValidFolder(path) || !ArtRules.TryParsePath(path, out string category, out string assetId, out string stem, violations))
                {
                    continue;
                }

                fileCount++;
                AssetImporter importer = AssetImporter.GetAtPath(path);
                switch (importer)
                {
                    case ModelImporter model:
                        violations.AddRange(ArtRules.CheckModel(category, assetId, stem,
                            ArtFacts.Model(path, AssetDatabase.LoadAssetAtPath<GameObject>(path), model)));
                        break;
                    case TextureImporter texture:
                        violations.AddRange(ArtRules.CheckTexture(category, assetId, stem, ArtFacts.Texture(path, texture)));
                        break;
                    case AudioImporter _:
                        var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                        violations.AddRange(ArtRules.CheckAudio(category, assetId, stem, new AssetFacts { Path = path, Channels = clip != null ? clip.channels : 0 }));
                        break;
                    default:
                        violations.Add(new Violation { Path = path, Rule = "File type", Detail = "export models as FBX, textures as PNG and audio as WAV or OGG" });
                        break;
                }
            }

            // Asset Production checklist: generated sources and tool licences are noted in each asset's folder.
            foreach (string category in AssetDatabase.GetSubFolders(ArtBudgets.IncomingRoot))
            {
                foreach (string asset in AssetDatabase.GetSubFolders(category))
                {
                    bool notes = Directory.GetFiles(asset, "*.md").Length + Directory.GetFiles(asset, "*.txt").Length > 0;
                    if (!notes)
                    {
                        violations.Add(new Violation { Path = asset, Rule = "Sources", Detail = "add a SOURCES.md noting how it was made and each tool's licence" });
                    }
                }
            }

            Write(violations, fileCount);
            return violations;
        }

        private static void Write(List<Violation> violations, int fileCount)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# Art import report");
            sb.AppendLine();
            sb.AppendLine(fileCount + " files checked in " + ArtBudgets.IncomingRoot + ", " + violations.Count + " violations.");
            sb.AppendLine();
            foreach (Violation v in violations)
            {
                sb.AppendLine("- `" + v.Path + "` **" + v.Rule + "**: " + v.Detail);
            }

            Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
            File.WriteAllText(ReportPath, sb.ToString());
        }

        private void Run()
        {
            _violations = CheckAll(out _checked);
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField(_checked + " files checked, " + _violations.Count + " violations", EditorStyles.boldLabel);
            if (GUILayout.Button("Check again"))
            {
                Run();
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            foreach (Violation v in _violations)
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button(Path.GetFileName(v.Path), EditorStyles.linkLabel, GUILayout.Width(220)))
                {
                    EditorGUIUtility.PingObject(AssetDatabase.LoadMainAssetAtPath(v.Path));
                }

                EditorGUILayout.LabelField(v.Rule + ": " + v.Detail, EditorStyles.wordWrappedLabel);
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();
        }
    }
}
