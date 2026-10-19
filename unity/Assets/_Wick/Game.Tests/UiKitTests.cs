using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

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
        public void EveryKitSpriteExistsAndIsNineSliced()
        {
            string uss = File.ReadAllText("Assets/_Wick/Game/UI/UiKit.uss");
            MatchCollection urls = Regex.Matches(uss, "guid=([0-9a-f]{32})");
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
