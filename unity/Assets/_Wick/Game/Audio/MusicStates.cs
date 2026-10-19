using Wick.Sim;

namespace Wick.Game
{
    public enum MusicState : byte
    {
        Silent,
        Calm,
        Tense,
        Escape,
    }

    /// <summary>
    /// The music state machine: calm while exploring, tense (heartbeat) when danger is close or a
    /// guardian is chasing, escape once Wick has reached the hoard and on a successful escape.
    /// Tension eases back only after a quiet spell, so the music doesn't flap.
    /// </summary>
    public sealed class MusicStates
    {
        public const float TenseAt = 0.35f;
        public const float CalmBelow = 0.15f;
        public const float CalmAfterSeconds = 2.5f;

        private float _quietFor;

        public MusicState State { get; private set; } = MusicState.Silent;

        public void Reset()
        {
            State = MusicState.Silent;
            _quietFor = 0f;
        }

        /// <param name="playing">False before the clock starts (scout) or with no raid loaded.</param>
        public MusicState Update(bool playing, float fear, bool chased, bool reachedHoard, Outcome outcome, float dt)
        {
            if (outcome == Outcome.Escaped)
            {
                return State = MusicState.Escape;
            }

            if (outcome == Outcome.Snuffed || !playing)
            {
                _quietFor = 0f;
                return State = playing ? MusicState.Silent : MusicState.Calm;
            }

            bool danger = fear >= TenseAt || chased;
            if (danger)
            {
                _quietFor = 0f;
                return State = MusicState.Tense;
            }

            _quietFor = fear < CalmBelow ? _quietFor + dt : 0f;
            if (State == MusicState.Tense && _quietFor < CalmAfterSeconds)
            {
                return State;
            }

            return State = reachedHoard ? MusicState.Escape : MusicState.Calm;
        }
    }
}
