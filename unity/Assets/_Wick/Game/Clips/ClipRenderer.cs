using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using Wick.Sim;
using Debug = UnityEngine.Debug;

namespace Wick.Game
{
    public sealed class ClipResult
    {
        public bool Ok;
        public string Reason;
        public string Path;
        public long Frames;
        public double Seconds;
        public ClipWindow Window;
    }

    /// <summary>
    /// Renders a raid's share clip offscreen: picks the highlight window, re-simulates silently to
    /// its start, then steps the replay frame by frame into a 720x1280 render texture with a
    /// cinematic follow camera, slowing to 0.3x around the key moment, with the vault link burnt
    /// in. Frames go to the GPU YUV converter and the platform encoder. Runs in the Raid scene and
    /// borrows its RaidRunner and views, so the clip shows exactly what the raid's views draw,
    /// visibility mask included.
    /// </summary>
    public sealed class ClipRenderer : MonoBehaviour
    {
        public const int Width = 720;
        public const int Height = 1280;
        public const int Fps = 30;
        public const int Bitrate = 4_000_000;
        public const float SlowMotion = 0.3f;

        /// <summary>Real time either side of the key moment that plays slowed.</summary>
        public const int SlowTicks = SimTuning.TicksPerSecond;

        /// <summary>Clip frames rendered per app frame: fast, while the app stays responsive.</summary>
        public const int FramesPerUpdate = 3;

        /// <summary>Frames the last picture is held after the raid ends.</summary>
        public const int HoldFrames = 20;

        private RaidRunner _runner;
        private VaultView _view;
        private RaidTuning _tuning;
        private Camera _source;

        public bool Busy { get; private set; }

        public void Bind(RaidRunner runner, VaultView view, RaidTuning tuning, Camera source)
        {
            _runner = runner;
            _view = view;
            _tuning = tuning;
            _source = source;
        }

        public IEnumerator Render(VaultDef vault, int seed, InputLog log, string watermark, string path, Action<ClipResult> done,
            RaidParams? raidParams = null)
        {
            RaidParams p = raidParams ?? RaidParams.Default;
            Busy = true;
            Stopwatch clock = Stopwatch.StartNew();
            List<TimedEvent> events = Highlights.Collect(vault, seed, log, out RaidResult result, p);
            ClipWindow window = Highlights.Pick(events, result.Ticks, result.Outcome, Highlights.BigTreasure(vault), p.RaidTicks);

            IClipEncoder encoder = ClipEncoders.Create();
            if (encoder == null || !encoder.Open(path, Width, Height, Fps, Bitrate))
            {
                Busy = false;
                done(new ClipResult { Ok = false, Reason = "This device can't make clips yet", Window = window });
                yield break;
            }

            var shader = Resources.Load<Shader>("Shaders/RgbToI420");
            var convert = new Material(shader);
            convert.SetVector("_Clip", new Vector4(Width, Height, QualitySettings.activeColorSpace == ColorSpace.Linear ? 1f : 0f, 0f));
            var frame = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var yuv = new RenderTexture(Width, Height * 3 / 2, 0, RenderTextureFormat.R8, RenderTextureReadWrite.Linear);
            Camera camera = MakeCamera(frame);
            GameObject overlay = MakeWatermark(camera, watermark);

            _runner.ManualClock = true;
            _runner.LoadReplay(vault, seed, log, p);
            _runner.SeekReplay(window.StartTick);

            var pending = new Queue<AsyncGPUReadbackRequest>();
            long frames = 0;
            int hold = HoldFrames;
            float zoom = 1.1f;
            Vector3 focus = RaidCamera.FocusFor(WickPosition(), _tuning);

            // Clips may lift the exposure a little so they read on social feeds (docs/m10-2-lighting.md).
            VisibilityMask mask = _runner.GetComponent<VisibilityMask>();
            Grade.Active?.SetExposureLift(mask != null ? mask.Profile.ClipExposureLift : 0f);
            try
            {
                while (true)
                {
                    for (int i = 0; i < FramesPerUpdate; i++)
                    {
                        int tick = _runner.Sim.State.Tick;
                        bool over = _runner.Sim.IsOver;
                        if (tick >= window.EndTick || over)
                        {
                            if (hold-- <= 0)
                            {
                                break;
                            }
                        }

                        bool slow = Math.Abs(tick - window.KeyTick) <= SlowTicks;
                        if (!over && tick < window.EndTick)
                        {
                            _runner.Advance((slow ? SlowMotion : 1f) / Fps);
                        }

                        _view.SyncAll();

                        // Cinematic: a closer, gently lagging follow that pushes in for the key moment.
                        focus = Vector3.Lerp(focus, RaidCamera.FocusFor(WickPosition(), _tuning), 0.18f);
                        zoom = Mathf.Lerp(zoom, slow ? 1.55f : 1.1f, 0.08f);
                        RaidCamera.Place(camera.transform, focus, _tuning, zoom);
                        RenderTo(camera, frame);
                        Graphics.Blit(frame, yuv, convert);
                        pending.Enqueue(AsyncGPUReadback.Request(yuv, 0, TextureFormat.R8));
                        frames++;
                    }

                    if (hold < 0)
                    {
                        break;
                    }

                    yield return null;
                    Drain(pending, encoder, false);
                }

                while (pending.Count > 0)
                {
                    yield return null;
                    Drain(pending, encoder, false);
                }
            }
            finally
            {
                Grade.Active?.SetExposureLift(0f);
                _runner.ManualClock = false;
                Kill(overlay);
                Kill(camera.gameObject);
                frame.Release();
                yuv.Release();
                Kill(convert);
                Busy = false;
            }

            long written = encoder.Close();
            done(new ClipResult
            {
                Ok = written > 0,
                Reason = written > 0 ? "Clip ready" : "The encoder wrote nothing",
                Path = path,
                Frames = written,
                Seconds = clock.Elapsed.TotalSeconds,
                Window = window,
            });
        }

