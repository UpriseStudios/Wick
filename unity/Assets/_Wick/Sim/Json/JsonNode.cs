using System;
using System.Collections.Generic;

namespace Wick.Sim
{
    public enum JsonKind : byte
    {
        Null,
        Bool,
        Integer,
        String,
        Array,
        Object,
    }

    /// <summary>
    /// Parsed JSON value. Objects keep member order and use linear lookup (no Dictionary),
    /// so nothing about parsing depends on hash ordering.
    /// </summary>
    public sealed class JsonNode
    {
        private readonly bool _bool;
        private readonly long _integer;
        private readonly string _string;
        private readonly List<JsonNode> _items;
        private readonly List<string> _keys;

        private JsonNode(JsonKind kind, bool b, long integer, string s, List<JsonNode> items, List<string> keys)
        {
            Kind = kind;
            _bool = b;
            _integer = integer;
            _string = s;
            _items = items;
            _keys = keys;
        }

        public JsonKind Kind { get; }

        public static JsonNode Null() => new JsonNode(JsonKind.Null, false, 0, null, null, null);
        public static JsonNode Bool(bool b) => new JsonNode(JsonKind.Bool, b, 0, null, null, null);
        public static JsonNode Integer(long v) => new JsonNode(JsonKind.Integer, false, v, null, null, null);
        public static JsonNode String(string s) => new JsonNode(JsonKind.String, false, 0, s, null, null);
        public static JsonNode Array(List<JsonNode> items) => new JsonNode(JsonKind.Array, false, 0, null, items, null);

        public static JsonNode Object(List<string> keys, List<JsonNode> values)
        {
            return new JsonNode(JsonKind.Object, false, 0, null, values, keys);
        }

        /// <summary>Array elements, or object values in member order.</summary>
        public int Count => _items == null ? 0 : _items.Count;

        public JsonNode this[int index]
        {
            get
            {
                Expect(JsonKind.Array);
                return _items[index];
            }
        }

        public bool AsBool()
        {
            Expect(JsonKind.Bool);
            return _bool;
        }

        public long AsLong()
        {
            Expect(JsonKind.Integer);
            return _integer;
        }

        public int AsInt()
        {
            long v = AsLong();
            if (v < int.MinValue || v > int.MaxValue)
            {
                throw new FormatException("JSON integer out of int range: " + v);
            }

            return (int)v;
        }

        public string AsString()
        {
            Expect(JsonKind.String);
            return _string;
        }

        /// <summary>Member value, or null if this object has no such key.</summary>
        public JsonNode Find(string key)
        {
            Expect(JsonKind.Object);
            for (int i = 0; i < _keys.Count; i++)
            {
                if (string.Equals(_keys[i], key, StringComparison.Ordinal))
                {
                    return _items[i];
                }
            }

            return null;
        }

        public JsonNode Get(string key)
        {
            JsonNode node = Find(key);
            if (node == null)
            {
                throw new FormatException("Missing JSON field '" + key + "'");
            }

            return node;
        }

        public int GetInt(string key) => Get(key).AsInt();
        public long GetLong(string key) => Get(key).AsLong();
        public string GetString(string key) => Get(key).AsString();

        public int GetInt(string key, int fallback)
        {
            JsonNode node = Find(key);
            return node == null || node.Kind == JsonKind.Null ? fallback : node.AsInt();
        }

        public string GetString(string key, string fallback)
        {
            JsonNode node = Find(key);
            return node == null || node.Kind == JsonKind.Null ? fallback : node.AsString();
        }

        private void Expect(JsonKind kind)
        {
            if (Kind != kind)
            {
                throw new FormatException("Expected JSON " + kind + " but found " + Kind);
            }
        }
    }
}
