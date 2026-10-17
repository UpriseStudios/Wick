namespace Wick.Sim
{
    public static class Motion
    {
        /// <summary>
        /// The tile a mover counts as standing on: its tile, or the tile it is moving into once
        /// it is at least halfway there. Used for hits, contacts, pickups and sight origins.
        /// </summary>
        public static TilePos OccupiedTile(TilePos tile, bool moving, Dir moveDir, int progress)
        {
            return moving && progress * 2 >= SubTile.PerTile ? tile.Step(moveDir) : tile;
        }

        /// <summary>Squared distance between two tiles, in tiles squared.</summary>
        public static int DistSq(TilePos a, TilePos b)
        {
            int dx = a.X - b.X;
            int dy = a.Y - b.Y;
            return dx * dx + dy * dy;
        }

        /// <summary>Chebyshev distance: 1 means adjacent, including diagonally.</summary>
        public static int Chebyshev(TilePos a, TilePos b)
        {
            int dx = a.X > b.X ? a.X - b.X : b.X - a.X;
            int dy = a.Y > b.Y ? a.Y - b.Y : b.Y - a.Y;
            return dx > dy ? dx : dy;
        }
    }
}
