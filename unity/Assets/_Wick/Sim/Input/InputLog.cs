using System;
using System.Collections.Generic;

namespace Wick.Sim
{
    /// <summary>
    /// Ordered player commands for one raid. Binary form, per command:
    /// unsigned LEB128 varint of (tick - previous tick), first relative to 0,
    /// then one byte: bits 0-2 command type, bits 3-4 direction, bits 5-7 zero.
    /// The encoding is canonical: every log has exactly one byte form.
    /// </summary>
    public sealed class InputLog
    {
        private const int TypeMask = 0x07;
        private const int DirShift = 3;
        private const int DirMask = 0x03;
        private const int ReservedMask = 0xE0;

        private readonly Command[] _commands;

        public InputLog(Command[] commands)
        {
            if (commands == null)
            {
                throw new ArgumentNullException(nameof(commands));
            }

            _commands = (Command[])commands.Clone();
            int previous = 0;
            for (int i = 0; i < _commands.Length; i++)
            {
                Command c = _commands[i];
                if (c.Tick < previous)
                {
                    throw new ArgumentException("Command ticks must be non-negative and non-decreasing (index " + i + ")");
                }

                if ((byte)c.Type > (byte)CommandType.Decoy || (byte)c.Dir > (byte)Dir.W)
                {
                    throw new ArgumentException("Invalid command at index " + i);
                }

                previous = c.Tick;
            }
        }

        public static readonly InputLog Empty = new InputLog(new Command[0]);

        public int Count => _commands.Length;

        public Command this[int index] => _commands[index];

        public byte[] Encode()
        {
            var bytes = new List<byte>(_commands.Length * 2);
            int previous = 0;
            for (int i = 0; i < _commands.Length; i++)
            {
                Command c = _commands[i];
                uint delta = (uint)(c.Tick - previous);
                while (delta >= 0x80)
                {
                    bytes.Add((byte)(delta | 0x80));
                    delta >>= 7;
                }

                bytes.Add((byte)delta);
                bytes.Add((byte)((int)c.Type | ((int)c.Dir << DirShift)));
                previous = c.Tick;
            }

            return bytes.ToArray();
        }

        public static InputLog Decode(byte[] data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            var commands = new List<Command>();
            int pos = 0;
            long tick = 0;
            while (pos < data.Length)
            {
                tick += ReadVarint(data, ref pos);
                if (tick > int.MaxValue)
                {
                    throw new FormatException("Input log tick overflow at byte " + pos);
                }

                if (pos >= data.Length)
                {
                    throw new FormatException("Input log truncated: missing command byte");
                }

                int b = data[pos++];
                int type = b & TypeMask;
                var dir = (Dir)((b >> DirShift) & DirMask);
                if ((b & ReservedMask) != 0 || type > (int)CommandType.Decoy)
                {
                    throw new FormatException("Invalid command byte 0x" + b.ToString("x2") + " at byte " + (pos - 1));
                }

                if (!Command.HasDir((CommandType)type) && dir != Dir.N)
                {
                    throw new FormatException("Direction set on a command without one at byte " + (pos - 1));
                }

                commands.Add(new Command((int)tick, (CommandType)type, dir));
            }

            return new InputLog(commands.ToArray());
        }

        public string ToBase64()
        {
            return Convert.ToBase64String(Encode());
        }

        public static InputLog FromBase64(string base64)
        {
            byte[] data;
            try
            {
                data = Convert.FromBase64String(base64);
            }
            catch (FormatException e)
            {
                throw new FormatException("Input log is not valid base64", e);
            }

            return Decode(data);
        }

        private static long ReadVarint(byte[] data, ref int pos)
        {
            long value = 0;
            int shift = 0;
            int start = pos;
            while (true)
            {
                if (pos >= data.Length)
                {
                    throw new FormatException("Input log truncated inside varint at byte " + start);
                }

                byte b = data[pos++];
                if (shift == 28 && (b & 0x70) != 0)
                {
                    throw new FormatException("Input log varint too large at byte " + start);
                }

                value |= (long)(b & 0x7F) << shift;
                if ((b & 0x80) == 0)
                {
                    if (b == 0 && pos - start > 1)
                    {
                        throw new FormatException("Non-canonical varint at byte " + start);
                    }

                    return value;
                }

                shift += 7;
                if (shift > 28)
                {
                    throw new FormatException("Input log varint too long at byte " + start);
                }
            }
        }
    }
}
