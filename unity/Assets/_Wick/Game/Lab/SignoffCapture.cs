using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;
using Wick.Sim;

namespace Wick.Game
{
    /// <summary>
    /// The lighting sign-off set (docs/m10-2-lighting.md, M10.2c): a fixed list of shots from the
    /// gameplay camera at phone resolution (1080 x 2340), each a real moment from the lighting lab or a
    /// golden replay. It covers each tile state, each flame mood, each trap warning, each guardian
    /// alert, the scout, Home, results and the silhouette check, plus a contact sheet, an index and a
    /// readability report. For every shot the report measures each always-readable item on screen
    /// against what's around it at 50% screen brightness, and flags any that doesn't clearly stand out.
    /// Runs in the Editor (Wick > Lighting > Capture Sign-off, which adds the editor's shots) and on a
    /// phone from the dev menu, so device captures are the same set.
    /// </summary>
    public sealed class SignoffCapture
    {
        public const int Width = 1080;
        public const int Height = 2340;

        /// <summary>A readable item must beat its surroundings by this much at 50% brightness.</summary>
        public const float MinContrast = 0.04f;

        private readonly RaidRunner _runner;
        private readonly VaultView _view;
        private readonly VisibilityMask _mask;
        private readonly Grade _grade;
        private readonly Camera _camera;
        private readonly RaidTuning _tuning;
        private readonly LightingLab _lab;
        private readonly string _dir;
        private readonly List<string> _shots = new List<string>();
        private readonly StringBuilder _report = new StringBuilder();
        private readonly HashSet<SimEventType> _events = new HashSet<SimEventType>();
        private int _flags;

        public SignoffCapture(RaidRunner runner, VaultView view, VisibilityMask mask, Grade grade, Camera camera, RaidTuning tuning,
            LightingLab lab, string dir)
        {
            _runner = runner;
            _view = view;
            _mask = mask;
            _grade = grade;
            _camera = camera;
            _tuning = tuning;
            _lab = lab;
            _dir = dir;
        }

        /// <summary>Shots taken so far, in order (file names without extension).</summary>
        public IReadOnlyList<string> Shots => _shots;

        public int Flags => _flags;

        /// <summary>Today's sign-off folder under root (docs/art/lighting-signoff in the repo; persistentDataPath on a phone).</summary>
        public static string Folder(string root)
        {
            return Path.Combine(root, DateTime.Now.ToString("yyyy-MM-dd"));
        }

