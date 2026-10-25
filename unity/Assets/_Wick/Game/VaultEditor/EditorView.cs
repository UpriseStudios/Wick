using System.Collections.Generic;
using UnityEngine;
using Wick.Sim;

namespace Wick.Game
{
    /// <summary>
    /// Draws a draft with the same prefabs as a raid (fully lit, no visibility mask), plus editor
    /// markers: entrance, hoard frame, owl patrols, the pit-free route and a placement ghost. Anything
    /// the validator complains about is tinted red (and a pit that cuts off treasure, or a tile the Pit
    /// tool refused); the selection glows, and an item being moved lifts and shows green or red for
    /// whether it can be dropped there.
    /// </summary>
    public sealed class EditorView : MonoBehaviour
    {
        /// <summary>Editor state drawn on top of the draft.</summary>
        public struct Marks
        {
            public int Selected;
            public int Moving;
            public bool MovingOk;
            public TileRect? HoardPreview;

            /// <summary>A tile the Pit tool just refused: drawn red.</summary>
            public TilePos? Refused;

            /// <summary>The pit-free walk from the entrance to the hoard, drawn as a green line; null for none.</summary>
            public List<TilePos> Route;

            public static Marks None => new Marks { Selected = -1, Moving = -1 };
        }

        private static readonly Color Ok = new Color(0.4f, 1f, 0.5f);

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
        private static readonly int LineColorId = Shader.PropertyToID("_Color");

        [SerializeField] private EditorAssets _assets;

        private readonly List<GameObject> _objects = new List<GameObject>();
        private readonly List<GameObject> _markers = new List<GameObject>();
        private GameObject[] _tiles;
        private PieceChoice[] _shownTiles;
        private int _shownWidth;
        private Transform _tileRoot;
        private Transform _objectRoot;
        private Transform _markerRoot;
        private MaterialPropertyBlock _block;
        private GameObject _ghost;
        private ObjectType _ghostType;

        public void Bind(EditorAssets assets)
        {
            _assets = assets;
        }

        private void Awake()
        {
            EnsureRoots();
        }

        private void EnsureRoots()
        {
            if (_tileRoot != null)
            {
                return;
            }

            _block = new MaterialPropertyBlock();
            _tileRoot = Child("Tiles");
            _objectRoot = Child("Objects");
            _markerRoot = Child("Markers");
        }

        /// <summary>Redraws the draft. Tiles update in place; objects and markers are rebuilt.</summary>
        public void Build(VaultDraft draft, List<VaultIssue> issues, Marks marks)
        {
            EnsureRoots();
            var badObjects = new HashSet<int>();
            var badTiles = new HashSet<TilePos>();
            foreach (VaultIssue issue in issues)
            {
                if (issue.ObjectId != 0)
                {
                    badObjects.Add(issue.ObjectId);
                }

                if ((issue.ObjectId == 0 || issue.Code == VaultIssueCode.PitBlocksRoute) && draft.InBounds(issue.Tile))
                {
                    badTiles.Add(issue.Tile); // a cut-off pile's issue names the pit in the way
                }
            }

            if (marks.Refused.HasValue)
            {
                badTiles.Add(marks.Refused.Value);
            }

            BuildTiles(draft, badTiles);

            Clear(_objects);
            foreach (DraftObject o in draft.Objects)
            {
                GameObject prefab = _assets.Registry.ForObject(o.Type);
                if (prefab == null)
                {
                    continue;
                }

                GameObject go = Instantiate(prefab, _objectRoot);
                Pose(go.transform, draft, o.Type, o.Pos, o.Rot);
                HideRaidOnlyParts(go);
                if (o.Id == marks.Moving)
                {
                    go.transform.position += Vector3.up * 0.3f;
                    Tint(go, marks.MovingOk ? Ok : Color.red, false);
                }
                else
                {
                    Tint(go, badObjects.Contains(o.Id) ? Color.red : (Color?)null, o.Id == marks.Selected);
                }

                _objects.Add(go);
            }

            foreach (DraftDecoration d in draft.Decorations)
            {
                GameObject go = DecorationProps.Spawn(d.Kind, d.Pos, d.Rot, draft.TileAt, _assets.Registry, _objectRoot);
                if (go == null)
                {
                    continue;
                }

                if (d.Id == marks.Moving)
                {
                    go.transform.position += Vector3.up * 0.3f;
                    Tint(go, marks.MovingOk ? Ok : Color.red, false);
                }
                else if (badObjects.Contains(d.Id) || d.Id == marks.Selected)
                {
                    Tint(go, badObjects.Contains(d.Id) ? Color.red : (Color?)null, d.Id == marks.Selected);
                }

                _objects.Add(go);
            }

            Clear(_markers);
            Marker(_assets.EntranceMarker, SimPose.TileCenter(draft.Entrance), Quaternion.identity);
            if (marks.HoardPreview.HasValue)
            {
                BuildHoardFrame(marks.HoardPreview.Value);
            }
            else if (draft.HasHoard)
            {
                BuildHoardFrame(draft.Hoard);
            }

            foreach (DraftObject o in draft.Objects)
            {
                if (o.Type == ObjectType.NightOwl && (o.Patrol.Count > 0 || o.Id == marks.Selected))
                {
                    BuildPatrol(o, o.Id == marks.Selected);
                }
            }

            if (marks.Route != null && marks.Route.Count > 1)
            {
                BuildRoute(marks.Route);
            }
        }

