using UnityEngine;
using Wick.Sim;

namespace Wick.Game
{
    /// <summary>Pure read-only calculations behind Wick's reactions (no Unity objects).</summary>
    public static class Reactions
    {
        /// <summary>
        /// 0..1: 1 when Wick stands on a danger, falling to 0 at rangeTiles. Dangers are timed
        /// traps in their damaging phase (Bellows: their whole gust line), untriggered Snuffer
        /// Bells, Night Owls that are chasing and Wax Moths drifting at Wick.
        /// </summary>
        public static float Fear(RaidSim sim, VaultDef vault, int shownTick, in WickState wick,
            GuardianState[] guardians, float rangeTiles)
        {
            TilePos at = wick.Occupied;
            float nearest = float.MaxValue;

            for (int i = 0; i < vault.ObjectCount; i++)
            {
                ObjectDef o = vault.GetObject(i);
                switch (o.Type)
                {
                    case ObjectType.DripPipe:
                    case ObjectType.SpikeFloor:
                        if (RaidSim.IsTrapActive(shownTick, o))
                        {
                            nearest = Mathf.Min(nearest, Distance(at, o.Pos));
                        }

                        break;

                    case ObjectType.Bellows:
                        if (RaidSim.IsTrapActive(shownTick, o))
                        {
                            TilePos t = o.Pos;
                            while (true)
                            {
                                t = t.Step(o.Facing);
                                if (!sim.OnGustLine(o, t))
                                {
                                    break;
                                }

                                nearest = Mathf.Min(nearest, Distance(at, t));
                            }
                        }

                        break;

                    case ObjectType.SnufferBell:
                        if (!sim.State.Spent[i])
                        {
                            nearest = Mathf.Min(nearest, Distance(at, o.Pos));
                        }

                        break;
                }
            }

            foreach (GuardianState g in guardians)
            {
                if (g.Mode == GuardianMode.Chase)
                {
                    nearest = Mathf.Min(nearest, Distance(at, g.Occupied));
                }
            }

            if (nearest == float.MaxValue || rangeTiles <= 0f)
            {
                return 0f;
            }

            return Mathf.Clamp01(1f - nearest / rangeTiles);
        }

        private static float Distance(TilePos a, TilePos b)
        {
            return Mathf.Sqrt(Sim.Motion.DistSq(a, b));
        }
    }
}
