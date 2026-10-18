using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Wick.Sim;

namespace Wick.Game
{
    /// <summary>
    /// The editor's UI Toolkit document: drafts list, top bar (level, budget, problems), the
    /// Defences shop (drag cards onto the grid), the Rooms tools, a selection bar that floats
    /// above the selected item, and Undo / Redo / Test / Publish.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class EditorUI : MonoBehaviour
    {
        [SerializeField] private VaultEditorController _controller;
        [SerializeField] private EditorInput _input;

        /// <summary>Panel pixels a finger must move off a shop card before it becomes a drag.</summary>
        private const float CardDragPx = 18f;

        private static readonly (RoomTool tool, string label)[] RoomTools =
        {
            (RoomTool.Wall, "Wall"), (RoomTool.Floor, "Floor"), (RoomTool.Door, "Door"),
            (RoomTool.Entrance, "Entrance"), (RoomTool.Hoard, "Hoard room"),
        };

        private static readonly ObjectType[] ShopItems =
        {
            ObjectType.Treasure, ObjectType.DripPipe, ObjectType.SpikeFloor, ObjectType.Bellows, ObjectType.Crate,
            ObjectType.Brazier, ObjectType.SnufferBell, ObjectType.NightOwl, ObjectType.WaxMoths,
        };

        private VisualElement _root;
        private VisualElement _drafts;
        private VisualElement _editor;
        private VisualElement _issuesPanel;
        private VisualElement _selectionBar;
        private VisualElement _shop;
        private VisualElement _roomTools;
        private Label _toast;
        private float _toastUntil;
        private readonly List<Button> _roomButtons = new List<Button>();
        private readonly List<VisualElement> _cards = new List<VisualElement>();
        private bool _ready;

        public void Bind(VaultEditorController controller, EditorInput input)
        {
            _controller = controller;
            _input = input;
        }

        public static string Name(ObjectType type)
        {
            switch (type)
            {
                case ObjectType.DripPipe: return "Drip Pipe";
                case ObjectType.SpikeFloor: return "Spikes";
                case ObjectType.SnufferBell: return "Snuffer Bell";
                case ObjectType.NightOwl: return "Night Owl";
                case ObjectType.WaxMoths: return "Wax Moths";
                default: return type.ToString();
            }
        }

        private void Start()
        {
            EnsureReady();
        }

        private void EnsureReady()
        {
            if (_ready)
            {
                return;
            }

            _ready = true;
            _root = GetComponent<UIDocument>().rootVisualElement;
            _drafts = _root.Q("drafts");
            _editor = _root.Q("editor");
            _issuesPanel = _root.Q("issues-panel");
            _selectionBar = _root.Q("selection-bar");
            _shop = _root.Q("shop");
            _roomTools = _root.Q("room-tools");
            _toast = _root.Q<Label>("toast");

            _root.Q<Button>("back-raids").clicked += () => _controller.BackToRaids();
            _root.Q<Button>("to-drafts").clicked += () => _controller.ShowDrafts();
            _root.Q<Button>("level-down").clicked += () => _controller.ChangeLevel(-1);
            _root.Q<Button>("level-up").clicked += () => _controller.ChangeLevel(1);
            _root.Q<Button>("issues-button").clicked += () => Show(_issuesPanel, !Visible(_issuesPanel));
            _root.Q<Button>("issues-close").clicked += () => Show(_issuesPanel, false);
            _root.Q<Button>("tab-defences").clicked += () => _controller.SetTab(EditorTab.Defences);
            _root.Q<Button>("tab-rooms").clicked += () => _controller.SetTab(EditorTab.Rooms);
            _root.Q<Button>("undo").clicked += () => _controller.Undo();
            _root.Q<Button>("redo").clicked += () => _controller.Redo();
            _root.Q<Button>("test").clicked += () => _controller.Test();
            _root.Q<Button>("publish").clicked += () => _controller.Publish();
            _root.Q<Button>("sel-rotate").clicked += () => _controller.RotateSelected();
            _root.Q<Button>("sel-patrol").clicked += () => _controller.TogglePatrolEditing();
            _root.Q<Button>("sel-clear").clicked += () => _controller.ClearPatrol();
            _root.Q<Button>("sel-remove").clicked += () => _controller.RemoveSelected();
            Button verify = _root.Q<Button>("verify");
            verify.clicked += () => Toast(_controller.VerifyClearCheck(), 6f);
            Show(verify, Debug.isDebugBuild);

            foreach ((RoomTool tool, string label) in RoomTools)
            {
                RoomTool t = tool;
                var b = new Button(() => _controller.SetRoomTool(t)) { text = label, userData = tool };
                b.AddToClassList("tool");
                _roomTools.Add(b);
                _roomButtons.Add(b);
            }

            foreach (ObjectType type in ShopItems)
            {
                _shop.Add(Card(type));
            }

            _input.IsOverUi = IsOverUi;
            Show(_drafts, false);
            Show(_editor, false);
            Show(_issuesPanel, false);
            Show(_selectionBar, false);
        }

        // ---- Screens ------------------------------------------------------------------------

        public void ShowDrafts(List<DraftStore.Entry> drafts, List<string> templates)
        {
            EnsureReady();
            Show(_drafts, true);
            Show(_editor, false);

            VisualElement list = _root.Q("draft-list");
            list.Clear();
            if (drafts.Count == 0)
            {
                list.Add(new Label("No vaults yet: start from a template.") { name = "empty" });
            }

            foreach (DraftStore.Entry e in drafts)
            {
                string id = e.DraftId;
                var row = new VisualElement();
                row.AddToClassList("draft-row");
                var open = new Button(() => _controller.OpenDraft(id)) { text = e.Name };
                open.AddToClassList("wide-button");
                var delete = new Button(() => _controller.DeleteDraft(id)) { text = "Delete" };
                delete.AddToClassList("small-button");
                row.Add(open);
                row.Add(delete);
                list.Add(row);
            }

            VisualElement templateList = _root.Q("template-list");
            templateList.Clear();
            for (int i = 0; i < templates.Count; i++)
            {
                int index = i;
                var b = new Button(() => _controller.NewFromTemplate(index)) { text = "New: " + Pretty(templates[i]) };
                b.AddToClassList("wide-button");
                templateList.Add(b);
            }
        }

        public void ShowEditor()
        {
            EnsureReady();
            Show(_drafts, false);
            Show(_editor, true);
            Show(_issuesPanel, false);
        }

        /// <summary>Hides the whole editor UI (during a Test raid) without rebuilding it.</summary>
        public void SetHidden(bool hidden)
        {
            EnsureReady();
            Show(_root, !hidden);
        }

        public void Toast(string text, float seconds = 2.5f)
        {
            EnsureReady();
            _toast.text = text;
            _toastUntil = Time.unscaledTime + seconds;
            Show(_toast, true);
        }

        private void Update()
        {
            if (!_ready)
            {
                return;
            }

            if (Visible(_toast) && Time.unscaledTime > _toastUntil)
            {
                Show(_toast, false);
            }

            PlaceSelectionBar();
        }

        /// <summary>Keeps the selection bar just above the selected item as the camera moves.</summary>
        private void PlaceSelectionBar()
        {
            Vector2 screen = default;
            bool show = Visible(_editor) && _controller.Draft != null && _root.panel != null &&
                _controller.SelectionScreenPoint(out screen);
            Show(_selectionBar, show);
            if (!show)
            {
                return;
            }

            Vector2 p = RuntimePanelUtils.ScreenToPanel(_root.panel, new Vector2(screen.x, Screen.height - screen.y));
            float w = _selectionBar.resolvedStyle.width;
            float h = _selectionBar.resolvedStyle.height;
            if (float.IsNaN(w) || float.IsNaN(h))
            {
                w = h = 0f; // first frame after showing: laid out next frame
            }

            float maxX = Mathf.Max(0f, _root.layout.width - w - 12f);
            _selectionBar.style.left = Mathf.Clamp(p.x - w * 0.5f, 12f, maxX);
            _selectionBar.style.top = Mathf.Max(260f, p.y - h);
        }

        /// <summary>Refreshes everything that depends on the draft.</summary>
        public void UpdateEditor(VaultEditorController c)
        {
            EnsureReady();
            VaultDraft d = c.Draft;
            bool defences = c.Tab == EditorTab.Defences;
            _root.Q<Label>("vault-name").text = d.Name;
            _root.Q<Label>("level-label").text = "Lv " + d.Level;

            int points = d.BuildPoints;
            int budget = VaultRules.Budget(d.Level);
            _root.Q<Label>("budget-label").text = "Build points " + points + " / " + budget;
            VisualElement fill = _root.Q("budget-fill");
            fill.style.width = Length.Percent(Mathf.Clamp01(points / (float)budget) * 100f);
            fill.EnableInClassList("over", points > budget);

            var issuesButton = _root.Q<Button>("issues-button");
            int n = c.Issues.Count;
            issuesButton.text = n == 0 ? "Ready" : n + (n == 1 ? " problem" : " problems");
            issuesButton.EnableInClassList("bad", n > 0);
            VisualElement issueList = _root.Q("issue-list");
            issueList.Clear();
            foreach (VaultIssue issue in c.Issues)
            {
                VaultIssue i = issue;
                var row = new Button(() =>
                {
                    Show(_issuesPanel, false);
                    _controller.FocusIssue(i);
                }) { text = issue.Message };
                row.AddToClassList("issue-row");
                issueList.Add(row);
            }

            if (n == 0)
            {
                Show(_issuesPanel, false);
            }

            _root.Q("tab-defences").EnableInClassList("selected", defences);
            _root.Q("tab-rooms").EnableInClassList("selected", !defences);
            Show(_shop, defences);
            Show(_roomTools, !defences);
            foreach (Button b in _roomButtons)
            {
                b.EnableInClassList("selected", (RoomTool)b.userData == c.RoomTool);
            }

            foreach (VisualElement card in _cards)
            {
                UpdateCard(card, d);
            }

            UpdateSelectionBar(c);

            _root.Q<Button>("undo").SetEnabled(c.CanUndo);
            _root.Q<Button>("redo").SetEnabled(c.CanRedo);
            _root.Q<Button>("publish").SetEnabled(c.CanPublish);

            string status;
            if (d.ClearCheck == null)
            {
                status = "Not tested yet: escape your vault with 1+ star to publish";
            }
            else if (!c.ClearCheckCurrent)
            {
                status = "Changed since your test: test again to publish";
            }
            else
            {
                status = "Clear-check passed (" + d.ClearCheck.Stars + " star" + (d.ClearCheck.Stars == 1 ? "" : "s") + ")";
            }

            if (!string.IsNullOrEmpty(d.PublishedVaultId))
            {
                status += "  |  published v" + d.PublishedVersion;
            }

            _root.Q<Label>("status").text = status;
            _root.Q<Label>("hint").text = Hint(c);
        }

        private void UpdateSelectionBar(VaultEditorController c)
        {
            DraftObject o = c.SelectedObject;
            if (o == null)
            {
                return;
            }

            bool owl = o.Type == ObjectType.NightOwl;
            _root.Q<Label>("selection-name").text = c.EditingPatrol
                ? "Tap tiles to add patrol points"
                : Name(o.Type) + (VaultRules.Cost(o.Type) > 0 ? ", " + VaultRules.Cost(o.Type) + " pts" : "");
            Show(_root.Q("sel-rotate"), !c.EditingPatrol && VaultEditorController.CanRotate(o.Type));
            Show(_root.Q("sel-patrol"), owl);
            _root.Q<Button>("sel-patrol").text = c.EditingPatrol ? "Done" : "Patrol";
            Show(_root.Q("sel-clear"), owl && c.EditingPatrol);
            Show(_root.Q("sel-remove"), !c.EditingPatrol);
        }

        // ---- Shop ---------------------------------------------------------------------------

        private VisualElement Card(ObjectType type)
        {
            var card = new VisualElement { userData = type };
            card.AddToClassList("card");
            card.Add(new Label(Name(type)) { name = "card-name", pickingMode = PickingMode.Ignore });
            var sub = new Label { name = "card-sub", pickingMode = PickingMode.Ignore };
            sub.AddToClassList("card-sub");
            card.Add(sub);

            bool down = false;
            bool dragging = false;
            Vector2 start = default;
            card.RegisterCallback<PointerDownEvent>(e =>
            {
                down = true;
                dragging = false;
                start = e.position;
                card.CapturePointer(e.pointerId);
            });
            card.RegisterCallback<PointerMoveEvent>(e =>
            {
                if (!down)
                {
                    return;
                }

                if (!dragging && Vector2.Distance(e.position, start) >= CardDragPx)
                {
                    string block = _controller.Draft.ShopBlock(type);
                    if (block != null)
                    {
                        down = false;
                        card.ReleasePointer(e.pointerId);
                        Toast(Name(type) + ": " + block);
                        return;
                    }

                    dragging = true;
                    _controller.Deselect();
                    card.AddToClassList("dragging");
                }

                if (dragging)
                {
                    _controller.UpdatePlacement(type, PanelToScreen(e.position));
                }
            });
            card.RegisterCallback<PointerUpEvent>(e =>
            {
                if (!down)
                {
                    return;
                }

                down = false;
                card.ReleasePointer(e.pointerId);
                card.RemoveFromClassList("dragging");
                if (dragging)
                {
                    dragging = false;
                    Vector2 screen = PanelToScreen(e.position);
                    _controller.EndPlacement(type, screen, IsOverUi(screen));
                }
                else
                {
                    string block = _controller.Draft.ShopBlock(type);
                    Toast(block != null ? Name(type) + ": " + block : Describe(type));
                }
            });
            card.RegisterCallback<PointerCaptureOutEvent>(_ =>
            {
                down = false;
                card.RemoveFromClassList("dragging");
                if (dragging)
                {
                    dragging = false;
                    _controller.EndPlacement(type, Vector2.zero, true);
                }
            });

            _cards.Add(card);
            return card;
        }

        private static void UpdateCard(VisualElement card, VaultDraft d)
        {
            var type = (ObjectType)card.userData;
            int cost = VaultRules.Cost(type);
            int required = VaultRules.RequiredLevel(type);
            string sub;
            if (required > d.Level)
            {
                sub = "Lv " + required;
            }
            else if (type == ObjectType.Treasure)
            {
                sub = d.Count(type) + " / " + VaultRules.MaxTreasurePiles(d.Level);
            }
            else if (type == ObjectType.Brazier)
            {
                sub = d.Count(type) + " / " + VaultRules.BrazierLimit(d.Level);
            }
            else
            {
                sub = cost + " pts";
            }

            card.Q<Label>("card-sub").text = sub;
            card.EnableInClassList("locked", required > d.Level);
            card.EnableInClassList("blocked", d.ShopBlock(type) != null);
        }

        private static string Describe(ObjectType type)
        {
            switch (type)
            {
                case ObjectType.Treasure: return "Treasure: drag into the hoard room";
                case ObjectType.Brazier: return "Brazier: drag onto a wall next to floor";
                case ObjectType.Bellows: return "Bellows: drag onto a wall; it blows into the room";
                case ObjectType.Crate: return "Crate: blocks the way and gives cover";
                default: return Name(type) + ": drag it onto the vault";
            }
        }

        /// <summary>Panel coordinates (y down) to screen pixels (y up).</summary>
        private Vector2 PanelToScreen(Vector2 panel)
        {
            float scale = Screen.width / Mathf.Max(1f, _root.layout.width);
            return new Vector2(panel.x * scale, Screen.height - panel.y * scale);
        }

        /// <summary>True when a screen point (pixels, y up) is on UI that should take the touch.</summary>
        public bool IsOverUi(Vector2 screenPosition)
        {
            IPanel panel = _root?.panel;
            if (panel == null)
            {
                return false;
            }

            Vector2 panelPos = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(screenPosition.x, Screen.height - screenPosition.y));
            for (VisualElement e = panel.Pick(panelPos); e != null; e = e.parent)
            {
                if (e.ClassListContains("blocks-input") && Visible(e))
                {
                    return true;
                }
            }

            return false;
        }

