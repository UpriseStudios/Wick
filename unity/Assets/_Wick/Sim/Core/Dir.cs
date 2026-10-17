namespace Wick.Sim
{
    /// <summary>Grid direction. Y grows downward (row index), so N is y - 1.</summary>
    public enum Dir : byte
    {
        N = 0,
        E = 1,
        S = 2,
        W = 3,
    }

    public static class DirExt
    {
        public static int Dx(this Dir dir)
        {
            switch (dir)
            {
                case Dir.E: return 1;
                case Dir.W: return -1;
                default: return 0;
            }
        }

        public static int Dy(this Dir dir)
        {
            switch (dir)
            {
                case Dir.N: return -1;
                case Dir.S: return 1;
                default: return 0;
            }
        }
    }
}