        /// <summary>Shows a lifted preview of an object being dragged from the shop.</summary>
        public void ShowGhost(ObjectType type, VaultDraft draft, TilePos tile, bool valid)
        {
            if (_ghost == null || _ghostType != type)
            {
                HideGhost();
                GameObject prefab = _assets.Registry.ForObject(type);
                _ghost = Instantiate(prefab, transform);
                _ghost.name = "Ghost";
                HideRaidOnlyParts(_ghost);
                _ghostType = type;
            }

            _ghost.SetActive(true);
            Pose(_ghost.transform, draft, type, tile, 0);
            _ghost.transform.position += Vector3.up * 0.3f;
            Tint(_ghost, valid ? Ok : Color.red, false);
        }

        public void HideGhost()
        {
            if (_ghost != null)
            {
                Kill(_ghost);
                _ghost = null;
            }
        }

        // ---- Pieces ---------------------------------------------------------------------

        private void BuildTiles(VaultDraft draft, HashSet<TilePos> badTiles)
        {
            int count = draft.Width * draft.Height;
            if (_tiles == null || _tiles.Length != count || _shownWidth != draft.Width)
            {
                _shownWidth = draft.Width;
                if (_tiles != null)
                {
                    foreach (GameObject t in _tiles)
                    {
                        Kill(t);
                    }
                }

                _tiles = new GameObject[count];
                _shownTiles = new PieceChoice[count];
            }

            // A tile's piece depends on its neighbours (walls turn corners), so compare pieces, not types.
            ThemeDefinition theme = _assets.Registry.Theme;
            System.Func<TilePos, TileType> tileAt = draft.TileAt;
            for (int y = 0; y < draft.Height; y++)
            {
                for (int x = 0; x < draft.Width; x++)
                {
                    int i = y * draft.Width + x;
                    var p = new TilePos(x, y);
                    PieceChoice piece = ThemeKit.Choose(draft.Width, draft.Height, tileAt, draft.Entrance, p);
                    if (_tiles[i] == null || !_shownTiles[i].Equals(piece))
                    {
                        if (_tiles[i] != null)
                        {
                            Kill(_tiles[i]);
                        }

                        _tiles[i] = theme.Spawn(piece, SimPose.TileCenter(p), _tileRoot);
                        _shownTiles[i] = piece;
                    }

                    Tint(_tiles[i], badTiles.Contains(p) ? Color.red : (Color?)null, false);
                }
            }
        }

        private void BuildHoardFrame(TileRect r)
        {
            float minX = r.MinX - 0.5f;
            float maxX = r.MaxX + 0.5f;
            float maxZ = -r.MinY + 0.5f;
            float minZ = -r.MaxY - 0.5f;
            float w = maxX - minX;
            float d = maxZ - minZ;
            Edge(new Vector3((minX + maxX) * 0.5f, 0.06f, maxZ), new Vector3(w, 0.06f, 0.08f));
            Edge(new Vector3((minX + maxX) * 0.5f, 0.06f, minZ), new Vector3(w, 0.06f, 0.08f));
            Edge(new Vector3(minX, 0.06f, (minZ + maxZ) * 0.5f), new Vector3(0.08f, 0.06f, d));
            Edge(new Vector3(maxX, 0.06f, (minZ + maxZ) * 0.5f), new Vector3(0.08f, 0.06f, d));
        }

        private void Edge(Vector3 center, Vector3 scale)
        {
            GameObject e = Marker(_assets.HoardEdge, center, Quaternion.identity);
            e.transform.localScale = scale;
        }

