using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Wick.Economy;
using Wick.Services;
using Wick.Sim;

namespace Wick.Game
{
    /// <summary>Rooms shapes the layout; Defences places and moves items like a base builder.</summary>
    public enum EditorTab
    {
        Defences,
        Rooms,
        Decor,
    }

    public enum RoomTool
    {
        Floor,
        Wall,
        Door,
        Entrance,
        Hoard,
        Ice,
    }

    /// <summary>
    /// The vault editor: owns the draft, applies gestures and shop drags as edits (each one an
    /// undo step, autosaved), re-validates after every change, and runs Test and Publish.
    /// Defences has no modes: tap selects, dragging an item moves it, dragging empty floor pans,
    /// and a drop that would add a problem springs back. Rooms paints the layout with one finger.
    /// </summary>
    public sealed class VaultEditorController : MonoBehaviour
    {
        private static readonly int GridId = Shader.PropertyToID("_WickVisGrid");
        private static readonly int BoostId = Shader.PropertyToID("_WickLitBoost");

        [SerializeField] private EditorAssets _assets;
        [SerializeField] private EditorView _view;
        [SerializeField] private EditorCamera _camera;
        [SerializeField] private EditorInput _input;
        [SerializeField] private EditorUI _ui;

        [Tooltip("Scene objects hidden while a Test raid runs on top (camera, UI, light, view).")]
        [SerializeField] private GameObject[] _hideDuringTest;

        private readonly WickBackend _backend = new WickBackend();

        /// <summary>Test runs since the last publish, for the vault_published event.</summary>
        private int _testRuns;
        private readonly UndoStack<VaultDraft> _undo = new UndoStack<VaultDraft>(d => d.Clone());
        private DraftStore _store;
        private VaultDraft _strokeBefore;
        private bool _strokeChanged;
        private bool _panning;
        private bool _testing;
        private TilePos _hoardStart;
        private BuilderCameo _cameo;

        /// <summary>Doorway tiles drawn as wall until the builder blasts them open.</summary>
        private List<TilePos> _closedDoorway;

        public VaultDraft Draft { get; private set; }
        public List<VaultIssue> Issues { get; private set; } = new List<VaultIssue>();
        public EditorTab Tab { get; private set; } = EditorTab.Defences;
        public RoomTool RoomTool { get; private set; } = RoomTool.Wall;

        /// <summary>The selected object's id (Defences tab), or -1.</summary>
        public int Selected { get; private set; } = -1;

        /// <summary>The object being dragged to a new tile, or -1. MoveProblem says why it can't stay there.</summary>
        public int Moving { get; private set; } = -1;

        public string MoveProblem { get; private set; }
        public bool EditingPatrol { get; private set; }

        /// <summary>The hoard rectangle being dragged out on the Rooms tab.</summary>
        public TileRect? HoardPreview { get; private set; }

        public bool CanUndo => _undo.CanUndo;
        public bool CanRedo => _undo.CanRedo;
        public DraftStore Store => _store;
        public DraftObject SelectedObject => Selected >= 0 && Draft != null ? Draft.Find(Selected) : null;
        public DraftDecoration SelectedDecoration => Selected >= 0 && Draft != null ? Draft.FindDecoration(Selected) : null;

        /// <summary>Decorations the player can place: the free set plus every trophy they've earned.</summary>
        public static List<string> DecorationTray()
        {
            var kinds = new List<string>(Decorations.Free);
            foreach (string cosmetic in EconomyClient.KnownCosmetics)
            {
                if (cosmetic.StartsWith("trophy_", StringComparison.Ordinal))
                {
                    kinds.Add(Decorations.TrophyKind(cosmetic));
                }
            }

            return kinds;
        }

        public bool ClearCheckCurrent =>
            Draft != null && Draft.ClearCheck != null && Draft.ClearCheck.MatchesLayout(Draft.ToVaultDef());

