using NUnit.Framework;
using Wick.Services;

namespace Wick.Game.Tests
{
    /// <summary>Release builds connect to a UGS environment the project actually has.</summary>
    public sealed class ServicesSettingsTests
    {
        [Test]
        public void ReleaseBuildsUseAKnownEnvironment()
        {
            CollectionAssert.Contains(new[] { "development", "production" }, ServicesSettings.Current.ReleaseEnvironment);
        }

        [Test]
        public void EmptyReleaseEnvironmentMeansProduction()
        {
            Assert.AreEqual("production", new ServicesSettings().ReleaseEnvironment);
        }
    }
}
