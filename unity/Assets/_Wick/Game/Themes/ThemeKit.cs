using System;
using Wick.Sim;

namespace Wick.Game
{
    /// <summary>The modular kit pieces every theme provides (docs/asset-production.md, Tile kits).</summary>
    public enum TilePiece : byte
    {
        Floor,
        WallStraight,
        WallCorner,
        WallFill,
        Door,
        Pit,
        Ice,
    }

    /// <summary>Which piece a tile shows, which way its decorated face points, and a stable variant seed.</summary>
    public readonly struct PieceChoice : IEquatable<PieceChoice>
    {
        public readonly TilePiece Piece;
        public readonly Dir Facing;
        public readonly int Seed;
        public readonly bool Entrance;

        public PieceChoice(TilePiece piece, Dir facing, int seed, bool entrance)
        {
            Piece = piece;
            Facing = facing;
            Seed = seed;
            Entrance = entrance;
        }

        public bool Equals(PieceChoice o)
        {
            return Piece == o.Piece && Facing == o.Facing && Seed == o.Seed && Entrance == o.Entrance;
        }

        public override bool Equals(object obj)
        {
            return obj is PieceChoice o && Equals(o);
        }

        public override int GetHashCode()
        {
            return ((int)Piece * 4 + (int)Facing) ^ (Seed << 4) ^ (Entrance ? 1 << 30 : 0);
        }
    }

    /// <summary>
    /// Picks the kit piece for each tile from the tile grid alone, so any vault renders in any
    /// theme the same way. Pieces are modelled facing +Z (north, towards tile y - 1):
    /// a straight wall's decorated face, a corner's first face (the second is 90 degrees clockwise),
    /// a door's opening axis. Presentation only: the sim never sees any of this.
    /// </summary>
    public static class ThemeKit
    {
        public static PieceChoice Choose(int width, int height, Func<TilePos, TileType> tileAt, TilePos entrance, TilePos p)
        {
            int seed = (int)(((uint)(p.X * 73856093) ^ (uint)(p.Y * 19349663)) & 0x7fffffff);
            bool isEntrance = p.Equals(entrance);
            switch (tileAt(p))
            {
                case TileType.Wall:
                    return Wall(width, height, tileAt, p, seed);
                case TileType.Door:
                    // Walls east and west: the corridor (and the door's opening) runs north-south.
                    bool northSouth = Closed(width, height, tileAt, p.Step(Dir.E)) && Closed(width, height, tileAt, p.Step(Dir.W));
                    return new PieceChoice(TilePiece.Door, northSouth ? Dir.N : Dir.E, seed, isEntrance);
                case TileType.Pit:
                    return new PieceChoice(TilePiece.Pit, Dir.N, seed, isEntrance);
                case TileType.Ice:
                    return new PieceChoice(TilePiece.Ice, Dir.N, seed, isEntrance);
                default:
                    return new PieceChoice(TilePiece.Floor, Dir.N, seed, isEntrance);
            }
        }

        private static PieceChoice Wall(int width, int height, Func<TilePos, TileType> tileAt, TilePos p, int seed)
        {
            bool n = !Closed(width, height, tileAt, p.Step(Dir.N));
            bool e = !Closed(width, height, tileAt, p.Step(Dir.E));
            bool s = !Closed(width, height, tileAt, p.Step(Dir.S));
            bool w = !Closed(width, height, tileAt, p.Step(Dir.W));
            int open = (n ? 1 : 0) + (e ? 1 : 0) + (s ? 1 : 0) + (w ? 1 : 0);

            if (open == 2)
            {
                if (n && e) return new PieceChoice(TilePiece.WallCorner, Dir.N, seed, false);
                if (e && s) return new PieceChoice(TilePiece.WallCorner, Dir.E, seed, false);
                if (s && w) return new PieceChoice(TilePiece.WallCorner, Dir.S, seed, false);
                if (w && n) return new PieceChoice(TilePiece.WallCorner, Dir.W, seed, false);
            }

            if (open == 0 || open == 4)
            {
                return new PieceChoice(TilePiece.WallFill, Dir.N, seed, false);
            }

            // One open side, two opposite (a thin wall) or three: face the first open side.
            Dir facing = n ? Dir.N : e ? Dir.E : s ? Dir.S : Dir.W;
            return new PieceChoice(TilePiece.WallStraight, facing, seed, false);
        }

        /// <summary>Walls and the outside of the vault are closed; everything else is open floor.</summary>
        private static bool Closed(int width, int height, Func<TilePos, TileType> tileAt, TilePos p)
        {
            return p.X < 0 || p.Y < 0 || p.X >= width || p.Y >= height || tileAt(p) == TileType.Wall;
        }
    }
}
