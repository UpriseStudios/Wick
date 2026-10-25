using System.Collections.Generic;
using UnityEngine;
using Wick.Sim;

namespace Wick.Game
{
    /// <summary>A sconce on a wall tile, lighting the floor tile it faces.</summary>
    public readonly struct Sconce
    {
        public readonly TilePos Wall;
        public readonly Dir Facing;

        public Sconce(TilePos wall, Dir facing)
        {
            Wall = wall;
            Facing = facing;
        }

        public TilePos Floor => Wall.Step(Facing);
    }

    /// <summary>
    /// The vault's static warm pools (docs/m10-2-lighting.md, layer 2). Vaults are assembled at
    /// runtime, so nothing can be baked: this places sconces along the walls and works out, per
    /// tile, how much warm light the sconces, players' torch decorations and the entrance cast.
    /// Deterministic from the vault alone. Presentation only: it never changes what is lit.
    /// </summary>
    public static class StaticLights
    {
        // The camera looks north, so sconces on south-facing wall faces read best.
        private static readonly Dir[] FacingPreference = { Dir.S, Dir.E, Dir.W, Dir.N };

        /// <summary>
        /// Sconces on wall faces beside open floor, scanning row by row, each at least `spacing` tiles
        /// (Chebyshev) from every other sconce and torch decoration, and never on a wall that already
        /// holds a brazier, bellows or decoration.
        /// </summary>
        public static List<Sconce> PlaceSconces(VaultDef vault, int spacing)
        {
            var taken = new HashSet<TilePos>();
            var lights = new List<TilePos>();
            for (int i = 0; i < vault.ObjectCount; i++)
            {
                taken.Add(vault.GetObject(i).Pos);
            }

            for (int i = 0; i < vault.DecorationCount; i++)
            {
                DecorationDef d = vault.GetDecoration(i);
                taken.Add(d.Pos);
                if (d.Kind == Decorations.Torch)
                {
                    lights.Add(d.Pos);
                }
            }

            var sconces = new List<Sconce>();
            for (int y = 0; y < vault.Height; y++)
            {
                for (int x = 0; x < vault.Width; x++)
                {
                    var wall = new TilePos(x, y);
                    if (vault.TileAt(wall) != TileType.Wall || taken.Contains(wall) || Near(lights, wall, spacing))
                    {
                        continue;
                    }

                    foreach (Dir d in FacingPreference)
                    {
                        if (vault.TileAt(wall.Step(d)) == TileType.Floor && wall.Step(d) != vault.Entrance)
                        {
                            sconces.Add(new Sconce(wall, d));
                            lights.Add(wall);
                            break;
                        }
                    }
                }
            }

            return sconces;
        }

        /// <summary>
        /// Warm light per tile (row-major, 0 to 1): each source lights tiles within `radius` tiles of
        /// its floor tile that it has a line of sight to, falling off smoothly to the edge. Sources are
        /// the sconces' and torches' floor tiles and the entrance. Walls in sight are lit too, so their
        /// faces glow.
        /// </summary>
        public static float[] Compute(VaultDef vault, IList<Sconce> sconces, float radius)
        {
            var sources = new List<TilePos> { vault.Entrance };
            foreach (Sconce s in sconces)
            {
                sources.Add(s.Floor);
            }

            for (int i = 0; i < vault.DecorationCount; i++)
            {
                DecorationDef d = vault.GetDecoration(i);
                if (d.Kind == Decorations.Torch)
                {
                    for (int dir = 0; dir < 4; dir++)
                    {
                        TilePos floor = d.Pos.Step((Dir)dir);
                        if (vault.TileAt(floor) == TileType.Floor)
                        {
                            sources.Add(floor);
                            break;
                        }
                    }
                }
            }

            var light = new float[vault.Width * vault.Height];
            int reach = Mathf.CeilToInt(radius);
            foreach (TilePos s in sources)
            {
                if (!vault.InBounds(s))
                {
                    continue;
                }

                for (int dy = -reach; dy <= reach; dy++)
                {
                    for (int dx = -reach; dx <= reach; dx++)
                    {
                        var t = new TilePos(s.X + dx, s.Y + dy);
                        float dist = Mathf.Sqrt(dx * dx + dy * dy);
                        if (!vault.InBounds(t) || dist > radius || !Lighting.HasLineOfSight(vault, s, t))
                        {
                            continue;
                        }

                        float k = 1f - dist / (radius + 0.5f);
                        light[t.Y * vault.Width + t.X] = Mathf.Max(light[t.Y * vault.Width + t.X], k * k);
                    }
                }
            }

            return light;
        }

        private static bool Near(List<TilePos> lights, TilePos p, int spacing)
        {
            foreach (TilePos l in lights)
            {
                if (Mathf.Max(Mathf.Abs(l.X - p.X), Mathf.Abs(l.Y - p.Y)) < spacing)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
