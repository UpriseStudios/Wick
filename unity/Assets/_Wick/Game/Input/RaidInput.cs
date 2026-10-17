using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using Wick.Sim;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;

namespace Wick.Game
{
    /// <summary>
    /// Reads one thumb through the Input System (the mouse stands in for it in the Editor),
    /// classifies gestures and queues sim commands on the RaidRunner for the next tick.
    /// Touches that start on UI (decoy and stealth buttons) are left to the UI.
    /// </summary>
    public sealed class RaidInput : MonoBehaviour
    {
        [SerializeField] private RaidRunner _runner;
        [SerializeField] private InputTuning _tuning;
        [SerializeField] private RaidTuning _raidTuning;

        private readonly List<Gesture> _gestures = new List<Gesture>(4);
        private SwipeClassifier _classifier;
        private int _fingerId = -1;
        private bool _mouseDown;

        /// <summary>Returns true when a screen point (pixels, y up) is over UI that handles it.</summary>
        public Func<Vector2, bool> IsOverUi;

        /// <summary>Stealth was toggled by a tap or the fallback button; true = now tiptoeing.</summary>
        public event Action<bool> StealthToggled;

        public InputTuning Tuning => _tuning;

        public void Bind(RaidRunner runner, InputTuning tuning, RaidTuning raidTuning)
        {
            _runner = runner;
            _tuning = tuning;
            _raidTuning = raidTuning;
        }

        private void OnEnable()
        {
            _classifier = new SwipeClassifier(_tuning);
            EnhancedTouchSupport.Enable();
        }

        private void OnDisable()
        {
            _classifier.Cancel(_gestures);
            Dispatch();
            EnhancedTouchSupport.Disable();
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused && _classifier != null)
            {
                _classifier.Cancel(_gestures);
                Dispatch();
                _fingerId = -1;
                _mouseDown = false;
            }
        }

        private void Update()
        {
            _classifier.Dpi = Screen.dpi;
            _classifier.CameraYaw = _raidTuning != null ? _raidTuning.CameraYaw : 0f;
            _classifier.TapTogglesStealth = !_tuning.UseStealthButton;
            double now = Time.unscaledTimeAsDouble;

            if (Touchscreen.current != null && Touch.activeTouches.Count > 0)
            {
                ReadTouches(now);
            }
            else if (Mouse.current != null)
            {
                ReadMouse(now);
            }

            _classifier.Tick(now, _gestures);
            Dispatch();
        }

        /// <summary>The fallback stealth button calls this.</summary>
        public void ToggleStealth()
        {
            if (_runner.Sim == null)
            {
                return;
            }

            bool on = !_runner.Sim.State.Wick.Tiptoe;
            if (_runner.Enqueue(on ? CommandType.TiptoeOn : CommandType.TiptoeOff))
            {
                StealthToggled?.Invoke(on);
            }
        }

        public void DropDecoy()
        {
            _runner.Enqueue(CommandType.Decoy);
        }

        private void ReadTouches(double now)
        {
            foreach (Touch touch in Touch.activeTouches)
            {
                int id = touch.finger.index;
                switch (touch.phase)
                {
                    case UnityEngine.InputSystem.TouchPhase.Began:
                        if (_fingerId < 0 && !OverUi(touch.screenPosition))
                        {
                            _fingerId = id;
                            _classifier.Begin(touch.screenPosition, now);
                        }

                        break;

                    case UnityEngine.InputSystem.TouchPhase.Moved:
                    case UnityEngine.InputSystem.TouchPhase.Stationary:
                        if (id == _fingerId)
                        {
                            _classifier.Move(touch.screenPosition, now, _gestures);
                        }

                        break;

                    case UnityEngine.InputSystem.TouchPhase.Ended:
                        if (id == _fingerId)
                        {
                            _classifier.End(touch.screenPosition, now, _gestures);
                            _fingerId = -1;
                        }

                        break;

                    case UnityEngine.InputSystem.TouchPhase.Canceled:
                        if (id == _fingerId)
                        {
                            _classifier.Cancel(_gestures);
                            _fingerId = -1;
                        }

                        break;
                }
            }
        }

        private void ReadMouse(double now)
        {
            Mouse mouse = Mouse.current;
            Vector2 pos = mouse.position.ReadValue();
            if (mouse.leftButton.wasPressedThisFrame && !OverUi(pos))
            {
                _mouseDown = true;
                _classifier.Begin(pos, now);
            }
            else if (_mouseDown && mouse.leftButton.isPressed)
            {
                _classifier.Move(pos, now, _gestures);
            }
            else if (_mouseDown && mouse.leftButton.wasReleasedThisFrame)
            {
                _mouseDown = false;
                _classifier.End(pos, now, _gestures);
            }
        }

        private bool OverUi(Vector2 screenPosition)
        {
            return IsOverUi != null && IsOverUi(screenPosition);
        }

        private void Dispatch()
        {
            foreach (Gesture g in _gestures)
            {
                switch (g.Action)
                {
                    case GestureAction.Dash:
                        _runner.Enqueue(CommandType.Dash, g.Dir);
                        break;
                    case GestureAction.RunStart:
                        _runner.Enqueue(CommandType.RunStart, g.Dir);
                        break;
                    case GestureAction.RunStop:
                        _runner.Enqueue(CommandType.RunStop);
                        break;
                    case GestureAction.ToggleStealth:
                        ToggleStealth();
                        break;
                }
            }

            _gestures.Clear();
        }
    }
}
