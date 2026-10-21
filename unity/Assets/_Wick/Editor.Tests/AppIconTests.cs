using NUnit.Framework;
using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEngine;

namespace Wick.Editor.Tests
{
    /// <summary>Every Android adaptive icon slot carries the Art/Icons layers.</summary>
    public sealed class AppIconTests
    {
        [Test]
        public void EveryAdaptiveSlotHasTheBackgroundAndForegroundLayers()
        {
            var background = AssetDatabase.LoadAssetAtPath<Texture2D>(AppIcons.Background);
            var foreground = AssetDatabase.LoadAssetAtPath<Texture2D>(AppIcons.Foreground);
            PlatformIcon[] icons = PlayerSettings.GetPlatformIcons(NamedBuildTarget.Android, AndroidPlatformIconKind.Adaptive);
            Assert.IsNotEmpty(icons);
            foreach (PlatformIcon icon in icons)
            {
                Texture2D[] layers = icon.GetTextures();
                Assert.AreEqual(background, layers[0], "run Wick > Art > Apply App Icons");
                Assert.AreEqual(foreground, layers[1], "run Wick > Art > Apply App Icons");
            }
        }

        [Test]
        public void AdaptiveLayersAreFullSize()
        {
            foreach (string path in new[] { AppIcons.Background, AppIcons.Foreground })
            {
                var t = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                Assert.AreEqual(432, t.width, path);
                Assert.AreEqual(432, t.height, path);
            }
        }
    }
}
