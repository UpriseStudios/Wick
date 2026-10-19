using Wick.Economy;

namespace Wick.CloudCode.Tests
{
    /// <summary>The bundled default economy, as the tests' source of numbers.</summary>
    public static class TestEconomy
    {
        public static readonly EconomyConfig Default = EconomyDefaults.Load();
        public static LootConfig Loot => Default.Loot;
    }
}
