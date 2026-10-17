using System.IO;
using NUnit.Framework;

namespace Wick.Sim.Tests
{
    public class JsonWriterTests
    {
        [Test]
        public void WritesNestedValues()
        {
            var w = new JsonWriter();
            w.BeginObject()
                .Field("a", 1)
                .Field("s", "x\"y\\z\n")
                .Field("t", true)
                .Name("list").BeginArray().Value(-5).Value("q").EndArray()
                .Name("o").BeginObject().EndObject()
                .EndObject();
            Assert.AreEqual("{\"a\":1,\"s\":\"x\\\"y\\\\z\\n\",\"t\":true,\"list\":[-5,\"q\"],\"o\":{}}", w.ToString());
        }

        [Test]
        public void ReaderReadsWhatWriterWrote()
        {
            var w = new JsonWriter();
            w.BeginObject().Field("name", "tab\there \u0001").Field("big", long.MaxValue).EndObject();
            JsonNode root = JsonReader.Parse(w.ToString());
            Assert.AreEqual("tab\there \u0001", root.GetString("name"));
            Assert.AreEqual(long.MaxValue, root.GetLong("big"));
        }

        [Test]
        public void UnclosedThrows()
        {
            var w = new JsonWriter();
            w.BeginObject();
            Assert.Throws<System.InvalidOperationException>(() => w.ToString());
        }

        [TestCaseSource(typeof(GoldenReplayTests), nameof(GoldenReplayTests.GoldenFiles))]
        public void Vault_RoundTripsThroughWrite(string file)
        {
            RaidFile raid = RaidFile.Parse(File.ReadAllText(Path.Combine(GoldenReplayTests.GoldenDirectory(), file)));
            string once = VaultJson.Write(raid.Vault);
            VaultDef again = VaultJson.Parse(once);
            Assert.AreEqual(once, VaultJson.Write(again), "write is stable");

            // The round-tripped vault plays identically.
            Assert.AreEqual(RaidRunner.RunToEnd(raid.Vault, raid.Seed, raid.Log).FinalHash,
                RaidRunner.RunToEnd(again, raid.Seed, raid.Log).FinalHash);
        }
    }
}
