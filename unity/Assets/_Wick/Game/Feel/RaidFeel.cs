using UnityEngine;
using Wick.Sim;

namespace Wick.Game
{
    /// <summary>
    /// The raid's feel, all presentation: haptics (light steps, medium flicker loss, heavy snuff),
    /// a hit-stop when a flicker is lost (the views pause; the sim never does), and gold that flies
    /// from Wick to the loot counter. Camera punches live in RaidCamera.
    /// </summary>
    public sealed class RaidFeel : MonoBehaviour
    {
        public const float FlickerHitStop = 0.08f;
        public const float SnuffHitStop = 0.16f;

        [SerializeField] private VaultView _vaultView;
        [SerializeField] private RaidUI _ui;
        [SerializeField] private Camera _camera;

        private TilePos _lastTile;
        private bool _hadTile;

        public void Bind(VaultView vaultView, RaidUI ui, Camera camera)
        {
            _vaultView = vaultView;
            _ui = ui;
            _camera = camera;
        }

        private void OnEnable()
        {
            _vaultView.Built += OnBuilt;
            _vaultView.SimEventRaised += OnSimEvent;
        }

        private void OnDisable()
        {
            _vaultView.Built -= OnBuilt;
            _vaultView.SimEventRaised -= OnSimEvent;
        }

        private void OnBuilt(VaultView view)
        {
            _hadTile = false;
        }

        private void OnSimEvent(SimEvent e)
        {
            bool live = _vaultView.Context != null && _vaultView.Context.Runner.Mode == RaidMode.Live;
            switch (e.Type)
            {
                case SimEventType.FlickerLost:
                    _vaultView.HitStop(FlickerHitStop);
                    if (live)
                    {
                        Haptics.Play(HapticStrength.Medium);
                    }

                    break;
                case SimEventType.Snuffed:
                    _vaultView.HitStop(SnuffHitStop);
                    if (live)
                    {
                        Haptics.Play(HapticStrength.Heavy);
                    }

                    break;
                case SimEventType.TreasureTaken:
                    if (_ui != null && _camera != null && _vaultView.Wick != null)
                    {
                        Vector3 screen = _camera.WorldToScreenPoint(_vaultView.Wick.transform.position + Vector3.up * 0.5f);
                        _ui.FlyCoins(screen, Mathf.Clamp(e.Value / 25, 3, 8));
                    }

                    break;
            }
        }

        private void Update()
        {
            ViewContext ctx = _vaultView.Context;
            if (ctx == null || ctx.Runner.Mode != RaidMode.Live)
            {
                return;
            }

            // A light tap for each step onto a new tile (none while tiptoeing: sneaking is quiet).
            WickState w = ctx.Current.Wick;
            if (!_hadTile)
            {
                _lastTile = w.Tile;
                _hadTile = true;
            }
            else if (!w.Tile.Equals(_lastTile))
            {
                _lastTile = w.Tile;
                if (!w.Tiptoe)
                {
                    Haptics.Play(HapticStrength.Light);
                }
            }
        }
    }
}
