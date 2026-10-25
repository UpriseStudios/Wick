using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Wick.Economy;
using Wick.Services;
using Wick.Sim;

namespace Wick.Game
{
    /// <summary>
    /// Home's economy screens: Upgrades (Vault Level and Lantern, timers, gems, ad speed-ups),
    /// Heist Pass, Achievements and Shop, plus the Privacy screen. All numbers come from the server's state and config;
    /// every action is a server call, and the screen redraws from the server's answer.
    /// </summary>
    public sealed class EconomyScreens : MonoBehaviour
    {
        private RaidUI _ui;
        private SkinCatalog _skins;
        private ThemeCatalog _themes;
        private readonly EconomyClient _economy = new EconomyClient();
        private readonly RewardedAds _ads = new RewardedAds();
        private string _screen;
        private EconomyStateReply _state;
        private float _refreshAt;
        private bool _busy;
        private bool _settling;

        public void Bind(RaidUI ui, SkinCatalog skins, ThemeCatalog themes)
        {
            _ui = ui;
            _skins = skins;
            _themes = themes;
        }

        public bool Showing => _screen != null;

        public async void Show(string screen)
        {
            _screen = screen;
            _ui.ShowSheet(Title(screen), "Loading...", new List<RaidUI.SheetRow>());
            _ui.SetSheetMessage(null, null);
            if (screen == "privacy" || screen == "settings")
            {
                DrawSettings();
                return;
            }

            if (screen == "shop")
            {
                _ = Shop.ConnectAsync();
            }

            await Refresh();
        }

        public void Close()
        {
            _screen = null;
        }

        private void Update()
        {
            // Timers count down on screen; the server stays the clock that counts. Redraw only while one
            // runs: a redraw rebuilds the buttons, and a tap that straddles it is lost.
            if (_screen == "upgrades" && _state?.Running?.Count > 0 && Time.unscaledTime > _refreshAt)
            {
                _refreshAt = Time.unscaledTime + 1f;
                if (_state.Running.Exists(r => r.EndsMs <= ServerNowMs))
                {
                    SettleFinished();
                }
                else
                {
                    Draw();
                }
            }
        }

        /// <summary>A build's time is up: the server applies it on the next read, so just read again.</summary>
        private async void SettleFinished()
        {
            if (_settling)
            {
                return;
            }

            _settling = true;
            try
            {
                await Refresh();
            }
            finally
            {
                _settling = false;
            }
        }