        public bool CanPublish => Draft != null && Issues.Count == 0 && ClearCheckCurrent && !Publishing;

        /// <summary>A PublishVault call is in flight.</summary>
        public bool Publishing { get; private set; }

        /// <summary>Only Bellows (gust) and Night Owl (gaze) care which way they face.</summary>
        public static bool CanRotate(ObjectType type)
        {
            return type == ObjectType.Bellows || type == ObjectType.NightOwl;
        }

        public void Bind(EditorAssets assets, EditorView view, EditorCamera camera, EditorInput input, EditorUI ui,
            GameObject[] hideDuringTest)
        {
            _assets = assets;
            _view = view;
            _camera = camera;
            _input = input;
            _ui = ui;
            _hideDuringTest = hideDuringTest;
        }

        private void Awake()
        {
            Application.targetFrameRate = 60;
            _store = new DraftStore(Application.persistentDataPath);
            _cameo = new GameObject("Builder Cameo").AddComponent<BuilderCameo>();
            _cameo.transform.SetParent(transform, false);
        }

        private void OnEnable()
        {
            _input.Gesture += OnGesture;
        }

        private void OnDisable()
        {
            _input.Gesture -= OnGesture;
        }

        private void Start()
        {
            ApplyLook();
            ShowDrafts();
        }

        // ---- Drafts -------------------------------------------------------------------------

        public void ShowDrafts()
        {
            _cameo.Skip();
            Draft = null;
            _ui.ShowDrafts(_store.List(), TemplateNames());
        }

        public void NewFromTemplate(int index)
        {
            TextAsset[] templates = Templates();
            VaultDraft d = VaultDraft.FromVault(VaultJson.Parse(templates[index].text), "Vault " + (_store.List().Count + 1));
            _store.Save(d);
            Open(d);
        }

        public void OpenDraft(string draftId)
        {
            Open(_store.Load(draftId));
        }

        public void DeleteDraft(string draftId)
        {
            _store.Delete(draftId);
            ShowDrafts();
        }

        private void Open(VaultDraft draft)
        {
            _cameo.Skip();
            Draft = draft;
            _undo.Clear();
            Tab = EditorTab.Defences;
            ClearSelection();
            _camera.Frame(draft.Width, draft.Height);
            _ui.ShowEditor();
            Refresh();
        }

        // ---- Tabs, selection and buttons ------------------------------------------------------

        public void SetTab(EditorTab tab)
        {
            Tab = tab;
            ClearSelection();
            Refresh();
        }

        public void SetRoomTool(RoomTool tool)
        {
            RoomTool = tool;
            Refresh();
        }

        public void Undo()
        {
            if (_undo.CanUndo)
            {
                Draft = _undo.Undo(Draft);
                AfterChange();
            }
        }

        public void Redo()
        {
            if (_undo.CanRedo)
            {
                Draft = _undo.Redo(Draft);
                AfterChange();
            }
        }

        /// <summary>
        /// The highest Vault Level a draft can be set to: the player's own (the server refuses
        /// anything higher), or any level in development builds for testing.
        /// </summary>
        public static int MaxDraftLevel => Debug.isDebugBuild ? VaultRules.MaxVaultLevel : EconomyClient.KnownVaultLevel;

        public void ChangeLevel(int delta)
        {
            Edit(d => d.SetLevel(Mathf.Clamp(d.Level + delta, VaultRules.MinVaultLevel, MaxDraftLevel)));
        }

