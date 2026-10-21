using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Wick.Editor
{
    /// <summary>
    /// Android builds (IL2CPP, ARM64). Batch:
    /// Unity.exe -batchmode -quit -projectPath unity -buildTarget Android -executeMethod Wick.Editor.AndroidBuild.BuildDev
    ///   BuildDev         development APK for device testing, profiler-connectable, remote content bundled
    ///   BuildReleaseApk  release configuration as an APK, signed with the debug key: test it on a phone and
    ///                    measure its size before an upload
    ///   BuildRelease     the Play Store AAB, signed with the upload key from environment variables
    /// </summary>
    public static class AndroidBuild
    {
        public const string OutputPath = "Builds/Wick-dev.apk";
        public const string ReleaseApkPath = "Builds/Wick-release.apk";
        public const string ReleaseBundlePath = "Builds/Wick-release.aab";

        /// <summary>The install-size budget (tech design, Testing and performance budgets).</summary>
        public const long InstallBudgetMb = 150;

        /// <summary>
        /// The upload key. The keystore file and its passwords stay on the developer's machine, never in the
        /// repo: set these in the environment before running BuildRelease.
        /// </summary>
        public const string KeystorePathVar = "WICK_KEYSTORE_PATH";
        public const string KeystorePassVar = "WICK_KEYSTORE_PASS";
        public const string KeyAliasVar = "WICK_KEY_ALIAS";
        public const string KeyPassVar = "WICK_KEY_PASS";

        [MenuItem("Wick/Build Android Dev APK")]
        public static void BuildDev()
        {
            Build(OutputPath, AddressablesSetup.Development, BuildOptions.Development, false); // remote groups ride along
        }

        [MenuItem("Wick/Build Android Release APK (debug-signed)")]
        public static void BuildReleaseApk()
        {
            Build(ReleaseApkPath, AddressablesSetup.Release, BuildOptions.None, false);
        }

        [MenuItem("Wick/Build Android Release AAB")]
        public static void BuildRelease()
        {
            string keystore = Environment.GetEnvironmentVariable(KeystorePathVar);
            string alias = Environment.GetEnvironmentVariable(KeyAliasVar);
            if (string.IsNullOrEmpty(keystore) || !System.IO.File.Exists(keystore) || string.IsNullOrEmpty(alias))
            {
                throw new Exception("BuildRelease needs " + KeystorePathVar + " (an existing upload keystore), " +
                    KeyAliasVar + ", " + KeystorePassVar + " and " + KeyPassVar + " set in the environment");
            }

            PlayerSettings.Android.useCustomKeystore = true;
            PlayerSettings.Android.keystoreName = keystore;
            PlayerSettings.Android.keystorePass = Environment.GetEnvironmentVariable(KeystorePassVar);
            PlayerSettings.Android.keyaliasName = alias;
            PlayerSettings.Android.keyaliasPass = Environment.GetEnvironmentVariable(KeyPassVar);
            try
            {
                Build(ReleaseBundlePath, AddressablesSetup.Release, BuildOptions.None, true);
            }
            finally
            {
                // Passwords live in memory only; the keystore path never reaches ProjectSettings.asset.
                PlayerSettings.Android.useCustomKeystore = false;
                PlayerSettings.Android.keystoreName = "";
                PlayerSettings.Android.keystorePass = "";
                PlayerSettings.Android.keyaliasName = "";
                PlayerSettings.Android.keyaliasPass = "";
            }
        }

        private static void Build(string path, string addressablesProfile, BuildOptions buildOptions, bool appBundle)
        {
            GoldenSync.Sync(true);
            AppIcons.Apply();
            ApplySizeSettings();
            EditorUserBuildSettings.buildAppBundle = appBundle;
            AddressablesSetup.UseProfile(addressablesProfile);

            var options = new BuildPlayerOptions
            {
                // Every enabled scene in Build Settings; Raid and Editor load each other by name.
                scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
                locationPathName = path,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = buildOptions,
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;
            if (summary.result != BuildResult.Succeeded)
            {
                throw new Exception("Android build failed: " + summary.result + " (" + summary.totalErrors + " errors)");
            }

            // The file itself, not summary.totalSize (Unity's uncompressed build data, several times larger).
            long mb = new System.IO.FileInfo(path).Length / (1024 * 1024);
            Debug.Log("AndroidBuild: " + summary.result + ", " + path + " " + mb + " MB of the " + InstallBudgetMb +
                " MB budget, version " + PlayerSettings.bundleVersion + " (" + PlayerSettings.Android.bundleVersionCode +
                "), " + summary.totalTime.TotalSeconds.ToString("0") + " s");
            if (mb > InstallBudgetMb)
            {
                Debug.LogWarning("AndroidBuild: over the install-size budget (" + mb + " MB > " + InstallBudgetMb + " MB)");
            }
        }

        /// <summary>
        /// Smaller native code: IL2CPP optimised for size, and Low managed stripping (unreachable code removed;
        /// Addressables writes link.xml for the types its content needs). Dev builds use the same settings so
        /// device tests exercise the stripped code.
        /// </summary>
        private static void ApplySizeSettings()
        {
            PlayerSettings.SetIl2CppCodeGeneration(NamedBuildTarget.Android, Il2CppCodeGeneration.OptimizeSize);
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.Android, ManagedStrippingLevel.Low);
        }
    }
}
