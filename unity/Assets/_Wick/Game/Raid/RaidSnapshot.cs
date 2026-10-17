using System;
using Wick.Sim;

namespace Wick.Game
{
    /// <summary>A copy of the moving parts of the sim state at one tick, for interpolation.</summary>
    public sealed class RaidSnapshot
    {
        public int Tick;
        public WickState Wick;
        public GuardianState[] Guardians = Array.Empty<GuardianState>();

        public void CopyFrom(RaidState state)
        {
            Tick = state.Tick;
            Wick = state.Wick;
            if (Guardians.Length != state.Guardians.Length)
            {
                Guardians = new GuardianState[state.Guardians.Length];
            }

            Array.Copy(state.Guardians, Guardians, Guardians.Length);
        }
    }
}
