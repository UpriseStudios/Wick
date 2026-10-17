using System.Collections.Generic;
using UnityEngine;

namespace Wick.Game
{
    public enum EditorGestureType : byte
    {
        Tap,
        LongPress,
        DragStart,
        DragMove,
        DragEnd,

        /// <summary>A one-finger drag was interrupted (second finger, cancel): undo the stroke's preview.</summary>
        DragCancel,

        /// <summary>Two fingers: Scale is the pinch factor this frame, Delta the centroid move in pixels.</summary>
        PinchPan,
    }

    public readonly struct EditorGesture
    {
        public readonly EditorGestureType Type;
        public readonly Vector2 Position;
        public readonly Vector2 Delta;
        public readonly float Scale;

        public EditorGesture(EditorGestureType type, Vector2 position, Vector2 delta = default, float scale = 1f)
        {
            Type = type;
            Position = position;
            Delta = delta;
            Scale = scale;
        }

        public override string ToString()
        {
            return Type.ToString();
        }
    }

    /// <summary>
    /// Editor gestures from raw touches (screen pixels, seconds): one finger is a tap, a long-press
    /// (held still) or a drag; two fingers pinch-zoom and pan. Plain C#, so it is unit-tested.
    /// </summary>
    public sealed class EditorGestures
    {
        private readonly EditorTuning _tuning;

        private int _fingers;
        private Vector2 _start;
        private Vector2 _last;
        private double _startTime;
        private bool _dragging;
        private bool _longPressed;
        private bool _consumed;

        private Vector2 _a;
        private Vector2 _b;

        public EditorGestures(EditorTuning tuning)
        {
            _tuning = tuning;
        }

        public float Dpi { get; set; } = 160f;

        public void FingerDown(int fingerCount, Vector2 a, Vector2 b, double time, List<EditorGesture> output)
        {
            if (fingerCount == 1 && _fingers == 0)
            {
                _start = a;
                _last = a;
                _startTime = time;
                _dragging = false;
                _longPressed = false;
                _consumed = false;
            }
            else if (fingerCount >= 2)
            {
                if (_dragging)
                {
                    output.Add(new EditorGesture(EditorGestureType.DragCancel, _last));
                }

                _dragging = false;
                _consumed = true; // no tap or long-press once a second finger lands
                _a = a;
                _b = b;
            }

            _fingers = fingerCount;
        }

        /// <summary>Call every frame while fingers are down, with current positions.</summary>
        public void FingersMoved(int fingerCount, Vector2 a, Vector2 b, double time, List<EditorGesture> output)
        {
            if (fingerCount >= 2 && _fingers >= 2)
            {
                float before = Vector2.Distance(_a, _b);
                float after = Vector2.Distance(a, b);
                Vector2 delta = (a + b) * 0.5f - (_a + _b) * 0.5f;
                float scale = before > 1f ? after / before : 1f;
                if (delta.sqrMagnitude > 0f || Mathf.Abs(scale - 1f) > 1e-4f)
                {
                    output.Add(new EditorGesture(EditorGestureType.PinchPan, (a + b) * 0.5f, delta, scale));
                }

                _a = a;
                _b = b;
                return;
            }

            if (fingerCount != 1 || _fingers != 1 || _consumed)
            {
                return;
            }

            if (!_dragging && ToDp(Vector2.Distance(a, _start)) >= _tuning.DragDp)
            {
                _dragging = true;
                output.Add(new EditorGesture(EditorGestureType.DragStart, _start));
            }

            if (_dragging)
            {
                if (a != _last)
                {
                    output.Add(new EditorGesture(EditorGestureType.DragMove, a, a - _last));
                }
            }
            else if (!_longPressed && time - _startTime >= _tuning.LongPressSeconds)
            {
                _longPressed = true;
                output.Add(new EditorGesture(EditorGestureType.LongPress, _start));
            }

            _last = a;
        }

        public void FingerUp(int fingersLeft, Vector2 position, double time, List<EditorGesture> output)
        {
            if (_fingers == 1 && fingersLeft == 0 && !_consumed)
            {
                if (_dragging)
                {
                    output.Add(new EditorGesture(EditorGestureType.DragEnd, position));
                }
                else if (!_longPressed && time - _startTime <= _tuning.TapMaxSeconds)
                {
                    output.Add(new EditorGesture(EditorGestureType.Tap, _start));
                }
            }

            _fingers = fingersLeft;
            if (fingersLeft == 0)
            {
                _dragging = false;
                _consumed = false;
            }
            else
            {
                _consumed = true; // lifting one of two fingers never starts a one-finger gesture
            }
        }

        private float ToDp(float pixels)
        {
            float dpi = Dpi > 0f ? Dpi : _tuning.FallbackDpi;
            return pixels * 160f / dpi;
        }
    }
}