        /// <summary>
        /// Grows the draft to its Vault Level's grid, then the builder blasts a doorway into the new
        /// space (decision B in docs/m10-plan.md). One undo step; a tap skips the cameo.
        /// </summary>
        public void Expand()
        {
            if (Draft == null || !Draft.CanExpand || _cameo.Playing)
            {
                return;
            }

            ExpandResult result = null;
            ClearSelection();
            Edit(d => (result = d.Expand()) != null);
            if (result == null)
            {
                return;
            }

            _camera.Frame(Draft.Width, Draft.Height);
            if (result.Doorway.Count < 2)
            {
                _ui.Toast("More room! Dig out the new space on the Rooms tab");
                return;
            }

            TilePos wall = result.Doorway[0];
            var room = new TilePos(2 * wall.X - result.Doorway[1].X, 2 * wall.Y - result.Doorway[1].Y);
            _closedDoorway = result.Doorway;
            Refresh();
            _cameo.Play(Draft, room, wall, _assets.Registry, _assets.HoardEdge,
                () =>
                {
                    _closedDoorway = null;
                    Refresh();
                },
                () => _ui.Toast("More room! Dig out the new space on the Rooms tab"));
        }

        public void RotateSelected()
        {
            if (SelectedObject != null)
            {
                Edit(d => d.Rotate(Selected));
            }
            else if (SelectedDecoration != null)
            {
                Edit(d => d.RotateDecoration(Selected));
            }
        }

        public void RemoveSelected()
        {
            if (SelectedObject != null || SelectedDecoration != null)
            {
                int id = Selected;
                ClearSelection();
                Edit(d => d.Delete(id));
            }
        }

        public void TogglePatrolEditing()
        {
            EditingPatrol = SelectedObject?.Type == ObjectType.NightOwl && !EditingPatrol;
            Refresh();
        }

        public void ClearPatrol()
        {
            if (SelectedObject != null)
            {
                Edit(d => d.ClearPatrol(Selected));
            }
        }

        public void Deselect()
        {
            ClearSelection();
            Refresh();
        }

        /// <summary>Moves the camera onto an issue's object (selecting it) or tile.</summary>
        public void FocusIssue(VaultIssue issue)
        {
            DraftObject o = issue.ObjectId != 0 ? Draft.Find(issue.ObjectId) : null;
            if (o != null)
            {
                _camera.FocusOn(o.Pos);
                if (Tab == EditorTab.Defences)
                {
                    Selected = o.Id;
                    EditingPatrol = false;
                }
            }
            else if (Draft.InBounds(issue.Tile))
            {
                _camera.FocusOn(issue.Tile);
            }

            Refresh();
        }

        /// <summary>Where the selection bar floats: just above the selected object, in screen pixels.</summary>
        public bool SelectionScreenPoint(out Vector2 screen)
        {
            screen = default;
            if (Moving >= 0)
            {
                return false;
            }

            DraftObject o = SelectedObject;
            DraftDecoration dec = SelectedDecoration;
            TilePos? at = o != null ? o.Pos : dec?.Pos;
            return at.HasValue && _camera.WorldToScreen(SimPose.TileCenter(at.Value) + Vector3.up * 1.2f, out screen);
        }

        private void ClearSelection()
        {
            Selected = -1;
            Moving = -1;
            MoveProblem = null;
            EditingPatrol = false;
            HoardPreview = null;
        }

        // ---- Shop drag ------------------------------------------------------------------------

        public void UpdatePlacement(ObjectType type, Vector2 screen)
        {
            if (_camera.ScreenToTile(screen, Draft, out TilePos tile))
            {
                _view.ShowGhost(type, Draft, tile, Draft.PlaceProblem(type, tile) == null);
            }
            else
            {
                _view.HideGhost();
            }
        }

        public void EndPlacement(ObjectType type, Vector2 screen, bool overUi)
        {
            _view.HideGhost();
            if (overUi || !_camera.ScreenToTile(screen, Draft, out TilePos tile))
            {
                return;
            }

            string problem = Draft.PlaceProblem(type, tile);
            if (problem != null)
            {
                _ui.Toast("Can't go there: " + problem);
                return;
            }

            Edit(d =>
            {
                Selected = d.Place(type, tile).Id;
                EditingPatrol = false;
                return true;
            });

            if (type == ObjectType.NightOwl)
            {
                _ui.Toast("Tap Patrol to give the owl a route");
            }
        }

