#if UNITY_IOS
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;

namespace Wick.Editor
{
    /// <summary>
    /// iOS builds: adds the Associated Domains entitlement so vault links
    /// (https://wick.uprisestudios.co.uk/v/CODE) open the app as Universal Links. The domain's
    /// apple-app-site-association file (web/public/.well-known) must list the app's Team ID.
    /// Untested until the iOS build pipeline exists.
    /// </summary>
    public static class IosLinks
    {
        [PostProcessBuild(100)]
        public static void AddAssociatedDomains(BuildTarget target, string path)
        {
            if (target != BuildTarget.iOS)
            {
                return;
            }

            string project = PBXProject.GetPBXProjectPath(path);
            var capabilities = new ProjectCapabilityManager(project, "Wick.entitlements", null, new PBXProject().GetUnityMainTargetGuid());
            capabilities.AddAssociatedDomains(new[] { "applinks:" + Wick.Services.VaultLink.Domain });
            capabilities.WriteToFile();
        }
    }
}
#endif
