using System;

namespace Wick.Sim
{
    /// <summary>
    /// Per-raid settings the server chooses and the sim reads: how long the raid lasts. Stored
    /// with every raid record and clear-check so replays use the values the raid was played with.
    /// </summary>
    public readonly struct RaidParams
    {
        /// <summary>Shortest and longest raid the sim accepts (30 s to 5 minutes).</summary>
        public const int MinRaidTicks = 30 * SimTuning.TicksPerSecond;
        public const int MaxRaidTicks = 300 * SimTuning.TicksPerSecond;

        public readonly int RaidTicks;

        public RaidParams(int raidTicks)
        {
            if (raidTicks < MinRaidTicks || raidTicks > MaxRaidTicks)
            {
                throw new ArgumentOutOfRangeException(nameof(raidTicks), raidTicks,
                    "Raid length must be " + MinRaidTicks + " to " + MaxRaidTicks + " ticks");
            }

            RaidTicks = raidTicks;
        }

        /// <summary>The standard 60-second raid.</summary>
        public static RaidParams Default => new RaidParams(SimTuning.RaidTicks);

        public static RaidParams ForSeconds(int seconds)
        {
            return new RaidParams(seconds * SimTuning.TicksPerSecond);
        }

        public void Write(JsonWriter w)
        {
            w.BeginObject().Field("raidTicks", RaidTicks).EndObject();
        }

        /// <summary>Reads a params object; a missing node or field means the default.</summary>
        public static RaidParams FromNode(JsonNode node)
        {
            if (node == null || node.Kind == JsonKind.Null)
            {
                return Default;
            }

            try
            {
                return new RaidParams(node.GetInt("raidTicks", SimTuning.RaidTicks));
            }
            catch (ArgumentOutOfRangeException e)
            {
                throw new FormatException(e.Message, e);
            }
        }
    }
}
