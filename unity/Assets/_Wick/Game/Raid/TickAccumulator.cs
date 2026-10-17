using Wick.Sim;

namespace Wick.Game
{
    /// <summary>
    /// Turns frame time into whole 30 Hz sim ticks. Alpha is how far the next tick is, for
    /// interpolating views between the previous and current sim state.
    /// </summary>
    public sealed class TickAccumulator
    {
        public const double TickSeconds = 1.0 / SimTuning.TicksPerSecond;

        /// <summary>A long hitch steps at most this many ticks, then drops the backlog.</summary>
        public const int MaxStepsPerFrame = 5;

        private double _accumulated;

        public float Alpha => (float)(_accumulated / TickSeconds);

        public void Reset()
        {
            _accumulated = 0;
        }

        /// <summary>Adds frame time and returns how many ticks to step now.</summary>
        public int Advance(double deltaSeconds)
        {
            _accumulated += deltaSeconds;
            int steps = 0;
            while (_accumulated >= TickSeconds && steps < MaxStepsPerFrame)
            {
                _accumulated -= TickSeconds;
                steps++;
            }

            if (_accumulated >= TickSeconds)
            {
                _accumulated = 0;
            }

            return steps;
        }
    }
}
