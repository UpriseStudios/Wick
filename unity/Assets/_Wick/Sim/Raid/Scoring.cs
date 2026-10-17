namespace Wick.Sim
{
    /// <summary>Stars and loot from a raid's state (game spec "Scoring: 3 stars").</summary>
    public static class Scoring
    {
        /// <summary>
        /// Stars are independent: 1 = reached the hoard (or, if the vault marks none, took any
        /// treasure); 2 = took at least half the treasure by value; 3 = escaped with every flicker.
        /// </summary>
        public static int Stars(VaultDef vault, RaidState state)
        {
            WickState w = state.Wick;
            int stars = 0;

            bool reachedHoard = vault.HasHoard ? w.ReachedHoard : w.CarriedPiles > 0;
            if (reachedHoard)
            {
                stars++;
            }

            long total = TotalTreasure(vault);
            if (total > 0 && (long)w.CarriedTreasure * 2 >= total)
            {
                stars++;
            }

            if (state.Outcome == Outcome.Escaped && w.Flickers == SimTuning.StartFlickers)
            {
                stars++;
            }

            return stars;
        }

        /// <summary>Loot only counts if Wick escapes.</summary>
        public static int Loot(RaidState state)
        {
            return state.Outcome == Outcome.Escaped ? state.Wick.CarriedTreasure : 0;
        }

        public static long TotalTreasure(VaultDef vault)
        {
            long total = 0;
            for (int i = 0; i < vault.ObjectCount; i++)
            {
                ObjectDef o = vault.GetObject(i);
                if (o.Type == ObjectType.Treasure)
                {
                    total += o.Value;
                }
            }

            return total;
        }
    }
}