        /// <summary>Every raid-scene shot. Yields between shots so a phone stays responsive.</summary>
        public IEnumerator CaptureRaidShots(Action<string> progress)
        {
            Directory.CreateDirectory(_dir);
            _report.Append("# Lighting sign-off: ").Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm")).Append('\n');
            _report.Append("\nDevice: ").Append(SystemInfo.deviceModel).Append(", quality ").Append(LightingQuality.Tier)
                .Append(". Each readable item is checked against its surroundings at 50% screen brightness (needs ")
                .Append(MinContrast.ToString("0.00")).Append(").\n");
            _runner.SimEventRaised += Track;
            try
            {
                WickState W() => _runner.Sim.State.Wick;
                bool Guardian(ObjectType kind, Func<GuardianState, bool> alert)
                {
                    foreach (GuardianState g in _runner.Sim.State.Guardians)
                    {
                        if (g.Kind == kind && alert(g))
                        {
                            return true;
                        }
                    }

                    return false;
                }

                var shots = new (string name, string source, Func<bool> when)[]
                {
                    ("01-tile-states", "lab", () => W().Tile.X >= 12 && W().Tile.Y == 6),
                    ("02-flame-calm", "lab", () => _runner.Sim.State.Tick >= 20 && W().Flickers == 3),
                    ("03-flame-flare", "lab", () => _events.Contains(SimEventType.TreasureTaken)),
                    ("04-flame-frantic", "pit-last-flicker", () => W().Flickers == 1 && !W().Falling && _runner.Sim.State.Tick > 10),
                    ("05-flame-tiptoe", "troll-tiptoe", () => W().Tiptoe && _runner.Sim.State.Tick > 30),
                    ("06-flame-snuffed", "pit-last-flicker", () => W().Flickers == 0),
                    ("07-warn-spikes", "spikes-bell", () => SpikeWarning()),
                    ("08-warn-bell", "spikes-bell", () => BellFalling()),
                    ("09-alert-owl", "owl-moths-decoy", () => Guardian(ObjectType.NightOwl, g => g.Mode == GuardianMode.Chase)),
                    ("10-alert-moths", "owl-moths-decoy", () => Guardian(ObjectType.WaxMoths, g => g.Mode == GuardianMode.Chase)),
                    ("11-alert-troll", "lab", () => Guardian(ObjectType.SleepyTroll, g => g.Mode == GuardianMode.Chase)),
                    ("12-alert-mimic", "mimic-bite", () => Guardian(ObjectType.MimicChest, g => g.ContactTicks > 0)),
                    ("13-alert-whiskers", "whiskers-trap", () => Guardian(ObjectType.SirWhiskers, g => g.Mode == GuardianMode.Chase)),
                    ("14-pits-and-brazier", "lab", () => _runner.Sim.State.Tick >= 2),
                };

                foreach ((string name, string source, Func<bool> when) in shots)
                {
                    progress?.Invoke("Capturing " + name);
                    Shot(name, source, when);
                    yield return null;
                }

                // The scout: the outer rooms' layout at Remembered brightness, defences hidden.
                progress?.Invoke("Capturing the scout and Home");
                _runner.LoadLive(_lab.Vault, LightingLab.Seed);
                Build();
                _view.SyncAll(); // views hide what the scout doesn't show
                Overview();
                Save("15-scout");
                _mask.FullyLit = true;
                _view.SyncAll();
                Save("16-home");
                _mask.FullyLit = false;
                yield return null;

                // Results grades over the end of the lab's walk.
                Shot("17-results-victory", "lab", () => _runner.Mode == RaidMode.Ended, save: false);
                _grade?.ShowResult(true, instant: true);
                Save("17-results-victory");
                _grade?.ShowResult(false, instant: true);
                Save("18-results-defeat");
                _grade?.ClearResult();
                yield return null;

                // Silhouettes: every tile Unseen, every body shown, no glows.
                _runner.LoadReplay(_lab.Vault, LightingLab.Seed, _lab.Walk);
                Build();
                _mask.Silhouette = true;
                _mask.Refresh();
                _view.SyncAll();
                Overview();
                Save("19-silhouette", check: false);
                _mask.Silhouette = false;
            }
            finally
            {
                _runner.SimEventRaised -= Track;
            }
        }

        /// <summary>Adds a shot rendered elsewhere (the Editor's vault editor shots) to the set.</summary>
        public void AddShot(string name, Texture2D image)
        {
            File.WriteAllBytes(Path.Combine(_dir, name + ".jpg"), image.EncodeToJPG(90));
            _shots.Add(name);
            _report.Append("\n## ").Append(name).Append("\n- (no readability check)\n");
        }

        /// <summary>Writes the contact sheet (contact-sheet.jpg), the index and the readability report.</summary>
        public void Finish()
        {
            const int columns = 6;
            const int thumbW = Width / 6;
            const int thumbH = Height / 6;
            int rows = (_shots.Count + columns - 1) / columns;
            var sheet = new Texture2D(columns * thumbW, rows * thumbH, TextureFormat.RGB24, false);
            var fill = new Color[sheet.width * sheet.height];
            sheet.SetPixels(fill);
            var index = new StringBuilder("# Lighting sign-off contact sheet\n\nLeft to right, top to bottom:\n\n");
            for (int i = 0; i < _shots.Count; i++)
            {
                var shot = new Texture2D(2, 2);
                shot.LoadImage(File.ReadAllBytes(Path.Combine(_dir, _shots[i] + ".jpg")));
                int x = (i % columns) * thumbW;
                int y = (rows - 1 - i / columns) * thumbH;
                for (int ty = 0; ty < thumbH; ty++)
                {
                    for (int tx = 0; tx < thumbW; tx++)
                    {
                        sheet.SetPixel(x + tx, y + ty, shot.GetPixelBilinear((tx + 0.5f) / thumbW, (ty + 0.5f) / thumbH));
                    }
                }

                UnityEngine.Object.DestroyImmediate(shot);
                index.Append(i + 1).Append(". ").Append(_shots[i]).Append('\n');
            }

            sheet.Apply();
            File.WriteAllBytes(Path.Combine(_dir, "contact-sheet.jpg"), sheet.EncodeToJPG(88));
            UnityEngine.Object.DestroyImmediate(sheet);
            File.WriteAllText(Path.Combine(_dir, "index.md"), index.ToString());
            _report.Append("\n**").Append(_flags).Append(" item(s) flagged.**\n");
            File.WriteAllText(Path.Combine(_dir, "report.md"), _report.ToString());
        }

