using UnityEditor;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace Wick.Editor
{
    /// <summary>
    /// Builds the remote content (skins, themes, seasonal) for the CDN with the Release profile,
    /// into ServerData/&lt;platform&gt;/, ready for tools/content/upload_r2.py.
    /// Batch: Unity.exe -batchmode -quit -projectPath unity -buildTarget Android -executeMethod Wick.Editor.ContentBuild.BuildRelease
    /// </summary>
    public static class ContentBuild
    {
        [MenuItem("Wick/Art/Build Release Content (CDN)")]
        public static void BuildRelease()
        {
            AddressablesSetup.UseProfile(AddressablesSetup.Release);
            try
            {
                AddressableAssetSettings.BuildPlayerContent(out var result);
                if (!string.IsNullOrEmpty(result.Error))
                {
                    throw new System.Exception("Content build failed: " + result.Error);
                }

                Debug.Log("ContentBuild: release content in ServerData/" + EditorUserBuildSettings.activeBuildTarget + " (" +
                    result.Duration.ToString("0") + " s). Upload it with tools/content/upload_r2.py.");
            }
            finally
            {
                AddressablesSetup.UseProfile(AddressablesSetup.Development); // dev builds stay self-contained
            }
        }
    }
}
