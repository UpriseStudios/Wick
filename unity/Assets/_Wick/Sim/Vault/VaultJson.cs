using System;

namespace Wick.Sim
{
    /// <summary>Reads the vault JSON document (tech-design "Data model") into a validated VaultDef.</summary>
    public static class VaultJson
    {
        public const int SupportedSchema = 1;
        public const int MaxGridSide = 64;

        public static VaultDef Parse(string json)
        {
            return FromNode(JsonReader.Parse(json));
        }

        public static VaultDef FromNode(JsonNode root)
        {
            int schema = root.GetInt("schema");
            if (schema != SupportedSchema)
            {
                throw new FormatException("Unsupported vault schema " + schema);
            }

            JsonNode grid = root.Get("grid");
            int w = grid.GetInt("w");
            int h = grid.GetInt("h");
            if (w < 1 || h < 1 || w > MaxGridSide || h > MaxGridSide)
            {
                throw new FormatException("Grid size out of range: " + w + "x" + h);
            }

            byte[] raw;
            try
            {
                raw = Convert.FromBase64String(root.GetString("tiles"));
            }
            catch (FormatException e)
            {
                throw new FormatException("Vault tiles are not valid base64", e);
            }

            if (raw.Length != w * h)
            {
                throw new FormatException("Vault tiles has " + raw.Length + " bytes, expected " + (w * h));
            }

            var tiles = new TileType[raw.Length];
            for (int i = 0; i < raw.Length; i++)
            {
                if (raw[i] > (byte)TileType.Ice)
                {
                    throw new FormatException("Unknown tile type " + raw[i] + " at index " + i);
                }

                tiles[i] = (TileType)raw[i];
            }

            TilePos entrance = ReadPos(root.Get("entrance"), "entrance");
            ObjectDef[] objects = ReadObjects(root.Find("objects"));

            var vault = new VaultDef(
                schema,
                root.GetString("vaultId", ""),
                root.GetInt("version", 0),
                root.GetInt("vaultLevel", 0),
                w,
                h,
                tiles,
                entrance,
                objects);

            if (!vault.InBounds(entrance) || !vault.IsWalkable(entrance))
            {
                throw new FormatException("Entrance " + entrance + " must be an in-bounds walkable tile");
            }

            for (int i = 0; i < vault.ObjectCount; i++)
            {
                ObjectDef o = vault.GetObject(i);
                if (!vault.InBounds(o.Pos))
                {
                    throw new FormatException("Object " + o.Id + " is out of bounds at " + o.Pos);
                }

                if (i > 0 && vault.GetObject(i - 1).Id == o.Id)
                {
                    throw new FormatException("Duplicate object id " + o.Id);
                }
            }

            return vault;
        }

        private static ObjectDef[] ReadObjects(JsonNode list)
        {
            if (list == null || list.Kind == JsonKind.Null)
            {
                return new ObjectDef[0];
            }

            var objects = new ObjectDef[list.Count];
            for (int i = 0; i < list.Count; i++)
            {
                JsonNode o = list[i];
                int rot = o.GetInt("rot", 0);
                if (rot < 0 || rot > 3)
                {
                    throw new FormatException("Object rot must be 0..3, got " + rot);
                }

                objects[i] = new ObjectDef(
                    o.GetInt("id"),
                    o.GetString("type"),
                    new TilePos(o.GetInt("x"), o.GetInt("y")),
                    rot,
                    o.GetInt("cycle", 0),
                    o.GetInt("offset", 0),
                    o.GetInt("value", 0),
                    ReadPatrol(o.Find("patrol")));
            }

            // Ids are unique (checked by the caller), so this order is fully determined.
            Array.Sort(objects, (a, b) => a.Id.CompareTo(b.Id));
            return objects;
        }

        private static TilePos[] ReadPatrol(JsonNode list)
        {
            if (list == null || list.Kind == JsonKind.Null)
            {
                return new TilePos[0];
            }

            var points = new TilePos[list.Count];
            for (int i = 0; i < list.Count; i++)
            {
                points[i] = ReadPos(list[i], "patrol point");
            }

            return points;
        }

        private static TilePos ReadPos(JsonNode pair, string what)
        {
            if (pair.Kind != JsonKind.Array || pair.Count != 2)
            {
                throw new FormatException(what + " must be [x, y]");
            }

            return new TilePos(pair[0].AsInt(), pair[1].AsInt());
        }
    }
}
