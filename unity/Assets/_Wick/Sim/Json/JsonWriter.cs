using System;
using System.Collections.Generic;
using System.Text;

namespace Wick.Sim
{
    /// <summary>
    /// Minimal JSON writer, the counterpart of JsonReader: integers, strings, booleans, null,
    /// arrays and objects, written compactly in call order. Used for vault documents.
    /// </summary>
    public sealed class JsonWriter
    {
        private readonly StringBuilder _sb = new StringBuilder();
        private readonly Stack<bool> _needsComma = new Stack<bool>();
        private bool _afterName;

        public JsonWriter BeginObject()
        {
            Separate();
            _sb.Append('{');
            _needsComma.Push(false);
            return this;
        }

        public JsonWriter EndObject()
        {
            _needsComma.Pop();
            _sb.Append('}');
            return this;
        }

        public JsonWriter BeginArray()
        {
            Separate();
            _sb.Append('[');
            _needsComma.Push(false);
            return this;
        }

        public JsonWriter EndArray()
        {
            _needsComma.Pop();
            _sb.Append(']');
            return this;
        }

        public JsonWriter Name(string name)
        {
            Separate();
            WriteString(name);
            _sb.Append(':');
            _afterName = true;
            return this;
        }

        public JsonWriter Value(long v)
        {
            Separate();
            _sb.Append(v.ToString(System.Globalization.CultureInfo.InvariantCulture));
            return this;
        }

        public JsonWriter Value(string s)
        {
            Separate();
            if (s == null)
            {
                _sb.Append("null");
            }
            else
            {
                WriteString(s);
            }

            return this;
        }

        public JsonWriter Value(bool b)
        {
            Separate();
            _sb.Append(b ? "true" : "false");
            return this;
        }

        public JsonWriter Field(string name, long v) => Name(name).Value(v);
        public JsonWriter Field(string name, string s) => Name(name).Value(s);
        public JsonWriter Field(string name, bool b) => Name(name).Value(b);

        public override string ToString()
        {
            if (_needsComma.Count != 0)
            {
                throw new InvalidOperationException("Unclosed JSON object or array");
            }

            return _sb.ToString();
        }

        private void Separate()
        {
            if (_afterName)
            {
                _afterName = false;
                return;
            }

            if (_needsComma.Count == 0)
            {
                return;
            }

            if (_needsComma.Peek())
            {
                _sb.Append(',');
            }
            else
            {
                _needsComma.Pop();
                _needsComma.Push(true);
            }
        }

        private void WriteString(string s)
        {
            _sb.Append('"');
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': _sb.Append("\\\""); break;
                    case '\\': _sb.Append("\\\\"); break;
                    case '\n': _sb.Append("\\n"); break;
                    case '\r': _sb.Append("\\r"); break;
                    case '\t': _sb.Append("\\t"); break;
                    default:
                        if (c < 0x20)
                        {
                            _sb.Append("\\u").Append(((int)c).ToString("x4"));
                        }
                        else
                        {
                            _sb.Append(c);
                        }

                        break;
                }
            }

            _sb.Append('"');
        }
    }
}
