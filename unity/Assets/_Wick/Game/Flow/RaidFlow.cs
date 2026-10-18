using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Wick.Services;
using Wick.Sim;

namespace Wick.Game
{
    /// <summary>
    /// The raid scene's flow: pick a dev vault, 5 s scout flyover (camera only, clock stopped),
    /// live raid, results with Retry. Dev builds can also replay golden raids, and every results
    /// screen compares the live hash with a headless replay of the recorded log. "My live vault"
    /// raids the player's published vault through the server (StartFriendRaid, then SubmitRaid),
    /// and loot counts only if the server's re-simulation verifies it.
    /// </summary>
    public sealed class RaidFlow : MonoBehaviour
    {
        [SerializeField] private RaidRunner _runner;
        [SerializeField] private RaidUI _ui;
        [SerializeField] private RaidCamera _camera;
        [SerializeField] private RaidTuning _tuning;

        private readonly List<TextAsset> _vaults = new List<TextAsset>();
        private readonly List<TextAsset> _goldens = new List<TextAsset>();
        private int _vaultIndex = -1;
        private float _scoutLeft;
        private RaidFile _replaying;
        private System.Random _seeds;
        private RaidLaunch.Request _launched;
        private readonly WickBackend _backend = new WickBackend();
        private bool _serverMode;
        private RaidStartReply _serverRaid;

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
            _ui.ServerRaidPressed += StartServerRaid;
        }

        private void OnDisable()
        {
            _runner.Ended -= OnEnded;
            _ui.VaultPicked -= StartVault;
            _ui.GoldenPicked -= StartGolden;
            _ui.RetryPressed -= Retry;
            _ui.MenuPressed -= OnMenu;
            _ui.EditorPressed -= OpenEditor;
            _ui.ServerRaidPressed -= StartServerRaid;
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

        private void ShowPicker()
        {
            _runner.Unload();
            var names = new List<string>();
            foreach (TextAsset v in _vaults)
            {
                names.Add(v.name);
            }

            _ui.ShowPicker(names);
        }

        private void StartVault(int index)
        {
            _vaultIndex = index;
            _replaying = null;
            _serverMode = false;
            VaultDef vault = VaultJson.Parse(_vaults[index].text);
            _runner.LoadLive(vault, _seeds.Next());
            _camera.BeginScout();
            _scoutLeft = _tuning.ScoutSeconds;
            _ui.ShowHud("SCOUTING");
        }

        private void StartLaunched()
        {
            _replaying = null;
            _runner.LoadLive(_launched.Vault, _seeds.Next());
            _camera.BeginScout();
            _scoutLeft = _tuning.ScoutSeconds;
            _ui.ShowHud("TEST RAID");
        }

        private void OnMenu()
        {
            if (_launched != null)
            {
                _runner.Unload();
                RaidLaunch.End(); // the editor unloads this scene
                return;
            }

            ShowPicker();
        }

        private void OpenEditor()
        {
            SceneManager.LoadScene("Editor");
        }

        /// <summary>Gets a single-use raid token for the player's own live vault (development only).</summary>
        private async void StartServerRaid()
        {
            _replaying = null;
            _vaultIndex = -1;
            _serverMode = true;
            _runner.Unload();
            _ui.ShowHud("CONNECTING");
            try
            {
                await _backend.SignInAsync();
                RaidStartReply start = await _backend.StartFriendRaidAsync(WickBackend.MyVaultId);
                if (!start.Ok)
                {
                    ShowPicker();
                    _ui.ShowPickerMessage(start.Reason);
                    return;
                }

                _serverRaid = start;
                _runner.LoadLive(VaultJson.Parse(start.Vault), start.Seed);
                _camera.BeginScout();
                _scoutLeft = _tuning.ScoutSeconds;
                _ui.ShowHud("LIVE RAID");
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
                    _ui.SetServerVerdict("Server check passed: +" + reply.Paid + " gold (" + reply.Gold + " total)", true);
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

        private void StartGolden(int index)
        {
            _serverMode = false;
            _replaying = RaidFile.Parse(_goldens[index].text);
            _runner.LoadReplay(_replaying.Vault, _replaying.Seed, _replaying.Log);
            _camera.EndScout();
            _ui.ShowHud("REPLAY " + _goldens[index].name);
        }

        private void Retry()
        {
            if (_launched != null)
            {
                StartLaunched();
            }
            else if (_serverMode)
            {
                StartServerRaid(); // a fresh token: each one is single use
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