        // ---- Shots ------------------------------------------------------------------------------

        private void Shot(string name, string source, Func<bool> when, bool save = true)
        {
            _spikeWarnedTick = -1;
            if (source == "lab")
            {
                _runner.LoadReplay(_lab.Vault, LightingLab.Seed, _lab.Walk);
            }
            else
            {
                RaidFile raid = RaidFile.Parse(Resources.Load<TextAsset>("DevGoldens/" + source).text);
                _runner.LoadReplay(raid.Vault, raid.Seed, raid.Log, raid.Params);
            }

            Build();
            bool found = false;
            while (true)
            {
                _view.SyncAll();
                if (when())
                {
                    found = true;
                    break;
                }

                if (_runner.Mode != RaidMode.Replay)
                {
                    break;
                }

                _events.Clear();
                _runner.Advance(TickAccumulator.TickSeconds);
            }

            if (!found)
            {
                _report.Append("\n## ").Append(name).Append("\n- MISSING: no such moment in ").Append(source).Append('\n');
                _flags++;
                return;
            }

            for (int i = 0; i < 3; i++)
            {
                _view.SyncAll(); // let the flame and glows settle on this frame
            }

            RaidCamera.Place(_camera.transform, RaidCamera.FocusFor(_view.Wick.transform.position, _tuning), _tuning);
            if (save)
            {
                Save(name);
            }
        }

        private void Build()
        {
            _view.Build(_runner);
            _grade?.ApplyProfile(_mask.Profile);
            if (_runner.Vault == _lab.Vault)
            {
                _lab.SpawnProps(_view);
            }
        }

        private void Overview()
        {
            VaultDef v = _runner.Vault;
            RaidCamera.Place(_camera.transform, SimPose.TileCenter(new TilePos(v.Width / 2, v.Height / 2)), _tuning, 0.6f);
        }

        private void Save(string name, bool check = true)
        {
            UnityEngine.Object.DestroyImmediate(Render()); // the first frame after a change can come out garbled in batch mode
            Texture2D image = Render();
            File.WriteAllBytes(Path.Combine(_dir, name + ".jpg"), image.EncodeToJPG(90));
            if (!_shots.Contains(name))
            {
                _shots.Add(name);
            }

            _report.Append("\n## ").Append(name).Append('\n');
            if (check)
            {
                CheckReadable(image);
            }
            else
            {
                _report.Append("- (silhouettes: no glows to check)\n");
            }

            UnityEngine.Object.DestroyImmediate(image);
        }

        private Texture2D Render()
        {
            _camera.aspect = Width / (float)Height;
            var rt = RenderTexture.GetTemporary(Width, Height, 24, RenderTextureFormat.ARGB32);
            var request = new RenderPipeline.StandardRequest { destination = rt };
            if (RenderPipeline.SupportsRenderRequest(_camera, request))
            {
                RenderPipeline.SubmitRenderRequest(_camera, request);
            }
            else
            {
                _camera.targetTexture = rt;
                _camera.Render();
                _camera.targetTexture = null;
            }

            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            tex.Apply();
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt);
            return tex;
        }

        // ---- Readability ------------------------------------------------------------------------

