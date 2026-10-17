using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Wick.Sim;

namespace Wick.Game
{
    /// <summary>
    /// The editor's UI Toolkit document: drafts list, top bar (level, budget, issues), paint
    /// tools, the trap tray (drag onto the grid), the owl panel and Undo / Redo / Test / Publish.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class EditorUI : MonoBehaviour
    {
        [SerializeField] private VaultEditorController _controller;
        [SerializeField] private EditorInput _input;

        private static readonly (EditorTool tool, string label)[] Tools =
        {
            (EditorTool.Select, "Select"), (EditorTool.Floor, "Floor"), (EditorTool.Wall, "Wall"),
            (EditorTool.Door, "Door"), (EditorTool.Pit, "Pit*"), (EditorTool.Ice, "Ice*"),
            (EditorTool.Entrance, "Entrance"), (EditorTool.Treasure, "Treasure"), (EditorTool.Hoard, "Hoard"),
        };

        private static readonly (ObjectType type, string label)[] TrayItems =
        {
            (ObjectType.DripPipe, "Drip Pipe"), (ObjectType.SpikeFloor, "Spikes"), (ObjectType.Bellows, "Bellows"),
            (ObjectType.Crate, "Crate"), (ObjectType.Brazier, "Brazier"), (ObjectType.SnufferBell, "Snuffer Bell"),
            (ObjectType.NightOwl, "Night Owl"), (ObjectType.WaxMoths, "Wax Moths"),
        };

        private VisualElement _root;
        private VisualElement _drafts;
        private VisualElement _editor;
        private VisualElement _issuesPanel;
        private VisualElement _owlPanel;
        private Label _toast;
        private float _toastUntil;
        private readonly List<Button> _toolButtons = new List<Button>();
        private readonly List<VisualElement> _trayItems = new List<VisualElement>();
        private bool _ready;

        public void Bind(VaultEditorController controller, EditorInput input)
        {
            _controller = controller;
            _input = input;
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
            _owlPanel = _root.Q("owl-panel");
            _toast = _root.Q<Label>("toast");

            _root.Q<Button>("back-raids").clicked += () => _controller.BackToRaids();
            _root.Q<Button>("to-drafts").clicked += () => _controller.ShowDrafts();
            _root.Q<Button>("level-down").clicked += () => _controller.ChangeLevel(-1);
            _root.Q<Button>("level-up").clicked += () => _controller.ChangeLevel(1);
            _root.Q<Button>("issues-button").clicked += () => Show(_issuesPanel, !Visible(_issuesPanel));
            _root.Q<Button>("issues-close").clicked += () => Show(_issuesPanel, false);
            _root.Q<Button>("undo").clicked += () => _controller.Undo();
            _root.Q<Button>("redo").clicked += () => _controller.Redo();
            _root.Q<Button>("test").clicked += () => _controller.Test();
            _root.Q<Button>("publish").clicked += () => _controller.Publish();
            _root.Q<Button>("owl-rotate").clicked += () => _controller.RotateSelected();
            _root.Q<Button>("owl-patrol").clicked += () => _controller.TogglePatrolEditing();
            _root.Q<Button>("owl-clear").clicked += () => _controller.ClearPatrol();
            _root.Q<Button>("owl-close").clicked += () => _controller.Deselect();
            Button verify = _root.Q<Button>("verify");
            verify.clicked += () => Toast(_controller.VerifyClearCheck(), 6f);
            Show(verify, Debug.isDebugBuild);

            VisualElement tools = _root.Q("tools");
            foreach ((EditorTool tool, string label) in Tools)
            {
                EditorTool t = tool;
                var b = new Button(() => _controller.SetTool(t)) { text = label };
                b.AddToClassList("tool");
                b.AddToClassList("blocks-input");
                b.userData = tool;
                tools.Add(b);
                _toolButtons.Add(b);
            }

            VisualElement tray = _root.Q("tray");
            foreach ((ObjectType type, string label) in TrayItems)
            {
                tray.Add(TrayItem(type, label));
            }

            _input.IsOverUi = IsOverUi;
            Show(_drafts, false);
            Show(_editor, false);
            Show(_issuesPanel, false);
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

        public void Toast(string text, float seconds = 2.5f)
        {
            EnsureReady();
            _toast.text = text;
            _toastUntil = Time.unscaledTime + seconds;
            Show(_toast, true);
        }

        private void Update()
        {
            if (_ready && Visible(_toast) && Time.unscaledTime > _toastUntil)
            {
                Show(_toast, false);
            }
        }

        /// <summary>Refreshes everything that depends on the draft.</summary>
        public void UpdateEditor(VaultEditorController c)
        {
            EnsureReady();
            VaultDraft d = c.Draft;
            _root.Q<Label>("vault-name").text = d.Name;
            _root.Q<Label>("level-label").text = "Lv " + d.Level;

            int points = d.BuildPoints;
            int budget = VaultRules.Budget(d.Level);
            _root.Q<Label>("budget-label").text = points + " / " + budget;
            VisualElement fill = _root.Q("budget-fill");
            fill.style.width = Length.Percent(Mathf.Clamp01(points / (float)budget) * 100f);
            fill.EnableInClassList("over", points > budget);

            var issuesButton = _root.Q<Button>("issues-button");
            issuesButton.text = c.Issues.Count == 0 ? "OK" : "Issues " + c.Issues.Count;
            issuesButton.EnableInClassList("bad", c.Issues.Count > 0);
            VisualElement issueList = _root.Q("issue-list");
            issueList.Clear();
            foreach (VaultIssue issue in c.Issues)
            {
                issueList.Add(new Label(issue.Message));
            }

            foreach (Button b in _toolButtons)
            {
                b.EnableInClassList("selected", (EditorTool)b.userData == c.Tool);
            }

            foreach (VisualElement item in _trayItems)
            {
                var type = (ObjectType)item.userData;
                item.EnableInClassList("locked", VaultRules.RequiredLevel(type) > d.Level);
            }

            bool owl = c.SelectedOwl >= 0;
            Show(_owlPanel, owl);
            _root.Q<Button>("owl-patrol").text = c.EditingPatrol ? "Done patrol" : "Edit patrol";
            _root.Q<Label>("owl-hint").text = c.EditingPatrol
                ? "Tap tiles to add patrol points; tap the last one to remove it."
                : "Night Owl selected.";

            _root.Q<Button>("undo").SetEnabled(c.CanUndo);
            _root.Q<Button>("redo").SetEnabled(c.CanRedo);
            _root.Q<Button>("publish").SetEnabled(c.CanPublish);

            string status;
            if (d.ClearCheck == null)
            {
                status = "Not tested: escape it with 1+ star to publish";
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
            _root.Q<Label>("tool-hint").text = Hint(c);
        }

        // ---- Tray drag --------------------------------------------------------------------------

        private VisualElement TrayItem(ObjectType type, string label)
        {
            var item = new VisualElement { userData = type };
            item.AddToClassList("tray-item");
            item.AddToClassList("blocks-input");
            item.Add(new Label(label) { pickingMode = PickingMode.Ignore });
            int cost = VaultRules.Cost(type);
            string sub = (cost == 0 ? "free" : cost + " pts") +
                (VaultRules.RequiredLevel(type) > 1 ? "  Lv " + VaultRules.RequiredLevel(type) : "");
            var costLabel = new Label(sub) { pickingMode = PickingMode.Ignore };
            costLabel.AddToClassList("tray-cost");
            item.Add(costLabel);

            bool dragging = false;
            item.RegisterCallback<PointerDownEvent>(e =>
            {
                if (item.ClassListContains("locked"))
                {
                    Toast(label + " unlocks at Vault Level " + VaultRules.RequiredLevel(type));
                    return;
                }

                dragging = true;
                item.CapturePointer(e.pointerId);
                item.AddToClassList("dragging");
            });
            item.RegisterCallback<PointerMoveEvent>(e =>
            {
                if (dragging)
                {
                    _controller.UpdatePlacement(type, PanelToScreen(e.position));
                }
            });
            item.RegisterCallback<PointerUpEvent>(e =>
            {
                if (!dragging)
                {
                    return;
                }

                dragging = false;
                item.ReleasePointer(e.pointerId);
                item.RemoveFromClassList("dragging");
                Vector2 screen = PanelToScreen(e.position);
                _controller.EndPlacement(type, screen, IsOverUi(screen));
            });
            item.RegisterCallback<PointerCaptureOutEvent>(_ =>
            {
                if (dragging)
                {
                    dragging = false;
                    item.RemoveFromClassList("dragging");
                    _controller.EndPlacement(type, Vector2.zero, true);
                }
            });

            _trayItems.Add(item);
            return item;
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
            switch (c.Tool)
            {
                case EditorTool.Select: return "Tap: rotate (owl: select). Drag: move. Hold: delete. Drag traps up from the tray.";
                case EditorTool.Pit:
                case EditorTool.Ice: return "Cosmetic for now: pits and ice have no effect yet.";
                case EditorTool.Entrance: return "Tap a tile to move the entrance.";
                case EditorTool.Treasure: return "Tap to add or remove a treasure pile.";
                case EditorTool.Hoard: return c.HoardCorner == null ? "Tap one corner of the hoard room." : "Now tap the opposite corner.";
                default: return "Tap or drag to paint. Hold an object to delete it.";
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
