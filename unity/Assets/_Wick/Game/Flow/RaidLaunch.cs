using System;
using Wick.Sim;

namespace Wick.Game
{
    /// <summary>One finished raid, as the Raid scene reports it back to whoever launched it.</summary>
    public readonly struct RaidRunReport
    {
        public readonly VaultDef Vault;
        public readonly int Seed;
        public readonly InputLog Log;
        public readonly RaidResult Result;
        public readonly RaidParams Params;

        public RaidRunReport(VaultDef vault, int seed, InputLog log, RaidResult result, RaidParams raidParams)
        {
            Vault = vault;
            Seed = seed;
            Log = log;
            Result = result;
            Params = raidParams;
        }
    }

    /// <summary>
    /// Hands a vault to the Raid scene when it is loaded on top of another scene (the editor's
    /// Test button). Presentation-side plumbing only; nothing here touches sim state.
    /// </summary>
    public static class RaidLaunch
    {
        public sealed class Request
        {
            public VaultDef Vault;

            /// <summary>The raid length raiders get at the vault's level, so a test run proves the real thing.</summary>
            public RaidParams Params = RaidParams.Default;

            /// <summary>Called after every finished run (Retry gives several).</summary>
            public Action<RaidRunReport> RunFinished;

            /// <summary>Called when the player leaves the raid ("Back to editor").</summary>
            public Action Done;
        }

        public static Request Pending { get; private set; }

        public static void Begin(Request request)
        {
            Pending = request;
        }

        public static void ReportRun(RaidRunReport report)
        {
            Pending?.RunFinished?.Invoke(report);
        }

        public static void End()
        {
            Request request = Pending;
            Pending = null;
            request?.Done?.Invoke();
        }
    }
}