        private async Task Refresh()
        {
            try
            {
                await _economy.RefreshConfigAsync();
                EconomyStateReply before = _state;
                _state = await _economy.StateAsync();
                _stateAtLocal = Time.realtimeSinceStartupAsDouble;
                Draw();
                if (before != null && _state.VaultLevel > before.VaultLevel)
                {
                    _ui.SetSheetMessage("Vault Level " + _state.VaultLevel + " complete!", true);
                }
                else if (before != null && _state.Lantern > before.Lantern)
                {
                    _ui.SetSheetMessage("Lantern " + _state.Lantern + " complete!", true);
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                _ui.SetSheetMessage("Couldn't reach the server. Check your connection.", false);
            }
        }

        private double _stateAtLocal;

        /// <summary>Server time now, from the last state's server time plus the time since it arrived.</summary>
        private long ServerNowMs => _state.ServerTimeMs + (long)((Time.realtimeSinceStartupAsDouble - _stateAtLocal) * 1000.0);

        private void Draw()
        {
            if (_screen == null || _state == null)
            {
                return;
            }

            EconomyConfig cfg = EconomyClient.Config;
            string wallet = _state.Gold + " gold  |  " + _state.Wax + " wax  |  " + _state.Gems + " gems  |  " + _state.Stars + " stars";
            switch (_screen)
            {
                case "upgrades":
                    _ui.ShowSheet("Upgrades", wallet + "\n" + (_state.Running?.Count ?? 0) + " of " + _state.Workshops + " workshops busy", Upgrades(cfg));
                    break;
                case "pass":
                    ShowLadder(cfg);
                    break;
                case "achievements":
                    _ui.ShowSheet("Achievements", wallet, Achievements(cfg));
                    break;
                case "shop":
                    _ui.ShowSheet("Shop", wallet, ShopRows(cfg));
                    break;
                case "skins":
                    _ui.ShowSheet("Skins and themes", "Looks only: every skin has Wick's size and moves, every theme the same vault.", SkinRows());
                    break;
            }
        }

        // ---- Upgrades -----------------------------------------------------------------------

        private List<RaidUI.SheetRow> Upgrades(EconomyConfig cfg)
        {
            var rows = new List<RaidUI.SheetRow>();
            VaultLevelStep v = _state.NextVaultLevel;
            rows.Add(UpgradeRow(UpgradeKinds.VaultLevel, "Vault Level " + _state.VaultLevel,
                v == null ? null : "Level " + v.Level + ": " + v.Gold + " gold, " + v.Stars + " raid stars (you have " + _state.Stars + "), " + Duration(v.Minutes * 60_000L),
                v != null && _state.Gold >= v.Gold && _state.Stars >= v.Stars, cfg));
            LanternStep l = _state.NextLantern;
            rows.Add(UpgradeRow(UpgradeKinds.Lantern, "Lantern " + _state.Lantern,
                l == null ? null : "Level " + l.Level + ": " + l.Wax + " wax, needs Vault Level " + l.MinVaultLevel + ", " + Duration(l.Minutes * 60_000L) +
                    " (a slightly bigger light in your raids)",
                l != null && _state.Wax >= l.Wax && _state.VaultLevel >= l.MinVaultLevel, cfg));

            DecoyStep dc = _state.NextDecoy;
            rows.Add(UpgradeRow(UpgradeKinds.Decoys, "Decoys " + (1 + _state.Decoys),
                dc == null ? null : "Carry " + (2 + _state.Decoys) + ": " + dc.Wax + " wax, needs Vault Level " + dc.MinVaultLevel + ", " +
                    Duration(dc.Minutes * 60_000L),
                dc != null && _state.Wax >= dc.Wax && _state.VaultLevel >= dc.MinVaultLevel, cfg));

            // Defences: every trap or guardian of a type, vault-wide, capped at half the Vault Level.
            int cap = DefenceLevels.Cap(_state.VaultLevel);
            for (int i = 0; i < ObjectTypes.Count; i++)
            {
                var type = (ObjectType)i;
                if (!ObjectTypes.IsUpgradable(type) || VaultRules.RequiredLevel(type) > _state.VaultLevel)
                {
                    continue;
                }

                int level = OwnedDefences.Level(_state.Defences, type);
                DefenceStep d = cfg.DefenceTo(level + 1);
                string next = d == null ? null
                    : d.Level > cap ? "Level " + d.Level + " needs Vault Level " + (2 * d.Level - 1)
                    : "Level " + d.Level + ": " + d.Gold + " gold, " + d.Wax + " wax, " + Duration(d.Minutes * 60_000L);
                rows.Add(UpgradeRow(UpgradeKinds.Defence(type), EditorUI.Name(type) + " " + level, next,
                    d != null && d.Level <= cap && _state.Gold >= d.Gold && _state.Wax >= d.Wax, cfg));
            }

            return rows;
        }

        private RaidUI.SheetRow UpgradeRow(string kind, string current, string next, bool affordable, EconomyConfig cfg)
        {
            RunningUpgradeReply running = _state.Running?.Find(r => r.Kind == kind);
            if (running != null)
            {
                long left = running.EndsMs - ServerNowMs;
                var row = new RaidUI.SheetRow { Text = current + " -> " + running.ToLevel + "\n" + (left > 0 ? "Ready in " + Duration(left) : "Finishing...") };
                if (left > 0) // when time is up the server completes it; nothing to press
                {
                    long gems = cfg.SpeedUp.GemsFor(left);
                    row.Button("Finish now (" + gems + " gems)", () => Act(() => _economy.FinishUpgradeAsync(kind, true)), _state.Gems >= gems);
                    int used = AdsUsed("upgrade_speedup");
                    AdPlacement p = cfg.Placement("upgrade_speedup");
                    row.Button("Watch ad: -" + p.Minutes + " min (" + used + "/" + p.Daily + ")", () => Ad("upgrade_speedup", kind),
                        RewardedAds.Available && used < p.Daily);
                }

                return row;
            }

            if (next == null)
            {
                return new RaidUI.SheetRow { Text = current + "\nAt the top level" };
            }

            return new RaidUI.SheetRow { Text = current + "\nNext: " + next }
                .Button("Start", () => Act(() => _economy.StartUpgradeAsync(kind)), affordable);
        }

        // ---- Heist Pass -----------------------------------------------------------------------

        /// <summary>The Heist Pass as a ladder of tiers (RaidUI.ShowLadder).</summary>
        private void ShowLadder(EconomyConfig cfg)
        {
            PassConfig p = cfg.Pass;
            long intoTier = _state.PassXp - (long)_state.PassTier * p.XpPerTier;
            bool complete = _state.PassTier >= p.Tiers;
            var tiers = new List<RaidUI.LadderTier>();
            foreach (PassReward r in p.Rewards)
            {
                tiers.Add(new RaidUI.LadderTier
                {
                    Tier = r.Tier,
                    FreeIcon = r.Free.IsEmpty ? null : IconFor(r.Free),
                    FreeText = Short(r.Free),
                    PremiumIcon = r.Premium.IsEmpty ? null : IconFor(r.Premium),
                    PremiumText = Short(r.Premium),
                    Reached = r.Tier <= _state.PassTier,
                    FreeClaimed = _state.PassClaimedFree?.Contains(r.Tier) == true,
                    PremiumClaimed = _state.PassClaimedPremium?.Contains(r.Tier) == true,
                    ClaimFree = () => ClaimTier(r.Tier, false, cfg),
                    ClaimPremium = () => ClaimTier(r.Tier, true, cfg),
                });
            }

            string id = "heist_pass_" + p.Season;
            _ui.ShowLadder("Season " + p.Season, _state.PassTier, p.Tiers, complete ? 1f : intoTier / (float)p.XpPerTier,
                complete ? "Season complete" : intoTier + " / " + p.XpPerTier + " XP to tier " + (_state.PassTier + 1),
                _state.PassPremium, "Unlock premium  " + Shop.Price(id), () => Buy(id), tiers);
        }

        /// <summary>Which icon shows a reward on the ladder.</summary>
        private static string IconFor(Grant g)
        {
            if (g.Cosmetics != null && g.Cosmetics.Count > 0) return "skins";
            if (g.Gems > 0) return "gem";
            if (g.Wax > 0) return "wax";
            if (g.Gold > 0) return "coin";
            if (g.Workshops > 0) return "build";
            if (g.Club) return "crown";
            return "pass";
        }

        /// <summary>A reward in a few words for a ladder card ("500", "Pip").</summary>
        private static string Short(Grant g)
        {
            if (g == null || g.IsEmpty) return "";
            if (g.Cosmetics != null && g.Cosmetics.Count > 0) return Pretty(g.Cosmetics[0]);
            if (g.Gems > 0) return g.Gems.ToString("N0");
            if (g.Wax > 0) return g.Wax.ToString("N0");
            if (g.Gold > 0) return g.Gold.ToString("N0");
            if (g.Workshops > 0) return "+" + g.Workshops + " workshop";
            return Describe(g);
        }

        private void ClaimTier(int tier, bool premium, EconomyConfig cfg)
        {
            Act(async () =>
            {
                EconomyReply r = await _economy.ClaimPassTierAsync(tier, premium);
                if (r.Ok)
                {
                    int days = (int)(DateTime.UtcNow - cfg.Pass.StartsUtc).TotalDays;
                    Telemetry.PassTier(tier, days);
                }

                return r;
            });
        }

        // ---- Achievements -----------------------------------------------------------------------

        private List<RaidUI.SheetRow> Achievements(EconomyConfig cfg)
        {
            var rows = new List<RaidUI.SheetRow>();
            foreach (Achievement a in cfg.Achievements)
            {
                AchievementProgressReply p = _state.Achievements?.Find(x => x.Id == a.Id) ?? new AchievementProgressReply { Id = a.Id };
                string medal = p.Claimed == 0 ? "" : " (" + TierName(p.Claimed) + ")";
                if (p.Claimed >= a.Tiers.Count)
                {
                    rows.Add(new RaidUI.SheetRow { Text = a.Title + medal + "\nAll tiers done: " + p.Value, Style = "held" });
                    continue;
                }

                long target = a.Tiers[p.Claimed];
                Grant reward = p.Claimed < a.Grants.Count ? a.Grants[p.Claimed] : null;
                rows.Add(new RaidUI.SheetRow { Text = a.Title + medal + "\n" + p.Value + " / " + target + " for " + TierName(p.Claimed + 1) + ": " + Describe(reward) }
                    .Button("Claim " + TierName(p.Claimed + 1), () => Act(() => _economy.ClaimAchievementAsync(a.Id)), p.Value >= target));
            }

            return rows;
        }

        private static string TierName(int tier)
        {
            return tier == 1 ? "bronze" : tier == 2 ? "silver" : "gold";
        }

        // ---- Shop ------------------------------------------------------------------------------

        private List<RaidUI.SheetRow> ShopRows(EconomyConfig cfg)
        {
            var rows = new List<RaidUI.SheetRow>();
            AdPlacement chest = cfg.Placement("shop_chest");
            int used = AdsUsed("shop_chest");
            rows.Add(new RaidUI.SheetRow { Text = "Free chest: " + Describe(chest.Grant) + "\n" + used + " of " + chest.Daily + " today" }
                .Button("Watch an ad", () => Ad("shop_chest", null), RewardedAds.Available && used < chest.Daily));

            bool club = _state.ClubUntilMs > ServerNowMs;
            if (club)
            {
                rows.Add(new RaidUI.SheetRow { Text = "Wick Club: " + cfg.Club.DailyGems + " gems a day" }
                    .Button("Claim today's gems", () => Act(() => _economy.ClaimClubGemsAsync())));
            }

            foreach (Wick.Economy.Product p in cfg.Products)
            {
                bool owned = p.Kind == "nonConsumable" && OwnsAll(p.Grant) || p.Grant.Club && club;
                rows.Add(new RaidUI.SheetRow { Text = Pretty(p.Id) + "\n" + Describe(p.Grant) }
                    .Button(owned ? "Owned" : Shop.Price(p.Id), () => Buy(p.Id), !owned));
            }

            return rows;
        }

        private bool OwnsAll(Grant g)
        {
            if (!string.IsNullOrEmpty(g.PassPremium))
            {
                return _state.PassPremium && g.PassPremium == _state.PassSeason;
            }

            if (g.Cosmetics == null || g.Cosmetics.Count == 0)
            {
                return false;
            }

            foreach (string c in g.Cosmetics)
            {
                if (_state.Cosmetics == null || !_state.Cosmetics.Contains(c))
                {
                    return false;
                }
            }

            return true;
        }

        // ---- Actions -----------------------------------------------------------------------------

        private async void Act(Func<Task<EconomyReply>> call)
        {
            if (_busy)
            {
                return;
            }

            _busy = true;
            try
            {
                EconomyReply r = await call();
                _ui.SetSheetMessage(r.Ok ? (r.Granted != null && !r.Granted.IsEmpty ? r.Reason + ": " + Describe(r.Granted) : r.Reason) : r.Reason, r.Ok);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                _ui.SetSheetMessage("Couldn't reach the server. Check your connection.", false);
            }
            finally
            {
                _busy = false;
            }

            await Refresh();
        }

        private async void Ad(string placement, string context)
        {
            if (_busy)
            {
                return;
            }

            _busy = true;
            try
            {
                AdOutcome o = await _ads.ShowAsync(placement, context);
                _ui.SetSheetMessage(o.Granted ? "Reward: " + (o.Grant != null && !o.Grant.IsEmpty ? Describe(o.Grant) : "done") : o.Reason, o.Granted);
            }
            finally
            {
                _busy = false;
            }

            await Refresh();
        }

        private async void Buy(string productId)
        {
            if (_busy)
            {
                return;
            }

            _busy = true;
            _ui.SetSheetMessage("Opening the store...", null);
            try
            {
                PurchaseReply r = await Shop.BuyAsync(productId);
                _ui.SetSheetMessage(r.Ok ? (r.NewlyGranted ? "Thanks! " + Describe(r.Grant) : "You already have that") : r.Reason, r.Ok);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                _ui.SetSheetMessage("The store isn't available right now.", false);
            }
            finally
            {
                _busy = false;
            }

            await Refresh();
        }

        private int AdsUsed(string placement)
        {
            return _state.AdsToday != null && _state.AdsToday.TryGetValue(placement, out int n) ? n : 0;
        }

        // ---- Text ----------------------------------------------------------------------------------

        public static string Describe(Grant g)
        {
            if (g == null || g.IsEmpty)
            {
                return "nothing";
            }

            var parts = new List<string>();
            if (g.Gold > 0) parts.Add(g.Gold + " gold");
            if (g.Wax > 0) parts.Add(g.Wax + " wax");
            if (g.Gems > 0) parts.Add(g.Gems + " gems");
            if (g.Workshops > 0) parts.Add(g.Workshops + " workshop" + (g.Workshops == 1 ? "" : "s"));
            foreach (string c in g.Cosmetics ?? new List<string>()) parts.Add(Pretty(c));
            if (!string.IsNullOrEmpty(g.PassPremium)) parts.Add("Heist Pass premium");
            if (g.Club) parts.Add("Wick Club");
            return string.Join(", ", parts);
        }

        private static string Pretty(string id)
        {
            var sb = new StringBuilder();
            foreach (string word in id.Split('_'))
            {
                if (word.Length > 0)
                {
                    sb.Append(sb.Length > 0 ? " " : "").Append(char.ToUpperInvariant(word[0])).Append(word.Substring(1));
                }
            }

            return sb.ToString();
        }

        // ---- Skins ----------------------------------------------------------------------------

        private List<RaidUI.SheetRow> SkinRows()
        {
            var rows = new List<RaidUI.SheetRow>();
            if (_skins == null)
            {
                return rows;
            }

            string equipped = _skins.EquippedId;
            foreach (SkinListing s in _skins.Skins)
            {
                bool owned = s.Id == SkinCatalog.DefaultId || _state.Cosmetics?.Contains(s.Id) == true;
                bool on = s.Id == equipped;
                string text = s.DisplayName + (s.StandIn ? "  (placeholder model)" : "") + "\n" +
                    (on ? "Equipped" : owned ? "Owned" : "From the shop or the Heist Pass");
                rows.Add(new RaidUI.SheetRow { Text = text, Style = on ? "held" : null }
                    .Button(on ? "Equipped" : "Equip", () => Equip(s.Id), owned && !on));
            }

            if (_themes != null)
            {
                string current = _themes.CurrentId;
                foreach (ThemeListing t in _themes.Themes)
                {
                    // Themes are on sale later; development builds can try them all now.
                    bool owned = t.Id == ThemeCatalog.DefaultId || Debug.isDebugBuild || _state.Cosmetics?.Contains(t.Id) == true;
                    rows.Add(new RaidUI.SheetRow
                    {
                        Text = "Vault theme: " + t.DisplayName + (t.StandIn ? "  (placeholder kit)" : "") + "\n" + (t.Id == current ? "In use" : owned ? "Owned" : "Coming soon"),
                        Style = t.Id == current ? "held" : null,
                    }.Button(t.Id == current ? "In use" : "Use", () =>
                    {
                        ThemeCatalog.Choose(t.Id);
                        _ = ContentLoader.PrefetchAsync(_skins, _themes);
                        _ui.SetSheetMessage("Vaults now show as " + t.DisplayName + ".", true);
                        Draw();
                    }, owned && t.Id != current));
                }
            }

            return rows;
        }

        private void Equip(string id)
        {
            if (SkinCatalog.Equip(id, _state.Cosmetics))
            {
                _ = ContentLoader.PrefetchAsync(_skins, _themes);
                _ui.SetSheetMessage("Equipped. Your next raid uses it.", true);
            }

            Draw();
        }

        // ---- Privacy ------------------------------------------------------------------------

        public const string PrivacyPolicyUrl = "https://" + VaultLink.Domain + "/privacy";

        /// <summary>The player ID (what a data request needs), ad choices where the region allows, and the policy.</summary>
        /// <summary>Sound, haptics, then the privacy items (player ID, ad choices, policy).</summary>
        private void DrawSettings()
        {
            var rows = new List<RaidUI.SheetRow>
            {
                VolumeRow("Music", AudioPrefs.Music),
                VolumeRow("Sound effects", AudioPrefs.Sfx),
                VolumeRow("Button sounds", AudioPrefs.Ui),
                new RaidUI.SheetRow { Text = "Vibration: " + (AudioPrefs.Haptics ? "on" : "off") }
                    .Button(AudioPrefs.Haptics ? "Turn off" : "Turn on", () =>
                    {
                        AudioPrefs.Haptics = !AudioPrefs.Haptics;
                        Haptics.Play(HapticStrength.Medium); // a taste of it when turned on
                        DrawSettings();
                    }),
                new RaidUI.SheetRow { Text = "Glow on flames: " + (Grade.Glow ? "on" : "off") + "\nTurn off if the game runs slowly." }
                    .Button(Grade.Glow ? "Turn off" : "Turn on", () =>
                    {
                        Grade.Glow = !Grade.Glow;
                        DrawSettings();
                    }),
            };
            rows.AddRange(PrivacyRows());
            _ui.ShowSheet("Settings", Consent.Personalised ? "Ads may be personalised." : "Ads are not personalised.", rows);
        }

        private RaidUI.SheetRow VolumeRow(string label, string channel)
        {
            float level = AudioPrefs.Get(channel);
            void Change(float by)
            {
                AudioPrefs.Set(channel, Mathf.Round((AudioPrefs.Get(channel) + by) * 10f) / 10f);
                AudioDirector.ApplyVolumes();
                DrawSettings();
            }

            return new RaidUI.SheetRow { Text = label + ": " + Mathf.RoundToInt(level * 100f) + "%" }
                .Button("-", () => Change(-0.1f), level > 0f)
                .Button("+", () => Change(0.1f), level < 1f);
        }

        private List<RaidUI.SheetRow> PrivacyRows()
        {
            string id = UgsBootstrap.PlayerId;
            var rows = new List<RaidUI.SheetRow>
            {
                new RaidUI.SheetRow { Text = "Your player ID: " + (id ?? "not signed in yet") + "\nInclude it if you email us about your data." }
                    .Button("Copy ID", () =>
                    {
                        GUIUtility.systemCopyBuffer = id;
                        _ui.SetSheetMessage("Player ID copied.", true);
                    }, id != null),
            };

            if (Consent.PrivacyOptionsAvailable)
            {
                rows.Add(new RaidUI.SheetRow { Text = "Change what you agreed to for ads and analytics." }
                    .Button("Ad privacy choices", ChangeAdChoices));
            }

            rows.Add(new RaidUI.SheetRow { Text = "How we use your information." }
                .Button("Privacy policy", () => Application.OpenURL(PrivacyPolicyUrl)));
            return rows;
        }

        private async void ChangeAdChoices()
        {
            await Consent.ShowPrivacyOptionsAsync();
            DrawSettings();
            _ui.SetSheetMessage("Your choices are saved.", true);
        }

        private static string Duration(long ms)
        {
            long s = Math.Max(0, (ms + 999) / 1000);
            if (s < 60) return s + " s";
            if (s < 3600) return s / 60 + " min " + (s % 60 > 0 ? s % 60 + " s" : "");
            return s / 3600 + " h " + s % 3600 / 60 + " min";
        }

        private static string Title(string screen)
        {
            switch (screen)
            {
                case "pass": return "Heist Pass";
                case "achievements": return "Achievements";
                case "shop": return "Shop";
                case "privacy":
                case "settings": return "Settings";
                case "skins": return "Skins and themes";
                default: return "Upgrades";
            }
        }
    }
}
