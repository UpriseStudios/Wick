using System;

namespace Wick.Sim
{
    /// <summary>Integer tile coordinate. (0,0) is the top-left tile.</summary>
    public readonly struct TilePos : IEquatable<TilePos>
    {
        public readonly int X;
        public readonly int Y;

        public TilePos(int x, int y)
        {
            X = x;
            Y = y;
        }

        public TilePos Step(Dir dir)
        {
            return new TilePos(X + dir.Dx(), Y + dir.Dy());
        }

        public bool Equals(TilePos other)
        {
            return X == other.X && Y == other.Y;
        }

        public override bool Equals(object obj)
        {
            return obj is TilePos other && Equals(other);
        }

        public override int GetHashCode()
        {
            return (X * 397) ^ Y;
        }

        public static bool operator ==(TilePos a, TilePos b)
        {
            return a.Equals(b);
        }

        public static bool operator !=(TilePos a, TilePos b)
        {
            return !a.Equals(b);
        }

        public override string ToString()
        {
            return "(" + X + "," + Y + ")";
        }
    }
}
