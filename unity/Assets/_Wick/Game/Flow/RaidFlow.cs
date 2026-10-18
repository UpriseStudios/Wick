using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
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

            ShowPicker();
        }

        private void Update()
        {
            if (_runner.Mode != RaidMode.Scout)
            {
                return;
            }

            _scoutLeft -= Time.deltaTime;
            if (_scoutLeft <= 0f)
            {
                _camera.EndScout();
                _runner.StartClock();
                _ui.SetBanner(null);
            }
        }

        // ---- Home -------------------------------------------------------------------------

        private void ShowPicker()
        {
            _runner.Unload();
            ClearModes();
            var names = new List<string>();
            foreach (TextAsset v in _vaults)
            {
                names.Add(v.name);
            }

            _ui.SetMenuLabel("Home");
            _ui.ShowPicker(names);
            RefreshHome();
        }

        /// <summary>Gold and Crowns, read from the player's server-written data.</summary>
        private async void RefreshHome()
        {
            _ui.SetHomeStats("Connecting...");
            try
            {
                Dictionary<string, string> data = await _cloudSave.LoadProtectedAsync("wallet", "profile");
                long gold = data.TryGetValue("wallet", out string w) ? JsonReader.Parse(w).Find("gold")?.AsLong() ?? 0 : 0;
                int crowns = data.TryGetValue("profile", out string p) ? JsonReader.Parse(p).Find("crowns")?.AsInt() ?? 0 : 0;
                _ui.SetHomeStats(gold + " gold  |  " + crowns + " Crowns");
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
            _runner.LoadLive(_launched.Vault, _seeds.Next());
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
                _runner.LoadLive(VaultJson.Parse(reply.Vault), reply.Seed);
                BeginScout(banner);
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
                    string crowns = reply.CrownsDelta == 0 ? "" : "  " + (reply.CrownsDelta > 0 ? "+" : "") + reply.CrownsDelta + " Crowns";
                    _ui.SetServerVerdict("Server check passed: +" + reply.Paid + " gold (" + reply.Gold + " total)" + crowns, true);
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

                ClearModes();
                _serverReplay = replay;
                _ui.SetMenuLabel("Raids on your vault");
                _runner.LoadReplay(VaultJson.Parse(replay.Vault), replay.Seed, InputLog.FromBase64(replay.Log));
                _camera.EndScout();
                _ui.ShowReplayHud("REPLAY", replay.Ticks);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                ShowPicker();
                _ui.ShowPickerMessage("Couldn't load that replay.");
            }
        }

        private void StartGolden(int index)
        {
            ClearModes();
            _replaying = RaidFile.Parse(_goldens[index].text);
            _runner.LoadReplay(_replaying.Vault, _replaying.Seed, _replaying.Log);
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

        private void OnEnded(RaidRunner runner, RaidResult result)
        {
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
                return;
            }

            if (_launched != null)
            {
                RaidLaunch.ReportRun(new RaidRunReport(runner.Vault, runner.Seed, runner.Sim.RecordedLog, result));
            }

            // Replay the recorded log headlessly: it must give the same hash as the live run.
            RaidResult replay = Sim.RaidRunner.RunToEnd(runner.Vault, runner.Seed, runner.Sim.RecordedLog);
            _ui.ShowResults(result, live, replay.HashHex, replay.FinalHash == result.FinalHash);

            if (_serverRaid != null)
            {
                RaidStartReply raid = _serverRaid;
                _serverRaid = null;
                SubmitServerRaid(raid, runner.Sim.RecordedLog, result);
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
