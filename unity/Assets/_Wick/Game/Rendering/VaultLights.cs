using System.Collections.Generic;
using UnityEngine;
using Wick.Sim;

namespace Wick.Game
{
    /// <summary>
    /// The vault's fixed lights, shared by the raid's VaultView and the editor's darkness preview:
    /// sconces hung along the walls (StaticLights.PlaceSconces), a glowing strip on every pit edge
    /// open to the floor, and the entrance's "way out" glow.
    /// </summary>
    public static class VaultLights
    {
        /// <summary>Hangs the theme's sconce on each placed wall face; returns the placements.</summary>
        public static List<Sconce> SpawnSconces(VaultDef vault, ThemeDefinition theme, LightingProfile p, Transform parent)
        {
            List<Sconce> sconces = StaticLights.PlaceSconces(vault, p.SconceSpacingTiles);
            if (theme.Sconce != null)
            {
                foreach (Sconce s in sconces)
                {
                    Vector3 face = SimPose.TileCenter(s.Wall) + SimPose.DirVector(s.Facing) * 0.5f;
                    Object.Instantiate(theme.Sconce, face, SimPose.Facing(s.Facing), parent);
                }
            }

            return sconces;
        }

        /// <summary>
        /// Pit edge strips and the entrance glow, each added to `glows` with the tile whose light decides
        /// it. Returns the entrance glow, or null if the theme has none.
        /// </summary>
        public static ReadableGlow SpawnTileGlows(VaultDef vault, ThemeDefinition theme, LightingProfile p, Transform parent,
            List<(ReadableGlow glow, TilePos tile)> glows)
        {
            if (theme.PitEdge != null)
            {
                for (int y = 0; y < vault.Height; y++)
                {
                    for (int x = 0; x < vault.Width; x++)
                    {
                        var pos = new TilePos(x, y);
                        if (vault.TileAt(pos) != TileType.Pit)
                        {
                            continue;
                        }

                        for (int d = 0; d < 4; d++)
                        {
                            var dir = (Dir)d;
                            TileType beside = vault.TileAt(pos.Step(dir));
                            if (beside != TileType.Pit && beside != TileType.Wall)
                            {
                                GameObject edge = Object.Instantiate(theme.PitEdge, SimPose.TileCenter(pos), SimPose.Facing(dir), parent);
                                glows.Add((Glow(edge, ReadableKind.PitEdge, p.PitEdgeColor), pos));
                            }
                        }
                    }
                }
            }

            if (theme.EntranceGlow == null)
            {
                return null;
            }

            GameObject entrance = Object.Instantiate(theme.EntranceGlow, SimPose.TileCenter(vault.Entrance), Quaternion.identity, parent);
            entrance.transform.localScale = Vector3.one * 1.6f;
            ReadableGlow glow = Glow(entrance, ReadableKind.Entrance, p.EntranceColor);
            glows.Add((glow, vault.Entrance));
            return glow;
        }

        private static ReadableGlow Glow(GameObject go, ReadableKind kind, Color color)
        {
            ReadableGlow glow = go.GetComponent<ReadableGlow>();
            if (glow == null) // not ??: a missing component is Unity's fake null
            {
                glow = go.AddComponent<ReadableGlow>();
            }

            glow.Kind = kind;
            glow.SetColor(color);
            return glow;
        }
    }
}
