using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Wick.Sim;

namespace Wick.Game
{
    public enum EditorTool
    {
        Floor,
        Wall,
        Door,
        Pit,
        Ice,
        Entrance,
        Treasure,
        Hoard,
        Select,
    }

    /// <summary>
    /// The vault editor: owns the draft, applies gestures and tray drags as edits (each one an
    /// undo step, autosaved), re-validates after every change, and runs Test and Publish.
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

        private readonly UndoStack<VaultDraft> _undo = new UndoStack<VaultDraft>(d => d.Clone());
        private DraftStore _store;
        private VaultDraft _strokeBefore;
        private bool _strokeChanged;
        private int _movingId = -1;
        private bool _panning;
        private bool _testing;

        public VaultDraft Draft { get; private set; }
        public List<VaultIssue> Issues { get; private set; } = new List<VaultIssue>();
        public EditorTool Tool { get; private set; } = EditorTool.Select;
        public int SelectedOwl { get; private set; } = -1;
        public bool EditingPatrol { get; private set; }
        public TilePos? HoardCorner { get; private set; }
        public bool CanUndo => _undo.CanUndo;
        public bool CanRedo => _undo.CanRedo;
        public DraftStore Store => _store;

        public bool ClearCheckCurrent =>
            Draft != null && Draft.ClearCheck != null && Draft.ClearCheck.MatchesLayout(Draft.ToVaultDef());

