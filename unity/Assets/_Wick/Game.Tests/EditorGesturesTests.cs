using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Wick.Game.Tests
{
    public class EditorGesturesTests
    {
        private EditorTuning _tuning;
        private EditorGestures _g;
        private List<EditorGesture> _out;

        [SetUp]
        public void SetUp()
        {
            _tuning = ScriptableObject.CreateInstance<EditorTuning>(); // 12 dp drag, 0.5 s long-press, 0.35 s tap
            _g = new EditorGestures(_tuning) { Dpi = 160f };
            _out = new List<EditorGesture>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_tuning);
        }

        private void AssertTypes(params EditorGestureType[] expected)
        {
            var actual = new List<EditorGestureType>();
            foreach (EditorGesture g in _out)
            {
                actual.Add(g.Type);
            }

            CollectionAssert.AreEqual(expected, actual);
        }

        [Test]
        public void QuickStillTouch_IsATap()
        {
            _g.FingerDown(1, new Vector2(100, 100), default, 0, _out);
            _g.FingersMoved(1, new Vector2(104, 102), default, 0.1, _out);
            _g.FingerUp(0, new Vector2(104, 102), 0.15, _out);
            AssertTypes(EditorGestureType.Tap);
            Assert.AreEqual(new Vector2(100, 100), _out[0].Position);
        }

        [Test]
        public void HoldingStill_IsALongPress_AndNotATap()
        {
            _g.FingerDown(1, Vector2.zero, default, 0, _out);
            _g.FingersMoved(1, Vector2.zero, default, 0.49, _out);
            Assert.IsEmpty(_out);
            _g.FingersMoved(1, Vector2.zero, default, 0.51, _out);
            _g.FingersMoved(1, Vector2.zero, default, 0.8, _out);
            _g.FingerUp(0, Vector2.zero, 0.9, _out);
            AssertTypes(EditorGestureType.LongPress);
        }

        [Test]
        public void Moving_IsADragWithMovesAndAnEnd()
        {
            _g.FingerDown(1, Vector2.zero, default, 0, _out);
            _g.FingersMoved(1, new Vector2(5, 0), default, 0.05, _out);
            _g.FingersMoved(1, new Vector2(20, 0), default, 0.1, _out);
            _g.FingersMoved(1, new Vector2(40, 0), default, 0.15, _out);
            _g.FingerUp(0, new Vector2(40, 0), 0.2, _out);
            AssertTypes(EditorGestureType.DragStart, EditorGestureType.DragMove, EditorGestureType.DragMove,
                EditorGestureType.DragEnd);
            Assert.AreEqual(Vector2.zero, _out[0].Position, "drag starts where the finger went down");
        }

        [Test]
        public void SecondFinger_CancelsTheDrag_AndPinches()
        {
            _g.FingerDown(1, Vector2.zero, default, 0, _out);
            _g.FingersMoved(1, new Vector2(30, 0), default, 0.1, _out);
            _g.FingerDown(2, new Vector2(30, 0), new Vector2(130, 0), 0.2, _out);
            _g.FingersMoved(2, new Vector2(10, 0), new Vector2(210, 0), 0.3, _out);
            _g.FingerUp(1, new Vector2(10, 0), 0.4, _out);
            _g.FingerUp(0, new Vector2(10, 0), 0.5, _out);
            AssertTypes(EditorGestureType.DragStart, EditorGestureType.DragMove, EditorGestureType.DragCancel,
                EditorGestureType.PinchPan);
            Assert.AreEqual(2f, _out[3].Scale, 1e-4f, "finger gap 100 -> 200");
            Assert.AreEqual(new Vector2(30, 0), _out[3].Delta, "centroid 80 -> 110");
        }

        [Test]
        public void LiftingOneOfTwoFingers_DoesNotTap()
        {
            _g.FingerDown(1, Vector2.zero, default, 0, _out);
            _g.FingerDown(2, Vector2.zero, new Vector2(100, 0), 0.05, _out);
            _g.FingerUp(1, Vector2.zero, 0.1, _out);
            _g.FingerUp(0, Vector2.zero, 0.15, _out);
            Assert.IsEmpty(_out);
        }

        [Test]
        public void DragThreshold_IsInDp()
        {
            _g.Dpi = 480f; // 3 px per dp: 12 dp = 36 px
            _g.FingerDown(1, Vector2.zero, default, 0, _out);
            _g.FingersMoved(1, new Vector2(30, 0), default, 0.05, _out);
            Assert.IsEmpty(_out);
            _g.FingersMoved(1, new Vector2(40, 0), default, 0.06, _out);
            AssertTypes(EditorGestureType.DragStart, EditorGestureType.DragMove);
        }
    }
}
