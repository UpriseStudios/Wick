using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Wick.Game
{
    /// <summary>
    /// The colour grade on the scene's global Volume (docs/m10-2-lighting.md, layer 8): tonemapping,
    /// a vignette, bloom that only flames and gold reach, and the theme's colour-grading LUT, all set
    /// from the vault's LightingProfile and the quality tier (no bloom on Low). On the results screen
    /// a warm victory or cool defeat grade blends in over it, and share clips can lift the exposure.
    /// Works on a runtime copy of the volume profile, so the asset is never edited.
    /// </summary>
    public sealed class Grade : MonoBehaviour
    {
        [SerializeField] private Volume _volume;

        private VolumeProfile _runtime;
        private Volume _results;
        private VolumeProfile _resultsProfile;
        private LightingProfile _profile;
        private float _resultsTarget;
        private float _exposureLift;

        /// <summary>The grade in the active raid scene, if any.</summary>
        public static Grade Active { get; private set; }

        public void Bind(Volume volume)
        {
            _volume = volume;
        }

        private void OnEnable()
        {
            Active = this;
            LightingQuality.Changed += OnQualityChanged;
            Apply();
        }

        private void OnDisable()
        {
            LightingQuality.Changed -= OnQualityChanged;
            if (Active == this)
            {
                Active = null;
            }
        }

        private void OnDestroy()
        {
            Kill(_runtime);
            Kill(_resultsProfile);
        }

        private void Update()
        {
            if (_results != null && _profile != null)
            {
                _results.weight = Mathf.MoveTowards(_results.weight, _resultsTarget,
                    Time.unscaledDeltaTime / Mathf.Max(0.01f, _profile.ResultsGradeSeconds));
            }
        }

        /// <summary>Takes a vault's lighting (VaultView calls this when a vault loads).</summary>
        public void ApplyProfile(LightingProfile profile)
        {
            _profile = profile;
            Apply();
        }

        /// <summary>Blends in the victory grade after an escape, the defeat grade after a snuff (at once if instant).</summary>
        public void ShowResult(bool escaped, bool instant = false)
        {
            if (_profile == null)
            {
                return;
            }

            Texture lut = escaped ? _profile.VictoryLut : _profile.DefeatLut;
            EnsureResults();
            if (_resultsProfile.TryGet(out ColorLookup look))
            {
                look.texture.Override(lut);
                look.contribution.Override(lut != null ? 1f : 0f);
            }

            _resultsTarget = lut != null ? 1f : 0f;
            if (instant)
            {
                _results.weight = _resultsTarget;
            }
        }

        public void ClearResult()
        {
            _resultsTarget = 0f;
            if (_results != null)
            {
                _results.weight = 0f;
            }
        }

        /// <summary>Extra exposure in stops while a share clip renders (0 to put it back).</summary>
        public void SetExposureLift(float stops)
        {
            _exposureLift = stops;
            Apply();
        }

        private void OnQualityChanged(QualityTier tier)
        {
            Apply();
        }

        private void Apply()
        {
            if (_volume == null || _volume.sharedProfile == null)
            {
                return;
            }

            if (_runtime == null)
            {
                _runtime = Instantiate(_volume.sharedProfile);
                _runtime.name = _volume.sharedProfile.name + " (runtime)";
                _volume.profile = _runtime;
            }

            QualityTier tier = LightingQuality.Tier;
            LightingProfile p = _profile;
            if (_runtime.TryGet(out Bloom bloom))
            {
                bloom.active = LightingQuality.Bloom(tier);
                bloom.downscale.Override(LightingQuality.BloomQuarterRes(tier) ? BloomDownscaleMode.Quarter : BloomDownscaleMode.Half);
                if (p != null)
                {
                    bloom.intensity.Override(p.BloomIntensity);
                    bloom.threshold.Override(p.BloomThreshold);
                }
            }

            if (p == null)
            {
                return;
            }

            if (_runtime.TryGet(out Vignette vignette))
            {
                vignette.intensity.Override(p.VignetteIntensity);
            }

            if (_runtime.TryGet(out ColorAdjustments adjust))
            {
                adjust.postExposure.Override(p.PostExposure + _exposureLift);
            }

            if (!_runtime.TryGet(out ColorLookup lookup))
            {
                lookup = _runtime.Add<ColorLookup>(true);
            }

            lookup.active = p.ColorGradeLut != null;
            lookup.texture.Override(p.ColorGradeLut);
            lookup.contribution.Override(p.LutContribution);
        }

        private void EnsureResults()
        {
            if (_results != null)
            {
                return;
            }

            _resultsProfile = ScriptableObject.CreateInstance<VolumeProfile>();
            _resultsProfile.name = "Results grade (runtime)";
            _resultsProfile.Add<ColorLookup>(true);
            _results = gameObject.AddComponent<Volume>();
            _results.isGlobal = true;
            _results.priority = (_volume != null ? _volume.priority : 0f) + 10f;
            _results.weight = 0f;
            _results.profile = _resultsProfile;
        }

        private static void Kill(Object o)
        {
            if (o == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(o);
            }
            else
            {
                DestroyImmediate(o);
            }
        }
    }
}
