using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using Wick.Economy;
using Wick.Services;
using Wick.Sim;

namespace Wick.Game
{
    /// <summary>
    /// The raid scene's flow. Home offers Raid (matchmaking), Raids on your vault (the defence log
    /// with Watch replay and Revenge) and the editor. A server raid gets a single-use token, plays
    /// with a 5 s scout flyover, and its loot counts only if the server's re-simulation verifies it.
    /// Dev builds add offline dev vaults, golden replays, your own live vault and raid-by-id, and
    /// every results screen compares the live hash with a headless replay of the recorded log.
    /// </summary>
    public sealed class RaidFlow : MonoBehaviour
    {
        [SerializeField] private RaidRunner _runner;
        [SerializeField] private RaidUI _ui;
        [SerializeField] private RaidCamera _camera;
        [SerializeField] private RaidTuning _tuning;

        private readonly List<TextAsset> _vaults = new List<TextAsset>();
        private readonly List<TextAsset> _goldens = new List<TextAsset>();
        private readonly WickBackend _backend = new WickBackend();
        private readonly CloudSaveClient _cloudSave = new CloudSaveClient();
        private int _vaultIndex = -1;
        private float _scoutLeft;
        private RaidFile _replaying;
        private System.Random _seeds;
        private RaidLaunch.Request _launched;

        /// <summary>How the current server raid got its token, so Retry can get another (they're single use).</summary>
        private Func<Task<RaidStartReply>> _serverStart;

        private string _serverBanner;
        private RaidStartReply _serverRaid;
        private ReplayReply _serverReplay;

        // The raid on the results screen, for its share clip and link.
        private VaultDef _lastVault;
        private int _lastSeed;
        private InputLog _lastLog;
        private RaidParams _lastParams = RaidParams.Default;
        private string _lastCode;
        private string _clipPath;
        private ClipRenderer _clips;

        private EconomyScreens _screens;

        /// <summary>The vault shown behind Home (the player's latest draft, else a template). Never raided.</summary>
        private VaultDef _homeVault;
        private Light[] _homeLights;
        private static bool _titleShown;
        private readonly EconomyClient _economy = new EconomyClient();
        private readonly RewardedAds _ads = new RewardedAds();
        private string _lastRaidId;
        private int _lastHitBy;
        private bool _quitLogged;

        public void Bind(RaidRunner runner, RaidUI ui, RaidCamera camera, RaidTuning tuning)
        {
            _runner = runner;
            _ui = ui;
            _camera = camera;
            _tuning = tuning;
        }

        private void Awake()
        {
            Application.targetFrameRate = 60;
            _seeds = new System.Random();
        }

        private void OnEnable()
        {
            _runner.Ended += OnEnded;
            _ui.VaultPicked += StartVault;
            _ui.GoldenPicked += StartGolden;
            _ui.RetryPressed += Retry;
            _ui.MenuPressed += OnMenu;
            _ui.EditorPressed += OpenEditor;
            _ui.ServerRaidPressed += RaidOwnVault;
            _ui.RaidPressed += RaidMatch;
            _ui.RaidByIdPressed += RaidById;
            _ui.RevengePressed += Revenge;
            _ui.DefencePressed += ShowDefence;
            _ui.DefenceBackPressed += ShowPicker;
            _ui.WatchPressed += Watch;
            _ui.ReplayPausePressed += TogglePause;
            _ui.ReplayScrubbed += Scrub;
            _ui.ReplayBackPressed += LeaveReplay;
            _ui.ShareClipPressed += ShareClip;
            _ui.SkipRaidPressed += SkipRaid;
            _ui.ShareLinkPressed += ShareLink;
            _ui.ShareMyVaultPressed += ShareMyVault;
            _ui.HomeScreenPressed += ShowScreen;
            _ui.SheetBackPressed += ShowPicker;
            _ui.DoubleLootPressed += DoubleLoot;
            _runner.SimEventRaised += TrackEvent;
            LinkRouter.Arrived += OnLink;
        }

