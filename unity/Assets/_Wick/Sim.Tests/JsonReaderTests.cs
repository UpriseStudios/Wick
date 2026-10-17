using System;
using NUnit.Framework;

namespace Wick.Sim.Tests
{
    public class JsonReaderTests
    {
        [Test]
        public void ParsesNestedObjectsAndArrays()
        {
            JsonNode root = JsonReader.Parse(" { \"a\": 1, \"b\": [true, false, null], \"c\": { \"d\": -42 } } ");
            Assert.AreEqual(1, root.GetInt("a"));
            JsonNode b = root.Get("b");
            Assert.AreEqual(3, b.Count);
            Assert.IsTrue(b[0].AsBool());
            Assert.IsFalse(b[1].AsBool());
            Assert.AreEqual(JsonKind.Null, b[2].Kind);
            Assert.AreEqual(-42, root.Get("c").GetInt("d"));
        }

        [Test]
        public void KeepsMemberOrderAndFindsByKey()
        {
            JsonNode root = JsonReader.Parse("{\"z\":1,\"a\":2}");
            Assert.AreEqual(2, root.GetInt("a"));
            Assert.IsNull(root.Find("missing"));
            Assert.Throws<FormatException>(() => root.Get("missing"));
        }

        [Test]
        public void DecodesStringEscapes()
        {
            JsonNode s = JsonReader.Parse("\"q\\\"b\\\\s\\/n\\nt\\tu\\u00e9\"");
            Assert.AreEqual("q\"b\\s/n\nt\tué", s.AsString());
        }

        [Test]
        public void ParsesLongExtremes()
        {
            Assert.AreEqual(long.MaxValue, JsonReader.Parse("9223372036854775807").AsLong());
            Assert.AreEqual(long.MinValue, JsonReader.Parse("-9223372036854775808").AsLong());
        }

        [TestCase("1.5")]
        [TestCase("1e3")]
        [TestCase("2E-1")]
        [TestCase("9223372036854775808")]
        [TestCase("-9223372036854775809")]
        [TestCase("01")]
        [TestCase("-")]
        [TestCase("{\"a\":1,}")]
        [TestCase("[1 2]")]
        [TestCase("{\"a\":1,\"a\":2}")]
        [TestCase("\"unterminated")]
        [TestCase("\"bad \\x escape\"")]
        [TestCase("tru")]
        [TestCase("1 2")]
        [TestCase("")]
        public void RejectsInvalidInput(string json)
        {
            Assert.Throws<FormatException>(() => JsonReader.Parse(json));
        }

        [Test]
        public void RejectsDeepNesting()
        {
            string deep = new string('[', JsonReader.MaxDepth + 2) + new string(']', JsonReader.MaxDepth + 2);
            Assert.Throws<FormatException>(() => JsonReader.Parse(deep));
        }

        [Test]
        public void IntOutOfRangeThrows()
        {
            JsonNode n = JsonReader.Parse("4294967296");
            Assert.Throws<FormatException>(() => n.AsInt());
        }
    }
}
