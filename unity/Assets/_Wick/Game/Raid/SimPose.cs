using UnityEngine;
using Wick.Sim;

namespace Wick.Game
{
    /// <summary>
    /// Maps sim positions to world space. Tile (x, y) has its centre at world (x, 0, -y), so
    /// sim north (y - 1) is world +z and the grid lines up with an unrotated camera.
    /// </summary>
    public static class SimPose
    {
        public static Vector3 TileCenter(TilePos p)
        {
            return new Vector3(p.X, 0f, -p.Y);
        }

        public static Vector3 DirVector(Dir dir)
        {
            return new Vector3(dir.Dx(), 0f, -dir.Dy());
        }

        public static Vector3 Position(TilePos tile, bool moving, Dir moveDir, int progress)
        {
            Vector3 p = TileCenter(tile);
            if (moving)
            {
                p += DirVector(moveDir) * (progress / (float)SubTile.PerTile);
            }

            return p;
        }

        public static Vector3 Position(in WickState w)
        {
            return Position(w.Tile, w.Moving, w.MoveDir, w.Progress);
        }

        public static Vector3 Position(in GuardianState g)
        {
            return Position(g.Tile, g.Moving, g.MoveDir, g.Progress);
        }

        public static Quaternion Facing(Dir dir)
        {
            return Quaternion.LookRotation(DirVector(dir), Vector3.up);
        }

        /// <summary>World tile under a world position (inverse of TileCenter, rounding).</summary>
        public static TilePos TileAt(Vector3 world)
        {
            return new TilePos(Mathf.RoundToInt(world.x), Mathf.RoundToInt(-world.z));
        }
    }
}
