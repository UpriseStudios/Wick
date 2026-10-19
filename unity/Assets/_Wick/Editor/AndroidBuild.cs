using System;
using System.Linq;
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

        /// <summary>The install-size budget (tech design, Testing and performance budgets).</summary>
        public const long InstallBudgetMb = 150;

        [MenuItem("Wick/Build Android Dev APK")]
        public static void BuildDev()
        {
            GoldenSync.Sync(true);
            EditorUserBuildSettings.buildAppBundle = false;
            AddressablesSetup.UseProfile(AddressablesSetup.Development); // remote groups ride along in dev APKs

            var options = new BuildPlayerOptions
            {
                // Every enabled scene in Build Settings; Raid and Editor load each other by name.
                scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
                locationPathName = OutputPath,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.Development,
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;
            if (summary.result != BuildResult.Succeeded)
            {
                throw new Exception("Android build failed: " + summary.result + " (" + summary.totalErrors + " errors)");
            }

            // The APK itself, not summary.totalSize (Unity's uncompressed build data, several times larger).
            long apkMb = new System.IO.FileInfo(OutputPath).Length / (1024 * 1024);
            Debug.Log("AndroidBuild: " + summary.result + ", APK " + apkMb + " MB of the " + InstallBudgetMb + " MB budget, " +
                summary.totalTime.TotalSeconds.ToString("0") + " s -> " + OutputPath);
            if (apkMb > InstallBudgetMb)
            {
                Debug.LogWarning("AndroidBuild: the APK is over the install-size budget (" + apkMb + " MB > " + InstallBudgetMb + " MB)");
            }
        }
    }
}
