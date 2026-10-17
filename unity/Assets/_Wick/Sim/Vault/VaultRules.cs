namespace Wick.Sim
{
    /// <summary>
    /// Vault building rules: what each object costs, which Vault Level unlocks it, and what each
    /// level allows. Shared by the client editor and the server's publish check, so both agree.
    /// Changing a number changes which vaults are publishable, not how raids play.
    /// </summary>
    public static class VaultRules
    {
        public const int MinVaultLevel = 1;
        public const int MaxVaultLevel = 10;

        /// <summary>Grid size for the vertical slice (larger grids come with higher levels later).</summary>
        public const int GridWidth = 9;
        public const int GridHeight = 13;

        /// <summary>The lowest Vault Level that can place this object.</summary>
        public static int RequiredLevel(ObjectType type)
        {
            switch (type)
            {
                case ObjectType.SnufferBell:
                case ObjectType.NightOwl:
                    return 2;
                case ObjectType.WaxMoths:
                    return 3;
                default:
                    return 1;
            }
        }

        /// <summary>Build points an object costs. Treasure and braziers are free (but limited).</summary>
        public static int Cost(ObjectType type)
        {
            switch (type)
            {
                case ObjectType.DripPipe: return 10;
                case ObjectType.SpikeFloor: return 12;
                case ObjectType.Bellows: return 15;
                case ObjectType.Crate: return 4;
                case ObjectType.SnufferBell: return 20;
                case ObjectType.NightOwl: return 30;
                case ObjectType.WaxMoths: return 25;
                default: return 0;
            }
        }

        /// <summary>Build points available at a Vault Level: 60, 90, 120, then +30 a level.</summary>
        public static int Budget(int level)
        {
            if (level <= 1) return 60;
            if (level == 2) return 90;
            return 120 + (level - 3) * 30;
        }

        public static int BrazierLimit(int level)
        {
            return level >= 3 ? 2 : 1;
        }

        public static int MaxTreasurePiles(int level)
        {
            if (level <= 1) return 3;
            if (level == 2) return 4;
            return 5;
        }

        /// <summary>Every treasure pile is worth this much at a Vault Level.</summary>
        public static int TreasureValue(int level)
        {
            return 100 * level;
        }

        /// <summary>Total build points the vault's objects cost.</summary>
        public static int BuildPoints(VaultDef vault)
        {
            int total = 0;
            for (int i = 0; i < vault.ObjectCount; i++)
            {
                total += Cost(vault.GetObject(i).Type);
            }

            return total;
        }
    }
}
