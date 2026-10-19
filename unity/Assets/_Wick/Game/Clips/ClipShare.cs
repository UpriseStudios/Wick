using Wick.Services;

namespace Wick.Game
{
    /// <summary>The native share sheet for clips and vault links (Native Share plugin).</summary>
    public static class ClipShare
    {
        public static string Invitation(string code)
        {
            return "Can you crack my vault in Wax Bandits? " + VaultLink.Build(code);
        }

        public static void ShareClip(string path, string code)
        {
            var share = new NativeShare().AddFile(path, "video/mp4").SetTitle("Share your Wick clip");
            if (code != null)
            {
                share.SetText(Invitation(code));
            }

            share.Share();
            Telemetry.VaultShared("clip", code);
        }

        public static void ShareLink(string code)
        {
            new NativeShare().SetText(Invitation(code)).SetTitle("Share your vault").Share();
            Telemetry.VaultShared("link", code);
        }
    }
}
