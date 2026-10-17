using System;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Wick.Editor
{
    /// <summary>
    /// Development APK for device testing (IL2CPP, ARM64, profiler-connectable).
    /// Menu: Wick > Build Android Dev APK. Batch:
    /// Unity.exe -batchmode -quit -projectPath unity -buildTarget Android -executeMethod Wick.Editor.AndroidBuild.BuildDev
    /// </summary>
    public static class AndroidBuild
    {
        public const string OutputPath = "Builds/Wick-dev.apk";

        [MenuItem("Wick/Build Android Dev APK")]
        public static void BuildDev()
        {
            GoldenSync.Sync(true);
            EditorUserBuildSettings.buildAppBundle = false;

            var options = new BuildPlayerOptions
            {
                scenes = new[] { "Assets/_Wick/Scenes/Raid.unity" },
                locationPathName = OutputPath,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.Development,
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;
            Debug.Log("AndroidBuild: " + summary.result + ", " + summary.totalSize / (1024 * 1024) + " MB, " +
                summary.totalTime.TotalSeconds.ToString("0") + " s -> " + OutputPath);
            if (summary.result != BuildResult.Succeeded)
            {
                throw new Exception("Android build failed: " + summary.result + " (" + summary.totalErrors + " errors)");
            }
        }
    }
}
