using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEngine;

namespace Wick.Editor
{
    /// <summary>
    /// Puts the Android adaptive app icon (background and foreground layers) in Player Settings.
    /// Every device gets the adaptive icon (minSdk 26), so there are no legacy or round icons.
    /// The art lives in Art/Icons (stand-ins from tools/icons/make_app_icon.py until final icon
    /// art replaces the files at the same paths). The Android build runs this first.
    /// </summary>
    public static class AppIcons
    {
        public const string Folder = "Assets/_Wick/Art/Icons/";
        public const string Background = Folder + "app_icon_background.png";
        public const string Foreground = Folder + "app_icon_foreground.png";

        [MenuItem("Wick/Art/Apply App Icons")]
        public static void Apply()
        {
            Texture2D background = Load(Background);
            Texture2D foreground = Load(Foreground);
            PlatformIcon[] icons = PlayerSettings.GetPlatformIcons(NamedBuildTarget.Android, AndroidPlatformIconKind.Adaptive);
            foreach (PlatformIcon icon in icons)
            {
                icon.SetTextures(background, foreground);
            }

            PlayerSettings.SetPlatformIcons(NamedBuildTarget.Android, AndroidPlatformIconKind.Adaptive, icons);
            AssetDatabase.SaveAssets();
            Debug.Log("[Wick] App icons applied");
        }

        private static Texture2D Load(string path)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer == null)
            {
                throw new System.IO.FileNotFoundException("Missing app icon art (run tools/icons/make_app_icon.py)", path);
            }

            if (importer.mipmapEnabled || importer.npotScale != TextureImporterNPOTScale.None ||
                importer.textureCompression != TextureImporterCompression.Uncompressed || !importer.alphaIsTransparency)
            {
                importer.mipmapEnabled = false;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }
}