        // ---- Decor tray drag -----------------------------------------------------------------------

        public void EndDecorPlacement(string kind, Vector2 screen, bool overUi)
        {
            if (overUi || !_camera.ScreenToTile(screen, Draft, out TilePos tile))
            {
                return;
            }

            string problem = Draft.DecorationPlaceProblem(kind, tile);
            if (problem != null)
            {
                _ui.Toast("Can't go there: " + problem);
                return;
            }

            Edit(d =>
            {
                Selected = d.PlaceDecoration(kind, tile).Id;
                return true;
            });
        }

        // ---- Gestures ---------------------------------------------------------------------------

        private void OnGesture(EditorGesture g)
        {
            if (Draft == null || _testing)
            {
                return;
            }

            if (_cameo.Playing)
            {
                _cameo.Skip(); // any touch skips the builder
                return;
            }

            if (g.Type == EditorGestureType.PinchPan)
            {
                _camera.Zoom(g.Scale);
                _camera.Pan(g.Delta);
                return;
            }

            bool onGrid = _camera.ScreenToTile(g.Position, Draft, out TilePos tile);
            if (Tab == EditorTab.Defences)
            {
                DefencesGesture(g, onGrid, tile);
            }
            else if (Tab == EditorTab.Decor)
            {
                DecorGesture(g, onGrid, tile);
            }
            else
            {
                RoomsGesture(g, onGrid, tile);
            }
        }

        /// <summary>Like Defences: tap selects a decoration, dragging one moves it, dragging floor pans.</summary>
        private void DecorGesture(EditorGesture g, bool onGrid, TilePos tile)
        {
            switch (g.Type)
            {
                case EditorGestureType.Tap:
                case EditorGestureType.LongPress:
                    DraftDecoration hit = onGrid ? Draft.DecorationAt(tile) : null;
                    Selected = hit != null ? hit.Id : -1;
                    Refresh();
                    break;

                case EditorGestureType.DragStart:
                    BeginStroke();
                    DraftDecoration target = onGrid ? Draft.DecorationAt(tile) : null;
                    if (target != null)
                    {
                        Selected = target.Id;
                        Moving = target.Id;
                        MoveProblem = null;
                        Refresh();
                    }
                    else
                    {
                        _panning = true;
                    }

                    break;

                case EditorGestureType.DragMove:
                    if (_panning)
                    {
                        _camera.Pan(g.Delta);
                    }
                    else if (Moving >= 0 && onGrid && Draft.MoveDecoration(Moving, tile))
                    {
                        _strokeChanged = true;
                        MoveProblem = VaultDraft.NewProblem(_strokeBefore, Draft);
                        Refresh();
                    }

                    break;

                case EditorGestureType.DragEnd:
                case EditorGestureType.DragCancel:
                    bool refused = Moving >= 0 && MoveProblem != null;
                    if (refused)
                    {
                        _ui.Toast("Can't go there: " + MoveProblem);
                    }

                    EndStroke(g.Type == EditorGestureType.DragEnd && !refused);
                    break;
            }
        }

