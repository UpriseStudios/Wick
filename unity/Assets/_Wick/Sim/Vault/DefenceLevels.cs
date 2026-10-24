using System;

namespace Wick.Sim
{
    /// <summary>
    /// A vault's defence upgrade levels, one per trap and guardian type (1 = not upgraded).
    /// Upgrades are vault-wide: every drip pipe in a vault shares the Drip Pipe level. Stored with
    /// the published vault, so a replay always uses the levels it was raided at. Immutable.
    /// </summary>
    public sealed class DefenceLevels
    {
        public const int MinLevel = 1;
        public const int MaxLevel = 5;

        private readonly byte[] _levels;

        private DefenceLevels(byte[] levels)
        {
            _levels = levels;
        }

        /// <summary>Every defence at level 1.</summary>
        public static DefenceLevels Base => new DefenceLevels(new byte[ObjectTypes.Count]);

        /// <summary>The level of a type; types that can't be upgraded are always 1.</summary>
        public int Level(ObjectType type)
        {
            int i = (int)type;
            return i < _levels.Length && _levels[i] > 0 ? _levels[i] : MinLevel;
        }

        /// <summary>A copy with one type at a new level.</summary>
        public DefenceLevels With(ObjectType type, int level)
        {
            if (!ObjectTypes.IsUpgradable(type))
            {
                throw new ArgumentException(type + " has no upgrade levels", nameof(type));
            }

            if (level < MinLevel || level > MaxLevel)
            {
                throw new ArgumentOutOfRangeException(nameof(level), level, "Defence levels are " + MinLevel + " to " + MaxLevel);
            }

            var copy = (byte[])_levels.Clone();
            copy[(int)type] = (byte)level;
            return new DefenceLevels(copy);
        }

        /// <summary>Whether every defence is at level 1.</summary>
        public bool IsBase
        {
            get
            {
                for (int i = 0; i < _levels.Length; i++)
                {
                    if (_levels[i] > MinLevel)
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        public bool SameAs(DefenceLevels other)
        {
            for (int i = 0; i < ObjectTypes.Count; i++)
            {
                if (Level((ObjectType)i) != other.Level((ObjectType)i))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>Writes {"drip_pipe": 3, ...}, listing only types above level 1.</summary>
        public void Write(JsonWriter w)
        {
            w.BeginObject();
            for (int i = 0; i < ObjectTypes.Count; i++)
            {
                var type = (ObjectType)i;
                if (Level(type) > MinLevel)
                {
                    w.Field(ObjectTypes.Name(type), Level(type));
                }
            }

            w.EndObject();
        }

        /// <summary>Reads a levels object; missing types are level 1, and a missing node is all level 1.</summary>
        public static DefenceLevels FromNode(JsonNode node)
        {
            DefenceLevels levels = Base;
            if (node == null || node.Kind == JsonKind.Null)
            {
                return levels;
            }

            for (int i = 0; i < ObjectTypes.Count; i++)
            {
                var type = (ObjectType)i;
                JsonNode value = node.Find(ObjectTypes.Name(type));
                if (value == null)
                {
                    continue;
                }

                if (!ObjectTypes.IsUpgradable(type))
                {
                    throw new FormatException(ObjectTypes.Name(type) + " has no defence levels");
                }

                int level = value.AsInt();
                if (level < MinLevel || level > MaxLevel)
                {
                    throw new FormatException("Defence level for " + ObjectTypes.Name(type) + " must be " + MinLevel + " to " + MaxLevel);
                }

                levels = levels.With(type, level);
            }

            return levels;
        }

        /// <summary>The highest level any defence may have at a Vault Level: half of it, rounded up.</summary>
        public static int Cap(int vaultLevel)
        {
            int cap = (vaultLevel + 1) / 2;
            return cap < MinLevel ? MinLevel : cap > MaxLevel ? MaxLevel : cap;
        }
    }
}