        private void OnDisable()
        {
            _runner.Ended -= OnEnded;
            _ui.VaultPicked -= StartVault;
            _ui.GoldenPicked -= StartGolden;
            _ui.RetryPressed -= Retry;
            _ui.MenuPressed -= OnMenu;
            _ui.EditorPressed -= OpenEditor;
            _ui.ServerRaidPressed -= RaidOwnVault;
            _ui.RaidPressed -= RaidMatch;
            _ui.RaidByIdPressed -= RaidById;
            _ui.RevengePressed -= Revenge;
            _ui.DefencePressed -= ShowDefence;
            _ui.DefenceBackPressed -= ShowPicker;
            _ui.WatchPressed -= Watch;
            _ui.ReplayPausePressed -= TogglePause;
            _ui.ReplayScrubbed -= Scrub;
            _ui.ReplayBackPressed -= LeaveReplay;
            _ui.ShareClipPressed -= ShareClip;
            _ui.ShareLinkPressed -= ShareLink;
            _ui.ShareMyVaultPressed -= ShareMyVault;
            _ui.HomeScreenPressed -= ShowScreen;
            _ui.SheetBackPressed -= ShowPicker;
            _ui.DoubleLootPressed -= DoubleLoot;
            _runner.SimEventRaised -= TrackEvent;
            LinkRouter.Arrived -= OnLink;
        }

        private void Start()
        {
            _vaults.AddRange(Load("DevVaults"));
            _goldens.AddRange(Load("DevGoldens"));
            var goldenNames = new List<string>();
            foreach (TextAsset g in _goldens)
            {
                goldenNames.Add(g.name);
            }

            _ui.SetGoldens(goldenNames);

            // Loaded on top of the editor by its Test button: play that vault straight away.
            _launched = RaidLaunch.Pending;
            if (_launched != null)
            {
                _ui.SetMenuLabel("Back to editor");
                StartLaunched();
                return;
            }

            _screens = gameObject.AddComponent<EconomyScreens>();
            VaultView vaultView = FindAnyObjectByType<VaultView>();
            WickSkin wick = vaultView != null && vaultView.Registry != null && vaultView.Registry.Wick != null
                ? vaultView.Registry.Wick.GetComponent<WickSkin>()
                : null;
            SkinCatalog skins = wick != null ? wick.Catalog : null;
            ThemeCatalog themes = vaultView != null && vaultView.Registry != null ? vaultView.Registry.Themes : null;
            _screens.Bind(_ui, skins, themes);
            _ = ContentLoader.PrefetchAsync(skins, themes); // the equipped skin and chosen theme, ready for the next raid
            ShowPicker();
            if (!_titleShown)
            {
                _titleShown = true;
                StartCoroutine(Title());
            }

            SettleConsent();
            LinkRouter.Init(this); // already done by Boot; this covers starting in the Raid scene
            IncomingLink link = LinkRouter.Take();
            if (link != null)
            {
                OnLink(link);
            }
        }

        private bool AtHome => _homeVault != null && _runner.Vault == _homeVault;

        private void Update()
        {
            if (_homeLights != null)
            {
                foreach (Light l in _homeLights)
                {
                    l.enabled = AtHome;
                }
            }

            if (_runner.Mode != RaidMode.Scout || AtHome)
            {
                return; // Home's vault is scenery: its clock never starts
            }

            _scoutLeft -= Time.deltaTime;
            if (_scoutLeft <= 0f)
            {
                _camera.EndScout();
                _runner.StartClock();
                _ui.SetBanner(null);
                _ui.ShowSkip(false);
            }
        }

        // ---- Home -------------------------------------------------------------------------

        private static int ClaimablePassTiers(EconomyStateReply s)
        {
            EconomyConfig cfg = EconomyClient.Config;
            int n = 0;
            foreach (PassReward r in cfg.Pass.Rewards)
            {
                if (r.Tier > s.PassTier)
                {
                    continue;
                }

                n += !r.Free.IsEmpty && s.PassClaimedFree?.Contains(r.Tier) != true ? 1 : 0;
                n += s.PassPremium && s.PassClaimedPremium?.Contains(r.Tier) != true ? 1 : 0;
            }

            return n;
        }

        private static int ClaimableAchievements(EconomyStateReply s)
        {
            int n = 0;
            foreach (AchievementProgressReply p in s.Achievements ?? new List<AchievementProgressReply>())
            {
                Achievement a = EconomyClient.Config.Achievement(p.Id);
                n += a != null && p.Claimed < a.Tiers.Count && p.Value >= a.Tiers[p.Claimed] ? 1 : 0;
            }

            return n;
        }

