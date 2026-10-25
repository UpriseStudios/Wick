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

        // Per-level lookup tables, never written; index 0 = Vault Level 1.
        // Numbers from docs/m10-design-notes.md, tuned from tools/balance results.
        private static readonly int[] GridWidths = { 12, 12, 14, 14, 16, 16, 18, 18, 18, 20 };
        private static readonly int[] GridHeights = { 16, 16, 18, 18, 22, 22, 24, 24, 24, 28 };
        // Raised for Levels 1 to 7 on 2026-10-25: the balance report found worst-case vaults there
        // escaped in 6 to 20 s.
        private static readonly int[] Budgets = { 120, 160, 210, 250, 300, 340, 400, 440, 480, 540 };
        private static readonly int[] Braziers = { 1, 1, 2, 2, 2, 3, 3, 3, 3, 4 };
        private static readonly int[] TreasurePiles = { 3, 4, 4, 5, 5, 6, 6, 7, 7, 8 };
        private static readonly int[] DecorationLimits = { 4, 6, 8, 10, 12, 14, 16, 18, 20, 22 };
        // By grid (see MinRouteTiles). Added 2026-10-25: Levels 1 to 3 cleared in 7 to 10 s because a
        // dash crossed the vault in about 2 s each way. Winding routes slow raids without touching movement.
        private static readonly int[] MinRoutes = { 36, 36, 36, 36, 44, 44, 48, 48, 48, 56 };

        /// <summary>The largest grid a Vault Level allows, in tiles.</summary>
        public static int GridWidth(int level) => GridWidths[Index(level)];

        public static int GridHeight(int level) => GridHeights[Index(level)];

        /// <summary>
        /// Whether a vault at this level may use a w x h grid: any size from the table up to the
        /// level's own, so a vault stays valid until its owner chooses to expand it.
        /// </summary>
        public static bool IsAllowedGrid(int width, int height, int level)
        {
            for (int l = MinVaultLevel; l <= Clamp(level); l++)
            {
                if (GridWidth(l) == width && GridHeight(l) == height)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>The lowest Vault Level that can place this object.</summary>
        public static int RequiredLevel(ObjectType type)
        {
            switch (type)
            {
                case ObjectType.SnufferBell:
                case ObjectType.NightOwl:
                    return 2;
                case ObjectType.WaxMoths:
                case ObjectType.IceTiles:
                    return 3;
                case ObjectType.SwingAxe:
                    return 4;
                case ObjectType.MimicChest:
                    return 5;
                case ObjectType.SleepyTroll:
                    return 6;
                case ObjectType.Fan:
                    return 7;
                case ObjectType.Gate:
                case ObjectType.Lever:
                    return 8;
                case ObjectType.SirWhiskers:
                    return 10;
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
                case ObjectType.IceTiles: return 3; // per tile
                case ObjectType.SwingAxe: return 22;
                case ObjectType.MimicChest: return 28;
                case ObjectType.SleepyTroll: return 35;
                case ObjectType.Fan: return 30;
                case ObjectType.Gate: return 20; // with its lever: a pair costs 20
                case ObjectType.SirWhiskers: return 60;
                default: return 0;
            }
        }

        /// <summary>Build points available at a Vault Level.</summary>
        public static int Budget(int level) => Budgets[Index(level)];

        /// <summary>
        /// The fewest build points a vault must use: 60% of its budget from Vault Level 2, so vaults
        /// can't stay sparse (Level 1 is free-form while players learn the editor).
        /// </summary>
        public static int MinBuildPoints(int level) => level <= 1 ? 0 : Budget(level) * 60 / 100;

        /// <summary>
        /// The shortest walk from the entrance to the hoard a vault this size must have, in tiles
        /// (VaultValidator.RouteTiles): about twice the grid's height, so the way in must wind. Set by
        /// grid, not Vault Level, so a vault nobody has expanded never needs a tighter maze; 0 for a
        /// grid the table doesn't list.
        /// </summary>
        public static int MinRouteTiles(int width, int height)
        {
            for (int l = MinVaultLevel; l <= MaxVaultLevel; l++)
            {
                if (GridWidth(l) == width && GridHeight(l) == height)
                {
                    return MinRoutes[Index(l)];
                }
            }

            return 0;
        }

        public static int BrazierLimit(int level) => Braziers[Index(level)];

        public static int MaxTreasurePiles(int level) => TreasurePiles[Index(level)];

        /// <summary>Decorations (cosmetic props, trophies included) a vault at this level can hold.</summary>
        public static int MaxDecorations(int level) => DecorationLimits[Index(level)];

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

            return total + IceTileCount(vault) * Cost(ObjectType.IceTiles);
        }

        public static int IceTileCount(VaultDef vault)
        {
            int n = 0;
            for (int i = 0; i < vault.Width * vault.Height; i++)
            {
                n += vault.TileAtIndex(i) == TileType.Ice ? 1 : 0;
            }

            return n;
        }

        private static int Clamp(int level)
        {
            return level < MinVaultLevel ? MinVaultLevel : level > MaxVaultLevel ? MaxVaultLevel : level;
        }

        private static int Index(int level)
        {
            return Clamp(level) - MinVaultLevel;
        }
    }
}
