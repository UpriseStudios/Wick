using System.Collections.Generic;
using UnityEngine;
using Wick.Sim;

namespace Wick.Game
{
    /// <summary>
    /// The raid scene's flow: pick a dev vault, 5 s scout flyover (camera only, clock stopped),
    /// live raid, results with Retry. Dev builds can also replay golden raids, and every results
    /// screen compares the live hash with a headless replay of the recorded log.
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
            _ui.MenuPressed += ShowPicker;
        }

        private void OnDisable()
        {
            _runner.Ended -= OnEnded;
            _ui.VaultPicked -= StartVault;
            _ui.GoldenPicked -= StartGolden;
            _ui.RetryPressed -= Retry;
            _ui.MenuPressed -= ShowPicker;
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
            VaultDef vault = VaultJson.Parse(_vaults[index].text);
            _runner.LoadLive(vault, _seeds.Next());
            _camera.BeginScout();
            _scoutLeft = _tuning.ScoutSeconds;
            _ui.ShowHud("SCOUTING");
        }

        private void StartGolden(int index)
        {
            _replaying = RaidFile.Parse(_goldens[index].text);
            _runner.LoadReplay(_replaying.Vault, _replaying.Seed, _replaying.Log);
            _camera.EndScout();
            _ui.ShowHud("REPLAY " + _goldens[index].name);
        }

        private void Retry()
        {
            if (_replaying != null)
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

            // Replay the recorded log headlessly: it must give the same hash as the live run.
            RaidResult replay = Sim.RaidRunner.RunToEnd(runner.Vault, runner.Seed, runner.Sim.RecordedLog);
            _ui.ShowResults(result, live, replay.HashHex, replay.FinalHash == result.FinalHash);
        }

        private static IEnumerable<TextAsset> Load(string folder)
        {
            TextAsset[] assets = Resources.LoadAll<TextAsset>(folder);
            System.Array.Sort(assets, (a, b) => string.CompareOrdinal(a.name, b.name));
            return assets;
        }
    }
}
