using UnityEngine;
using UnityEngine.Audio;
using Wick.Sim;

namespace Wick.Game
{
    /// <summary>
    /// The game's sound: sim events become sound effects (SoundMap), Wick's steps are counted from
    /// his tile changes, three music layers crossfade by MusicStates, and the music ducks briefly
    /// on key moments. Everything plays through the mixer's Music, SFX and UI groups, whose
    /// exposed volumes come from AudioPrefs. Presentation only: it reads the sim, never writes it.
    /// </summary>
    public sealed class AudioDirector : MonoBehaviour
    {
        public const string MusicVolume = "MusicVolume";
        public const string SfxVolume = "SfxVolume";
        public const string UiVolume = "UiVolume";

        private const int Voices = 8;
        private const float DuckDb = -9f;
        private const float DuckSeconds = 1.2f;
        private const float FadeSeconds = 1.5f;

        private static AudioDirector _active;

        [SerializeField] private AudioMixer _mixer;
        [SerializeField] private SfxBank _bank;
        [SerializeField] private VaultView _vaultView;

        private AudioSource[] _voices;
        private int _nextVoice;
        private AudioSource _ui;
        private AudioSource[] _music;
        private readonly MusicStates _states = new MusicStates();
        private float _duck;
        private TilePos _lastTile;
        private bool _hadTile;
        private Outcome _lastOutcome;

        public MusicState Music => _states.State;

        public void Bind(AudioMixer mixer, SfxBank bank, VaultView vaultView)
        {
            _mixer = mixer;
            _bank = bank;
            _vaultView = vaultView;
        }

        /// <summary>The UI click, from any screen (no-op where no director is loaded).</summary>
        public static void Tap()
        {
            if (_active != null)
            {
                _active.PlayUi(SoundMap.UiTap);
            }
        }

        /// <summary>Re-reads the volume settings into the mixer.</summary>
        public static void ApplyVolumes()
        {
            if (_active != null)
            {
                _active.SetVolumes();
            }
        }

        private void Awake()
        {
            AudioMixerGroup Group(string name)
            {
                AudioMixerGroup[] found = _mixer != null ? _mixer.FindMatchingGroups(name) : null;
                return found != null && found.Length > 0 ? found[0] : null;
            }

            _voices = new AudioSource[Voices];
            for (int i = 0; i < Voices; i++)
            {
                _voices[i] = Source("Sfx " + i, Group("SFX"), false);
            }

            _ui = Source("UI", Group("UI"), false);
            _music = new[]
            {
                Source("Music calm", Group("Music"), true, _bank != null ? _bank.MusicCalm : null),
                Source("Music tense", Group("Music"), true, _bank != null ? _bank.MusicTense : null),
                Source("Music escape", Group("Music"), true, _bank != null ? _bank.MusicEscape : null),
            };
        }

        private AudioSource Source(string name, AudioMixerGroup group, bool loop, AudioClip clip = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var s = go.AddComponent<AudioSource>();
            s.outputAudioMixerGroup = group;
            s.playOnAwake = false;
            s.loop = loop;
            s.spatialBlend = 0f; // 2D: the camera is fixed, and phones are mostly mono speakers
            s.clip = clip;
            s.volume = 0f;
            return s;
        }

        private void OnEnable()
        {
            _active = this;
            SetVolumes();
            if (_vaultView != null)
            {
                _vaultView.Built += OnBuilt;
                _vaultView.SimEventRaised += OnSimEvent;
            }

            // Calm and tense share a tempo: start every layer together, silent, so crossfades stay in step.
            foreach (AudioSource m in _music)
            {
                if (m.clip != null)
                {
                    m.Play();
                }
            }
        }

        private void OnDisable()
        {
            if (_active == this)
            {
                _active = null;
            }

            if (_vaultView != null)
            {
                _vaultView.Built -= OnBuilt;
                _vaultView.SimEventRaised -= OnSimEvent;
            }
        }

        private void OnBuilt(VaultView view)
        {
            _states.Reset();
            _hadTile = false;
            _lastOutcome = Outcome.Running;
        }

        private void OnSimEvent(SimEvent e)
        {
            ObjectType source = ObjectType.Treasure;
            VaultDef vault = _vaultView.Context?.Vault;
            if (e.EntityId > 0 && vault != null)
            {
                for (int i = 0; i < vault.ObjectCount; i++)
                {
                    if (vault.GetObject(i).Id == e.EntityId)
                    {
                        source = vault.GetObject(i).Type;
                        break;
                    }
                }
            }

            Play(SoundMap.For(e.Type, source));
            if (SoundMap.Ducks(e.Type))
            {
                _duck = DuckSeconds;
            }
        }

