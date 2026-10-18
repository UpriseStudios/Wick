using System;
using Unity.Services.Analytics;
using UnityEngine;

namespace Wick.Services
{
    /// <summary>
    /// The custom analytics events (docs/tech-design.md, Analytics). Nothing is collected without
    /// consent: until the consent flow lands (M14) only development builds collect, so production
    /// builds stay silent. Each event needs its schema in the UGS dashboard's Event Manager.
    /// </summary>
    public static class Telemetry
    {
        private static bool _started;

        /// <summary>Set by the consent flow (M14). Development builds count as consenting for testing.</summary>
        public static bool Consented { get; set; } = Debug.isDebugBuild;

        /// <param name="channel">"link" or "clip".</param>
        public static void VaultShared(string channel, string vaultCode)
        {
            Record(new CustomEvent("vault_shared") { { "channel", channel }, { "vaultCode", vaultCode ?? "" } });
        }

        /// <param name="source">"link", "referrer", "paste" or "typed".</param>
        public static void LinkOpened(string vaultCode, bool installed, string source)
        {
            Record(new CustomEvent("link_opened") { { "vaultCode", vaultCode }, { "installed", installed }, { "source", source } });
        }

        private static async void Record(CustomEvent e)
        {
            if (!Consented)
            {
                return;
            }

            try
            {
                await UgsBootstrap.EnsureSignedInAsync();
                if (!_started)
                {
                    AnalyticsService.Instance.StartDataCollection();
                    _started = true;
                }

                AnalyticsService.Instance.RecordEvent(e);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("Analytics: " + ex.Message); // never let analytics break the game
            }
        }
    }
}
