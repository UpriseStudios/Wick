using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Wick.Sim;

namespace Wick.Game
{
    /// <summary>
    /// Dev-menu device check for M10.2b (docs/m10-2-lighting.md, Mobile performance budget): replays
    /// a golden raid for a few seconds at each quality tier, and once more as a baseline with the
    /// optional lighting off (Low, no post-processing, no atmosphere), and reports each run's average
    /// and 1%-low frame time. A tier's lighting and post cost is roughly its time minus the
    /// baseline's (budget: under 3 ms on High and Medium, under 2 ms on Low). Whole-frame times, so
    /// run it on the phone with nothing else open. The player's own tier is put back afterwards.
    /// </summary>
    public sealed class LightingBenchmark : MonoBehaviour
    {
        private const float WarmSeconds = 1f;
        private const float MeasureSeconds = 5f;

        public bool Running { get; private set; }

        public IEnumerator Run(RaidRunner runner, VaultView view, RaidFile raid, Camera camera, Action<string> report)
        {
            Running = true;
            QualityTier own = LightingQuality.Tier;
            var data = camera != null ? camera.GetUniversalAdditionalCameraData() : null;
            bool post = data != null && data.renderPostProcessing;
            var text = new StringBuilder("Lighting benchmark (" + SystemInfo.deviceModel + ")\n");
            float baseline = 0f;
            try
            {
                foreach (string config in new[] { "Baseline", "Low", "Medium", "High" })
                {
                    bool isBaseline = config == "Baseline";
                    LightingQuality.Tier = isBaseline ? QualityTier.Low : (QualityTier)Enum.Parse(typeof(QualityTier), config);
                    if (data != null)
                    {
                        data.renderPostProcessing = !isBaseline && post;
                    }

                    report(text + "Running " + config + "...");
                    runner.LoadReplay(raid.Vault, raid.Seed, raid.Log, raid.Params);
                    view.Build(runner);
                    if (isBaseline)
                    {
                        Transform air = view.transform.Find("Vault/Atmosphere");
                        if (air != null)
                        {
                            air.gameObject.SetActive(false);
                        }
                    }

                    float[] frames = new float[2000];
                    int n = 0;
                    float t = 0f;
                    while (t < WarmSeconds + MeasureSeconds)
                    {
                        yield return null;
                        float dt = Time.unscaledDeltaTime;
                        t += dt;
                        if (runner.Mode != RaidMode.Replay)
                        {
                            runner.LoadReplay(raid.Vault, raid.Seed, raid.Log, raid.Params); // keep it moving
                            view.Build(runner);
                        }

                        if (t > WarmSeconds && n < frames.Length)
                        {
                            frames[n++] = dt * 1000f;
                        }
                    }

                    (float avg, float low) = Stats(frames, n);
                    if (isBaseline)
                    {
                        baseline = avg;
                    }

                    text.Append(config.PadRight(9)).Append(avg.ToString("0.0")).Append(" ms avg (")
                        .Append((1000f / Mathf.Max(0.01f, avg)).ToString("0")).Append(" fps), 1% low ").Append(low.ToString("0.0")).Append(" ms");
                    if (!isBaseline)
                    {
                        float budget = config == "Low" ? 2f : 3f;
                        float cost = avg - baseline;
                        text.Append(", lighting+post ~").Append(cost.ToString("0.0")).Append(" ms").Append(cost <= budget ? " (ok)" : " (OVER " + budget + ")");
                    }

                    text.Append('\n');
                }
            }
            finally
            {
                LightingQuality.Tier = own;
                if (data != null)
                {
                    data.renderPostProcessing = post;
                }

                Running = false;
            }

            Debug.Log(text.ToString());
            report(text.ToString());
        }

        /// <summary>Average frame time and the 1% low (the 99th-percentile frame time), in ms.</summary>
        public static (float avg, float low) Stats(float[] frames, int n)
        {
            if (n == 0)
            {
                return (0f, 0f);
            }

            var sorted = new float[n];
            Array.Copy(frames, sorted, n);
            Array.Sort(sorted);
            float sum = 0f;
            for (int i = 0; i < n; i++)
            {
                sum += sorted[i];
            }

            return (sum / n, sorted[Mathf.Min(n - 1, (int)(n * 0.99f))]);
        }
    }
}
