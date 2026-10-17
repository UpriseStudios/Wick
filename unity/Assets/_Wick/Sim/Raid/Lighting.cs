using System;

namespace Wick.Sim
{
    /// <summary>
    /// Which tiles Wick's flame lights. The sim owns this; rendering only visualises it.
    /// A tile is lit when its centre is within the light radius of Wick's exact position
    /// (circle, integer maths) and there is a clear line of sight to it. Walls block light
    /// but are themselves lit, so their faces show.
    /// </summary>
    public static class Lighting
    {
        /// <summary>
        /// Light radius in sub-tile units for a flame level, halved by tiptoe, then shrunk by a
        /// Bellows gust (never below MinLightRadius while any flicker is left).
        /// </summary>
        public static int RadiusUnits(int flickers, bool tiptoe, bool gustDimmed = false)
        {
            int radius;
            if (flickers >= 3) radius = SimTuning.LightRadius3Flickers;
            else if (flickers == 2) radius = SimTuning.LightRadius2Flickers;
            else if (flickers == 1) radius = SimTuning.LightRadius1Flicker;
            else return 0;

            if (tiptoe)
            {
                radius /= SimTuning.TiptoeLightDivisor;
            }

            if (gustDimmed)
            {
                radius -= SimTuning.GustDimUnits;
            }

            return radius < SimTuning.MinLightRadius ? SimTuning.MinLightRadius : radius;
        }

        public static int RadiusUnits(in WickState wick)
        {
            return RadiusUnits(wick.Flickers, wick.Tiptoe, wick.GustDimTicks > 0);
        }

        /// <summary>Fills lit (row-major, Width * Height) for Wick's current state. No allocation.</summary>
        public static void Compute(VaultDef vault, in WickState wick, bool[] lit)
        {
            Array.Clear(lit, 0, lit.Length);

            int radius = RadiusUnits(in wick);
            if (radius <= 0)
            {
                return;
            }

            // Exact position in sub-tile units; tile (x, y) has its centre at (x, y) * PerTile.
            long px = (long)wick.Tile.X * SubTile.PerTile;
            long py = (long)wick.Tile.Y * SubTile.PerTile;
            if (wick.Moving)
            {
                px += wick.MoveDir.Dx() * wick.Progress;
                py += wick.MoveDir.Dy() * wick.Progress;
            }

            // Line of sight is traced from the tile Wick is mostly in.
            TilePos origin = wick.Occupied;

            long r2 = (long)radius * radius;
            int reach = radius / SubTile.PerTile + 1;
            int minX = Math.Max(0, origin.X - reach);
            int maxX = Math.Min(vault.Width - 1, origin.X + reach);
            int minY = Math.Max(0, origin.Y - reach);
            int maxY = Math.Min(vault.Height - 1, origin.Y + reach);

            for (int y = minY; y <= maxY; y++)
            {
                long dy = (long)y * SubTile.PerTile - py;
                for (int x = minX; x <= maxX; x++)
                {
                    long dx = (long)x * SubTile.PerTile - px;
                    if (dx * dx + dy * dy > r2)
                    {
                        continue;
                    }

                    var target = new TilePos(x, y);
                    if (HasLineOfSight(vault, origin, target))
                    {
                        lit[y * vault.Width + x] = true;
                    }
                }
            }
        }

        /// <summary>
        /// Integer Bresenham from a to b. Walls between them block; b itself may be a wall.
        /// A diagonal step cannot squeeze between two walls that touch at a corner.
        /// </summary>
        public static bool HasLineOfSight(VaultDef vault, TilePos a, TilePos b)
        {
            int x = a.X;
            int y = a.Y;
            int dx = Math.Abs(b.X - a.X);
            int dy = -Math.Abs(b.Y - a.Y);
            int sx = a.X < b.X ? 1 : -1;
            int sy = a.Y < b.Y ? 1 : -1;
            int err = dx + dy;

            while (x != b.X || y != b.Y)
            {
                int e2 = 2 * err;
                int nx = x;
                int ny = y;
                if (e2 >= dy)
                {
                    err += dy;
                    nx += sx;
                }

                if (e2 <= dx)
                {
                    err += dx;
                    ny += sy;
                }

                if (nx != x && ny != y && IsWall(vault, nx, y) && IsWall(vault, x, ny))
                {
                    return false;
                }

                x = nx;
                y = ny;
                if (x == b.X && y == b.Y)
                {
                    return true;
                }

                if (IsWall(vault, x, y))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsWall(VaultDef vault, int x, int y)
        {
            return vault.TileAt(new TilePos(x, y)) == TileType.Wall;
        }
    }
}
