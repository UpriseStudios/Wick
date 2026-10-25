using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;
using Wick.Sim;
using Wick.Sim.Tests;

namespace Wick.Game.Tests
{
    public class AudioAndFeelTests
    {
        private const string MixerPath = "Assets/_Wick/Audio/WickMixer.mixer";
        private const string BankPath = "Assets/_Wick/Audio/SfxBank.asset";
        private readonly List<GameObject> _spawned = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _spawned)
            {
                Object.DestroyImmediate(go);
            }

            _spawned.Clear();
        }

        // ---- Sound -------------------------------------------------------------------------------

        [Test]
        public void SimEventsMakeTheirSounds()
        {
            Assert.AreEqual(SoundMap.Drip, SoundMap.For(SimEventType.TrapFired, ObjectType.DripPipe));
            Assert.AreEqual(SoundMap.SnufferBell, SoundMap.For(SimEventType.TrapFired, ObjectType.SnufferBell));
            Assert.AreEqual(SoundMap.SpikeWarning, SoundMap.For(SimEventType.TrapWarning, ObjectType.SpikeFloor));
            Assert.IsNull(SoundMap.For(SimEventType.TrapWarning, ObjectType.DripPipe), "only the spikes rumble a warning");
            Assert.AreEqual(SoundMap.Moths, SoundMap.For(SimEventType.GuardianAlerted, ObjectType.WaxMoths));
            Assert.AreEqual(SoundMap.OwlAlert, SoundMap.For(SimEventType.GuardianAlerted, ObjectType.NightOwl));
            Assert.AreEqual(SoundMap.Snuff, SoundMap.For(SimEventType.Snuffed, ObjectType.Treasure));
            Assert.IsTrue(SoundMap.Ducks(SimEventType.Snuffed));
            Assert.IsFalse(SoundMap.Ducks(SimEventType.TrapWarning));
        }

        [Test]
        public void MusicFollowsTheRaid_WithoutFlapping()
        {
            var m = new MusicStates();
            Assert.AreEqual(MusicState.Calm, m.Update(false, 0f, false, false, Outcome.Running, 0.1f), "scouting: calm");
            Assert.AreEqual(MusicState.Calm, m.Update(true, 0.1f, false, false, Outcome.Running, 0.1f));
            Assert.AreEqual(MusicState.Tense, m.Update(true, 0.5f, false, false, Outcome.Running, 0.1f), "danger close");
            Assert.AreEqual(MusicState.Tense, m.Update(true, 0.05f, false, false, Outcome.Running, 1f), "eases off only after a quiet spell");
            Assert.AreEqual(MusicState.Calm, m.Update(true, 0.05f, false, false, Outcome.Running, 2f));
            Assert.AreEqual(MusicState.Tense, m.Update(true, 0f, true, false, Outcome.Running, 0.1f), "an owl chasing");
            m.Reset();
            Assert.AreEqual(MusicState.Escape, m.Update(true, 0f, false, true, Outcome.Running, 0.1f), "running home with the hoard");
            Assert.AreEqual(MusicState.Silent, m.Update(true, 0f, false, true, Outcome.Snuffed, 0.1f));
            Assert.AreEqual(MusicState.Escape, m.Update(false, 0f, false, true, Outcome.Escaped, 0.1f));
        }

        [Test]
        public void TheMixerHasMusicSfxAndUi_WithExposedVolumes()
        {
            var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
            Assert.IsNotNull(mixer, "the generated mixer must import");
            foreach (string group in new[] { "Music", "SFX", "UI" })
            {
                Assert.AreEqual(1, mixer.FindMatchingGroups(group).Length, group + " group");
            }

            // SetFloat only works in Play mode, so read the Editor's own model of the mixer: each
            // exposed name must point at its group's volume.
            var controller = (Object)mixer;
            System.Type type = controller.GetType();
            var exposed = (System.Array)type.GetProperty("exposedParameters").GetValue(controller);
            var byName = new Dictionary<string, string>();
            foreach (object e in exposed)
            {
                byName[(string)e.GetType().GetField("name").GetValue(e)] = e.GetType().GetField("guid").GetValue(e).ToString();
            }

            (string param, string group)[] wanted = { (AudioDirector.MusicVolume, "Music"), (AudioDirector.SfxVolume, "SFX"), (AudioDirector.UiVolume, "UI") };
            foreach ((string param, string group) in wanted)
            {
                Assert.IsTrue(byName.ContainsKey(param), param + " is exposed");
                object g = mixer.FindMatchingGroups(group)[0];
                string volume = g.GetType().GetMethod("GetGUIDForVolume").Invoke(g, null).ToString();
                Assert.AreEqual(volume, byName[param], param + " drives the " + group + " volume");
            }
        }

        [Test]
        public void EverySoundHasAClip()
        {
            var bank = AssetDatabase.LoadAssetAtPath<SfxBank>(BankPath);
            foreach (string id in SoundMap.All)
            {
                SfxCue cue = bank.Find(id);
                Assert.IsNotNull(cue, id);
                Assert.Greater(cue.Clips.Length, 0, id + " has no clip");
            }

            Assert.IsNotNull(bank.MusicCalm);
            Assert.IsNotNull(bank.MusicTense);
            Assert.IsNotNull(bank.MusicEscape);
        }

        /// <summary>Without exactly one listener a scene is silent (or Unity complains every frame).</summary>
        [TestCase("Assets/_Wick/Scenes/Raid.unity")]
        [TestCase("Assets/_Wick/Scenes/Editor.unity")]
        public void EveryPlayedSceneHasOneAudioListener(string path)
        {
            UnityEngine.SceneManagement.Scene scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(path,
                UnityEditor.SceneManagement.OpenSceneMode.Additive);
            try
            {
                int listeners = 0;
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    listeners += root.GetComponentsInChildren<AudioListener>(true).Length;
                }

                Assert.AreEqual(1, listeners, path);
            }
            finally
            {
                UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void VolumesMapToDecibels()
        {
            Assert.AreEqual(0f, AudioPrefs.ToDecibels(1f), 0.001f);
            Assert.AreEqual(-6.02f, AudioPrefs.ToDecibels(0.5f), 0.01f);
            Assert.AreEqual(-80f, AudioPrefs.ToDecibels(0f));
        }

        // ---- Hit-stop is presentation only -------------------------------------------------------

        [Test]
        public void HitStopFreezesTheViews_NeverTheSim()
        {
            RaidFile raid = RaidFile.Parse(File.ReadAllText(Path.Combine(GoldenReplayTests.GoldenDirectory(), "full-heist.json")));
            var go = new GameObject("Hit-stop rig (test)");
            _spawned.Add(go);
            go.SetActive(false);
            RaidRunner runner = go.AddComponent<RaidRunner>();
            VisibilityMask mask = go.AddComponent<VisibilityMask>();
            VaultView view = go.AddComponent<VaultView>();
            var tuning = ScriptableObject.CreateInstance<RaidTuning>();
            mask.Bind(runner, null);
            view.Bind(runner, AssetDatabase.LoadAssetAtPath<ViewRegistry>("Assets/_Wick/Data/ViewRegistry.asset"), mask, tuning);
            go.SetActive(true);
            RaidResult result = default;
            bool ended = false;
            runner.Ended += (_, r) =>
            {
                result = r;
                ended = true;
            };

            runner.LoadReplay(raid.Vault, raid.Seed, raid.Log);
            view.Build(runner); // edit mode runs no OnEnable, so build by hand
            for (int i = 0; i < 30; i++)
            {
                runner.Advance(TickAccumulator.TickSeconds);
                view.SyncAll();
            }

            Vector3 held = view.Wick.transform.position;
            int tickAtStop = runner.Sim.State.Tick;
            view.HitStop(1000f);
            while (runner.Mode == RaidMode.Replay)
            {
                runner.Advance(TickAccumulator.TickSeconds);
                view.SyncAll();
                Assert.AreEqual(held, view.Wick.transform.position, "views hold still during a hit-stop");
            }

            Assert.Greater(runner.Sim.State.Tick, tickAtStop + 30, "the sim kept running");
            Assert.IsTrue(ended);
            Assert.AreEqual(raid.ExpectedHash, result.HashHex, "a hit-stop can't change the raid");
        }
    }
}
