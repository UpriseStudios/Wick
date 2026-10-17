using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;

namespace Wick.Game
{
    /// <summary>
    /// Reads touches (or the mouse in the Editor: left button = one finger, wheel = pinch,
    /// right button = pan) into EditorGestures. Touches that start on UI are left to the UI.
    /// </summary>
    public sealed class EditorInput : MonoBehaviour
    {
        [SerializeField] private EditorTuning _tuning;

        private readonly List<EditorGesture> _gestures = new List<EditorGesture>(8);
        private readonly List<int> _ignored = new List<int>(4);
        private readonly List<Vector2> _points = new List<Vector2>(4);
        private EditorGestures _classifier;
        private int _count;
        private bool _mouseDown;
        private Vector2 _lastRight;
        private Vector2 _lastA;

        public Func<Vector2, bool> IsOverUi;
        public event Action<EditorGesture> Gesture;

        public void Bind(EditorTuning tuning)
        {
            _tuning = tuning;
        }

        private void OnEnable()
        {
            _classifier = new EditorGestures(_tuning);
            EnhancedTouchSupport.Enable();
            _count = 0;
            _ignored.Clear();
        }

        private void OnDisable()
        {
            EnhancedTouchSupport.Disable();
            _mouseDown = false;
        }

        private void Update()
        {
            _classifier.Dpi = Screen.dpi;
            double now = Time.unscaledTimeAsDouble;

            if (Touchscreen.current != null && (Touch.activeTouches.Count > 0 || _count > 0))
            {
                ReadTouches(now);
            }
            else if (Mouse.current != null)
            {
                ReadMouse(now);
            }

            foreach (EditorGesture g in _gestures)
            {
                Gesture?.Invoke(g);
            }

            _gestures.Clear();
        }

        private void ReadTouches(double now)
        {
            _points.Clear();
            foreach (Touch t in Touch.activeTouches)
            {
                int id = t.finger.index;
                if (t.phase == UnityEngine.InputSystem.TouchPhase.Began && IsOverUi != null && IsOverUi(t.screenPosition))
                {
                    if (!_ignored.Contains(id))
                    {
                        _ignored.Add(id);
                    }
                }

                bool ended = t.phase == UnityEngine.InputSystem.TouchPhase.Ended || t.phase == UnityEngine.InputSystem.TouchPhase.Canceled;
                if (ended)
                {
                    _ignored.Remove(id);
                }
                else if (!_ignored.Contains(id))
                {
                    _points.Add(t.screenPosition);
                }
            }

            int count = Mathf.Min(_points.Count, 2);
            Vector2 a = count > 0 ? _points[0] : _lastA; // a lifted finger ends where it was last seen
            Vector2 b = count > 1 ? _points[1] : a;

            if (count > _count)
            {
                _classifier.FingerDown(count, a, b, now, _gestures);
            }
            else if (count < _count)
            {
                _classifier.FingerUp(count, a, now, _gestures);
            }

            if (count > 0)
            {
                _classifier.FingersMoved(count, a, b, now, _gestures);
            }

            _count = count;
            if (count > 0)
            {
                _lastA = a;
            }
        }

        private void ReadMouse(double now)
        {
            Mouse mouse = Mouse.current;
            Vector2 pos = mouse.position.ReadValue();

            if (mouse.leftButton.wasPressedThisFrame && (IsOverUi == null || !IsOverUi(pos)))
            {
                _mouseDown = true;
                _classifier.FingerDown(1, pos, pos, now, _gestures);
            }

            if (_mouseDown)
            {
                _classifier.FingersMoved(1, pos, pos, now, _gestures);
                if (mouse.leftButton.wasReleasedThisFrame)
                {
                    _mouseDown = false;
                    _classifier.FingerUp(0, pos, now, _gestures);
                }
            }

            float scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.01f)
            {
                _gestures.Add(new EditorGesture(EditorGestureType.PinchPan, pos, Vector2.zero, scroll > 0 ? 1.1f : 1f / 1.1f));
            }

            if (mouse.rightButton.isPressed)
            {
                if (!mouse.rightButton.wasPressedThisFrame)
                {
                    _gestures.Add(new EditorGesture(EditorGestureType.PinchPan, pos, pos - _lastRight, 1f));
                }

                _lastRight = pos;
            }
        }
    }
}
