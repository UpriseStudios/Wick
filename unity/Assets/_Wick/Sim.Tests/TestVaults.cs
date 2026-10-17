using System;
using System.Text;

namespace Wick.Sim.Tests
{
    /// <summary>
    /// Builds vault JSON from ASCII maps so tests read like the level they describe.
    /// '.' floor, '#' wall, 'D' door, 'O' pit, '~' ice, 'E' entrance (floor).
    /// </summary>
    public static class TestVaults
    {
        public static byte[] MapToTiles(string[] rows)
        {
            int h = rows.Length;
            int w = rows[0].Length;
            var bytes = new byte[w * h];
            for (int y = 0; y < h; y++)
            {
                if (rows[y].Length != w)
                {
                    throw new ArgumentException("Map rows must all be the same width");
                }

                for (int x = 0; x < w; x++)
                {
                    bytes[y * w + x] = (byte)ToTile(rows[y][x]);
                }
            }

            return bytes;
        }

        public static string Json(string[] rows, string objectsJson = "[]")
        {
            TilePos entrance = FindEntrance(rows);
            var sb = new StringBuilder();
            sb.Append("{\"schema\":1,\"vaultId\":\"v_test\",\"version\":1,\"vaultLevel\":1,");
            sb.Append("\"grid\":{\"w\":").Append(rows[0].Length).Append(",\"h\":").Append(rows.Length).Append("},");
            sb.Append("\"tiles\":\"").Append(Convert.ToBase64String(MapToTiles(rows))).Append("\",");
            sb.Append("\"entrance\":[").Append(entrance.X).Append(',').Append(entrance.Y).Append("],");
            sb.Append("\"objects\":").Append(objectsJson).Append('}');
            return sb.ToString();
        }

        public static VaultDef Parse(params string[] rows)
        {
            return VaultJson.Parse(Json(rows));
        }

        private static TilePos FindEntrance(string[] rows)
        {
            for (int y = 0; y < rows.Length; y++)
            {
                int x = rows[y].IndexOf('E');
                if (x >= 0)
                {
                    return new TilePos(x, y);
                }
            }

            throw new ArgumentException("Map has no 'E' entrance");
        }

        private static TileType ToTile(char c)
        {
            switch (c)
            {
                case '.':
                case 'E': return TileType.Floor;
                case '#': return TileType.Wall;
                case 'D': return TileType.Door;
                case 'O': return TileType.Pit;
                case '~': return TileType.Ice;
                default: throw new ArgumentException("Unknown map char '" + c + "'");
            }
        }
    }
}
