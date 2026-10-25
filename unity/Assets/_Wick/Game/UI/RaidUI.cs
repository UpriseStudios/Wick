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

        /// <summary>Skip the served vault during the scout (mean vault protection: free, a few an hour).</summary>
        public event Action SkipRaidPressed;
        public event Action ShareLinkPressed;
        public event Action ShareMyVaultPressed;
        public event Action SheetBackPressed;
        public event Action DoubleLootPressed;
        public event Action<string> HomeScreenPressed;

        /// <summary>The title screen was tapped.</summary>
        public event Action TitleTapped;

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
            SafeArea.Attach(_root.Q("root") ?? _root); // every layer clears notches and gesture bars
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
            UiFeedback.Register(_root);
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
            _root.Q<Button>("skip-raid").clicked += () => SkipRaidPressed?.Invoke();
            _root.Q<Button>("share-link").clicked += () => ShareLinkPressed?.Invoke();
            _root.Q<Button>("sheet-back").clicked += () => SheetBackPressed?.Invoke();
            _root.Q<Button>("ladder-close").clicked += () => SheetBackPressed?.Invoke();
            _root.Q<Button>("ladder-buy").clicked += () => _ladderBuy?.Invoke();
            _root.Q<Button>("double-loot").clicked += () => DoubleLootPressed?.Invoke();

            // Home: one big Raid button, Build, the upgrade chip and the round menu icons.
            _root.Q<Button>("home-raid").clicked += () => RaidPressed?.Invoke();
            _root.Q<Button>("home-build").clicked += () => EditorPressed?.Invoke();
            _root.Q<Button>("home-upgrade").clicked += () => HomeScreenPressed?.Invoke("upgrades");
            foreach ((string button, string screen) in new[]
                     { ("home-shop", "shop"), ("home-pass", "pass"), ("home-skins", "skins"), ("home-settings", "settings"), ("home-trophies", "achievements") })
            {
                _root.Q<Button>(button).clicked += () => HomeScreenPressed?.Invoke(screen);
            }

            _root.Q<Button>("home-log").clicked += () => DefencePressed?.Invoke();
            _root.Q<Button>("home-share").clicked += () => ShareMyVaultPressed?.Invoke();
            _root.Q<Button>("home-dev").clicked += () => Show(_root.Q("home-more"), true);
            _root.Q<Button>("more-close").clicked += () => Show(_root.Q("home-more"), false);
            Show(_root.Q("home-dev"), DevBuild);
            _root.Q("title-screen").RegisterCallback<ClickEvent>(_ => TitleTapped?.Invoke());
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

        /// <summary>
        /// Home: the vault behind (3D), wallet and Crowns on plaques, the round menu icons, RAID and
        /// Build. Development builds add a Test icon for the offline vaults and raid-by-id.
        /// </summary>
        public void ShowPicker(IReadOnlyList<string> vaultNames)
        {
            EnsureReady();
            Show(_root.Q("home-more"), false);
            VisualElement list = _root.Q("more-list");
            list.Clear();
            if (DevBuild)
            {
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

        /// <summary>The wallet and Crowns on Home's plaques.</summary>
        public void SetWallet(long gold, long wax, long gems, int crowns)
        {
            EnsureReady();
            _picker.Q<Label>("home-gold").text = gold.ToString("N0");
            _picker.Q<Label>("home-wax").text = wax.ToString("N0");
            _picker.Q<Label>("home-gems").text = gems.ToString("N0");
            _picker.Q<Label>("home-crowns").text = crowns.ToString("N0");
        }

        /// <summary>A red count on a Home icon ("pass", "trophies"); 0 hides it.</summary>
        public void SetBadge(string icon, int count)
        {
            EnsureReady();
            var badge = _picker.Q<Label>("badge-" + icon);
            if (badge != null)
            {
                badge.text = count.ToString();
                Show(badge, count > 0);
            }
        }

        /// <summary>The title screen over everything, with a load bar until `ready`, then "Tap to play".</summary>
        public void ShowTitle(string version, float progress, bool ready)
        {
            EnsureReady();
            VisualElement title = _root.Q("title-screen");
            Show(title, true);
            title.BringToFront();
            _root.Q<Label>("title-version").text = version;
            _root.Q("title-fill").style.width = Length.Percent(Mathf.Clamp01(progress) * 100f);
            Show(_root.Q("title-tap"), ready);
            Show(_root.Q(className: "title-bar"), !ready);
        }

        public void HideTitle()
        {
            EnsureReady();
            Show(_root.Q("title-screen"), false);
        }

        /// <summary>One tier of the Heist Pass ladder.</summary>
        public sealed class LadderTier
        {
            public int Tier;
            public string FreeIcon, FreeText, PremiumIcon, PremiumText;
            public bool Reached, FreeClaimed, PremiumClaimed;
            public Action ClaimFree, ClaimPremium;
        }

        private Action _ladderBuy;

        /// <summary>
        /// The Heist Pass as a ladder: a track of tier nodes (gold where reached), premium rewards above it
        /// (locked until bought), free rewards below, each with its claim button. Opens at your tier.
        /// </summary>
        public void ShowLadder(string season, int tier, int tiers, float xpFraction, string xpText, bool premium, string buyLabel, Action buy,
            IReadOnlyList<LadderTier> list)
        {
            EnsureReady();
            VisualElement ladder = _root.Q("ladder");
            ShowOnly(ladder);
            ladder.Q<Label>("ladder-season").text = season;
            ladder.Q<Label>("ladder-tier").text = "Tier " + tier + " / " + tiers;
            ladder.Q("ladder-xp-fill").style.width = Length.Percent(Mathf.Clamp01(xpFraction) * 100f);
            ladder.Q<Label>("ladder-xp-text").text = xpText;
            var buyButton = ladder.Q<Button>("ladder-buy");
            buyButton.text = buyLabel ?? "";
            _ladderBuy = buy;
            Show(buyButton, !premium && buy != null);

            var track = ladder.Q<ScrollView>("ladder-track");
            track.horizontalScrollerVisibility = ScrollerVisibility.Hidden; // swipe the track; no bar
            track.verticalScrollerVisibility = ScrollerVisibility.Hidden;
            track.Clear();
            VisualElement current = null;
            foreach (LadderTier t in list)
            {
                var col = new VisualElement();
                col.AddToClassList("ladder-col");
                col.Add(Reward(t.PremiumIcon, t.PremiumText, t.Reached, t.PremiumClaimed, premium, true, t.ClaimPremium));
                var nodeRow = new VisualElement();
                nodeRow.AddToClassList("ladder-node-row");
                var line = new VisualElement();
                line.AddToClassList("ladder-line");
                line.EnableInClassList("reached", t.Reached);
                nodeRow.Add(line);
                var node = new VisualElement();
                node.AddToClassList("ladder-node");
                node.EnableInClassList("reached", t.Reached);
                node.EnableInClassList("current", t.Tier == Mathf.Max(1, tier));
                var number = new Label(t.Tier.ToString());
                number.AddToClassList("lettering");
                number.AddToClassList("ladder-node-label");
                node.Add(number);
                nodeRow.Add(node);
                col.Add(nodeRow);
                col.Add(Reward(t.FreeIcon, t.FreeText, t.Reached, t.FreeClaimed, true, false, t.ClaimFree));
                track.Add(col);
                if (t.Tier == Mathf.Max(1, tier))
                {
                    current = col;
                }
            }

            if (current != null)
            {
                track.schedule.Execute(() => track.ScrollTo(current)).StartingIn(50);
            }
        }

        private VisualElement Reward(string icon, string text, bool reached, bool claimed, bool unlocked, bool premium, Action claim)
        {
            var card = new VisualElement();
            card.AddToClassList("ladder-reward");
            card.AddToClassList("card");
            card.EnableInClassList("premium", premium);
            if (string.IsNullOrEmpty(icon))
            {
                card.style.opacity = 0.35f; // no reward on this row at this tier
                return card;
            }

            var art = new VisualElement();
            art.AddToClassList("icon");
            art.AddToClassList("icon-" + icon);
            art.AddToClassList("ladder-reward-icon");
            card.Add(art);
            var label = new Label(text);
            label.AddToClassList("lettering");
            label.AddToClassList("ladder-reward-text");
            card.Add(label);
            if (claimed)
            {
                var done = new Label("Claimed");
                done.AddToClassList("lettering");
                done.AddToClassList("ladder-done");
                card.Add(done);
            }
            else if (!unlocked)
            {
                var locked = new Label("Pass only");
                locked.AddToClassList("ladder-lock");
                card.Add(locked);
            }
            else if (reached && claim != null)
            {
                var b = new Button(claim) { text = "Claim" };
                b.AddToClassList("btn-gold");
                b.AddToClassList("ladder-claim");
                card.Add(b);
            }

            return card;
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
            var ladderMessage = _root.Q<Label>("ladder-message");
            ladderMessage.text = text ?? "";
            ladderMessage.EnableInClassList("good", ok == true);
            ladderMessage.EnableInClassList("bad", ok == false);
            Show(ladderMessage, !string.IsNullOrEmpty(text));
            var label = _sheet.Q<Label>("sheet-message");
            label.text = text ?? "";
            label.EnableInClassList("good", ok == true);
            label.EnableInClassList("bad", ok == false);
            Show(label, !string.IsNullOrEmpty(text));
        }

        /// <summary>What the server paid for the raid: gold, wax and the Crowns change, as reward plaques.</summary>
        public void SetResultRewards(long gold, long wax, int crowns)
        {
            EnsureReady();
            _results.Q<Label>("result-loot").text = "+" + gold.ToString("N0");
            _results.Q<Label>("result-wax").text = "+" + wax.ToString("N0");
            Show(_results.Q("reward-wax"), wax > 0);
            _results.Q<Label>("result-crowns").text = (crowns > 0 ? "+" : "") + crowns;
            Show(_results.Q("reward-crowns"), crowns != 0);
        }

        /// <summary>Shows or hides the results screen's "double your loot" ad offer.</summary>
        public void SetDoubleLoot(bool offered, string label = null)
        {
            EnsureReady();
            var b = _root.Q<Button>("double-loot");
            b.Q<Label>("double-label").text = label ?? "×2 LOOT";
            Show(b.Q<Label>("double-sub"), label == null);
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
            var mine = _root.Q<Label>("my-vault-id");
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

        /// <summary>
        /// Gold that flies from a screen point (Wick) to the loot counter, a few coins staggered,
        /// then a pulse on the counter. Presentation only.
        /// </summary>
        public void FlyCoins(Vector2 screenFrom, int count)
        {
            if (_root?.panel == null || !Visible(_hud))
            {
                return;
            }

            Vector2 from = RuntimePanelUtils.ScreenToPanel(_root.panel, new Vector2(screenFrom.x, Screen.height - screenFrom.y));
            Vector2 to = _root.WorldToLocal(_loot.worldBound.center);
            for (int i = 0; i < count; i++)
            {
                var coin = new VisualElement { pickingMode = PickingMode.Ignore };
                coin.AddToClassList("fly-coin");
                coin.AddToClassList("icon");
                coin.AddToClassList("icon-coin");
                _root.Add(coin);
                Vector2 jitter = new Vector2(UnityEngine.Random.Range(-40f, 40f), UnityEngine.Random.Range(-30f, 30f));
                float start = Time.unscaledTime + i * 0.05f;
                const float flight = 0.5f;
                coin.style.left = from.x;
                coin.style.top = from.y;
                coin.schedule.Execute(() =>
                {
                    float t = Mathf.Clamp01((Time.unscaledTime - start) / flight);
                    float ease = t * t; // speeds up into the counter
                    Vector2 arc = Vector2.Lerp(from + jitter, to, ease) + Vector2.up * (-60f * Mathf.Sin(t * Mathf.PI));
                    coin.style.left = arc.x;
                    coin.style.top = arc.y;
                    coin.style.opacity = t <= 0f ? 0f : 1f;
                    if (t >= 1f)
                    {
                        coin.RemoveFromHierarchy();
                        _loot.style.scale = new Scale(new Vector3(1.25f, 1.25f, 1f));
                        _loot.schedule.Execute(() => _loot.style.scale = new Scale(Vector3.one)).StartingIn(80);
                    }
                }).Every(16).Until(() => coin.parent == null);
            }
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

        /// <summary>Shows or hides the scout's Skip button.</summary>
        public void ShowSkip(bool visible)
        {
            EnsureReady();
            Show(_root.Q("skip-raid"), visible);
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

            _results.Q<Label>("result-loot").text = "+" + result.Loot.ToString("N0");
            Show(_results.Q("reward-wax"), false);
            Show(_results.Q("reward-crowns"), false);
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
            int ticksLeft = _runner.Sim.Params.RaidTicks - _runner.Sim.State.Tick;
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

            PerfProbe probe = _runner.GetComponent<PerfProbe>();
            _devFps.text = probe != null && probe.Summary.Length > 0 ? probe.Summary : "FPS " + Mathf.RoundToInt(_fps);
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
            Show(_root.Q("home-more"), false);
            Show(_root.Q("ladder"), screen != null && screen == _root.Q("ladder"));
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