        private void DefencesGesture(EditorGesture g, bool onGrid, TilePos tile)
        {
            switch (g.Type)
            {
                case EditorGestureType.Tap:
                case EditorGestureType.LongPress:
                    if (EditingPatrol)
                    {
                        if (onGrid && g.Type == EditorGestureType.Tap)
                        {
                            Edit(d => d.AddWaypoint(Selected, tile));
                        }

                        break;
                    }

                    DraftObject hit = onGrid ? Draft.ObjectAt(tile) : null;
                    Selected = hit != null ? hit.Id : -1;
                    Refresh();
                    break;

                case EditorGestureType.DragStart:
                    BeginStroke();
                    DraftObject target = onGrid && !EditingPatrol ? Draft.ObjectAt(tile) : null;
                    if (target != null)
                    {
                        Selected = target.Id;
                        Moving = target.Id;
                        MoveProblem = null;
                        Refresh();
                    }
                    else
                    {
                        _panning = true;
                    }

                    break;

                case EditorGestureType.DragMove:
                    if (_panning)
                    {
                        _camera.Pan(g.Delta);
                    }
                    else if (Moving >= 0 && onGrid && Draft.Move(Moving, tile))
                    {
                        _strokeChanged = true;
                        MoveProblem = VaultDraft.NewProblem(_strokeBefore, Draft);
                        Refresh();
                    }

                    break;

                case EditorGestureType.DragEnd:
                case EditorGestureType.DragCancel:
                    bool refused = Moving >= 0 && MoveProblem != null;
                    if (refused)
                    {
                        _ui.Toast("Can't go there: " + MoveProblem);
                    }

                    EndStroke(g.Type == EditorGestureType.DragEnd && !refused); // a refused drop springs back
                    break;
            }
        }

        private void RoomsGesture(EditorGesture g, bool onGrid, TilePos tile)
        {
            switch (g.Type)
            {
                case EditorGestureType.Tap:
                    if (!onGrid)
                    {
                        break;
                    }

                    if (RoomTool == RoomTool.Hoard)
                    {
                        _ui.Toast("Drag from one corner of the hoard room to the other");
                    }
                    else
                    {
                        Edit(d => ApplyRoomTool(d, tile));
                    }

                    break;

                case EditorGestureType.DragStart:
                    BeginStroke();
                    if (!onGrid)
                    {
                        _panning = true;
                    }
                    else if (RoomTool == RoomTool.Hoard)
                    {
                        _hoardStart = tile;
                        HoardPreview = Rect(tile, tile);
                        Refresh();
                    }
                    else
                    {
                        StrokeOver(tile);
                    }

                    break;

                case EditorGestureType.DragMove:
                    if (_panning)
                    {
                        _camera.Pan(g.Delta);
                    }
                    else if (onGrid && HoardPreview.HasValue)
                    {
                        HoardPreview = Rect(_hoardStart, tile);
                        Refresh();
                    }
                    else if (onGrid)
                    {
                        StrokeOver(tile);
                    }

                    break;

                case EditorGestureType.DragEnd:
                    if (HoardPreview.HasValue)
                    {
                        TileRect r = HoardPreview.Value;
                        HoardPreview = null;
                        _strokeChanged |= Draft.SetHoard(new TilePos(r.MinX, r.MinY), new TilePos(r.MaxX, r.MaxY));
                    }

                    EndStroke(true);
                    break;

                case EditorGestureType.DragCancel:
                    HoardPreview = null;
                    EndStroke(false);
                    break;
            }
        }

        private void StrokeOver(TilePos tile)
        {
            if (ApplyRoomTool(Draft, tile))
            {
                _strokeChanged = true;
                Refresh();
            }
        }

        private bool ApplyRoomTool(VaultDraft d, TilePos tile)
        {
            switch (RoomTool)
            {
                case RoomTool.Entrance: return d.SetEntrance(tile);
                case RoomTool.Wall: return d.PaintTile(tile, TileType.Wall);
                case RoomTool.Door: return d.PaintTile(tile, TileType.Door);
                case RoomTool.Floor: return d.PaintTile(tile, TileType.Floor);
                case RoomTool.Ice: return d.PaintTile(tile, TileType.Ice);
                default: return false;
            }
        }

        private void BeginStroke()
        {
            _panning = false;
            _strokeBefore = Draft.Clone();
            _strokeChanged = false;
        }

        /// <summary>A whole drag is one undo step; an abandoned one restores the draft.</summary>
        private void EndStroke(bool commit)
        {
            if (_strokeBefore != null && _strokeChanged)
            {
                if (commit)
                {
                    _undo.Record(_strokeBefore);
                    _store.Save(Draft);
                }
                else
                {
                    Draft = _strokeBefore;
                }
            }

            _strokeBefore = null;
            _strokeChanged = false;
            _panning = false;
            Moving = -1;
            MoveProblem = null;
            Refresh();
        }

