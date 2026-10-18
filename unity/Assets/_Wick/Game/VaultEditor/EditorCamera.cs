using UnityEngine;
using Wick.Sim;

namespace Wick.Game
{
    /// <summary>Editor camera: fixed steep 3/4 angle, pinch to zoom, drag to pan, and tile picking.</summary>
    [RequireComponent(typeof(Camera))]
    public sealed class EditorCamera : MonoBehaviour
    {
        [SerializeField] private EditorTuning _tuning;

        private Camera _camera;
        private Vector3 _focus;
        private float _distance;
        private Vector2 _min;
        private Vector2 _max;

        public void Bind(EditorTuning tuning)
        {
            _tuning = tuning;
        }

        private void Awake()
        {
            _camera = GetComponent<Camera>();
            _distance = _tuning.Distance;
        }

        /// <summary>Centres on a vault of this size and remembers its bounds for panning.</summary>
        public void Frame(int width, int height)
        {
            if (_camera == null)
            {
                _camera = GetComponent<Camera>();
            }

            _min = new Vector2(0f, -(height - 1));
            _max = new Vector2(width - 1, 0f);

            // Fit the grid's width (the tight side in portrait) with a little margin, and shift the
            // view so the vault sits above the tool bars at the bottom of the screen.
            float aspect = _camera.aspect > 0f ? _camera.aspect : 9f / 16f;
            float halfWidth = width * 0.5f + 0.4f;
            float fit = halfWidth / (Mathf.Tan(_camera.fieldOfView * 0.5f * Mathf.Deg2Rad) * aspect);
            _distance = Mathf.Clamp(Mathf.Max(_tuning.Distance, fit), _tuning.MinDistance, _tuning.MaxDistance);
            _focus = new Vector3((width - 1) * 0.5f, 0f, -(height - 1) * 0.5f - _tuning.FrameShiftTiles);
            Place();
        }

        public void Zoom(float pinchScale)
        {
            if (pinchScale > 0f)
            {
                _distance = Mathf.Clamp(_distance / pinchScale, _tuning.MinDistance, _tuning.MaxDistance);
            }
        }

        /// <summary>Moves the view so the ground under the finger follows it.</summary>
        public void Pan(Vector2 screenDelta)
        {
            float worldPerPixel = 2f * _distance * Mathf.Tan(_camera.fieldOfView * 0.5f * Mathf.Deg2Rad) / Screen.height;
            float pitchStretch = 1f / Mathf.Sin(_tuning.Pitch * Mathf.Deg2Rad);
            _focus -= new Vector3(screenDelta.x * worldPerPixel, 0f, screenDelta.y * worldPerPixel * pitchStretch);
            _focus.x = Mathf.Clamp(_focus.x, _min.x, _max.x);
            _focus.z = Mathf.Clamp(_focus.z, _min.y - _tuning.FrameShiftTiles, _max.y);
        }

        /// <summary>Centres a tile in the view above the bottom bars.</summary>
        public void FocusOn(TilePos tile)
        {
            Vector3 p = SimPose.TileCenter(tile);
            _focus = new Vector3(Mathf.Clamp(p.x, _min.x, _max.x), 0f,
                Mathf.Clamp(p.z - _tuning.FrameShiftTiles, _min.y - _tuning.FrameShiftTiles, _max.y));
        }

        /// <summary>A world point in screen pixels (y up); false when it is behind the camera.</summary>
        public bool WorldToScreen(Vector3 world, out Vector2 screen)
        {
            Vector3 s = _camera.WorldToScreenPoint(world);
            screen = new Vector2(s.x, s.y);
            return s.z > 0f;
        }

        private void LateUpdate()
        {
            Place();
        }

        private void Place()
        {
            Quaternion rotation = Quaternion.Euler(_tuning.Pitch, 0f, 0f);
            transform.SetPositionAndRotation(_focus - rotation * Vector3.forward * _distance, rotation);
        }

        /// <summary>
        /// The tile under a screen point. Wall tops (height 1) are checked first so tapping the top
        /// of a wall picks that wall, not the floor behind it.
        /// </summary>
        public bool ScreenToTile(Vector2 screen, VaultDraft draft, out TilePos tile)
        {
            Ray ray = _camera.ScreenPointToRay(screen);
            if (Hit(ray, 1f, out tile) && draft.InBounds(tile) && draft.TileAt(tile) == TileType.Wall)
            {
                return true;
            }

            return Hit(ray, 0f, out tile) && draft.InBounds(tile);
        }

        private static bool Hit(Ray ray, float height, out TilePos tile)
        {
            tile = default;
            var plane = new Plane(Vector3.up, new Vector3(0f, height, 0f));
            if (!plane.Raycast(ray, out float enter))
            {
                return false;
            }

            tile = SimPose.TileAt(ray.GetPoint(enter));
            return true;
        }
    }
}