        /// <summary>Hands finished readbacks to the encoder in the order they were requested.</summary>
        private static void Drain(Queue<AsyncGPUReadbackRequest> pending, IClipEncoder encoder, bool wait)
        {
            while (pending.Count > 0 && (wait || pending.Peek().done))
            {
                AsyncGPUReadbackRequest request = pending.Dequeue();
                if (wait)
                {
                    request.WaitForCompletion();
                }

                if (!request.hasError)
                {
                    encoder.AddFrame(request.GetData<byte>());
                }
            }
        }

        private static void RenderTo(Camera camera, RenderTexture target)
        {
            var request = new RenderPipeline.StandardRequest { destination = target };
            if (RenderPipeline.SupportsRenderRequest(camera, request))
            {
                RenderPipeline.SubmitRenderRequest(camera, request);
            }
            else
            {
                camera.Render();
            }
        }

        private static void Kill(UnityEngine.Object o)
        {
            if (Application.isPlaying)
            {
                Destroy(o);
            }
            else
            {
                DestroyImmediate(o); // the Editor's clip preview runs outside Play mode
            }
        }

        private Vector3 WickPosition()
        {
            return _view.Wick != null ? _view.Wick.transform.position : Vector3.zero;
        }

        private Camera MakeCamera(RenderTexture target)
        {
            var go = new GameObject("Clip Camera");
            Camera camera = go.AddComponent<Camera>();
            if (_source != null)
            {
                camera.CopyFrom(_source);
            }

            camera.targetTexture = target;
            camera.aspect = Width / (float)Height;
            camera.enabled = false; // rendered by hand, once per clip frame
            return camera;
        }

        /// <summary>The vault link, burnt into every frame along the bottom.</summary>
        private static GameObject MakeWatermark(Camera camera, string text)
        {
            var go = new GameObject("Clip Watermark");
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = camera.nearClipPlane + 0.05f;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(Width, Height);

            var band = new GameObject("Band");
            band.transform.SetParent(go.transform, false);
            var image = band.AddComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0.55f);
            var rect = band.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.sizeDelta = new Vector2(0f, 150f);

            var label = new GameObject("Link");
            label.transform.SetParent(band.transform, false);
            var t = label.AddComponent<Text>();
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.text = text;
            t.fontSize = 34;
            t.lineSpacing = 1.1f;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = new Color(1f, 0.86f, 0.55f);
            var labelRect = label.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.sizeDelta = Vector2.zero;
            return go;
        }
    }
}