        /// <summary>First launch asks the age question; then consent settles and rewarded ads can load.</summary>
        private void SettleConsent()
        {
            if (Consent.Settled)
            {
                RewardedAds.Init();
                return;
            }

            if (Consent.Adult == null)
            {
                _ui.ShowAgePrompt(adult =>
                {
                    Consent.SetAdult(adult);
                    SettleThenAds();
                });
            }
            else
            {
                SettleThenAds();
            }
        }

        private async void SettleThenAds()
        {
            await Consent.SettleAsync();
            try
            {
                await _backend.SignInAsync();
                RewardedAds.Init();
            }
            catch (Exception e)
            {
                Debug.LogWarning("Ads wait for sign-in: " + e.Message);
            }
        }

        private void ShowScreen(string screen)
        {
            _screens.Show(screen);
        }

        private void ShowPicker()
        {
            _screens?.Close();
            ClearModes();
            ShowHomeVault();
            var names = new List<string>();
            foreach (TextAsset v in _vaults)
            {
                names.Add(v.name);
            }

            _ui.SetMenuLabel("Home");
            _ui.ShowPicker(names);
            RefreshHome();
        }

        /// <summary>The title art, a load bar until signed in (or a few seconds), then "Tap to play".</summary>
        private IEnumerator Title()
        {
            bool tapped = false;
            void OnTap() => tapped = true;
            string version = "v" + Application.version;
            float started = Time.unscaledTime;
            while (!UgsBootstrap.IsSignedIn && Time.unscaledTime - started < 6f)
            {
                _ui.ShowTitle(version, Mathf.Clamp01((Time.unscaledTime - started) / 3f) * 0.9f, false);
                yield return null;
            }

            _ui.ShowTitle(version, 1f, true);
            _ui.TitleTapped += OnTap;
            while (!tapped)
            {
                yield return null;
            }

            _ui.TitleTapped -= OnTap;
            _ui.HideTitle();
        }

        /// <summary>Home's backdrop: your latest vault (or a template), warmly lit, framed whole.</summary>
        private void ShowHomeVault()
        {
            _homeVault = HomeVault();
            _runner.LoadLive(_homeVault, 1); // presentation only: the clock never starts at Home
            _camera.ShowHome(_homeVault);
            if (_homeLights == null)
            {
                _homeLights = new Light[2];
                for (int i = 0; i < _homeLights.Length; i++)
                {
                    var go = new GameObject("Home light " + i);
                    go.transform.SetParent(transform, false);
                    Light l = go.AddComponent<Light>();
                    l.type = LightType.Point;
                    l.shadows = LightShadows.None;
                    l.color = i == 0 ? new Color(1f, 0.66f, 0.34f) : new Color(1f, 0.78f, 0.45f);
                    _homeLights[i] = l;
                }
            }

            // A wide warm wash over the middle and a gold glow over the hoard, like torchlight.
            Vector3 middle = SimPose.TileCenter(new TilePos(_homeVault.Width / 2, _homeVault.Height / 2));
            _homeLights[0].transform.position = middle + Vector3.up * 3.2f;
            _homeLights[0].range = Mathf.Max(_homeVault.Width, _homeVault.Height) * 1.1f;
            _homeLights[0].intensity = 7f;
            Vector3 hoard = _homeVault.HasHoard
                ? SimPose.TileCenter(new TilePos((_homeVault.Hoard.MinX + _homeVault.Hoard.MaxX) / 2, (_homeVault.Hoard.MinY + _homeVault.Hoard.MaxY) / 2))
                : middle;
            _homeLights[1].transform.position = hoard + Vector3.up * 2f;
            _homeLights[1].range = 7f;
            _homeLights[1].intensity = 5f;
        }

