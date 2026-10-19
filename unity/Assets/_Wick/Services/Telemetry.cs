using System;
using Unity.Services.Analytics;
using UnityEngine;

namespace Wick.Services
{
    /// <summary>
    /// The custom analytics events (docs/tech-design.md, Analytics). Nothing is collected without
    /// consent (Consent sets Consented). Each event needs its schema in the UGS dashboard's Event
    /// Manager; see docs/analytics-events.md.
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

        /// <param name="snuffCause">The object type that took the last flicker, or "" if not snuffed.</param>
        public static void RaidEnd(string vaultId, string outcome, int stars, int durationTicks, int flickersLeft, string snuffCause)
        {
            Record(new CustomEvent("raid_end")
            {
                { "vaultId", vaultId ?? "" }, { "outcome", outcome }, { "stars", stars }, { "durationTicks", durationTicks },
                { "flickersLeft", flickersLeft }, { "snuffCause", snuffCause ?? "" },
            });
        }

        public static void RaidQuit(int tick, int flickersLeft)
        {
            Record(new CustomEvent("raid_quit") { { "tick", tick }, { "flickersLeft", flickersLeft } });
        }

        public static void VaultPublished(int traps, int buildPoints, int clearCheckAttempts)
        {
            Record(new CustomEvent("vault_published") { { "traps", traps }, { "buildPoints", buildPoints }, { "clearCheckAttempts", clearCheckAttempts } });
        }

        public static void AdReward(string placement)
        {
            Record(new CustomEvent("ad_reward") { { "placement", placement } });
        }

        public static void Purchase(string product, string price)
        {
            Record(new CustomEvent("purchase") { { "product", product }, { "price", price } });
        }

        public static void PassTier(int tier, int daysIntoSeason)
        {
            Record(new CustomEvent("pass_tier") { { "tier", tier }, { "daysIntoSeason", daysIntoSeason } });
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
