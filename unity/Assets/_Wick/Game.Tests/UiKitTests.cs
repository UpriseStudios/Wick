using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.Text;

namespace Wick.Game.Tests
{
    public class UiKitTests
    {
        [Test]
        public void SafeAreaInsetsFollowTheNotchAndGestureBar()
        {
            // A 1080 x 2400 phone with a 100 px notch at the top and a 60 px gesture bar at the bottom,
            // shown on a panel half that size.
            var safe = new Rect(0f, 60f, 1080f, 2400f - 100f - 60f);
            Vector4 inset = SafeArea.Insets(safe, new Vector2(1080f, 2400f), new Vector2(540f, 1200f));
            Assert.AreEqual(0f, inset.x, 0.01f, "left");
            Assert.AreEqual(50f, inset.y, 0.01f, "top: the notch");
            Assert.AreEqual(0f, inset.z, 0.01f, "right");
            Assert.AreEqual(30f, inset.w, 0.01f, "bottom: the gesture bar");
        }

        [Test]
        public void TheFontsRenderLatinExtended()
        {
            string uss = File.ReadAllText("Assets/_Wick/Game/UI/UiKit.uss");
            MatchCollection fonts = Regex.Matches(uss, @"-unity-font-definition: url\(""project://database/([^?]+)\?");
            Assert.GreaterOrEqual(fonts.Count, 2, "body and display fonts");
            foreach (Match m in fonts)
            {
                var font = AssetDatabase.LoadAssetAtPath<FontAsset>(m.Groups[1].Value);
                Assert.IsNotNull(font, m.Groups[1].Value);
                // Polish, Czech, Turkish, Hungarian, Nordic, German, Spanish, plus the tagline's en dash.
                bool all = font.HasCharacters("ŁłŚśŻźĆćŘřŠšŽžĞğİıŐőŰűØøÆæßÑñ–€", out uint[] missing, false, true);
                Assert.IsTrue(all, m.Groups[1].Value + " can't draw: " + string.Join(" ", System.Array.ConvertAll(missing ?? new uint[0], c => "U+" + c.ToString("X4"))));
            }
        }

        [Test]
        public void EveryKitSpriteExistsAndIsNineSliced()
        {
            string uss = File.ReadAllText("Assets/_Wick/Game/UI/UiKit.uss");
            MatchCollection urls = Regex.Matches(uss, "guid=([0-9a-f]{32})&type=3"); // sprites; the fonts are type 2
            Assert.Greater(urls.Count, 3, "UiKit.uss styles the buttons, rows and panels");
            foreach (Match m in urls)
            {
                string path = AssetDatabase.GUIDToAssetPath(m.Groups[1].Value);
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                Assert.IsNotNull(sprite, path + " is not a sprite");
                Assert.Greater(sprite.border.x, 0f, path + " has no 9-slice border");
            }
        }
    }
}