        /// <summary>
        /// Every always-readable glow showing in the dark in this shot (on a tile Wick's light isn't
        /// on): its brightest pixel against the median of a ring around it, halved for a phone at 50%
        /// brightness. Off-screen items, and any right by Wick (his own flame outshines them), are
        /// skipped; a glow hidden behind a wall from this camera angle can still be flagged, so check
        /// flagged shots by eye.
        /// </summary>
        private void CheckReadable(Texture2D image)
        {
            int checkedItems = 0;
            Vector3 wick = _view.Wick != null ? _camera.WorldToScreenPoint(_view.Wick.transform.position + Vector3.up * 0.6f) : Vector3.zero;
            var wickPx = new Vector2(wick.x / _camera.pixelWidth * Width, wick.y / _camera.pixelHeight * Height);
            foreach (ReadableGlow glow in _view.GetComponentsInChildren<ReadableGlow>())
            {
                Renderer shown = null;
                foreach (Renderer r in glow.Renderers)
                {
                    shown = r.enabled && r.gameObject.activeInHierarchy ? r : shown;
                }

                if (shown == null)
                {
                    continue;
                }

                Vector3 sp = _camera.WorldToScreenPoint(shown.bounds.center);
                var px = new Vector2(sp.x / _camera.pixelWidth * Width, sp.y / _camera.pixelHeight * Height);
                Vector3 c = shown.bounds.center;
                var tile = new TilePos(Mathf.RoundToInt(c.x), Mathf.RoundToInt(-c.z));
                if (sp.z <= 0f || px.x < 12 || px.y < 12 || px.x > Width - 12 || px.y > Height - 12 ||
                    _mask.IsVisible(tile) || Vector2.Distance(px, wickPx) < 140f)
                {
                    continue; // off screen, in Wick's light (the rule is about the dark), or under his own flame
                }

                float contrast = Contrast(image, (int)px.x, (int)px.y) * 0.5f;
                checkedItems++;
                if (contrast < MinContrast)
                {
                    _flags++;
                    _report.Append("- FLAG ").Append(glow.Kind).Append(" (").Append(glow.name).Append(") at ")
                        .Append((int)px.x).Append(',').Append((int)px.y).Append(": contrast ").Append(contrast.ToString("0.000")).Append('\n');
                }
            }

            _report.Append("- ").Append(checkedItems).Append(" readable item(s) on screen checked\n");
        }

        /// <summary>The brightest luma in an 11 px square at (x, y) minus the median luma on a ring 24 px out.</summary>
        public static float Contrast(Texture2D image, int x, int y)
        {
            float item = 0f;
            for (int dy = -5; dy <= 5; dy++)
            {
                for (int dx = -5; dx <= 5; dx++)
                {
                    item = Mathf.Max(item, Luma(image, x + dx, y + dy));
                }
            }

            var ring = new float[16];
            for (int i = 0; i < ring.Length; i++)
            {
                float a = i * Mathf.PI * 2f / ring.Length;
                ring[i] = Luma(image, x + Mathf.RoundToInt(Mathf.Cos(a) * 24f), y + Mathf.RoundToInt(Mathf.Sin(a) * 24f));
            }

            Array.Sort(ring);
            return item - ring[ring.Length / 2];
        }

        private static float Luma(Texture2D image, int x, int y)
        {
            Color c = image.GetPixel(Mathf.Clamp(x, 0, image.width - 1), Mathf.Clamp(y, 0, image.height - 1));
            return 0.299f * c.r + 0.587f * c.g + 0.114f * c.b;
        }

        // ---- Moments ----------------------------------------------------------------------------

        private void Track(SimEvent e)
        {
            _events.Add(e.Type);
            int index = e.EntityId > 0 ? _runner.Vault.IndexOfObject(e.EntityId) : -1;
            if (e.Type == SimEventType.TrapWarning && index >= 0 && _runner.Vault.GetObject(index).Type == ObjectType.SpikeFloor)
            {
                _spikeWarnedTick = _runner.Sim.State.Tick;
            }
        }

        /// <summary>A spike floor warned within the last few ticks (its red plate is flashing).</summary>
        private bool SpikeWarning()
        {
            return _spikeWarnedTick >= 0 && _runner.Sim.State.Tick - _spikeWarnedTick <= 4;
        }

        private int _spikeWarnedTick = -1;

        private bool BellFalling()
        {
            VaultDef v = _runner.Vault;
            for (int i = 0; i < v.ObjectCount; i++)
            {
                if (v.GetObject(i).Type == ObjectType.SnufferBell && !_runner.Sim.State.Spent[i] && _runner.Sim.State.BellFall[i] > 0)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
