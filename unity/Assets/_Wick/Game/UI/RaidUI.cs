using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
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

        public event Action<int> VaultPicked;
        public event Action<int> GoldenPicked;
        public event Action RetryPressed;
        public event Action MenuPressed;

        public static bool DevBuild => Debug.isDebugBuild;

        public void Bind(RaidRunner runner, RaidInput input, DevOverlay overlay)
        {
            _runner = runner;
            _input = input;
            _overlay = overlay;
        }

        private void OnEnable()
        {
            _root = GetComponent<UIDocument>().rootVisualElement;
            _hud = _root.Q("hud");
            _picker = _root.Q("picker");
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

        public void ShowPicker(IReadOnlyList<string> vaultNames)
        {
            VisualElement list = _picker.Q("picker-list");
            list.Clear();
            for (int i = 0; i < vaultNames.Count; i++)
            {
                int index = i;
                var b = new Button(() => VaultPicked?.Invoke(index)) { text = vaultNames[i] };
                b.AddToClassList("wide-button");
                list.Add(b);
            }

            ShowOnly(_picker);
        }

        public void SetGoldens(IReadOnlyList<string> goldenNames)
        {
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
            ShowOnly(_hud);
            SetBanner(banner);
        }

        public void SetBanner(string text)
        {
            _banner.text = text ?? "";
            Show(_banner, !string.IsNullOrEmpty(text));
        }

        public void ShowResults(RaidResult result, string liveHash, string replayHash, bool? match)
        {
            ShowOnly(_results);
            _results.Q<Label>("result-title").text =
                result.Outcome == Outcome.Escaped ? "ESCAPED" : result.Outcome == Outcome.Snuffed ? "SNUFFED" : "TIME UP";
            for (int i = 0; i < _stars.Count; i++)
            {
                _stars[i].EnableInClassList("on", i < result.Stars);
            }

            _results.Q<Label>("result-loot").text = "Loot " + result.Loot;
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
            _fps = Mathf.Lerp(_fps, 1f / Mathf.Max(Time.unscaledDeltaTime, 1e-4f), 0.05f);
            Show(_stealthButton, _input.Tuning.UseStealthButton);

            if (_runner.Sim == null || !Visible(_hud))
            {
                UpdateDev();
                return;
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
            Show(_results, screen == _results);
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
