namespace Wick.Sim
{
    /// <summary>Running 64-bit FNV-1a. Multi-byte values are fed little-endian.</summary>
    public struct Fnv1a64
    {
        public const ulong OffsetBasis = 14695981039346656037UL;
        private const ulong Prime = 1099511628211UL;

        // Stored XOR OffsetBasis so default(Fnv1a64) is a fresh hash.
        private ulong _x;

        public ulong Value => _x ^ OffsetBasis;

        public void AddByte(byte b)
        {
            ulong v = _x ^ OffsetBasis;
            v = unchecked((v ^ b) * Prime);
            _x = v ^ OffsetBasis;
        }

        public void AddBool(bool b)
        {
            AddByte(b ? (byte)1 : (byte)0);
        }

        public void AddInt(int v)
        {
            uint u = unchecked((uint)v);
            AddByte((byte)u);
            AddByte((byte)(u >> 8));
            AddByte((byte)(u >> 16));
            AddByte((byte)(u >> 24));
        }

        public void AddLong(ulong v)
        {
            AddInt(unchecked((int)v));
            AddInt(unchecked((int)(v >> 32)));
        }
    }
}
