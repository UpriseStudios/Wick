using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;
using Wick.Editor.ArtPipeline;
using Wick.Game;

namespace Wick.Editor
{
    /// <summary>
    /// Builds the audio mixer (Master with Music, SFX and UI groups, their volumes exposed as
    /// MusicVolume, SfxVolume and UiVolume) and the SfxBank. Each cue uses the files in
    /// Art/Incoming/sfx/&lt;id&gt;/ (and music/&lt;id&gt;/) when they exist; otherwise a short synthesised
    /// stand-in, so every sound plays before the real audio arrives.
    /// Menu: Wick > Art > Build Audio. Also run by Wick > Rebuild Placeholder Content.
    /// </summary>
    public static class AudioBuilder
    {
        private const string Root = "Assets/_Wick";
        public const string MixerPath = Root + "/Audio/WickMixer.mixer";
        public const string BankPath = Root + "/Audio/SfxBank.asset";
        private const string StandInDir = Root + "/Art/StandIns/Audio";
        private const int Rate = 44100;

        [MenuItem("Wick/Art/Build Audio")]
        public static void BuildMenu()
        {
            Build();
            AssetDatabase.SaveAssets();
            Debug.Log("AudioBuilder: mixer and sound bank rebuilt.");
        }

        public static (AudioMixer, SfxBank) Build()
        {
            Directory.CreateDirectory(Root + "/Audio");
            Directory.CreateDirectory(StandInDir);
            AudioMixer mixer = BuildMixer();

            var bank = AssetDatabase.LoadAssetAtPath<SfxBank>(BankPath);
            if (bank == null)
            {
                bank = ScriptableObject.CreateInstance<SfxBank>();
                AssetDatabase.CreateAsset(bank, BankPath);
            }

            bank.Cues.Clear();
            foreach (string id in SoundMap.All)
            {
                AudioClip[] real = Incoming("sfx", id);
                bank.Cues.Add(new SfxCue
                {
                    Id = id,
                    Clips = real.Length > 0 ? real : new[] { StandIn(id, Synth(id)) },
                    StandIn = real.Length == 0,
                    Volume = id == SoundMap.Tiptoe ? 0.35f : id == SoundMap.Step ? 0.45f : 0.8f,
                    PitchJitter = id == SoundMap.UiTap ? 0.02f : 0.06f,
                });
            }

            bank.MusicCalm = Music("music_calm");
            bank.MusicTense = Music("music_tense");
            bank.MusicEscape = Music("music_escape");
            EditorUtility.SetDirty(bank);
            return (mixer, bank);
        }

        private static AudioClip Music(string id)
        {
            AudioClip[] real = Incoming("music", id);
            return real.Length > 0 ? real[0] : StandIn(id, Synth(id));
        }

