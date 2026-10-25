using System;

namespace Wick.Sim
{
    /// <summary>
    /// Per-raid settings the server chooses and the sim reads: how long the raid lasts, and the
    /// raider's Wick upgrades (Lantern light bonus, decoys). Stored with every raid record and
    /// clear-check so replays use the values the raid was played with. The upgrades are capped by
    /// Vault Level, the same for everyone at a level; clear-checks are always played as base Wick.
    /// </summary>
    public readonly struct RaidParams
    {
        /// <summary>Shortest and longest raid the sim accepts (30 s to 5 minutes).</summary>
        public const int MinRaidTicks = 30 * SimTuning.TicksPerSecond;
        public const int MaxRaidTicks = 300 * SimTuning.TicksPerSecond;

        /// <summary>The biggest Lantern bonus the sim accepts (2 tiles), and the most decoys.</summary>
        public const int MaxLightBonus = 2 * SubTile.PerTile;
        public const int MaxDecoys = 3;

        public readonly int RaidTicks;

        /// <summary>Lantern: light radius added, in sub-tile units.</summary>
        public readonly int LightBonus;

        /// <summary>Decoys Wick carries (1 for everyone; the decoy upgrade adds one).</summary>
        public readonly int Decoys;

        public RaidParams(int raidTicks, int lightBonus = 0, int decoys = 1)
        {
            if (raidTicks < MinRaidTicks || raidTicks > MaxRaidTicks)
            {
                throw new ArgumentOutOfRangeException(nameof(raidTicks), raidTicks,
                    "Raid length must be " + MinRaidTicks + " to " + MaxRaidTicks + " ticks");
            }

            if (lightBonus < 0 || lightBonus > MaxLightBonus)
            {
                throw new ArgumentOutOfRangeException(nameof(lightBonus), lightBonus, "Light bonus must be 0 to " + MaxLightBonus);
            }

            if (decoys < 1 || decoys > MaxDecoys)
            {
                throw new ArgumentOutOfRangeException(nameof(decoys), decoys, "Decoys must be 1 to " + MaxDecoys);
            }

            RaidTicks = raidTicks;
            LightBonus = lightBonus;
            Decoys = decoys;
        }

        /// <summary>Base Wick: no Lantern bonus, one decoy (how every clear-check is played).</summary>
        public bool IsBaseWick => LightBonus == 0 && Decoys == 1;

        /// <summary>The same raid length with base Wick.</summary>
        public RaidParams AsBaseWick => new RaidParams(RaidTicks);

        /// <summary>The standard 60-second raid.</summary>
        public static RaidParams Default => new RaidParams(SimTuning.RaidTicks);

        public static RaidParams ForSeconds(int seconds)
        {
            return new RaidParams(seconds * SimTuning.TicksPerSecond);
        }

        public void Write(JsonWriter w)
        {
            w.BeginObject().Field("raidTicks", RaidTicks);
            if (!IsBaseWick)
            {
                w.Field("lightBonus", LightBonus).Field("decoys", Decoys);
            }

            w.EndObject();
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
                return new RaidParams(node.GetInt("raidTicks", SimTuning.RaidTicks), node.GetInt("lightBonus", 0), node.GetInt("decoys", 1));
            }
            catch (ArgumentOutOfRangeException e)
            {
                throw new FormatException(e.Message, e);
            }
        }
    }
}
