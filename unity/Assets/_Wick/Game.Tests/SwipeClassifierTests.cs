using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Wick.Sim;

namespace Wick.Game.Tests
{
    public class SwipeClassifierTests
    {
        private InputTuning _tuning;
        private SwipeClassifier _swipe;
        private List<Gesture> _out;

        [SetUp]
        public void SetUp()
        {
            _tuning = ScriptableObject.CreateInstance<InputTuning>(); // 40 dp, 0.15 s hold, 0.25 s / 10 dp tap
            _swipe = new SwipeClassifier(_tuning) { Dpi = 160f }; // 1 px = 1 dp
            _out = new List<Gesture>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_tuning);
        }

        [Test]
        public void Dash_FiresAsSoonAsTheSwipePasses40dp()
        {
            _swipe.Begin(new Vector2(100, 100), 0);
            _swipe.Move(new Vector2(100, 139), 0.02, _out);
            Assert.IsEmpty(_out, "39 dp");
            _swipe.Move(new Vector2(100, 141), 0.03, _out);
            AssertGestures("Dash(N)");
        }

        [TestCase(60, 0, "Dash(E)")]
        [TestCase(-60, 10, "Dash(W)")]
        [TestCase(5, -60, "Dash(S)")]
        [TestCase(-20, 50, "Dash(N)")]
        public void Direction_IsTheDominantScreenAxis(float dx, float dy, string expected)
        {
            _swipe.Begin(Vector2.zero, 0);
            _swipe.Move(new Vector2(dx, dy), 0.05, _out);
            AssertGestures(expected);
        }

        [Test]
        public void Threshold_IsInDp_NotPixels()
        {
            _swipe.Dpi = 480f; // 3 px per dp: 40 dp = 120 px
            _swipe.Begin(Vector2.zero, 0);
            _swipe.Move(new Vector2(0, 110), 0.05, _out);
            Assert.IsEmpty(_out);
            _swipe.Move(new Vector2(0, 121), 0.06, _out);
            AssertGestures("Dash(N)");
        }

        [Test]
        public void QuickSwipe_IsOnlyADash()
        {
            _swipe.Begin(Vector2.zero, 0);
            _swipe.Move(new Vector2(0, 60), 0.05, _out);
            _swipe.Tick(0.1, _out);
            _swipe.End(new Vector2(0, 70), 0.12, _out);
            AssertGestures("Dash(N)");
        }

        [Test]
        public void SwipeThenHold_Runs_AndReleaseStops()
        {
            _swipe.Begin(Vector2.zero, 0);
            _swipe.Move(new Vector2(60, 0), 0.05, _out);
            _swipe.Tick(0.19, _out);
            Assert.AreEqual(1, _out.Count, "not held long enough yet");
            _swipe.Move(new Vector2(65, 2), 0.15, _out); // within 10 dp: still holding
            _swipe.Tick(0.21, _out);
            AssertGestures("Dash(E)", "RunStart(E)");
            _swipe.End(new Vector2(65, 2), 1.0, _out);
            AssertGestures("Dash(E)", "RunStart(E)", "RunStop");
        }

        [Test]
        public void StillMovingAtTheEnd_RestartsTheHoldTimer()
        {
            _swipe.Begin(Vector2.zero, 0);
            _swipe.Move(new Vector2(60, 0), 0.05, _out);
            _swipe.Move(new Vector2(90, 0), 0.15, _out);
            _swipe.Tick(0.25, _out);
            Assert.AreEqual(1, _out.Count, "hold counts from 0.15");
            _swipe.Tick(0.31, _out);
            AssertGestures("Dash(E)", "RunStart(E)");
        }

        [Test]
        public void DraggingWhileRunning_Steers()
        {
            _swipe.Begin(Vector2.zero, 0);
            _swipe.Move(new Vector2(60, 0), 0.05, _out);
            _swipe.Tick(0.3, _out);
            _swipe.Move(new Vector2(60, 50), 0.5, _out);
            AssertGestures("Dash(E)", "RunStart(E)", "RunStart(N)");
        }

        [Test]
        public void Tap_TogglesStealth()
        {
            _swipe.Begin(new Vector2(200, 300), 0);
            _swipe.Move(new Vector2(204, 303), 0.05, _out);
            _swipe.End(new Vector2(204, 303), 0.1, _out);
            AssertGestures("ToggleStealth");
        }

        [Test]
        public void LongPressOrDrift_IsNotATap()
        {
            _swipe.Begin(Vector2.zero, 0);
            _swipe.End(Vector2.zero, 0.5, _out);
            _swipe.Begin(Vector2.zero, 1);
            _swipe.End(new Vector2(20, 0), 1.1, _out);
            Assert.IsEmpty(_out);
        }

        [Test]
        public void AfterOneTap_SwipesStillMoveWick()
        {
            _swipe.Begin(Vector2.zero, 0);
            _swipe.End(Vector2.zero, 0.1, _out);
            _swipe.Begin(Vector2.zero, 0.5);
            _swipe.Move(new Vector2(0, -60), 0.55, _out);
            _swipe.End(new Vector2(0, -60), 0.6, _out);
            _swipe.Begin(Vector2.zero, 1);
            _swipe.Move(new Vector2(60, 0), 1.05, _out);
            _swipe.End(new Vector2(60, 0), 1.1, _out);
            AssertGestures("ToggleStealth", "Dash(S)", "Dash(E)");
        }

        [Test]
        public void FallbackScheme_TapsDoNothing()
        {
            _swipe.TapTogglesStealth = false;
            _swipe.Begin(Vector2.zero, 0);
            _swipe.End(Vector2.zero, 0.1, _out);
            Assert.IsEmpty(_out);
        }

        [Test]
        public void CameraYaw_RotatesSwipeDirections()
        {
            _swipe.CameraYaw = 90f;
            _swipe.Begin(Vector2.zero, 0);
            _swipe.Move(new Vector2(0, 60), 0.05, _out);
            AssertGestures("Dash(E)");
        }

        [Test]
        public void Cancel_StopsARun_WithoutATap()
        {
            _swipe.Begin(Vector2.zero, 0);
            _swipe.Move(new Vector2(60, 0), 0.05, _out);
            _swipe.Tick(0.3, _out);
            _swipe.Cancel(_out);
            AssertGestures("Dash(E)", "RunStart(E)", "RunStop");
        }

        private void AssertGestures(params string[] expected)
        {
            var actual = new string[_out.Count];
            for (int i = 0; i < _out.Count; i++)
            {
                actual[i] = _out[i].ToString();
            }

            CollectionAssert.AreEqual(expected, actual);
        }
    }
}