        private static TileRect Rect(TilePos a, TilePos b)
        {
            return new TileRect(Mathf.Min(a.X, b.X), Mathf.Min(a.Y, b.Y), Mathf.Max(a.X, b.X), Mathf.Max(a.Y, b.Y));
        }

        // ---- Edits ----------------------------------------------------------------------------

        private delegate bool DraftEdit(VaultDraft d);

        private void Edit(DraftEdit edit)
        {
            VaultDraft before = Draft.Clone();
            if (edit(Draft))
            {
                _undo.Record(before);
                AfterChange();
            }
            else
            {
                Refresh();
            }
        }

        private void AfterChange()
        {
            _store.Save(Draft);
            Refresh();
        }

        public void Refresh()
        {
            if (Draft == null)
            {
                return;
            }

            if (Selected >= 0 && Draft.Find(Selected) == null && Draft.FindDecoration(Selected) == null)
            {
                ClearSelection();
            }

            // The draft is built and tested with the levels the server will stamp on it.
            Draft.DefenceLevels = OwnedDefences.For(EconomyClient.KnownDefences, Draft.Level);
            Issues = VaultValidator.Validate(Draft.ToVaultDef());
            _view.Build(Drawn(), Issues, new EditorView.Marks
            {
                Selected = Selected,
                Moving = Moving,
                MovingOk = MoveProblem == null,
                HoardPreview = HoardPreview,
            });
            _ui.UpdateEditor(this);
        }

        /// <summary>The draft as drawn: during the builder's walk the new doorway is still wall.</summary>
        private VaultDraft Drawn()
        {
            if (_closedDoorway == null)
            {
                return Draft;
            }

            VaultDraft drawn = Draft.Clone();
            foreach (TilePos p in _closedDoorway)
            {
                drawn.PaintTile(p, TileType.Wall);
            }

            return drawn;
        }

        // ---- Test and publish ---------------------------------------------------------------------

        public void Test()
        {
            if (Draft == null || _testing)
            {
                return;
            }

            ClearSelection();
            _store.Save(Draft);
            VaultDef vault = Draft.ToVaultDef();
            RaidLaunch.Begin(new RaidLaunch.Request
            {
                Vault = vault,
                Params = new RaidParams(EconomyClient.Config.Raid.TicksFor(Draft.Level)),
                RunFinished = OnTestRun,
                Done = () => StartCoroutine(ReturnFromTest()),
            });
            StartCoroutine(LoadTest());
        }

        private IEnumerator LoadTest()
        {
            _testing = true;
            SetEditorVisible(false);
            AsyncOperation load = SceneManager.LoadSceneAsync("Raid", LoadSceneMode.Additive);
            yield return load;
            SceneManager.SetActiveScene(SceneManager.GetSceneByName("Raid"));
        }

        private IEnumerator ReturnFromTest()
        {
            SceneManager.SetActiveScene(gameObject.scene);
            yield return SceneManager.UnloadSceneAsync("Raid");
            SetEditorVisible(true);
            ApplyLook();
            _testing = false;
            Refresh();
        }

        /// <summary>An escape with at least 1 star on the current layout becomes the clear-check.</summary>
        private void OnTestRun(RaidRunReport report)
        {
            _testRuns++;
            RaidResult r = report.Result;
            if (r.Outcome == Outcome.Escaped && r.Stars >= 1)
            {
                Draft.ClearCheck = ClearCheck.FromRun(report.Vault, report.Seed, report.Log, r, report.Params);
                _store.Save(Draft);
                _ui.Toast("Clear-check passed: " + r.Stars + " star" + (r.Stars == 1 ? "" : "s"));
            }
            else
            {
                _ui.Toast(r.Outcome == Outcome.Escaped ? "Escaped with no stars: reach the hoard" : "Not escaped: try again");
            }
        }

