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
        MimicChest,
        SleepyTroll,
        Fan,

        /// <summary>A barred gate on a door tile; closed, it stops Wick and guardians but not light or wind.</summary>
        Gate,

        /// <summary>Opens the gates it is linked to while Wick stands on it, and for a while after.</summary>
        Lever,

        /// <summary>The signature guardian: a vault cat who stalks moving flames and pounces.</summary>
        SirWhiskers,
    }

    public static class ObjectTypes
    {
        /// <summary>How many object types there are (the enum's values are 0 to Count - 1).</summary>
        public const int Count = (int)ObjectType.SirWhiskers + 1;

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
                case "mimic_chest": return ObjectType.MimicChest;
                case "sleepy_troll": return ObjectType.SleepyTroll;
                case "fan": return ObjectType.Fan;
                case "gate": return ObjectType.Gate;
                case "lever": return ObjectType.Lever;
                case "sir_whiskers": return ObjectType.SirWhiskers;
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
                case ObjectType.MimicChest: return "mimic_chest";
                case ObjectType.SleepyTroll: return "sleepy_troll";
                case ObjectType.Fan: return "fan";
                case ObjectType.Gate: return "gate";
                case ObjectType.Lever: return "lever";
                case ObjectType.SirWhiskers: return "sir_whiskers";
                default: throw new ArgumentOutOfRangeException(nameof(type));
            }
        }

        public static bool IsTimedTrap(ObjectType type)
        {
            return type == ObjectType.DripPipe || type == ObjectType.Bellows || type == ObjectType.SpikeFloor ||
                type == ObjectType.SwingAxe || type == ObjectType.Fan;
        }

        public static bool IsTrap(ObjectType type)
        {
            return IsTimedTrap(type) || type == ObjectType.SnufferBell || type == ObjectType.IceTiles;
        }

        public static bool IsGuardian(ObjectType type)
        {
            return type == ObjectType.NightOwl || type == ObjectType.WaxMoths || type == ObjectType.MimicChest ||
                type == ObjectType.SleepyTroll || type == ObjectType.SirWhiskers;
        }

        /// <summary>Lever Doors: a gate and the levers that open it.</summary>
        public static bool IsPuzzle(ObjectType type)
        {
            return type == ObjectType.Gate || type == ObjectType.Lever;
        }

        /// <summary>
        /// Traps, guardians and gates have defence upgrade levels (Lever Doors upgrade through their
        /// gates); treasure, braziers, crates and levers don't.
        /// </summary>
        public static bool IsUpgradable(ObjectType type)
        {
            // Sir Whiskers grows as his own feature later; he has no levels in M10.
            return IsTrap(type) || (IsGuardian(type) && type != ObjectType.SirWhiskers) || type == ObjectType.Gate;
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
                case ObjectType.Fan: return 4 * (SimTuning.FanGustTicks + 2); // a still moment every quarter
                default: return 0;
            }
        }
    }
}