        private void Update()
        {
            ViewContext ctx = _vaultView != null ? _vaultView.Context : null;
            bool playing = ctx != null && (ctx.Runner.Mode == RaidMode.Live || ctx.Runner.Mode == RaidMode.Replay);
            float fear = 0f;
            bool chased = false, reachedHoard = false;
            Outcome outcome = Outcome.Running;
            if (ctx != null && ctx.Runner.Sim != null)
            {
                WickState w = ctx.Current.Wick;
                fear = _vaultView.Wick != null && _vaultView.Wick.Reactions != null ? _vaultView.Wick.Reactions.FearLevel : 0f;
                foreach (GuardianState g in ctx.Current.Guardians)
                {
                    chased |= g.Mode == GuardianMode.Chase;
                }

                reachedHoard = w.ReachedHoard;
                outcome = ctx.Runner.Sim.State.Outcome;
                if (playing)
                {
                    Steps(w);
                }
            }

            if (outcome == Outcome.Escaped && _lastOutcome != Outcome.Escaped)
            {
                Play(SoundMap.Escape);
                _duck = DuckSeconds;
            }

            _lastOutcome = outcome;
            MusicState state = _states.Update(playing, fear, chased, reachedHoard, outcome, Time.deltaTime);
            Crossfade(state);
            _duck = Mathf.Max(0f, _duck - Time.deltaTime);
            SetMusicLevel();
        }

        /// <summary>A footstep each time Wick arrives on a new tile; quieter tiptoes when sneaking.</summary>
        private void Steps(in WickState w)
        {
            if (!_hadTile)
            {
                _lastTile = w.Tile;
                _hadTile = true;
                return;
            }

            if (!w.Tile.Equals(_lastTile))
            {
                _lastTile = w.Tile;
                Play(w.Tiptoe ? SoundMap.Tiptoe : SoundMap.Step);
            }
        }

        private void Crossfade(MusicState state)
        {
            float step = Time.deltaTime / FadeSeconds;
            for (int i = 0; i < _music.Length; i++)
            {
                // Calm stays under tense (tense adds the heartbeat on top); escape replaces both.
                float target = state == MusicState.Calm ? (i == 0 ? 1f : 0f)
                    : state == MusicState.Tense ? (i == 2 ? 0f : 1f)
                    : state == MusicState.Escape ? (i == 2 ? 1f : 0f)
                    : 0f;
                _music[i].volume = Mathf.MoveTowards(_music[i].volume, target, step);
            }
        }

        public void Play(string cueId)
        {
            SfxCue cue = cueId != null && _bank != null ? _bank.Find(cueId) : null;
            if (cue == null || cue.Clips.Length == 0)
            {
                return;
            }

            AudioSource s = _voices[_nextVoice];
            _nextVoice = (_nextVoice + 1) % _voices.Length;
            s.pitch = 1f + Random.Range(-cue.PitchJitter, cue.PitchJitter); // presentation randomness, never the sim's
            s.PlayOneShot(cue.Clips[Random.Range(0, cue.Clips.Length)], cue.Volume);
        }

        private void PlayUi(string cueId)
        {
            SfxCue cue = _bank != null ? _bank.Find(cueId) : null;
            if (cue != null && cue.Clips.Length > 0)
            {
                _ui.PlayOneShot(cue.Clips[0], cue.Volume);
            }
        }

        private void SetVolumes()
        {
            if (_mixer == null)
            {
                return;
            }

            _mixer.SetFloat(SfxVolume, AudioPrefs.ToDecibels(AudioPrefs.Get(AudioPrefs.Sfx)));
            _mixer.SetFloat(UiVolume, AudioPrefs.ToDecibels(AudioPrefs.Get(AudioPrefs.Ui)));
            SetMusicLevel();
        }

        private void SetMusicLevel()
        {
            if (_mixer != null)
            {
                float duck = _duck > 0f ? DuckDb * Mathf.Clamp01(_duck / 0.3f) : 0f; // eases back over the last 0.3 s
                _mixer.SetFloat(MusicVolume, AudioPrefs.ToDecibels(AudioPrefs.Get(AudioPrefs.Music)) + duck);
            }
        }
    }
}
