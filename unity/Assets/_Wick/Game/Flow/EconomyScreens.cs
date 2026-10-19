using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Wick.Economy;
using Wick.Services;

namespace Wick.Game
{
    /// <summary>
    /// Home's economy screens: Upgrades (Vault Level and Lantern, timers, gems, ad speed-ups),
    /// Heist Pass, Achievements and Shop. All numbers come from the server's state and config;
    /// every action is a server call, and the screen redraws from the server's answer.
    /// </summary>
    public sealed class EconomyScreens : MonoBehaviour
    {
        private RaidUI _ui;
        private readonly EconomyClient _economy = new EconomyClient();
        private readonly RewardedAds _ads = new RewardedAds();
        private string _screen;
        private EconomyStateReply _state;
        private float _refreshAt;
        private bool _busy;

        public void Bind(RaidUI ui)
        {
            _ui = ui;
        }

        public bool Showing => _screen != null;

        public async void Show(string screen)
        {
            _screen = screen;
            _ui.ShowSheet(Title(screen), "Loading...", new List<RaidUI.SheetRow>());
            _ui.SetSheetMessage(null, null);
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
            // Timers count down on screen; the server stays the clock that counts.
            if (_screen == "upgrades" && _state != null && Time.unscaledTime > _refreshAt)
            {
                _refreshAt = Time.unscaledTime + 1f;
                Draw();
            }
        }

        private async Task Refresh()
        {
            try
            {
                await _economy.RefreshConfigAsync();
                _state = await _economy.StateAsync();
                _stateAtLocal = Time.realtimeSinceStartupAsDouble;
                Draw();
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
                    _ui.ShowSheet("Heist Pass", PassHeader(cfg), Pass(cfg));
                    break;
                case "achievements":
                    _ui.ShowSheet("Achievements", wallet, Achievements(cfg));
                    break;
                case "shop":
                    _ui.ShowSheet("Shop", wallet, ShopRows(cfg));
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
                    " (a slightly bigger light, from the next balance update)",
                l != null && _state.Wax >= l.Wax && _state.VaultLevel >= l.MinVaultLevel, cfg));
            return rows;
        }

        private RaidUI.SheetRow UpgradeRow(string kind, string current, string next, bool affordable, EconomyConfig cfg)
        {
            RunningUpgradeReply running = _state.Running?.Find(r => r.Kind == kind);
            if (running != null)
            {
                long left = running.EndsMs - ServerNowMs;
                var row = new RaidUI.SheetRow { Text = current + " -> " + running.ToLevel + "\n" + (left > 0 ? "Ready in " + Duration(left) : "Ready!") };
                if (left <= 0)
                {
                    row.Button("Finish", () => Act(() => _economy.FinishUpgradeAsync(kind, false)));
                }
                else
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

        private string PassHeader(EconomyConfig cfg)
        {
            PassConfig p = cfg.Pass;
            long intoTier = _state.PassXp - (long)_state.PassTier * p.XpPerTier;
            return "Season " + p.Season + "  |  tier " + _state.PassTier + " of " + p.Tiers + "  |  " +
                (_state.PassTier >= p.Tiers ? "complete" : intoTier + "/" + p.XpPerTier + " XP to the next") +
                (_state.PassPremium ? "  |  Premium" : "");
        }

        private List<RaidUI.SheetRow> Pass(EconomyConfig cfg)
        {
            var rows = new List<RaidUI.SheetRow>();
            if (!_state.PassPremium)
            {
                string id = "heist_pass_" + cfg.Pass.Season;
                rows.Add(new RaidUI.SheetRow { Text = "Unlock the premium row: every tier you've passed unlocks too." }
                    .Button("Buy Heist Pass (" + Shop.Price(id) + ")", () => Buy(id)));
            }

            foreach (PassReward r in cfg.Pass.Rewards)
            {
                bool reached = r.Tier <= _state.PassTier;
                bool freeClaimed = _state.PassClaimedFree?.Contains(r.Tier) == true;
                bool premiumClaimed = _state.PassClaimedPremium?.Contains(r.Tier) == true;
                var row = new RaidUI.SheetRow
                {
                    Text = "Tier " + r.Tier + (reached ? "" : " (locked)") + "\nFree: " + Describe(r.Free) + "\nPremium: " + Describe(r.Premium),
                    Style = reached ? "held" : null,
                };
                if (!r.Free.IsEmpty)
                {
                    row.Button(freeClaimed ? "Claimed" : "Claim", () => ClaimTier(r.Tier, false, cfg), reached && !freeClaimed);
                }

                row.Button(premiumClaimed ? "Claimed" : "Claim premium", () => ClaimTier(r.Tier, true, cfg),
                    reached && !premiumClaimed && _state.PassPremium);
                rows.Add(row);
            }

            return rows;
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
                default: return "Upgrades";
            }
        }
    }
}