        public void Publish()
        {
            if (!CanPublish)
            {
                return;
            }

            VaultDef vault = Draft.ToVaultDef();
            ClearCheckResult check = ClearCheck.Verify(vault, Draft.ClearCheck);
            if (!check.Ok)
            {
                _ui.Toast("Clear-check failed: " + check.Reason);
                return;
            }

            PublishToServer(Draft, VaultJson.WriteDocument(vault, Draft.ClearCheck));
        }

        /// <summary>
        /// The vault goes live only if the server's own checks pass: building rules, then a
        /// re-simulation of the clear-check run. Until it answers, nothing is published.
        /// </summary>
        private async void PublishToServer(VaultDraft draft, string document)
        {
            Publishing = true;
            _ui.Toast("Publishing: the server is replaying your clear-check...", 30f);
            Refresh();
            try
            {
                PublishReply reply = await _backend.PublishVaultAsync(document);
                if (reply.Ok)
                {
                    _store.MarkPublished(draft, reply.VaultId, reply.Version);
                    VaultDef published = draft.ToVaultDef();
                    int traps = 0;
                    for (int i = 0; i < published.ObjectCount; i++)
                    {
                        ObjectType t = published.GetObject(i).Type;
                        traps += ObjectTypes.IsTrap(t) || ObjectTypes.IsGuardian(t) ? 1 : 0;
                    }

                    Telemetry.VaultPublished(traps, draft.BuildPoints, _testRuns);
                    _testRuns = 0;
                    _ui.Toast("Your vault is live (version " + reply.Version + ")");
                }
                else
                {
                    string why = reply.Issues != null && reply.Issues.Count > 0 ? reply.Issues[0] : reply.Reason;
                    _ui.Toast("Not published: " + why, 5f);
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                _ui.Toast("Couldn't reach the server, so your vault wasn't published. Try again.", 5f);
            }
            finally
            {
                Publishing = false;
                Refresh();
            }
        }

        /// <summary>Dev check: re-simulate the stored clear-check and compare hashes.</summary>
        public string VerifyClearCheck()
        {
            if (Draft?.ClearCheck == null)
            {
                return "No clear-check yet";
            }

            ClearCheckResult r = ClearCheck.Verify(Draft.ToVaultDef(), Draft.ClearCheck);
            string replay = r.Replay.HasValue ? r.Replay.Value.HashHex : "-";
            return (r.Ok ? "MATCH " : "FAIL ") + "stored " + Draft.ClearCheck.Hash + " replay " + replay +
                (r.Ok ? "" : " (" + r.Reason + ")");
        }

        public void BackToRaids()
        {
            SceneManager.LoadScene("Raid");
        }

        // ---- Helpers ----------------------------------------------------------------------------

        private void SetEditorVisible(bool visible)
        {
            _input.enabled = visible;
            _ui.SetHidden(!visible);
            foreach (GameObject go in _hideDuringTest)
            {
                // Never deactivate the UI: re-enabling a UIDocument rebuilds its tree and drops every
                // button callback EditorUI registered, which left the editor dead after a Test.
                if (go != _ui.gameObject)
                {
                    go.SetActive(visible);
                }
            }
        }

        /// <summary>The editor shows the whole vault lit (no raid visibility mask).</summary>
        private void ApplyLook()
        {
            Shader.SetGlobalVector(GridId, Vector4.zero);
            Shader.SetGlobalFloat(BoostId, _assets.Tuning.LitBoost);
        }

        private static TextAsset[] Templates()
        {
            TextAsset[] t = Resources.LoadAll<TextAsset>("VaultTemplates");
            System.Array.Sort(t, (a, b) => string.CompareOrdinal(a.name, b.name));
            return t;
        }

        private static List<string> TemplateNames()
        {
            var names = new List<string>();
            foreach (TextAsset t in Templates())
            {
                names.Add(t.name);
            }

            return names;
        }
    }
}
