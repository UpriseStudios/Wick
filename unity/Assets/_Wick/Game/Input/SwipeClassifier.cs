using System.Collections.Generic;
using UnityEngine;
using Wick.Sim;

namespace Wick.Game
{
    public enum GestureAction : byte
    {
        Dash,
        RunStart,
        RunStop,
        ToggleStealth,
    }

    public readonly struct Gesture
    {
        public readonly GestureAction Action;
        public readonly Dir Dir;

        public Gesture(GestureAction action, Dir dir = Dir.N)
        {
            Action = action;
            Dir = dir;
        }

        public override string ToString()
        {
            return Action == GestureAction.Dash || Action == GestureAction.RunStart ? Action + "(" + Dir + ")" : Action.ToString();
        }
    }

    /// <summary>
    /// One-thumb gestures from a single touch, in screen pixels and seconds:
    /// a drag past SwipeDp is a Dash (sent at once); holding still after it for HoldSeconds is
    /// RunStart, and lifting the finger is RunStop; while running, a new drag past SwipeDp
    /// steers the run; a short touch that barely moved is ToggleStealth. Swipes work the same
    /// whether or not Wick is in stealth. Plain C#, no Unity input types, so it is unit-tested.
    /// </summary>
    public sealed class SwipeClassifier
    {
        private readonly InputTuning _tuning;

        private bool _down;
        private Vector2 _start;
        private double _startTime;
        private bool _swiped;
        private bool _running;
        private Dir _dir;
        private Vector2 _anchor;
        private double _anchorTime;

        public SwipeClassifier(InputTuning tuning)
        {
            _tuning = tuning;
        }

        /// <summary>Screen density used to convert pixels to dp.</summary>
        public float Dpi { get; set; } = 160f;

        /// <summary>Camera yaw in degrees: which world direction "up the screen" means.</summary>
        public float CameraYaw { get; set; }

        /// <summary>When false, taps do not toggle stealth (fallback stealth-button scheme).</summary>
        public bool TapTogglesStealth { get; set; } = true;

        public bool IsTouching => _down;

        public void Begin(Vector2 position, double time)
        {
            _down = true;
            _start = position;
            _startTime = time;
            _swiped = false;
            _running = false;
            _anchor = position;
            _anchorTime = time;
        }

        public void Move(Vector2 position, double time, List<Gesture> output)
        {
            if (!_down)
            {
                return;
            }

            if (!_swiped)
            {
                Vector2 delta = position - _start;
                if (ToDp(delta.magnitude) >= _tuning.SwipeDp)
                {
                    _swiped = true;
                    _dir = ToDir(delta);
                    output.Add(new Gesture(GestureAction.Dash, _dir));
                    _anchor = position;
                    _anchorTime = time;
                }

                return;
            }

            Vector2 fromAnchor = position - _anchor;
            if (_running && ToDp(fromAnchor.magnitude) >= _tuning.SwipeDp)
            {
                Dir steer = ToDir(fromAnchor);
                _anchor = position;
                if (steer != _dir)
                {
                    _dir = steer;
                    output.Add(new Gesture(GestureAction.RunStart, _dir));
                }
            }
            else if (!_running && ToDp(fromAnchor.magnitude) > _tuning.HoldStillDp)
            {
                // Still moving at the end of the swipe: restart the hold timer.
                _anchor = position;
                _anchorTime = time;
            }
        }

        /// <summary>Call every frame while touching: detects holding still at the end of a swipe.</summary>
        public void Tick(double time, List<Gesture> output)
        {
            if (_down && _swiped && !_running && time - _anchorTime >= _tuning.HoldSeconds)
            {
                _running = true;
                output.Add(new Gesture(GestureAction.RunStart, _dir));
            }
        }

        public void End(Vector2 position, double time, List<Gesture> output)
        {
            if (!_down)
            {
                return;
            }

            _down = false;
            if (_running)
            {
                output.Add(new Gesture(GestureAction.RunStop));
            }
            else if (!_swiped && TapTogglesStealth && time - _startTime <= _tuning.TapMaxSeconds &&
                     ToDp((position - _start).magnitude) <= _tuning.TapMaxDp)
            {
                output.Add(new Gesture(GestureAction.ToggleStealth));
            }

            _running = false;
            _swiped = false;
        }

        /// <summary>The touch was taken away (e.g. app paused): stop any run, no tap.</summary>
        public void Cancel(List<Gesture> output)
        {
            if (_down && _running)
            {
                output.Add(new Gesture(GestureAction.RunStop));
            }

            _down = false;
            _running = false;
            _swiped = false;
        }

        private float ToDp(float pixels)
        {
            float dpi = Dpi > 0f ? Dpi : _tuning.FallbackDpi;
            return pixels * 160f / dpi;
        }

        /// <summary>Screen delta (y up) to a sim direction, through the camera yaw.</summary>
        private Dir ToDir(Vector2 screen)
        {
            float yaw = CameraYaw * Mathf.Deg2Rad;
            float cos = Mathf.Cos(yaw);
            float sin = Mathf.Sin(yaw);
            float x = screen.x * cos + screen.y * sin;   // world +x = east
            float z = -screen.x * sin + screen.y * cos;  // world +z = north
            if (Mathf.Abs(x) > Mathf.Abs(z))
            {
                return x > 0f ? Dir.E : Dir.W;
            }

            return z > 0f ? Dir.N : Dir.S;
        }
    }
}