        private void BuildPatrol(DraftObject owl, bool selected)
        {
            var points = new List<Vector3> { SimPose.TileCenter(owl.Pos) + Vector3.up * 0.1f };
            foreach (TilePos p in owl.Patrol)
            {
                Marker(_assets.Waypoint, SimPose.TileCenter(p), Quaternion.identity);
                points.Add(SimPose.TileCenter(p) + Vector3.up * 0.1f);
            }

            if (owl.Patrol.Count > 1)
            {
                points.Add(SimPose.TileCenter(owl.Patrol[0]) + Vector3.up * 0.1f); // the loop closes
            }

            var lineGo = new GameObject("Patrol " + owl.Id);
            lineGo.transform.SetParent(_markerRoot, false);
            var line = lineGo.AddComponent<LineRenderer>();
            line.sharedMaterial = _assets.PatrolLine;
            line.widthMultiplier = selected ? 0.12f : 0.07f;
            line.positionCount = points.Count;
            line.SetPositions(points.ToArray());
            line.numCapVertices = 2;
            _markers.Add(lineGo);
        }

        /// <summary>The safe route preview: the patrol line's material, tinted green.</summary>
        private void BuildRoute(List<TilePos> route)
        {
            var points = new Vector3[route.Count];
            for (int i = 0; i < route.Count; i++)
            {
                points[i] = SimPose.TileCenter(route[i]) + Vector3.up * 0.12f;
            }

            var lineGo = new GameObject("Safe route");
            lineGo.transform.SetParent(_markerRoot, false);
            var line = lineGo.AddComponent<LineRenderer>();
            line.sharedMaterial = _assets.PatrolLine;
            line.widthMultiplier = 0.1f;
            line.positionCount = points.Length;
            line.SetPositions(points);
            line.numCapVertices = 2;
            line.numCornerVertices = 2;
            line.GetPropertyBlock(_block);
            _block.SetColor(LineColorId, new Color(Ok.r, Ok.g, Ok.b, 0.9f));
            line.SetPropertyBlock(_block);
            _markers.Add(lineGo);
        }

        /// <summary>Same placement rules as the raid views (BrazierView, TimedTrapView on walls).</summary>
        private static void Pose(Transform t, VaultDraft draft, ObjectType type, TilePos pos, int rot)
        {
            t.position = SimPose.TileCenter(pos);
            t.rotation = SimPose.Facing((Dir)rot);
            if (type == ObjectType.Bellows && draft.TileAt(pos) == TileType.Wall)
            {
                t.position += SimPose.DirVector((Dir)rot) * 0.5f; // on the wall face it blows from
            }

            if (type == ObjectType.Brazier)
            {
                for (int d = 0; d < 4; d++)
                {
                    var dir = (Dir)d;
                    TilePos n = pos.Step(dir);
                    if (draft.InBounds(n) && draft.TileAt(n) != TileType.Wall)
                    {
                        t.rotation = SimPose.Facing(dir);
                        t.position = SimPose.TileCenter(pos) + SimPose.DirVector(dir) * 0.5f;
                        break;
                    }
                }
            }
        }

        /// <summary>Hides parts that only make sense mid-raid (spike warning flash).</summary>
        private static void HideRaidOnlyParts(GameObject go)
        {
            Transform warning = go.transform.Find("Warning");
            if (warning != null)
            {
                warning.gameObject.SetActive(false);
            }
        }

        private void Tint(GameObject go, Color? color, bool highlight)
        {
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>())
            {
                if (color == null && !highlight)
                {
                    r.SetPropertyBlock(null);
                    continue;
                }

                r.GetPropertyBlock(_block);
                if (color != null)
                {
                    _block.SetColor(BaseColorId, color.Value);
                    _block.SetColor(EmissionId, color.Value * 0.35f);
                }
                else
                {
                    _block.SetColor(EmissionId, new Color(0.25f, 0.35f, 0.6f));
                }

                r.SetPropertyBlock(_block);
            }
        }

        private GameObject Marker(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            GameObject go = Instantiate(prefab, position, rotation, _markerRoot);
            _markers.Add(go);
            return go;
        }

        private static void Kill(Object o)
        {
            if (Application.isPlaying)
            {
                Destroy(o);
            }
            else
            {
                DestroyImmediate(o);
            }
        }

        private Transform Child(string name)
        {
            var t = new GameObject(name).transform;
            t.SetParent(transform, false);
            return t;
        }

        private static void Clear(List<GameObject> list)
        {
            foreach (GameObject go in list)
            {
                Kill(go);
            }

            list.Clear();
        }
    }
}
