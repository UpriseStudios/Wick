namespace Wick.Sim
{
    /// <summary>The Crowns both sides end a raid with, and how much each changed.</summary>
    public readonly struct CrownChange
    {
        public readonly int Raider;
        public readonly int Defender;
        public readonly int RaiderDelta;
        public readonly int DefenderDelta;

        public CrownChange(int raider, int defender, int raiderDelta, int defenderDelta)
        {
            Raider = raider;
            Defender = defender;
            RaiderDelta = raiderDelta;
            DefenderDelta = defenderDelta;
        }
    }

    /// <summary>
    /// Crown rating (trophies). Everyone starts at 0 and never drops below it. The raider's change
    /// comes from a table of stars against the rating gap (defender minus raider): beating a
    /// stronger vault earns more, failing against one costs less. The defender moves the opposite
    /// way; seed (bot) vaults never change. Matchmaking only: raids play the same at any rating.
    /// </summary>
    public static class CrownRules
    {
        public const int Start = 0;

        /// <summary>Gap bands, defender minus raider: 0 is ≤ -200, 2 is within ±49, 4 is ≥ +200.</summary>
        public static int Band(int gap)
        {
            if (gap <= -200) return 0;
            if (gap <= -50) return 1;
            if (gap < 50) return 2;
            if (gap < 200) return 3;
            return 4;
        }

        /// <summary>The table: stars 0 is a failed raid.</summary>
        public static int RaiderDelta(int stars, int band)
        {
            switch (stars)
            {
                case 0:
                    switch (band) { case 0: return -20; case 1: return -17; case 2: return -14; case 3: return -11; default: return -8; }
                case 1:
                    switch (band) { case 0: return 4; case 1: return 6; case 2: return 8; case 3: return 10; default: return 12; }
                case 2:
                    switch (band) { case 0: return 8; case 1: return 12; case 2: return 16; case 3: return 20; default: return 24; }
                default:
                    switch (band) { case 0: return 12; case 1: return 18; case 2: return 24; case 3: return 30; default: return 36; }
            }
        }

        public static CrownChange Apply(int raider, int defender, int stars, bool defenderIsBot)
        {
            int delta = RaiderDelta(stars < 0 ? 0 : stars, Band(defender - raider));
            int newRaider = Floor(raider + delta);
            int newDefender = defenderIsBot ? defender : Floor(defender - delta);
            return new CrownChange(newRaider, newDefender, newRaider - raider, newDefender - defender);
        }

        private static int Floor(int crowns)
        {
            return crowns < Start ? Start : crowns;
        }
    }
}