        private static AudioClip[] Incoming(string category, string id)
        {
            string folder = ArtBudgets.IncomingRoot + "/" + category + "/" + id;
            var clips = new List<AudioClip>();
            if (AssetDatabase.IsValidFolder(folder))
            {
                foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { folder }))
                {
                    clips.Add(AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath(guid)));
                }
            }

            return clips.ToArray();
        }

        // ---- Mixer -------------------------------------------------------------------------------

        /// <summary>
        /// Unity has no public API for creating mixers, so this writes the asset's YAML: one
        /// snapshot, four groups with an Attenuation effect each, three exposed volumes.
        /// </summary>
        private static AudioMixer BuildMixer()
        {
            var existing = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
            if (existing != null && existing.FindMatchingGroups("UI").Length == 1)
            {
                return existing; // keep its GUIDs stable once made
            }

            string[] names = { "Master", "Music", "SFX", "UI" };
            string[] groupIds = new string[4], volumes = new string[4], pitches = new string[4], effectIds = new string[4], mixLevels = new string[4];
            for (int i = 0; i < 4; i++)
            {
                groupIds[i] = Guid();
                volumes[i] = Guid();
                pitches[i] = Guid();
                effectIds[i] = Guid();
                mixLevels[i] = Guid();
            }

            long[] groupFile = { 24300002, 24300010, 24300020, 24300030 };
            long[] effectFile = { 24400004, 24400012, 24400022, 24400032 };
            var y = new StringBuilder();
            y.AppendLine("%YAML 1.1");
            y.AppendLine("%TAG !u! tag:unity3d.com,2011:");
            y.AppendLine("--- !u!241 &24100000");
            y.AppendLine("AudioMixerController:");
            Header(y, 0, "WickMixer");
            y.AppendLine("  m_OutputGroup: {fileID: 0}");
            y.AppendLine("  m_MasterGroup: {fileID: 24300002}");
            y.AppendLine("  m_Snapshots:");
            y.AppendLine("  - {fileID: 24500006}");
            y.AppendLine("  m_StartSnapshot: {fileID: 24500006}");
            y.AppendLine("  m_SuspendThreshold: -80");
            y.AppendLine("  m_EnableSuspend: 1");
            y.AppendLine("  m_UpdateMode: 0");
            y.AppendLine("  m_ExposedParameters:");
            string[] exposed = { AudioDirector.MusicVolume, AudioDirector.SfxVolume, AudioDirector.UiVolume };
            for (int i = 1; i < 4; i++)
            {
                y.AppendLine("  - guid: " + volumes[i]);
                y.AppendLine("    name: " + exposed[i - 1]);
            }

            y.AppendLine("  m_AudioMixerGroupViews:");
            y.AppendLine("  - guids:");
            foreach (string g in groupIds)
            {
                y.AppendLine("    - " + g);
            }

            y.AppendLine("    name: View");
            y.AppendLine("  m_CurrentViewIndex: 0");
            y.AppendLine("  m_TargetSnapshot: {fileID: 24500006}");

            for (int i = 0; i < 4; i++)
            {
                y.AppendLine("--- !u!243 &" + groupFile[i]);
                y.AppendLine("AudioMixerGroupController:");
                Header(y, 0, names[i]);
                y.AppendLine("  m_AudioMixer: {fileID: 24100000}");
                y.AppendLine("  m_GroupID: " + groupIds[i]);
                if (i == 0)
                {
                    y.AppendLine("  m_Children:");
                    for (int c = 1; c < 4; c++)
                    {
                        y.AppendLine("  - {fileID: " + groupFile[c] + "}");
                    }
                }
                else
                {
                    y.AppendLine("  m_Children: []");
                }

                y.AppendLine("  m_Volume: " + volumes[i]);
                y.AppendLine("  m_Pitch: " + pitches[i]);
                y.AppendLine("  m_Send: 00000000000000000000000000000000");
                y.AppendLine("  m_Effects:");
                y.AppendLine("  - {fileID: " + effectFile[i] + "}");
                y.AppendLine("  m_UserColorIndex: 0");
                y.AppendLine("  m_Mute: 0");
                y.AppendLine("  m_Solo: 0");
                y.AppendLine("  m_BypassEffects: 0");

                y.AppendLine("--- !u!244 &" + effectFile[i]);
                y.AppendLine("AudioMixerEffectController:");
                Header(y, 3, "");
                y.AppendLine("  m_EffectID: " + effectIds[i]);
                y.AppendLine("  m_EffectName: Attenuation");
                y.AppendLine("  m_MixLevel: " + mixLevels[i]);
                y.AppendLine("  m_Parameters: []");
                y.AppendLine("  m_SendTarget: {fileID: 0}");
                y.AppendLine("  m_EnableWetMix: 0");
                y.AppendLine("  m_Bypass: 0");
            }

            y.AppendLine("--- !u!245 &24500006");
            y.AppendLine("AudioMixerSnapshotController:");
            Header(y, 0, "Snapshot");
            y.AppendLine("  m_AudioMixer: {fileID: 24100000}");
            y.AppendLine("  m_SnapshotID: " + Guid());
            y.AppendLine("  m_FloatValues: {}");
            y.AppendLine("  m_TransitionOverrides: {}");

            File.WriteAllText(MixerPath, y.ToString());
            AssetDatabase.ImportAsset(MixerPath, ImportAssetOptions.ForceSynchronousImport);
            return AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
        }

        private static void Header(StringBuilder y, int hideFlags, string name)
        {
            y.AppendLine("  m_ObjectHideFlags: " + hideFlags);
            y.AppendLine("  m_CorrespondingSourceObject: {fileID: 0}");
            y.AppendLine("  m_PrefabInstance: {fileID: 0}");
            y.AppendLine("  m_PrefabAsset: {fileID: 0}");
            y.AppendLine("  m_Name: " + name);
        }

        private static string Guid()
        {
            return System.Guid.NewGuid().ToString("N");
        }

        // ---- Stand-in sounds -------------------------------------------------------------------

        private static AudioClip StandIn(string id, float[] samples)
        {
            string path = StandInDir + "/" + id + ".wav";
            WriteWav(path, samples);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }

        /// <summary>Short cartoony placeholders from the SFX list's descriptions; deterministic.</summary>
        private static float[] Synth(string id)
        {
            var rng = new System.Random(id.GetHashCode());
            switch (id)
            {
                case SoundMap.Step: return Pluck(330f, 0.06f, 0.5f, rng, noise: 0.6f);
                case SoundMap.Tiptoe: return Pluck(880f, 0.04f, 0.35f, rng, noise: 0.4f);
                case SoundMap.Dash: return Sweep(400f, 1400f, 0.18f, rng, noise: 0.7f);
                case SoundMap.FlameFlare: return Mix(Sweep(120f, 380f, 0.4f, rng, noise: 0.8f), Chime(1568f, 0.4f, 0.3f));
                case SoundMap.FlickerLost: return Mix(Sweep(900f, 300f, 0.35f, rng, noise: 0.9f), Sweep(520f, 380f, 0.3f, rng, noise: 0f, gain: 0.4f));
                case SoundMap.Snuff: return Mix(Sweep(220f, 60f, 0.5f, rng, noise: 0.6f), Sweep(600f, 200f, 0.6f, rng, noise: 0f, gain: 0.3f));
                case SoundMap.GoldGrab: return Mix(Chime(1318f, 0.35f, 0.6f), Delay(Chime(1760f, 0.35f, 0.5f), 0.06f), Delay(Chime(2093f, 0.4f, 0.4f), 0.12f));
                case SoundMap.Escape: return Mix(Chime(523f, 0.5f, 0.5f), Delay(Chime(659f, 0.5f, 0.5f), 0.12f), Delay(Chime(784f, 0.8f, 0.6f), 0.24f));
                case SoundMap.Drip: return Mix(Sweep(1200f, 500f, 0.12f, rng, noise: 0f), Delay(Sweep(3000f, 2000f, 0.3f, rng, noise: 1f, gain: 0.25f), 0.08f));
                case SoundMap.Bellows: return Sweep(90f, 60f, 0.6f, rng, noise: 1f);
                case SoundMap.SpikeWarning: return Sweep(55f, 45f, 0.7f, rng, noise: 0.5f);
                case SoundMap.SnufferBell: return Mix(Chime(392f, 1.2f, 0.7f), Chime(1048f, 0.8f, 0.3f));
                case SoundMap.OwlAlert: return Mix(Sweep(420f, 340f, 0.3f, rng, noise: 0.1f), Delay(Sweep(420f, 330f, 0.35f, rng, noise: 0.1f), 0.35f));
                case SoundMap.Moths: return Sweep(180f, 200f, 0.5f, rng, noise: 0.5f, gain: 0.5f);
                case SoundMap.UiTap: return Pluck(1200f, 0.03f, 0.4f, rng, noise: 0.2f);
                case "music_calm": return Loop(100f, new[] { 130.8f, 196f, 220f, 174.6f }, heartbeat: false, rng);
                case "music_tense": return Loop(100f, new[] { 130.8f, 196f, 220f, 174.6f }, heartbeat: true, rng);
                case "music_escape": return Loop(140f, new[] { 261.6f, 392f, 440f, 349.2f }, heartbeat: false, rng);
                default: return Pluck(440f, 0.1f, 0.5f, rng);
            }
        }

        private static float[] Pluck(float freq, float seconds, float gain, System.Random rng, float noise = 0f)
        {
            var s = new float[(int)(seconds * Rate)];
            for (int i = 0; i < s.Length; i++)
            {
                float t = i / (float)Rate;
                float env = Mathf.Exp(-t / (seconds * 0.3f));
                s[i] = gain * env * (Mathf.Sin(2f * Mathf.PI * freq * t) * (1f - noise) + ((float)rng.NextDouble() * 2f - 1f) * noise);
            }

            return s;
        }

        private static float[] Sweep(float from, float to, float seconds, System.Random rng, float noise, float gain = 0.7f)
        {
            var s = new float[(int)(seconds * Rate)];
            float phase = 0f, lp = 0f;
            for (int i = 0; i < s.Length; i++)
            {
                float k = i / (float)s.Length;
                phase += 2f * Mathf.PI * Mathf.Lerp(from, to, k) / Rate;
                float env = Mathf.Sin(Mathf.PI * Mathf.Min(1f, k * 6f) * 0.5f) * (1f - k);
                lp += (((float)rng.NextDouble() * 2f - 1f) - lp) * Mathf.Clamp01(Mathf.Lerp(from, to, k) / 4000f);
                s[i] = gain * env * (Mathf.Sin(phase) * (1f - noise) + lp * 2f * noise);
            }

            return s;
        }

        private static float[] Chime(float freq, float seconds, float gain)
        {
            var s = new float[(int)(seconds * Rate)];
            for (int i = 0; i < s.Length; i++)
            {
                float t = i / (float)Rate;
                float env = Mathf.Exp(-t * 5f / seconds);
                s[i] = gain * env * (Mathf.Sin(2f * Mathf.PI * freq * t) + 0.4f * Mathf.Sin(2f * Mathf.PI * freq * 2.76f * t));
            }

            return s;
        }

        /// <summary>Eight beats of plucked bass and a high pluck on the off-beat; tense adds a heartbeat. Loops seamlessly.</summary>
        private static float[] Loop(float bpm, float[] roots, bool heartbeat, System.Random rng)
        {
            int beat = (int)(Rate * 60f / bpm);
            var s = new float[beat * 8];
            for (int b = 0; b < 8; b++)
            {
                float root = roots[b / 2 % roots.Length];
                Add(s, Pluck(root, 0.5f, 0.35f, rng), b * beat);
                Add(s, Pluck(root * 2f, 0.25f, 0.18f, rng), b * beat + beat / 2);
                if (heartbeat && b % 2 == 0)
                {
                    Add(s, Pluck(55f, 0.15f, 0.6f, rng), b * beat);
                    Add(s, Pluck(50f, 0.15f, 0.45f, rng), b * beat + beat / 4);
                }
            }

            return s;
        }

        private static void Add(float[] into, float[] sound, int at)
        {
            for (int i = 0; i < sound.Length; i++)
            {
                into[(at + i) % into.Length] += sound[i]; // wraps, so tails spill into the loop's start
            }
        }

        private static float[] Delay(float[] s, float seconds)
        {
            var d = new float[s.Length + (int)(seconds * Rate)];
            Array.Copy(s, 0, d, d.Length - s.Length, s.Length);
            return d;
        }

        private static float[] Mix(params float[][] parts)
        {
            int n = 0;
            foreach (float[] p in parts)
            {
                n = Math.Max(n, p.Length);
            }

            var m = new float[n];
            foreach (float[] p in parts)
            {
                for (int i = 0; i < p.Length; i++)
                {
                    m[i] += p[i];
                }
            }

            return m;
        }

        private static void WriteWav(string path, float[] samples)
        {
            float peak = 0.0001f;
            foreach (float f in samples)
            {
                peak = Math.Max(peak, Math.Abs(f));
            }

            float scale = Math.Min(1f, 0.9f / peak); // normalise loudness so every stand-in sits at the same peak
            using var w = new BinaryWriter(File.Create(path));
            w.Write(Encoding.ASCII.GetBytes("RIFF"));
            w.Write(36 + samples.Length * 2);
            w.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
            w.Write(16);
            w.Write((short)1);
            w.Write((short)1);
            w.Write(Rate);
            w.Write(Rate * 2);
            w.Write((short)2);
            w.Write((short)16);
            w.Write(Encoding.ASCII.GetBytes("data"));
            w.Write(samples.Length * 2);
            foreach (float f in samples)
            {
                w.Write((short)Mathf.Clamp(f * scale * 32767f, -32768f, 32767f));
            }
        }
    }
}
