using Unity.Profiling;
using UnityEngine;
using Wick.Sim;

namespace Wick.Game
{
    /// <summary>
    /// Measures a raid against the performance budgets (tech design): average and 1%-low frame rate
    /// (60 fps target on mid-range, 30 fps floor on low-end), draw calls (under 150) and the sim's
    /// cost per tick (under 0.2 ms). Shown in the dev menu; a summary is logged at each raid's end,
    /// so replaying a golden raid on a phone is the benchmark.
    /// </summary>
    public sealed class PerfProbe : MonoBehaviour
    {
        public const float TargetFps = 60f;
        public const float FloorFps = 30f;
        public const int MaxDrawCalls = 150;
        public const double MaxStepMs = 0.2;

        [SerializeField] private RaidRunner _runner;

        private const int Window = 600; // ten seconds at 60 fps
        private readonly float[] _frames = new float[Window];
        private readonly float[] _sorted = new float[Window];
        private int _count;
        private int _next;
        private ProfilerRecorder _drawCalls;
        private long _maxDrawCalls;
        private double _maxStepMs;
        private double _stepMsTotal;
        private int _steps;

        public string Summary { get; private set; } = "";

        public void Bind(RaidRunner runner)
        {
            _runner = runner;
        }

        private void OnEnable()
        {
            _drawCalls = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count"); // development builds report it
            _runner.Loaded += Reset;
            _runner.Ticked += OnTick;
            _runner.Ended += OnEnded;
        }

        private void OnDisable()
        {
            _drawCalls.Dispose();
            _runner.Loaded -= Reset;
            _runner.Ticked -= OnTick;
            _runner.Ended -= OnEnded;
        }

        private void Reset(RaidRunner runner)
        {
            _count = _next = _steps = 0;
            _maxDrawCalls = 0;
            _maxStepMs = _stepMsTotal = 0;
        }

        private void OnTick(RaidRunner runner)
        {
            _maxStepMs = System.Math.Max(_maxStepMs, runner.LastStepMs);
            _stepMsTotal += runner.LastStepMs;
            _steps++;
        }

        private void Update()
        {
            if (_runner.Mode != RaidMode.Live && _runner.Mode != RaidMode.Replay)
            {
                return;
            }

            _frames[_next] = Time.unscaledDeltaTime;
            _next = (_next + 1) % Window;
            _count = Mathf.Min(_count + 1, Window);
            if (_drawCalls.Valid)
            {
                _maxDrawCalls = System.Math.Max(_maxDrawCalls, _drawCalls.LastValue);
            }

            Summary = Describe();
        }

        private void OnEnded(RaidRunner runner, RaidResult result)
        {
            Summary = Describe();
            Debug.Log("PerfProbe: " + Summary + "  (" + SystemInfo.deviceModel + ")");
        }

        public string Describe()
        {
            if (_count == 0)
            {
                return "";
            }

            float sum = 0f;
            for (int i = 0; i < _count; i++)
            {
                sum += _frames[i];
                _sorted[i] = _frames[i];
            }

            System.Array.Sort(_sorted, 0, _count);
            float avgFps = _count / Mathf.Max(sum, 1e-4f);
            float lowFps = 1f / Mathf.Max(_sorted[Mathf.Max(0, _count - 1 - _count / 100)], 1e-4f); // the 99th-percentile frame
            double avgStep = _steps > 0 ? _stepMsTotal / _steps : 0;
            string draws = _drawCalls.Valid ? _maxDrawCalls + " draws" + (_maxDrawCalls > MaxDrawCalls ? " OVER" : "") : "draws n/a";
            return Mathf.RoundToInt(avgFps) + " fps (1% low " + Mathf.RoundToInt(lowFps) + (lowFps < FloorFps ? " UNDER FLOOR" : "") + "), " +
                draws + ", sim " + avgStep.ToString("0.000") + " ms avg / " + _maxStepMs.ToString("0.000") + " max" +
                (_maxStepMs > MaxStepMs ? " OVER" : "");
        }
    }
}