        private static VaultDef HomeVault()
        {
            try
            {
                var store = new DraftStore(Application.persistentDataPath);
                List<DraftStore.Entry> drafts = store.List();
                if (drafts.Count > 0)
                {
                    return store.Load(drafts[0].DraftId).ToVaultDef();
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("Home vault: " + e.Message);
            }

            TextAsset[] templates = Resources.LoadAll<TextAsset>("VaultTemplates");
            return VaultJson.Parse(templates[0].text);
        }

        private static string UpgradeName(string kind)
        {
            if (kind == UpgradeKinds.VaultLevel)
            {
                return "Vault Level";
            }

            if (kind == UpgradeKinds.Decoys)
            {
                return "Decoys";
            }

            return UpgradeKinds.TryParseDefence(kind, out ObjectType type) ? EditorUI.Name(type) : "Lantern";
        }

        /// <summary>Wallet, Crowns, the running upgrade and badges, from the server's economy state.</summary>
        private async void RefreshHome()
        {
            _ui.SetHomeStats("Connecting...");
            try
            {
                EconomyStateReply s = await _economy.StateAsync();
                _ui.SetWallet(s.Gold, s.Wax, s.Gems, s.Crowns);
                RunningUpgradeReply running = s.Running != null && s.Running.Count > 0 ? s.Running[0] : null;
                _ui.SetHomeStats(s.VaultNeedsRetest
                    ? "Your defences got stronger: test your vault again to use them"
                    : running != null
                        ? UpgradeName(running.Kind) + " " + running.ToLevel + " building"
                        : "Vault Level " + s.VaultLevel + "  ·  " + s.Stars + " ★");
                _ui.SetBadge("pass", ClaimablePassTiers(s));
                _ui.SetBadge("trophies", ClaimableAchievements(s));
            }
            catch (Exception e)
            {
                Debug.LogWarning("Home stats: " + e.Message);
                _ui.SetHomeStats("Offline");
            }
        }

        private void ClearModes()
        {
            _replaying = null;
            _vaultIndex = -1;
            _serverStart = null;
            _serverRaid = null;
            _serverReplay = null;
        }

        private void OpenEditor()
        {
            SceneManager.LoadScene("Editor");
        }

        private void OnMenu()
        {
            if (_launched != null)
            {
                _runner.Unload();
                RaidLaunch.End(); // the editor unloads this scene
                return;
            }

            if (_serverReplay != null)
            {
                ShowDefence();
                return;
            }

            ShowPicker();
        }

        // ---- Offline raids (dev vaults, editor Test) -----------------------------------------

        private void StartVault(int index)
        {
            ClearModes();
            _vaultIndex = index;
            VaultDef vault = VaultJson.Parse(_vaults[index].text);
            _runner.LoadLive(vault, _seeds.Next());
            BeginScout("SCOUTING");
        }

        private void StartLaunched()
        {
            _replaying = null;
            _runner.LoadLive(_launched.Vault, _seeds.Next(), _launched.Params);
            BeginScout("TEST RAID");
        }

        private void BeginScout(string banner)
        {
            _camera.BeginScout();
            _scoutLeft = _tuning.ScoutSeconds;
            _ui.ShowHud(banner);
        }

        // ---- Server raids ------------------------------------------------------------------

        private void RaidMatch()
        {
            StartServerRaid(() => _backend.GetRaidTargetAsync(), "RAID");
        }

        private void RaidOwnVault()
        {
            StartServerRaid(() => _backend.StartFriendRaidAsync(WickBackend.MyVaultId), "YOUR VAULT");
        }

        private void RaidById(string vaultId)
        {
            if (string.IsNullOrEmpty(vaultId))
            {
                _ui.ShowPickerMessage("Type a vault id first");
                return;
            }

            StartServerRaid(() => _backend.StartFriendRaidAsync(vaultId), "FRIEND RAID");
        }

        /// <summary>A vault link, install referrer or pasted code: raid that vault.</summary>
        private void OnLink(IncomingLink link)
        {
            LinkRouter.Take();
            if (_launched != null || (_clips != null && _clips.Busy))
            {
                return;
            }

            Telemetry.LinkOpened(link.Code, link.Source == "link", link.Source);
            if (link.NeedsConfirm)
            {
                _ui.ShowLinkPrompt(link.Code, () => RaidById(link.Code));
            }
            else
            {
                RaidById(link.Code);
            }
        }

        /// <summary>
        /// Mean vault protection: during the scout, drop this vault for free (the server returns the
        /// raid-limit slot) and serve another.
        /// </summary>
        private async void SkipRaid()
        {
            RaidStartReply raid = _serverRaid;
            if (raid == null || _runner.Mode != RaidMode.Scout)
            {
                return;
            }

            _ui.ShowSkip(false);
            try
            {
                RaidStartReply skipped = await _backend.SkipRaidAsync(raid.Token);
                if (!skipped.Ok)
                {
                    _ui.SetBanner(skipped.Reason);
                    return;
                }

                _serverRaid = null;
                RaidMatch();
            }
            catch (Exception e)
            {
                Debug.LogWarning("Skip: " + e.Message);
            }
        }

        private void Revenge(string vaultId)
        {
            StartServerRaid(() => _backend.StartFriendRaidAsync(vaultId, "revenge"), "REVENGE");
        }

        private async void StartServerRaid(Func<Task<RaidStartReply>> start, string banner)
        {
            ClearModes();
            _serverStart = start;
            _serverBanner = banner;
            _runner.Unload();
            _ui.ShowHud("CONNECTING");
            try
            {
                await _backend.SignInAsync();
                RaidStartReply reply = await start();
                if (!reply.Ok)
                {
                    ShowPicker();
                    _ui.ShowPickerMessage(reply.Reason);
                    return;
                }

                _serverStart = start;
                _serverBanner = banner;
                _serverRaid = reply;
                _ui.SetMenuLabel("Home");
                _runner.LoadLive(VaultJson.Parse(reply.Vault), reply.Seed, ParamsFor(reply.RaidTicks, reply.LightBonus, reply.Decoys));
                BeginScout(banner);
                _ui.ShowSkip(reply.Kind == "match"); // only matchmaking raids can be skipped
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                ShowPicker();
                _ui.ShowPickerMessage("Couldn't reach the server. Check your connection and try again.");
            }
        }

        /// <summary>Loot counts only if the server's re-simulation of this raid matches it.</summary>
        private async void SubmitServerRaid(RaidStartReply raid, InputLog log, RaidResult result)
        {
            _ui.SetServerVerdict("Checking your raid with the server...", null);
            try
            {
                SubmitReply reply = await _backend.SubmitRaidAsync(raid.Token, log, result);
                if (reply.Verified)
                {
                    _ui.SetResultRewards(reply.Paid, reply.WaxEarned, reply.CrownsDelta);
                    _ui.SetServerVerdict("Server check passed  ·  " + reply.Gold.ToString("N0") + " gold in your vault", true);
                    _lastRaidId = "r_" + raid.Token;
                    if (reply.Paid > 0 && reply.Outcome == "Escaped")
                    {
                        _ui.SetDoubleLoot(RewardedAds.Available);
                    }
                }
                else
                {
                    _ui.SetServerVerdict("Couldn't verify this raid. No loot paid.", false, reply.Reason);
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                _ui.SetServerVerdict("Couldn't reach the server. No loot paid.", false, e.Message);
            }
        }

        // ---- Defence log and replays ----------------------------------------------------------

        private async void ShowDefence()
        {
            _runner.Unload();
            ClearModes();
            _ui.ShowPickerMessage("Loading raids on your vault...");
            try
            {
                await _backend.SignInAsync();
                DefenceLogReply log = await _backend.GetDefenceLogAsync();
                _ui.ShowDefenceLog(log, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                _ui.ShowPickerMessage("Couldn't load the defence log. Check your connection.");
            }
        }

        private async void Watch(string raidId)
        {
            try
            {
                ReplayReply replay = await _backend.GetReplayAsync(raidId);
                if (!replay.Ok)
                {
                    ShowPicker();
                    _ui.ShowPickerMessage(replay.Reason);
                    return;
                }

                if (replay.SimVersion != SimInfo.Version)
                {
                    // Raids are re-simulated to replay them, and this one ran under older rules.
                    ShowPicker();
                    _ui.ShowPickerMessage("That raid was played on an older version of the game, so it can't be replayed.");
                    return;
                }

                ClearModes();
                _serverReplay = replay;
                _ui.SetMenuLabel("Raids on your vault");
                _runner.LoadReplay(VaultJson.Parse(replay.Vault), replay.Seed, InputLog.FromBase64(replay.Log), ParamsFor(replay.RaidTicks, replay.LightBonus, replay.Decoys));
                _camera.EndScout();
                _ui.ShowReplayHud("REPLAY", replay.Ticks);
                _lastCode = replay.Code;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                ShowPicker();
                _ui.ShowPickerMessage("Couldn't load that replay.");
            }
        }

        /// <summary>A server's raid settings; a 0 length comes from a server older than M10 and means 60 s.</summary>
        private static RaidParams ParamsFor(int raidTicks, int lightBonus, int decoys)
        {
            return raidTicks == 0 ? RaidParams.Default : new RaidParams(raidTicks, lightBonus, Math.Max(1, decoys));
        }

        private void StartGolden(int index)
        {
            ClearModes();
            _replaying = RaidFile.Parse(_goldens[index].text);
            _runner.LoadReplay(_replaying.Vault, _replaying.Seed, _replaying.Log, _replaying.Params);
            _camera.EndScout();
            _ui.ShowReplayHud("REPLAY " + _goldens[index].name, _replaying.ExpectedTicks);
        }

        private void TogglePause()
        {
            _runner.Paused = !_runner.Paused;
            _ui.SetReplayPaused(_runner.Paused);
        }

        private void Scrub(int tick)
        {
            _runner.SeekReplay(tick);
        }

        private void LeaveReplay()
        {
            if (_serverReplay != null)
            {
                ShowDefence();
            }
            else
            {
                ShowPicker();
            }
        }

        // ---- Results ------------------------------------------------------------------------

        private void Retry()
        {
            if (_launched != null)
            {
                StartLaunched();
            }
            else if (_serverReplay != null)
            {
                ReplayReply replay = _serverReplay;
                _runner.SeekReplay(0);
                _ui.ShowReplayHud("REPLAY", replay.Ticks);
            }
            else if (_serverStart != null)
            {
                StartServerRaid(_serverStart, _serverBanner); // a fresh token: each one is single use
            }
            else if (_replaying != null)
            {
                ShowPicker();
            }
            else if (_vaultIndex >= 0)
            {
                StartVault(_vaultIndex);
            }
        }

        private void TrackEvent(SimEvent e)
        {
            if (e.Type == SimEventType.FlickerLost || e.Type == SimEventType.TrapFired)
            {
                _lastHitBy = e.EntityId;
            }
        }

        private void OnApplicationPause(bool paused)
        {
            // Leaving the app mid-raid is where players give up.
            if (paused && _runner.Mode == RaidMode.Live && !_quitLogged)
            {
                _quitLogged = true;
                Telemetry.RaidQuit(_runner.Sim.State.Tick, _runner.Sim.State.Wick.Flickers);
            }
        }

        private async void DoubleLoot()
        {
            _ui.SetDoubleLoot(false, "Watching...");
            AdOutcome o = await _ads.ShowAsync("raid_double_loot", _lastRaidId);
            _ui.SetDoubleLoot(false, o.Granted ? "+" + (o.Grant?.Gold ?? 0) + " DOUBLED" : o.Reason);
        }

        private string SnuffCause(VaultDef vault)
        {
            for (int i = 0; i < vault.ObjectCount; i++)
            {
                if (vault.GetObject(i).Id == _lastHitBy)
                {
                    return vault.GetObject(i).Type.ToString();
                }
            }

            return "";
        }

        private void OnEnded(RaidRunner runner, RaidResult result)
        {
            if (_clips != null && _clips.Busy)
            {
                return; // the clip renderer is replaying this raid offscreen
            }

            if (_replaying == null && _serverReplay == null)
            {
                Telemetry.RaidEnd(_serverRaid?.VaultId ?? (_vaultIndex >= 0 ? _vaults[_vaultIndex].name : "test"), result.Outcome.ToString(), result.Stars,
                    result.Ticks, result.Flickers, result.Outcome == Outcome.Snuffed ? SnuffCause(runner.Vault) : "");
            }

            _quitLogged = false;
            _lastHitBy = 0;

            _lastVault = runner.Vault;
            _lastSeed = runner.Seed;
            _lastLog = runner.Sim.RecordedLog;
            _lastParams = runner.Params;
            _lastCode = _serverRaid?.Code ?? (_serverReplay != null ? _lastCode : null);
            _clipPath = null;
            string live = result.HashHex;
            if (_replaying != null)
            {
                bool matches = _replaying.Matches(result);
                _ui.ShowResults(result, live, _replaying.ExpectedHash + " (golden)", matches);
                return;
            }

            if (_serverReplay != null)
            {
                // The replay re-ran the raid on this device: it must end in the hash the server verified.
                bool matches = _serverReplay.Hash == live;
                _ui.ShowResults(result, live, _serverReplay.Hash + " (server)", matches);
                _ui.SetServerVerdict(matches ? "This replay matches the server's record of the raid" : "This replay doesn't match the server's record",
                    matches);
                _ui.SetShare(true, _lastCode != null, null);
                return;
            }

            if (_launched != null)
            {
                RaidLaunch.ReportRun(new RaidRunReport(runner.Vault, runner.Seed, runner.Sim.RecordedLog, result, runner.Params));
            }

            // Replay the recorded log headlessly: it must give the same hash as the live run.
            RaidResult replay = Sim.RaidRunner.RunToEnd(runner.Vault, runner.Seed, runner.Sim.RecordedLog, runner.Params);
            _ui.ShowResults(result, live, replay.HashHex, replay.FinalHash == result.FinalHash);
            _ui.SetShare(true, _lastCode != null, null);

            if (_serverRaid != null)
            {
                RaidStartReply raid = _serverRaid;
                _serverRaid = null;
                SubmitServerRaid(raid, runner.Sim.RecordedLog, result);
            }
        }

        // ---- Sharing --------------------------------------------------------------------------

        private void ShareClip()
        {
            if (_lastVault == null)
            {
                return;
            }

            if (_clipPath != null)
            {
                ClipShare.ShareClip(_clipPath, _lastCode);
                return;
            }

            if (_clips == null)
            {
                _clips = gameObject.AddComponent<ClipRenderer>();
                _clips.Bind(_runner, FindAnyObjectByType<VaultView>(), _tuning, Camera.main);
            }

            if (_clips.Busy)
            {
                return;
            }

            _ui.SetShare(false, _lastCode != null, "Making your clip...");
            string path = System.IO.Path.Combine(Application.temporaryCachePath, "wick-clip.mp4");
            string watermark = "Can you crack this vault?\n" + (_lastCode != null ? VaultLink.Build(_lastCode) : VaultLink.Domain);
            StartCoroutine(_clips.Render(_lastVault, _lastSeed, _lastLog, watermark, path, OnClip, _lastParams));
        }

        private void OnClip(ClipResult clip)
        {
            string timing = clip.Frames + " frames in " + clip.Seconds.ToString("0.0") + " s";
            Debug.Log("Clip: " + clip.Reason + ", " + timing + ", window " + clip.Window.StartTick + "-" + clip.Window.EndTick +
                " key " + clip.Window.KeyTick);
            if (!clip.Ok)
            {
                _ui.SetShare(false, _lastCode != null, clip.Reason);
                return;
            }

            _clipPath = clip.Path;
            _ui.SetShare(true, _lastCode != null, RaidUI.DevBuild ? "Clip ready (" + timing + ")" : null);
            ClipShare.ShareClip(_clipPath, _lastCode);
        }

        private void ShareLink()
        {
            if (_lastCode != null)
            {
                ClipShare.ShareLink(_lastCode);
            }
        }

        /// <summary>Shares the player's own published vault (its code is in their vault_live).</summary>
        private async void ShareMyVault()
        {
            try
            {
                Dictionary<string, string> data = await _cloudSave.LoadProtectedAsync("vault_live");
                string code = data.TryGetValue("vault_live", out string json) ? JsonReader.Parse(json).Find("code")?.AsString() : null;
                if (code == null)
                {
                    _ui.ShowPickerMessage("Publish a vault from the editor first");
                    return;
                }

                ClipShare.ShareLink(code);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                _ui.ShowPickerMessage("Couldn't reach the server. Check your connection and try again.");
            }
        }

        private static IEnumerable<TextAsset> Load(string folder)
        {
            TextAsset[] assets = Resources.LoadAll<TextAsset>(folder);
            System.Array.Sort(assets, (a, b) => string.CompareOrdinal(a.name, b.name));
            return assets;
        }
    }
}
