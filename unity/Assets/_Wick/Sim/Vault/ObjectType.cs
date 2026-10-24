using System;

namespace Wick.Sim
{
    /// <summary>Placeable vault objects. JSON names are snake_case (see Parse).</summary>
    public enum ObjectType : byte
    {
        DripPipe,
        Bellows,
        SpikeFloor,
        SnufferBell,
        NightOwl,
        WaxMoths,
        Treasure,
        Brazier,
        Crate,

        /// <summary>Ice is painted as tiles, never placed as an object; this type carries its cost, unlock and upgrade level.</summary>
        IceTiles,

        SwingAxe,
    }

    public static class ObjectTypes
    {
        /// <summary>How many object types there are (the enum's values are 0 to Count - 1).</summary>
        public const int Count = (int)ObjectType.SwingAxe + 1;

        public static ObjectType Parse(string name)
        {
            switch (name)
            {
                case "drip_pipe": return ObjectType.DripPipe;
                case "bellows": return ObjectType.Bellows;
                case "spike_floor": return ObjectType.SpikeFloor;
                case "snuffer_bell": return ObjectType.SnufferBell;
                case "night_owl": return ObjectType.NightOwl;
                case "wax_moths": return ObjectType.WaxMoths;
                case "treasure": return ObjectType.Treasure;
                case "brazier": return ObjectType.Brazier;
                case "crate": return ObjectType.Crate;
                case "ice_tiles": return ObjectType.IceTiles;
                case "swing_axe": return ObjectType.SwingAxe;
                default: throw new FormatException("Unknown object type '" + name + "'");
            }
        }

        public static string Name(ObjectType type)
        {
            switch (type)
            {
                case ObjectType.DripPipe: return "drip_pipe";
                case ObjectType.Bellows: return "bellows";
                case ObjectType.SpikeFloor: return "spike_floor";
                case ObjectType.SnufferBell: return "snuffer_bell";
                case ObjectType.NightOwl: return "night_owl";
                case ObjectType.WaxMoths: return "wax_moths";
                case ObjectType.Treasure: return "treasure";
                case ObjectType.Brazier: return "brazier";
                case ObjectType.Crate: return "crate";
                case ObjectType.IceTiles: return "ice_tiles";
                case ObjectType.SwingAxe: return "swing_axe";
                default: throw new ArgumentOutOfRangeException(nameof(type));
            }
        }

        public static bool IsTimedTrap(ObjectType type)
        {
            return type == ObjectType.DripPipe || type == ObjectType.Bellows || type == ObjectType.SpikeFloor ||
                type == ObjectType.SwingAxe;
        }

        public static bool IsTrap(ObjectType type)
        {
            return IsTimedTrap(type) || type == ObjectType.SnufferBell || type == ObjectType.IceTiles;
        }

        public static bool IsGuardian(ObjectType type)
        {
            return type == ObjectType.NightOwl || type == ObjectType.WaxMoths;
        }

        /// <summary>Traps and guardians have defence upgrade levels; treasure, braziers and crates don't.</summary>
        public static bool IsUpgradable(ObjectType type)
        {
            return IsTrap(type) || IsGuardian(type);
        }

        /// <summary>Smallest valid cycle for a timed trap, so its phases never overlap.</summary>
        public static int MinCycle(ObjectType type)
        {
            switch (type)
            {
                case ObjectType.DripPipe: return SimTuning.DripWindowTicks + 1;
                case ObjectType.Bellows: return SimTuning.GustTicks + 1;
                case ObjectType.SpikeFloor: return SimTuning.SpikeRaisedTicks + SimTuning.SpikeWarnTicks + 1;
                case ObjectType.SwingAxe: return 4 * SimTuning.AxeSweepTicks + 4; // its passes never overlap
                default: return 0;
            }
        }
    }
}
