using System;

namespace Wick.Sim
{
    /// <summary>
    /// PCG-XSH-RR 64/32 (O'Neill, pcg-random.org). The only source of randomness in the sim.
    /// Seeding matches the reference pcg32_srandom_r.
    /// </summary>
    public struct Pcg32
    {
        private const ulong Multiplier = 6364136223846793005UL;

        private ulong _state;
        private ulong _inc;

        public Pcg32(ulong seed, ulong stream)
        {
            _state = 0UL;
            _inc = (stream << 1) | 1UL;
            NextUInt();
            _state = unchecked(_state + seed);
            NextUInt();
        }

        public ulong State => _state;
        public ulong Inc => _inc;

        public uint NextUInt()
        {
            ulong old = _state;
            _state = unchecked(old * Multiplier + _inc);
            uint xorShifted = (uint)(((old >> 18) ^ old) >> 27);
            int rot = (int)(old >> 59);
            return (xorShifted >> rot) | (xorShifted << ((-rot) & 31));
        }

        /// <summary>Uniform int in [0, bound), without modulo bias.</summary>
        public int NextInt(int bound)
        {
            if (bound <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(bound), "bound must be positive");
            }

            uint ubound = (uint)bound;
            uint threshold = unchecked(0u - ubound) % ubound;
            while (true)
            {
                uint r = NextUInt();
                if (r >= threshold)
                {
                    return (int)(r % ubound);
                }
            }
        }
    }
}
