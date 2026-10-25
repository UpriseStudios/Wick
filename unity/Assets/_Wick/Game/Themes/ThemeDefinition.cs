using System.Collections.Generic;
using UnityEngine;

namespace Wick.Game
{
    /// <summary>
    /// One vault theme: a modular 1 m tile kit (pieces face +Z) plus its ambient light. Any vault
    /// renders in any theme, because ThemeKit picks pieces from the tile grid alone; missing pieces
    /// fall back to simpler ones (corner to straight to fill, door/pit/ice to floor).
    /// </summary>
    [CreateAssetMenu(menuName = "Wick/Vault Theme", fileName = "Theme")]
    public sealed class ThemeDefinition : ScriptableObject
    {
        public string Id;
        public string DisplayName;

        [Header("Kit (1 m pieces, decorated face towards +Z)")]
        public GameObject Floor;
        public GameObject[] FloorVariants = new GameObject[0];
        public GameObject WallStraight;
        public GameObject WallCorner;
        public GameObject WallFill;
        public GameObject Door;
        public GameObject Pit;
        public GameObject Ice;
        public GameObject EntranceArch;

        [Header("Lighting (docs/m10-2-lighting.md)")]
        [Tooltip("Every lighting value for this theme. Null: the Classic Treasury defaults.")]
        public LightingProfile Lighting;

        [Tooltip("A wall sconce, hung on a wall face (+Z towards the floor it lights).")]
        public GameObject Sconce;

        [Tooltip("One glowing strip along a pit's edge (+Z towards the floor beside it), always readable.")]
        public GameObject PitEdge;

        [Tooltip("The entrance's \"way out\" glow on the floor, always readable.")]
        public GameObject EntranceGlow;

        [Header("Ambient (trilight; superseded by Lighting when set)")]
        public Color AmbientSky = new Color(0.11f, 0.10f, 0.14f);
        public Color AmbientEquator = new Color(0.07f, 0.06f, 0.08f);
        public Color AmbientGround = new Color(0.03f, 0.03f, 0.04f);

        public Sprite Icon;
        public bool StandIn;

        public GameObject PrefabFor(PieceChoice c)
        {
            switch (c.Piece)
            {
                case TilePiece.Floor:
                    int n = FloorVariants != null ? FloorVariants.Length : 0;
                    int pick = n == 0 ? 0 : c.Seed % (n + 2); // the plain floor most often, variants now and then
                    return pick < 2 || FloorVariants[pick - 2] == null ? Floor : FloorVariants[pick - 2];
                case TilePiece.WallCorner:
                    return First(WallCorner, WallStraight, WallFill);
                case TilePiece.WallStraight:
                    return First(WallStraight, WallFill);
                case TilePiece.WallFill:
                    return First(WallFill, WallStraight);
                case TilePiece.Door:
                    return First(Door, Floor);
                case TilePiece.Pit:
                    return First(Pit, Floor);
                case TilePiece.Ice:
                    return First(Ice, Floor);
                default:
                    return Floor;
            }
        }

        /// <summary>Spawns one tile's piece (and the entrance arch on the entrance tile).</summary>
        public GameObject Spawn(PieceChoice c, Vector3 position, Transform parent)
        {
            GameObject prefab = PrefabFor(c);
            GameObject tile = prefab != null ? Instantiate(prefab, position, SimPose.Facing(c.Facing), parent) : new GameObject("Empty tile");
            if (prefab == null)
            {
                tile.transform.SetParent(parent, false);
                tile.transform.position = position;
            }

            if (c.Entrance && EntranceArch != null)
            {
                Instantiate(EntranceArch, position, SimPose.Facing(c.Facing), tile.transform);
            }

            return tile;
        }

        public void ApplyAmbient()
        {
            if (Lighting != null)
            {
                Lighting.ApplyAmbient();
                return;
            }

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = AmbientSky;
            RenderSettings.ambientEquatorColor = AmbientEquator;
            RenderSettings.ambientGroundColor = AmbientGround;
        }

        /// <summary>Every prefab this theme uses, for checks.</summary>
        public IEnumerable<GameObject> Pieces()
        {
            foreach (GameObject g in new[] { Floor, WallStraight, WallCorner, WallFill, Door, Pit, Ice, EntranceArch, Sconce, PitEdge, EntranceGlow })
            {
                if (g != null)
                {
                    yield return g;
                }
            }

            foreach (GameObject g in FloorVariants)
            {
                if (g != null)
                {
                    yield return g;
                }
            }
        }

        private static GameObject First(params GameObject[] options)
        {
            foreach (GameObject g in options)
            {
                if (g != null)
                {
                    return g;
                }
            }

            return null;
        }
    }
}