        // ---- Helpers ----------------------------------------------------------------------------

        private static string Hint(VaultEditorController c)
        {
            if (c.Tab == EditorTab.Defences)
            {
                if (c.EditingPatrol)
                {
                    return "Tap tiles to add patrol points; tap the last one again to remove it. Done when finished.";
                }

                return c.SelectedObject != null
                    ? "Drag it to move. Tap empty floor to deselect."
                    : "Drag from the shop to place. Drag an item to move it. Drag the floor to look around.";
            }

            switch (c.RoomTool)
            {
                case RoomTool.Entrance: return "Tap a tile to move the entrance. Two fingers to pan.";
                case RoomTool.Hoard: return "Drag from one corner of the hoard room to the other. Two fingers to pan.";
                default: return "Tap or drag to paint. Two fingers to pan and zoom.";
            }
        }

        private static string Pretty(string templateName)
        {
            int dash = templateName.IndexOf('-');
            string name = dash >= 0 ? templateName.Substring(dash + 1) : templateName;
            name = name.Replace('-', ' ');
            return name.Length > 0 ? char.ToUpperInvariant(name[0]) + name.Substring(1) : name;
        }

        private static void Show(VisualElement e, bool visible)
        {
            e.EnableInClassList("hidden", !visible);
        }

        private static bool Visible(VisualElement e)
        {
            return !e.ClassListContains("hidden");
        }
    }
}
