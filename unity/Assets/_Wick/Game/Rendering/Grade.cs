using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Wick.Game
{
    /// <summary>
    /// The colour grade on the scene's global Volume: tonemapping, a warm white balance, a little less
    /// saturation, a vignette, and a soft glow (bloom) that only the flames and gold are bright enough
    /// to trigger. The glow is the expensive part, so Settings can turn it off (kept on the device).
    /// </summary>
    public sealed class Grade : MonoBehaviour
    {
        private const string GlowKey = "wick.glow";

        [SerializeField] private Volume _volume;

        private static Grade _active;

        public static bool Glow
        {
            get => PlayerPrefs.GetInt(GlowKey, 1) == 1;
            set
            {
                PlayerPrefs.SetInt(GlowKey, value ? 1 : 0);
                PlayerPrefs.Save();
                if (_active != null)
                {
                    _active.Apply();
                }
            }
        }

        public void Bind(Volume volume)
        {
            _volume = volume;
        }

        private void OnEnable()
        {
            _active = this;
            Apply();
        }

        private void OnDisable()
        {
            if (_active == this)
            {
                _active = null;
            }
        }

        private void Apply()
        {
            if (_volume != null && _volume.sharedProfile != null && _volume.sharedProfile.TryGet(out Bloom bloom))
            {
                bloom.active = Glow;
            }
        }
    }
}
