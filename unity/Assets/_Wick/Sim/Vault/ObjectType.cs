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
    }

    public static class ObjectTypes
    {
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
                default: throw new FormatException("Unknown object type '" + name + "'");
            }
        }

        public static bool IsTimedTrap(ObjectType type)
        {
            return type == ObjectType.DripPipe || type == ObjectType.Bellows || type == ObjectType.SpikeFloor;
        }

        public static bool IsTrap(ObjectType type)
        {
            return IsTimedTrap(type) || type == ObjectType.SnufferBell;
        }

        public static bool IsGuardian(ObjectType type)
        {
            return type == ObjectType.NightOwl || type == ObjectType.WaxMoths;
        }

        /// <summary>Smallest valid cycle for a timed trap, so its phases never overlap.</summary>
        public static int MinCycle(ObjectType type)
        {
            switch (type)
            {
                case ObjectType.DripPipe: return SimTuning.DripWindowTicks + 1;
                case ObjectType.Bellows: return SimTuning.GustTicks + 1;
                case ObjectType.SpikeFloor: return SimTuning.SpikeRaisedTicks + SimTuning.SpikeWarnTicks + 1;
                default: return 0;
            }
        }
    }
}