        public bool CanPublish => Draft != null && Issues.Count == 0 && ClearCheckCurrent;

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
            Draft = draft;
            _undo.Clear();
            Tool = EditorTool.Select;
            SelectedOwl = -1;
            EditingPatrol = false;
            HoardCorner = null;
            _camera.Frame(draft.Width, draft.Height);
            _ui.ShowEditor();
            Refresh();
        }

        // ---- Tools and buttons ----------------------------------------------------------------

        public void SetTool(EditorTool tool)
        {
            Tool = tool;
            HoardCorner = null;
            EditingPatrol = false;
            if (tool != EditorTool.Select)
            {
                SelectedOwl = -1;
            }

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

        public void ChangeLevel(int delta)
        {
            Edit(d => d.SetLevel(Mathf.Clamp(d.Level + delta, 1, 3)));
        }

        public void RotateSelected()
        {
            if (SelectedOwl >= 0)
            {
                Edit(d => d.Rotate(SelectedOwl));
            }
        }

        public void TogglePatrolEditing()
        {
            EditingPatrol = SelectedOwl >= 0 && !EditingPatrol;
            Refresh();
        }

        public void ClearPatrol()
        {
            if (SelectedOwl >= 0)
            {
                Edit(d => d.ClearPatrol(SelectedOwl));
            }
        }

        public void Deselect()
        {
            SelectedOwl = -1;
            EditingPatrol = false;
            Refresh();
        }

        // ---- Tray drag ------------------------------------------------------------------------

        public void UpdatePlacement(ObjectType type, Vector2 screen)
        {
            if (_camera.ScreenToTile(screen, Draft, out TilePos tile))
            {
                _view.ShowGhost(type, Draft, tile, PlacementValid(type, tile));
            }
            else
            {
                _view.HideGhost();
            }
        }

        public void EndPlacement(ObjectType type, Vector2 screen, bool overUi)
        {
            _view.HideGhost();
            if (!overUi && _camera.ScreenToTile(screen, Draft, out TilePos tile))
            {
                // Invalid placements are allowed and shown in red, so the builder can see why.
                Edit(d =>
                {
                    DraftObject o = d.Place(type, tile);
                    if (type == ObjectType.NightOwl)
                    {
                        SelectedOwl = o.Id;
                    }

                    return true;
                });
            }
        }

        /// <summary>Would placing this object here add a problem about it?</summary>
        private bool PlacementValid(ObjectType type, TilePos tile)
        {
            VaultDraft trial = Draft.Clone();
            DraftObject o = trial.Place(type, tile);
            foreach (VaultIssue issue in VaultValidator.Validate(trial.ToVaultDef()))
            {
                if (issue.ObjectId == o.Id || issue.Code == VaultIssueCode.OverBudget)
                {
                    return false;
                }
            }

            return true;
        }

        // ---- Gestures ---------------------------------------------------------------------------

        private void OnGesture(EditorGesture g)
        {
            if (Draft == null || _testing)
            {
                return;
            }

            if (g.Type == EditorGestureType.PinchPan)
            {
                _camera.Zoom(g.Scale);
                _camera.Pan(g.Delta);
                return;
            }

            bool onGrid = _camera.ScreenToTile(g.Position, Draft, out TilePos tile);
            switch (g.Type)
            {
                case EditorGestureType.Tap:
                    if (onGrid)
                    {
                        Tap(tile);
                    }

                    break;

                case EditorGestureType.LongPress:
                    DraftObject target = onGrid ? Draft.ObjectAt(tile) : null;
                    if (target != null)
                    {
                        if (target.Id == SelectedOwl)
                        {
                            SelectedOwl = -1;
                            EditingPatrol = false;
                        }

                        Edit(d => d.Delete(target.Id));
                        _ui.Toast("Deleted " + target.Type);
                    }

                    break;

                case EditorGestureType.DragStart:
                    StartDrag(onGrid, tile);
                    break;

                case EditorGestureType.DragMove:
                    if (_panning)
                    {
                        _camera.Pan(g.Delta);
                    }
                    else if (onGrid)
                    {
                        DragOver(tile);
                    }

                    break;

                case EditorGestureType.DragEnd:
                    EndDrag(true);
                    break;

                case EditorGestureType.DragCancel:
                    EndDrag(false);
                    break;
            }
        }

        private void Tap(TilePos tile)
        {
            if (EditingPatrol && SelectedOwl >= 0)
            {
                Edit(d => d.AddWaypoint(SelectedOwl, tile));
                return;
            }

            switch (Tool)
            {
                case EditorTool.Entrance:
                    Edit(d => d.SetEntrance(tile));
                    break;

                case EditorTool.Treasure:
                    Edit(d => d.ToggleTreasure(tile));
                    break;

                case EditorTool.Hoard:
                    if (HoardCorner == null)
                    {
                        HoardCorner = tile;
                        _ui.Toast("Now tap the opposite corner");
                        Refresh();
                    }
                    else
                    {
                        TilePos first = HoardCorner.Value;
                        HoardCorner = null;
                        Edit(d => d.SetHoard(first, tile));
                    }

                    break;

                case EditorTool.Select:
                    DraftObject o = Draft.ObjectAt(tile);
                    if (o == null)
                    {
                        Deselect();
                    }
                    else if (o.Type == ObjectType.NightOwl && o.Id != SelectedOwl)
                    {
                        SelectedOwl = o.Id;
                        EditingPatrol = false;
                        Refresh();
                    }
                    else
                    {
                        Edit(d => d.Rotate(o.Id));
                    }

                    break;

                default:
                    Edit(d => d.PaintTile(tile, ToTile(Tool)));
                    break;
            }
        }

        private void StartDrag(bool onGrid, TilePos tile)
        {
            _panning = false;
            _movingId = -1;
            _strokeBefore = Draft.Clone();
            _strokeChanged = false;

            if (IsPaintTool(Tool) && onGrid)
            {
                _strokeChanged |= Draft.PaintTile(tile, ToTile(Tool));
                RefreshIfChanged();
            }
            else if (Tool == EditorTool.Select && onGrid && Draft.ObjectAt(tile) != null)
            {
                _movingId = Draft.ObjectAt(tile).Id;
            }
            else
            {
                _panning = true;
            }
        }

        private void DragOver(TilePos tile)
        {
            if (IsPaintTool(Tool))
            {
                if (Draft.PaintTile(tile, ToTile(Tool)))
                {
                    _strokeChanged = true;
                    Refresh();
                }
            }
            else if (_movingId >= 0 && Draft.Move(_movingId, tile))
            {
                _strokeChanged = true;
                Refresh();
            }
        }

        private void EndDrag(bool commit)
        {
            if (_strokeBefore != null && _strokeChanged)
            {
                if (commit)
                {
                    _undo.Record(_strokeBefore); // a whole stroke is one undo step
                    AfterChange();
                }
                else
                {
                    Draft = _strokeBefore;
                    Refresh();
                }
            }

            _strokeBefore = null;
            _strokeChanged = false;
            _movingId = -1;
            _panning = false;
        }

        private void RefreshIfChanged()
        {
            if (_strokeChanged)
            {
                Refresh();
            }
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

            if (SelectedOwl >= 0 && Draft.Find(SelectedOwl) == null)
            {
                SelectedOwl = -1;
                EditingPatrol = false;
            }

            Issues = VaultValidator.Validate(Draft.ToVaultDef());
            _view.Build(Draft, Issues, SelectedOwl, HoardCorner);
            _ui.UpdateEditor(this);
        }

        // ---- Test and publish ---------------------------------------------------------------------

        public void Test()
        {
            if (Draft == null || _testing)
            {
                return;
            }

            _store.Save(Draft);
            VaultDef vault = Draft.ToVaultDef();
            RaidLaunch.Begin(new RaidLaunch.Request
            {
                Vault = vault,
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
            RaidResult r = report.Result;
            if (r.Outcome == Outcome.Escaped && r.Stars >= 1)
            {
                Draft.ClearCheck = ClearCheck.FromRun(report.Vault, report.Seed, report.Log, r);
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

            _store.Publish(Draft);
            _ui.Toast("Published " + Draft.PublishedVaultId + " v" + Draft.PublishedVersion + " (local)");
            Refresh();
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
            foreach (GameObject go in _hideDuringTest)
            {
                go.SetActive(visible);
            }
        }

        /// <summary>The editor shows the whole vault lit (no raid visibility mask).</summary>
        private void ApplyLook()
        {
            Shader.SetGlobalVector(GridId, Vector4.zero);
            Shader.SetGlobalFloat(BoostId, _assets.Tuning.LitBoost);
        }

        private static bool IsPaintTool(EditorTool tool)
        {
            return tool <= EditorTool.Ice;
        }

        private static TileType ToTile(EditorTool tool)
        {
            switch (tool)
            {
                case EditorTool.Wall: return TileType.Wall;
                case EditorTool.Door: return TileType.Door;
                case EditorTool.Pit: return TileType.Pit;
                case EditorTool.Ice: return TileType.Ice;
                default: return TileType.Floor;
            }
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
