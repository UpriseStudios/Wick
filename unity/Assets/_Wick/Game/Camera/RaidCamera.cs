using UnityEngine;
using Wick.Sim;

namespace Wick.Game
{
    /// <summary>
    /// Fixed-angle 3/4 camera: smooth follow on Wick, a short zoom punch when he loses a
    /// flicker, and the scout flyover from the far end of the vault back to him.
    /// Runs after the views so it follows this frame's interpolated position.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public sealed class RaidCamera : MonoBehaviour
    {
        [SerializeField] private VaultView _vaultView;
        [SerializeField] private RaidTuning _tuning;

        private Vector3 _focus;
        private Vector3 _velocity;
        private float _punch;
        private bool _scouting;
        private float _scoutTime;
        private Vector3 _scoutFrom;

        public bool IsScouting => _scouting;

        public void Bind(VaultView vaultView, RaidTuning tuning)
        {
            _vaultView = vaultView;
            _tuning = tuning;
        }

        private void OnEnable()
        {
            _vaultView.SimEventRaised += OnSimEvent;
            _vaultView.Built += OnBuilt;
        }

        private void OnDisable()
        {
            _vaultView.SimEventRaised -= OnSimEvent;
            _vaultView.Built -= OnBuilt;
        }

        /// <summary>Flies from the vault's far end to Wick over ScoutSeconds.</summary>
        public void BeginScout()
        {
            VaultDef vault = _vaultView.Context.Vault;
            _scoutFrom = SimPose.TileCenter(new TilePos(vault.Width / 2, 0));
            _scoutTime = 0f;
            _scouting = true;
            _focus = _scoutFrom;
            _velocity = Vector3.zero;
        }

        public void EndScout()
        {
            _scouting = false;
        }

        private void OnBuilt(VaultView view)
        {
            _focus = FocusFor(view.Wick.transform.position, _tuning);
            _velocity = Vector3.zero;
            _punch = 0f;
        }

        private void OnSimEvent(SimEvent e)
        {
            // A punch scaled to the moment: snuff hardest, a lost flicker, then small nudges for a
            // grab or a near miss.
            float strength = e.Type == SimEventType.Snuffed ? 1.3f
                : e.Type == SimEventType.FlickerLost ? 1f
                : e.Type == SimEventType.NearMiss ? 0.45f
                : e.Type == SimEventType.TreasureTaken ? 0.3f
                : 0f;
            _punch = Mathf.Max(_punch, strength);
        }

        private void LateUpdate()
        {
            if (_vaultView.Wick == null)
            {
                return;
            }

            Vector3 wick = FocusFor(_vaultView.Wick.transform.position, _tuning);
            if (_scouting)
            {
                _scoutTime += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(_scoutTime / _tuning.ScoutSeconds));
                _focus = Vector3.Lerp(_scoutFrom, wick, t);
            }
            else
            {
                _focus = Vector3.SmoothDamp(_focus, wick, ref _velocity, _tuning.FollowSmoothTime);
            }

            _punch = Mathf.MoveTowards(_punch, 0f, Time.deltaTime / Mathf.Max(0.01f, _tuning.PunchSeconds));
            float zoom = 1f + (_tuning.PunchZoom - 1f) * Mathf.Sin(Mathf.Min(_punch, 1f) * Mathf.PI * 0.5f) * Mathf.Max(1f, _punch);
            Place(transform, _focus, _tuning, zoom);
        }

        /// <summary>Where the camera centres for Wick at this position (looking ahead, north).</summary>
        public static Vector3 FocusFor(Vector3 wick, RaidTuning tuning)
        {
            return wick + Vector3.forward * tuning.CameraLookAhead;
        }

        /// <summary>Puts a camera transform at the raid angle looking at focus.</summary>
        public static void Place(Transform camera, Vector3 focus, RaidTuning tuning, float zoom = 1f)
        {
            Quaternion rotation = Quaternion.Euler(tuning.CameraPitch, tuning.CameraYaw, 0f);
            float distance = tuning.CameraDistance / zoom;
            camera.SetPositionAndRotation(focus - rotation * Vector3.forward * distance, rotation);
        }
    }
}
