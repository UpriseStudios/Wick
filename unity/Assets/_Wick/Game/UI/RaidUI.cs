using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Wick.Services;
using Wick.Sim;

namespace Wick.Game
{
    /// <summary>
    /// The raid's UI Toolkit document: HUD, vault picker, results and the dev menu.
    /// Reads sim state for display; buttons go through RaidInput like any other input.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class RaidUI : MonoBehaviour
    {
        [SerializeField] private RaidRunner _runner;
        [SerializeField] private RaidInput _input;
        [SerializeField] private DevOverlay _overlay;

        private VisualElement _root;
        private VisualElement _hud;
        private VisualElement _picker;
        private VisualElement _defence;
        private VisualElement _sheet;
        private VisualElement _replayBar;
        private Slider _scrub;
        private bool _scrubbing;
        private VisualElement _results;
        private VisualElement _devMenu;
        private Button _devToggle;
        private Label _timer;
        private Label _loot;
        private Label _banner;
        private VisualElement _stealthIcon;
        private Button _decoy;
        private Button _stealthButton;
        private readonly List<VisualElement> _flickers = new List<VisualElement>();
        private readonly List<VisualElement> _stars = new List<VisualElement>();
        private Label _devFps;
        private Label _devTick;
        private Label _devHash;
        private float _fps;
        private bool _ready;

        public event Action<int> VaultPicked;
        public event Action<int> GoldenPicked;
        public event Action RetryPressed;
        public event Action MenuPressed;
        public event Action EditorPressed;
        public event Action ServerRaidPressed;
        public event Action RaidPressed;
        public event Action DefencePressed;
        public event Action DefenceBackPressed;
        public event Action<string> RaidByIdPressed;
        public event Action<string> WatchPressed;
        public event Action<string> RevengePressed;
        public event Action ReplayPausePressed;
        public event Action<int> ReplayScrubbed;
        public event Action ReplayBackPressed;
        public event Action ShareClipPressed;
        public event Action ShareLinkPressed;
        public event Action ShareMyVaultPressed;
        public event Action SheetBackPressed;
        public event Action DoubleLootPressed;
        public event Action<string> HomeScreenPressed;

        public static bool DevBuild => Debug.isDebugBuild;

        public void Bind(RaidRunner runner, RaidInput input, DevOverlay overlay)
        {
            _runner = runner;
            _input = input;
            _overlay = overlay;
        }

        private void Start()
        {
            EnsureReady();
        }

        /// <summary>
        /// Queries the document on first use: UIDocument builds its tree in its own OnEnable,
        /// and other scripts may call in from their Start before ours runs.
        /// </summary>
        private void EnsureReady()
        {
            if (_ready)
            {
                return;
            }

            _ready = true;
            _root = GetComponent<UIDocument>().rootVisualElement;
            _hud = _root.Q("hud");
            _picker = _root.Q("picker");
            _defence = _root.Q("defence");
            _sheet = _root.Q("sheet");
            _replayBar = _root.Q("replay-bar");
            _scrub = _root.Q<Slider>("replay-scrub");
            _results = _root.Q("results");
            _devMenu = _root.Q("dev-menu");
            _devToggle = _root.Q<Button>("dev-toggle");
            _timer = _root.Q<Label>("timer");
            _loot = _root.Q<Label>("loot");
            _banner = _root.Q<Label>("banner");
            _stealthIcon = _root.Q("stealth-icon");
            _decoy = _root.Q<Button>("decoy-button");
            _stealthButton = _root.Q<Button>("stealth-button");
            _flickers.Clear();
            _flickers.AddRange(_root.Q("flickers").Children());
            _stars.Clear();
            _stars.AddRange(_root.Q("stars").Children());
            _devFps = _root.Q<Label>("dev-fps");
            _devTick = _root.Q<Label>("dev-tick");
            _devHash = _root.Q<Label>("dev-hash");

            _decoy.clicked += () => _input.DropDecoy();
            _stealthButton.clicked += () => _input.ToggleStealth();
            _root.Q<Button>("retry-button").clicked += () => RetryPressed?.Invoke();
            _root.Q<Button>("menu-button").clicked += () => MenuPressed?.Invoke();
            _root.Q<Button>("dev-vaults").clicked += () => MenuPressed?.Invoke();
            _devToggle.clicked += () => Show(_devMenu, !Visible(_devMenu));
            _root.Q<Button>("defence-back").clicked += () => DefenceBackPressed?.Invoke();
            _root.Q<Button>("replay-pause").clicked += () => ReplayPausePressed?.Invoke();
            _root.Q<Button>("replay-back").clicked += () => ReplayBackPressed?.Invoke();
            _root.Q<Button>("share-clip").clicked += () => ShareClipPressed?.Invoke();
            _root.Q<Button>("share-link").clicked += () => ShareLinkPressed?.Invoke();
            _root.Q<Button>("sheet-back").clicked += () => SheetBackPressed?.Invoke();
            _root.Q<Button>("double-loot").clicked += () => DoubleLootPressed?.Invoke();
            _scrub.RegisterValueChangedCallback(e => ReplayScrubbed?.Invoke(Mathf.RoundToInt(e.newValue)));
            _scrub.RegisterCallback<PointerDownEvent>(_ => _scrubbing = true, TrickleDown.TrickleDown);
            _scrub.RegisterCallback<PointerUpEvent>(_ => _scrubbing = false, TrickleDown.TrickleDown);
            _scrub.RegisterCallback<PointerCaptureOutEvent>(_ => _scrubbing = false, TrickleDown.TrickleDown);

            var grid = _root.Q<Toggle>("dev-grid");
            grid.RegisterValueChangedCallback(e => _overlay.ShowGrid = e.newValue);
            var lit = _root.Q<Toggle>("dev-lit");
            lit.RegisterValueChangedCallback(e => _overlay.ShowLit = e.newValue);
            var stealth = _root.Q<Toggle>("dev-stealth-button");
            stealth.SetValueWithoutNotify(_input.Tuning.UseStealthButton);
            stealth.RegisterValueChangedCallback(e => _input.Tuning.UseStealthButton = e.newValue);

            _input.IsOverUi = IsOverUi;
            Show(_devToggle, DevBuild);
            Show(_devMenu, false);
            ShowOnly(null);
        }

        private void OnDisable()
        {
            if (_input != null)
            {
                _input.IsOverUi = null;
            }
        }

        // ---- Screens ----------------------------------------------------------------

        /// <summary>Home: Raid, Raids on your vault, the editor, and (dev builds) test vaults and tools.</summary>
        public void ShowPicker(IReadOnlyList<string> vaultNames)
        {
            EnsureReady();
            VisualElement list = _picker.Q("picker-list");
            list.Clear();
            list.Add(WideButton("Raid", () => RaidPressed?.Invoke()));
            list.Add(WideButton("Raids on your vault", () => DefencePressed?.Invoke(), "secondary"));
            list.Add(WideButton("Upgrades", () => HomeScreenPressed?.Invoke("upgrades"), "secondary"));
            list.Add(WideButton("Heist Pass", () => HomeScreenPressed?.Invoke("pass"), "secondary"));
            list.Add(WideButton("Achievements", () => HomeScreenPressed?.Invoke("achievements"), "secondary"));
            list.Add(WideButton("Shop", () => HomeScreenPressed?.Invoke("shop"), "secondary"));
            list.Add(WideButton("Vault editor", () => EditorPressed?.Invoke(), "secondary"));
            list.Add(WideButton("Share my vault", () => ShareMyVaultPressed?.Invoke(), "secondary"));

            if (DevBuild)
            {
                list.Add(new Label("Development") { name = "dev-section" });
                list.Q<Label>("dev-section").AddToClassList("section");
                for (int i = 0; i < vaultNames.Count; i++)
                {
                    int index = i;
                    list.Add(WideButton(vaultNames[i] + " (offline)", () => VaultPicked?.Invoke(index), "small"));
                }

                list.Add(WideButton("My live vault (server)", () => ServerRaidPressed?.Invoke(), "small"));
                var row = new VisualElement();
                row.AddToClassList("id-row");
                var field = new TextField { name = "raid-id", value = "" };
                field.AddToClassList("id-field");
                field.textEdition.placeholder = "Vault id, e.g. v_abc123";
                row.Add(field);
                var go = new Button(() => RaidByIdPressed?.Invoke(field.value.Trim())) { text = "Raid" };
                go.AddToClassList("log-button");
                row.Add(go);
                list.Add(row);
                var mine = new Label("Your vault id: " + (WickBackend.MyVaultId ?? "sign in first")) { name = "my-vault-id" };
                mine.AddToClassList("mono");
                mine.selection.isSelectable = true;
                list.Add(mine);
            }


            ShowPickerMessage(null);
            ShowOnly(_picker);
        }

        /// <summary>A row on a list screen: a line of text and its buttons.</summary>
        public sealed class SheetRow
        {
            public string Text;
            public string Style;
            public readonly List<(string Label, Action Act, bool Enabled)> Buttons = new List<(string, Action, bool)>();

            public SheetRow Button(string label, Action act, bool enabled = true)
            {
                Buttons.Add((label, act, enabled));
                return this;
            }
        }

        public void ShowSheet(string title, string subtitle, IReadOnlyList<SheetRow> rows)
        {
            EnsureReady();
            ShowOnly(_sheet);
            _sheet.Q<Label>("sheet-title").text = title;
            _sheet.Q<Label>("sheet-subtitle").text = subtitle ?? "";
            var list = _sheet.Q<ScrollView>("sheet-list");
            float scroll = list.scrollOffset.y;
            list.Clear();
            foreach (SheetRow r in rows)
            {
                var row = new VisualElement();
                row.AddToClassList("log-row");
                var text = new Label(r.Text);
                text.AddToClassList("log-text");
                if (r.Style != null)
                {
                    text.AddToClassList(r.Style);
                }

                row.Add(text);
                if (r.Buttons.Count > 0)
                {
                    var buttons = new VisualElement();
                    buttons.AddToClassList("log-buttons");
                    foreach ((string label, Action act, bool enabled) in r.Buttons)
                    {
                        var b = new Button(act) { text = label };
                        b.AddToClassList("log-button");
                        b.SetEnabled(enabled);
                        buttons.Add(b);
                    }

                    row.Add(buttons);
                }

                list.Add(row);
            }

            list.schedule.Execute(() => list.scrollOffset = new Vector2(0f, scroll));
        }

        public void SetSheetMessage(string text, bool? ok)
        {
            EnsureReady();
            var label = _sheet.Q<Label>("sheet-message");
            label.text = text ?? "";
            label.EnableInClassList("good", ok == true);
            label.EnableInClassList("bad", ok == false);
            Show(label, !string.IsNullOrEmpty(text));
        }

        /// <summary>Shows or hides the results screen's "double your loot" ad offer.</summary>
        public void SetDoubleLoot(bool offered, string label = null)
        {
            EnsureReady();
            var b = _root.Q<Button>("double-loot");
            if (label != null)
            {
                b.text = label;
            }

            b.SetEnabled(offered);
            Show(b, offered || label != null);
        }

        /// <summary>First launch: the age question that decides personalised ads and analytics.</summary>
        public void ShowAgePrompt(Action<bool> answered)
        {
            EnsureReady();
            ShowPickerMessage("Are you 18 or older? (Under-18s only see non-personalised ads.)");
            VisualElement list = _picker.Q("picker-list");
            var row = new VisualElement { name = "age-prompt" };
            row.AddToClassList("prompt-row");
            void Answer(bool adult)
            {
                row.RemoveFromHierarchy();
                ShowPickerMessage(null);
                answered(adult);
            }

            var yes = new Button(() => Answer(true)) { text = "I'm 18 or older" };
            yes.AddToClassList("log-button");
            var no = new Button(() => Answer(false)) { text = "I'm under 18" };
            no.AddToClassList("log-button");
            row.Add(yes);
            row.Add(no);
            list.Insert(0, row);
        }

        /// <summary>Asks before raiding a vault code found on the clipboard (iOS first launch).</summary>
        public void ShowLinkPrompt(string code, Action yes)
        {
            EnsureReady();
            ShowPickerMessage("Open the vault you were sent? (" + code + ")");
            VisualElement list = _picker.Q("picker-list");
            var row = new VisualElement { name = "link-prompt" };
            row.AddToClassList("prompt-row");
            var go = new Button(() =>
            {
                row.RemoveFromHierarchy();
                ShowPickerMessage(null);
                yes();
            }) { text = "Raid it" };
            go.AddToClassList("log-button");
            var no = new Button(() =>
            {
                row.RemoveFromHierarchy();
                ShowPickerMessage(null);
            }) { text = "Not now" };
            no.AddToClassList("log-button");
            row.Add(go);
            row.Add(no);
            list.Insert(0, row);
        }

        /// <summary>Which share buttons the results screen offers, and the clip progress line.</summary>
        public void SetShare(bool clip, bool link, string status)
        {
            EnsureReady();
            _root.Q<Button>("share-clip").SetEnabled(clip);
            _root.Q<Button>("share-link").SetEnabled(link);
            Show(_root.Q("share-row"), clip || link);
            var label = _root.Q<Label>("clip-status");
            label.text = status ?? "";
            Show(label, !string.IsNullOrEmpty(status));
        }

        public void SetHomeStats(string text)
        {
            EnsureReady();
            _picker.Q<Label>("home-stats").text = text ?? "";
            var mine = _picker.Q<Label>("my-vault-id");
            if (mine != null)
            {
                mine.text = "Your vault id: " + (WickBackend.MyVaultId ?? "sign in first");
            }
        }

        /// <summary>The defence log: stats, then each raid with Watch replay and Revenge.</summary>
        public void ShowDefenceLog(DefenceLogReply log, long nowMs)
        {
            EnsureReady();
            ShowOnly(_defence);
            string shield = log.ShieldUntilMs > nowMs ? "  |  shielded " + ((log.ShieldUntilMs - nowMs) / 60000 + 1) + " min" : "";
            _defence.Q<Label>("defence-stats").text = log.Raids + " raids, " + log.Snuffs + " snuffed" +
                (string.IsNullOrEmpty(log.DeadliestTrap) ? "" : ", deadliest: " + Pretty(log.DeadliestTrap)) + shield;

            var list = _defence.Q<ScrollView>("defence-list");
            list.Clear();
            if (log.Entries == null || log.Entries.Count == 0)
            {
                var none = new Label("No raids yet. Publish a vault and wait for raiders.");
                none.AddToClassList("log-text");
                list.Add(none);
                return;
            }

            foreach (DefenceEntryReply e in log.Entries)
            {
                var row = new VisualElement();
                row.AddToClassList("log-row");
                bool robbed = e.GoldLost > 0 || (e.Outcome == "Escaped" && e.Stars > 0);
                string money = robbed ? "lost " + e.GoldLost + " gold" : "+" + e.Bonus + " gold";
                string crowns = (e.CrownsDelta >= 0 ? "+" : "") + e.CrownsDelta + " Crowns";
                var text = new Label(Ago(nowMs - e.AtMs) + "  " + Verb(e.Outcome) + "  " + e.Stars + " star" + (e.Stars == 1 ? "" : "s") +
                    "\n" + money + ", " + crowns);
                text.AddToClassList("log-text");
                text.AddToClassList(robbed ? "robbed" : "held");
                row.Add(text);

                var buttons = new VisualElement();
                buttons.AddToClassList("log-buttons");
                string raidId = e.RaidId;
                string revenge = e.RevengeVaultId;
                var watch = new Button(() => WatchPressed?.Invoke(raidId)) { text = "Watch replay" };
                watch.AddToClassList("log-button");
                buttons.Add(watch);
                var hit = new Button(() => RevengePressed?.Invoke(revenge)) { text = "Revenge" };
                hit.AddToClassList("log-button");
                hit.AddToClassList("revenge");
                buttons.Add(hit);
                row.Add(buttons);
                list.Add(row);
            }
        }

        /// <summary>The HUD for a replay, with a scrub bar over its ticks.</summary>
        public void ShowReplayHud(string banner, int totalTicks)
        {
            ShowHud(banner);
            _scrub.highValue = Mathf.Max(1, totalTicks);
            _scrub.SetValueWithoutNotify(0);
            Show(_replayBar, true);
            SetReplayPaused(false);
        }

        public void SetReplayPaused(bool paused)
        {
            EnsureReady();
            _root.Q<Button>("replay-pause").text = paused ? "Play" : "Pause";
        }

        private Button WideButton(string text, Action onClick, string extraClass = null)
        {
            var b = new Button(onClick) { text = text };
            b.AddToClassList("wide-button");
            if (extraClass != null)
            {
                b.AddToClassList(extraClass);
            }

            return b;
        }

        private static string Verb(string outcome)
        {
            switch (outcome)
            {
                case "Escaped": return "Escaped";
                case "Snuffed": return "Snuffed";
                default: return "Ran out of time";
            }
        }

        private static string Ago(long ms)
        {
            long minutes = Math.Max(0, ms / 60000);
            if (minutes < 1) return "just now";
            if (minutes < 60) return minutes + " min ago";
            if (minutes < 48 * 60) return minutes / 60 + " h ago";
            return minutes / (24 * 60) + " days ago";
        }

        private static string Pretty(string typeName)
        {
            var sb = new System.Text.StringBuilder();
            foreach (char c in typeName)
            {
                if (char.IsUpper(c) && sb.Length > 0)
                {
                    sb.Append(' ');
                }

                sb.Append(c);
            }

            return sb.ToString();
        }

        /// <summary>A line above the vault list, e.g. why a live raid couldn't start. Null hides it.</summary>
        public void ShowPickerMessage(string text)
        {
            EnsureReady();
            var label = _picker.Q<Label>("picker-message");
            label.text = text ?? "";
            Show(label, !string.IsNullOrEmpty(text));
        }

        /// <summary>
        /// The server's verdict on a live raid, under the results. ok: true = verified and paid,
        /// false = not verified (nothing paid), null = still waiting. detail shows in dev builds.
        /// </summary>
        public void SetServerVerdict(string text, bool? ok, string detail = null)
        {
            EnsureReady();
            var label = _results.Q<Label>("result-server");
            label.text = text ?? "";
            label.EnableInClassList("good", ok == true);
            label.EnableInClassList("bad", ok == false);
            Show(label, !string.IsNullOrEmpty(text));
            var dev = _results.Q<Label>("result-server-detail");
            dev.text = detail ?? "";
            Show(dev, DevBuild && !string.IsNullOrEmpty(detail));
        }

        /// <summary>Renames the results screen's second button ("Vaults", or "Back to editor").</summary>
        public void SetMenuLabel(string text)
        {
            EnsureReady();
            _root.Q<Button>("menu-button").text = text;
        }

        public void SetGoldens(IReadOnlyList<string> goldenNames)
        {
            EnsureReady();
            var list = _root.Q<ScrollView>("dev-goldens");
            list.Clear();
            for (int i = 0; i < goldenNames.Count; i++)
            {
                int index = i;
                var b = new Button(() =>
                {
                    Show(_devMenu, false);
                    GoldenPicked?.Invoke(index);
                }) { text = goldenNames[i] };
                b.AddToClassList("wide-button");
                list.Add(b);
            }
        }

        public void ShowHud(string banner)
        {
            EnsureReady();
            ShowOnly(_hud);
            SetBanner(banner);
        }

        public void SetBanner(string text)
        {
            EnsureReady();
            _banner.text = text ?? "";
            Show(_banner, !string.IsNullOrEmpty(text));
        }

        public void ShowResults(RaidResult result, string liveHash, string replayHash, bool? match)
        {
            EnsureReady();
            ShowOnly(_results);
            _results.Q<Label>("result-title").text =
                result.Outcome == Outcome.Escaped ? "ESCAPED" : result.Outcome == Outcome.Snuffed ? "SNUFFED" : "TIME UP";
            for (int i = 0; i < _stars.Count; i++)
            {
                _stars[i].EnableInClassList("on", i < result.Stars);
            }

            _results.Q<Label>("result-loot").text = "Loot " + result.Loot;
            SetServerVerdict(null, null);
            SetShare(false, false, null);
            SetDoubleLoot(false);
            VisualElement dev = _results.Q("result-dev");
            Show(dev, DevBuild);
            _results.Q<Label>("result-live-hash").text = "live   " + liveHash;
            _results.Q<Label>("result-replay-hash").text = "replay " + replayHash;
            var matchLabel = _results.Q<Label>("result-match");
            matchLabel.text = match == null ? "" : match.Value ? "MATCH" : "MISMATCH";
            matchLabel.EnableInClassList("bad", match == false);
        }

        // ---- Per frame --------------------------------------------------------------

        private void Update()
        {
            EnsureReady();
            _fps = Mathf.Lerp(_fps, 1f / Mathf.Max(Time.unscaledDeltaTime, 1e-4f), 0.05f);
            Show(_stealthButton, _input.Tuning.UseStealthButton);

            if (_runner.Sim == null || !Visible(_hud))
            {
                UpdateDev();
                return;
            }

            if (Visible(_replayBar) && !_scrubbing)
            {
                _scrub.SetValueWithoutNotify(_runner.Sim.State.Tick);
            }

            WickState w = _runner.Current.Wick;
            int ticksLeft = SimTuning.RaidTicks - _runner.Sim.State.Tick;
            int seconds = Mathf.CeilToInt(ticksLeft / (float)SimTuning.TicksPerSecond);
            _timer.text = seconds.ToString();
            _timer.EnableInClassList("urgent", seconds <= 10 && _runner.Mode != RaidMode.Scout);
            _loot.text = w.CarriedTreasure.ToString();
            for (int i = 0; i < _flickers.Count; i++)
            {
                _flickers[i].EnableInClassList("out", i >= w.Flickers);
            }

            _stealthIcon.EnableInClassList("active", w.Tiptoe);
            _stealthButton.EnableInClassList("active", w.Tiptoe);
            _decoy.EnableInClassList("used", w.DecoyUsed);
            Show(_decoy, _runner.Mode != RaidMode.Replay);
            UpdateDev();
        }

        private void UpdateDev()
        {
            if (!Visible(_devMenu))
            {
                return;
            }

            _devFps.text = "FPS " + Mathf.RoundToInt(_fps);
            if (_runner.Sim != null)
            {
                _devTick.text = "tick " + _runner.Sim.State.Tick + "  seed " + _runner.Seed + "  " + _runner.Mode;
                _devHash.text = "hash " + RaidResult.FormatHash(_runner.Sim.State.Hash.Value);
            }
        }

        // ---- Touch routing ------------------------------------------------------------

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

        // ---- Helpers ----------------------------------------------------------------

        private void ShowOnly(VisualElement screen)
        {
            Show(_hud, screen == _hud);
            Show(_picker, screen == _picker);
            Show(_defence, screen == _defence);
            Show(_sheet, screen == _sheet);
            Show(_results, screen == _results);
            Show(_replayBar, false);
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
