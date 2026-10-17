using System;
using System.Collections.Generic;
using System.Text;

namespace Wick.Sim
{
    /// <summary>
    /// Minimal JSON parser shared by client, server and tools. Numbers must be integers
    /// (no fraction or exponent), keeping floating point out of the sim entirely.
    /// Used only when loading data, never inside RaidSim.Step.
    /// </summary>
    public static class JsonReader
    {
        public const int MaxDepth = 32;

        public static JsonNode Parse(string text)
        {
            if (text == null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            var parser = new Parser(text);
            parser.SkipWhitespace();
            JsonNode root = parser.ReadValue(0);
            parser.SkipWhitespace();
            if (!parser.AtEnd)
            {
                throw parser.Error("Unexpected trailing characters");
            }

            return root;
        }

        private struct Parser
        {
            private readonly string _s;
            private int _i;

            public Parser(string s)
            {
                _s = s;
                _i = 0;
            }

            public bool AtEnd => _i >= _s.Length;

            public FormatException Error(string message)
            {
                return new FormatException(message + " at position " + _i);
            }

            public void SkipWhitespace()
            {
                while (_i < _s.Length)
                {
                    char c = _s[_i];
                    if (c != ' ' && c != '\t' && c != '\n' && c != '\r')
                    {
                        return;
                    }

                    _i++;
                }
            }

            public JsonNode ReadValue(int depth)
            {
                if (depth > MaxDepth)
                {
                    throw Error("JSON nested too deeply");
                }

                if (AtEnd)
                {
                    throw Error("Unexpected end of JSON");
                }

                char c = _s[_i];
                switch (c)
                {
                    case '{': return ReadObject(depth);
                    case '[': return ReadArray(depth);
                    case '"': return JsonNode.String(ReadString());
                    case 't': ReadLiteral("true"); return JsonNode.Bool(true);
                    case 'f': ReadLiteral("false"); return JsonNode.Bool(false);
                    case 'n': ReadLiteral("null"); return JsonNode.Null();
                    default:
                        if (c == '-' || (c >= '0' && c <= '9'))
                        {
                            return JsonNode.Integer(ReadInteger());
                        }

                        throw Error("Unexpected character '" + c + "'");
                }
            }

            private JsonNode ReadObject(int depth)
            {
                _i++; // {
                var keys = new List<string>();
                var values = new List<JsonNode>();
                SkipWhitespace();
                if (Peek() == '}')
                {
                    _i++;
                    return JsonNode.Object(keys, values);
                }

                while (true)
                {
                    SkipWhitespace();
                    if (Peek() != '"')
                    {
                        throw Error("Expected object key");
                    }

                    string key = ReadString();
                    for (int k = 0; k < keys.Count; k++)
                    {
                        if (string.Equals(keys[k], key, StringComparison.Ordinal))
                        {
                            throw Error("Duplicate key '" + key + "'");
                        }
                    }

                    SkipWhitespace();
                    Consume(':');
                    SkipWhitespace();
                    keys.Add(key);
                    values.Add(ReadValue(depth + 1));
                    SkipWhitespace();
                    char c = Next();
                    if (c == '}')
                    {
                        return JsonNode.Object(keys, values);
                    }

                    if (c != ',')
                    {
                        _i--;
                        throw Error("Expected ',' or '}'");
                    }
                }
            }

            private JsonNode ReadArray(int depth)
            {
                _i++; // [
                var items = new List<JsonNode>();
                SkipWhitespace();
                if (Peek() == ']')
                {
                    _i++;
                    return JsonNode.Array(items);
                }

                while (true)
                {
                    SkipWhitespace();
                    items.Add(ReadValue(depth + 1));
                    SkipWhitespace();
                    char c = Next();
                    if (c == ']')
                    {
                        return JsonNode.Array(items);
                    }

                    if (c != ',')
                    {
                        _i--;
                        throw Error("Expected ',' or ']'");
                    }
                }
            }

            private string ReadString()
            {
                _i++; // opening quote
                var sb = new StringBuilder();
                while (true)
                {
                    if (AtEnd)
                    {
                        throw Error("Unterminated string");
                    }

                    char c = _s[_i++];
                    if (c == '"')
                    {
                        return sb.ToString();
                    }

                    if (c < 0x20)
                    {
                        _i--;
                        throw Error("Control character in string");
                    }

                    if (c != '\\')
                    {
                        sb.Append(c);
                        continue;
                    }

                    char e = Next();
                    switch (e)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'u': sb.Append(ReadHex4()); break;
                        default:
                            _i--;
                            throw Error("Invalid escape '\\" + e + "'");
                    }
                }
            }

            private char ReadHex4()
            {
                if (_i + 4 > _s.Length)
                {
                    throw Error("Truncated \\u escape");
                }

                int v = 0;
                for (int k = 0; k < 4; k++)
                {
                    char h = _s[_i++];
                    int d;
                    if (h >= '0' && h <= '9') d = h - '0';
                    else if (h >= 'a' && h <= 'f') d = h - 'a' + 10;
                    else if (h >= 'A' && h <= 'F') d = h - 'A' + 10;
                    else
                    {
                        _i--;
                        throw Error("Invalid hex digit in \\u escape");
                    }

                    v = (v << 4) | d;
                }

                return (char)v;
            }

            private long ReadInteger()
            {
                int start = _i;
                bool negative = false;
                if (Peek() == '-')
                {
                    negative = true;
                    _i++;
                }

                if (AtEnd || _s[_i] < '0' || _s[_i] > '9')
                {
                    throw Error("Expected digit");
                }

                if (_s[_i] == '0' && _i + 1 < _s.Length && _s[_i + 1] >= '0' && _s[_i + 1] <= '9')
                {
                    throw Error("Leading zeros are not allowed");
                }

                // Accumulate as a negative number so long.MinValue fits.
                long value = 0;
                while (!AtEnd && _s[_i] >= '0' && _s[_i] <= '9')
                {
                    int digit = _s[_i] - '0';
                    if (value < (long.MinValue + digit) / 10)
                    {
                        _i = start;
                        throw Error("Integer out of range");
                    }

                    value = value * 10 - digit;
                    _i++;
                }

                if (!AtEnd && (_s[_i] == '.' || _s[_i] == 'e' || _s[_i] == 'E'))
                {
                    throw Error("Non-integer numbers are not allowed in sim data");
                }

                if (negative)
                {
                    return value;
                }

                if (value == long.MinValue)
                {
                    _i = start;
                    throw Error("Integer out of range");
                }

                return -value;
            }

            private void ReadLiteral(string literal)
            {
                if (string.CompareOrdinal(_s, _i, literal, 0, literal.Length) != 0)
                {
                    throw Error("Invalid literal");
                }

                _i += literal.Length;
            }

            private char Peek()
            {
                return AtEnd ? '\0' : _s[_i];
            }

            private char Next()
            {
                if (AtEnd)
                {
                    throw Error("Unexpected end of JSON");
                }

                return _s[_i++];
            }

            private void Consume(char expected)
            {
                if (Peek() != expected)
                {
                    throw Error("Expected '" + expected + "'");
                }

                _i++;
            }
        }
    }
}
