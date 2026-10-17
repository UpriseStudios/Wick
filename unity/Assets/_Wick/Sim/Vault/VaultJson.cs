using System;

namespace Wick.Sim
{
    /// <summary>
    /// Reads the vault JSON document (tech-design "Data model") into a validated VaultDef.
    /// The server parses client-supplied vaults with this, so every rule the sim relies on
    /// is checked here.
    /// </summary>
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

            bool hasHoard = false;
            var hoard = new TileRect(0, 0, -1, -1);
            JsonNode hoardNode = root.Find("hoard");
            if (hoardNode != null && hoardNode.Kind != JsonKind.Null)
            {
                hoard = ReadRect(hoardNode, w, h);
                hasHoard = true;
            }

            var vault = new VaultDef(
                schema,
                root.GetString("vaultId", ""),
                root.GetInt("version", 0),
                root.GetInt("vaultLevel", 0),
                w,
                h,
                tiles,
                entrance,
                objects,
                hasHoard,
                hoard);

            if (!vault.InBounds(entrance) || !vault.IsWalkable(entrance))
            {
                throw new FormatException("Entrance " + entrance + " must be an in-bounds walkable tile");
            }

            for (int i = 0; i < vault.ObjectCount; i++)
            {
                ObjectDef o = vault.GetObject(i);
                if (i > 0 && vault.GetObject(i - 1).Id == o.Id)
                {
                    throw new FormatException("Duplicate object id " + o.Id);
                }

                ValidateObject(vault, o);
            }

            return vault;
        }

        private static void ValidateObject(VaultDef vault, ObjectDef o)
        {
            string what = "Object " + o.Id + " (" + o.Type + ")";
            if (!vault.InBounds(o.Pos))
            {
                throw new FormatException(what + " is out of bounds at " + o.Pos);
            }

            bool onWall = vault.TileAt(o.Pos) == TileType.Wall;
            if (o.Type == ObjectType.Brazier)
            {
                if (!onWall)
                {
                    throw new FormatException(what + " must be on a wall tile");
                }

                bool reachable = false;
                for (int d = 0; d < 4; d++)
                {
                    reachable |= vault.IsWalkable(o.Pos.Step((Dir)d));
                }

                if (!reachable)
                {
                    throw new FormatException(what + " has no walkable tile next to it");
                }

                return;
            }

            if (onWall)
            {
                throw new FormatException(what + " cannot be on a wall tile");
            }

            for (int i = 0; i < vault.ObjectCount; i++)
            {
                ObjectDef other = vault.GetObject(i);
                bool crateInvolved = o.Type == ObjectType.Crate || other.Type == ObjectType.Crate;
                if (other.Id != o.Id && other.Pos == o.Pos && crateInvolved)
                {
                    throw new FormatException(what + " shares its tile with a crate");
                }
            }

            if ((o.Type == ObjectType.Crate || o.Type == ObjectType.SnufferBell) && o.Pos == vault.Entrance)
            {
                throw new FormatException(what + " cannot be on the entrance");
            }

            if (ObjectTypes.IsTimedTrap(o.Type))
            {
                int min = ObjectTypes.MinCycle(o.Type);
                if (o.Cycle < min)
                {
                    throw new FormatException(what + " needs cycle >= " + min + ", got " + o.Cycle);
                }

                if (o.Offset < 0 || o.Offset >= o.Cycle)
                {
                    throw new FormatException(what + " needs 0 <= offset < cycle, got " + o.Offset);
                }
            }

            if (o.Type == ObjectType.NightOwl)
            {
                foreach (TilePos p in o.Patrol)
                {
                    if (!vault.IsWalkable(p))
                    {
                        throw new FormatException(what + " has a patrol point on a blocked tile " + p);
                    }
                }
            }

            if (o.Type == ObjectType.Treasure && (o.Value <= 0 || o.Value > SimTuning.MaxTreasureValue))
            {
                throw new FormatException(what + " needs 0 < value <= " + SimTuning.MaxTreasureValue);
            }
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
                    ObjectTypes.Parse(o.GetString("type")),
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

        private static TileRect ReadRect(JsonNode node, int w, int h)
        {
            if (node.Kind != JsonKind.Array || node.Count != 4)
            {
                throw new FormatException("hoard must be [x1, y1, x2, y2]");
            }

            var r = new TileRect(node[0].AsInt(), node[1].AsInt(), node[2].AsInt(), node[3].AsInt());
            if (r.MinX < 0 || r.MinY < 0 || r.MaxX >= w || r.MaxY >= h || r.MinX > r.MaxX || r.MinY > r.MaxY)
            {
                throw new FormatException("hoard rect is out of bounds or inverted");
            }

            return r;
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
