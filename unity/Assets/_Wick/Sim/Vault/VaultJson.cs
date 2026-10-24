using System;

namespace Wick.Sim
{
    /// <summary>
    /// Reads the vault JSON document (tech-design "Data model") into a VaultDef. Checks what the
    /// sim needs to run safely (structure, bounds, ids, trap timing); design rules for
    /// player-built vaults are VaultValidator's job, and anything a player builds must pass both.
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
                hoard,
                root.GetString("theme", "crypt"),
                DefenceLevels.FromNode(root.Find("defenceLevels")),
                ReadDecorations(root.Find("decor"), w, h));

            if (!vault.InBounds(entrance))
            {
                throw new FormatException("Entrance " + entrance + " is out of bounds");
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
            if (o.Type == ObjectType.IceTiles)
            {
                throw new FormatException(what + ": ice is painted as tiles, not placed as an object");
            }

            if (!vault.InBounds(o.Pos))
            {
                throw new FormatException(what + " is out of bounds at " + o.Pos);
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
                    if (!vault.InBounds(p))
                    {
                        throw new FormatException(what + " has a patrol point out of bounds " + p);
                    }
                }
            }

            if (o.Type == ObjectType.Treasure && (o.Value <= 0 || o.Value > SimTuning.MaxTreasureValue))
            {
                throw new FormatException(what + " needs 0 < value <= " + SimTuning.MaxTreasureValue);
            }
        }

        /// <summary>
        /// Writes a vault as compact JSON in the data-model layout. Fields the sim does not use
        /// (ownerId, buildPoints, clearCheck) are added by the callers that own them.
        /// </summary>
        public static string Write(VaultDef vault)
        {
            var w = new JsonWriter();
            w.BeginObject();
            WriteFields(w, vault);
            w.EndObject();
            return w.ToString();
        }

        /// <summary>
        /// The full vault document as stored and published: the layout plus its build points and,
        /// when present, the clear-check run.
        /// </summary>
        public static string WriteDocument(VaultDef vault, ClearCheck clearCheck)
        {
            var w = new JsonWriter();
            w.BeginObject();
            WriteFields(w, vault);
            w.Field("buildPoints", VaultRules.BuildPoints(vault));
            if (clearCheck != null)
            {
                w.Name("clearCheck");
                clearCheck.Write(w);
            }

            w.EndObject();
            return w.ToString();
        }

        /// <summary>The clear-check stored in a vault document, or null.</summary>
        public static ClearCheck ReadClearCheck(JsonNode root)
        {
            JsonNode node = root.Find("clearCheck");
            return node == null || node.Kind == JsonKind.Null ? null : ClearCheck.FromNode(node);
        }

        /// <summary>Writes the vault's fields into an object the caller has already begun.</summary>
        public static void WriteFields(JsonWriter w, VaultDef vault)
        {
            w.Field("schema", vault.Schema);
            w.Field("vaultId", vault.VaultId ?? "");
            w.Field("version", vault.Version);
            w.Field("vaultLevel", vault.VaultLevel);
            w.Field("theme", vault.Theme ?? "crypt");
            w.Name("grid").BeginObject().Field("w", vault.Width).Field("h", vault.Height).EndObject();

            var bytes = new byte[vault.Width * vault.Height];
            for (int i = 0; i < bytes.Length; i++)
            {
                bytes[i] = (byte)vault.TileAtIndex(i);
            }

            w.Field("tiles", Convert.ToBase64String(bytes));
            w.Name("entrance");
            WritePos(w, vault.Entrance);
            if (vault.HasHoard)
            {
                TileRect r = vault.Hoard;
                w.Name("hoard").BeginArray().Value(r.MinX).Value(r.MinY).Value(r.MaxX).Value(r.MaxY).EndArray();
            }

            // Only upgraded vaults carry levels, so vaults from before M10 read and write unchanged.
            if (!vault.DefenceLevels.IsBase)
            {
                w.Name("defenceLevels");
                vault.DefenceLevels.Write(w);
            }

            if (vault.DecorationCount > 0)
            {
                w.Name("decor").BeginArray();
                for (int i = 0; i < vault.DecorationCount; i++)
                {
                    DecorationDef d = vault.GetDecoration(i);
                    w.BeginObject().Field("id", d.Id).Field("kind", d.Kind).Field("x", d.Pos.X).Field("y", d.Pos.Y);
                    if (d.Rot != 0)
                    {
                        w.Field("rot", d.Rot);
                    }

                    w.EndObject();
                }

                w.EndArray();
            }

            w.Name("objects").BeginArray();
            for (int i = 0; i < vault.ObjectCount; i++)
            {
                ObjectDef o = vault.GetObject(i);
                w.BeginObject();
                w.Field("id", o.Id);
                w.Field("type", ObjectTypes.Name(o.Type));
                w.Field("x", o.Pos.X);
                w.Field("y", o.Pos.Y);
                if (o.Rot != 0)
                {
                    w.Field("rot", o.Rot);
                }

                if (ObjectTypes.IsTimedTrap(o.Type))
                {
                    w.Field("cycle", o.Cycle);
                    w.Field("offset", o.Offset);
                }

                if (o.Type == ObjectType.Treasure)
                {
                    w.Field("value", o.Value);
                }

                if (o.Patrol.Length > 0)
                {
                    w.Name("patrol").BeginArray();
                    foreach (TilePos p in o.Patrol)
                    {
                        WritePos(w, p);
                    }

                    w.EndArray();
                }

                w.EndObject();
            }

            w.EndArray();
        }

        private static void WritePos(JsonWriter w, TilePos p)
        {
            w.BeginArray().Value(p.X).Value(p.Y).EndArray();
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

        /// <summary>Decorations: the sim only needs them in bounds; VaultValidator checks the rest.</summary>
        private static DecorationDef[] ReadDecorations(JsonNode list, int w, int h)
        {
            if (list == null || list.Kind == JsonKind.Null)
            {
                return new DecorationDef[0];
            }

            var decorations = new DecorationDef[list.Count];
            for (int i = 0; i < list.Count; i++)
            {
                JsonNode d = list[i];
                var pos = new TilePos(d.GetInt("x"), d.GetInt("y"));
                int rot = d.GetInt("rot", 0);
                if (pos.X < 0 || pos.Y < 0 || pos.X >= w || pos.Y >= h || rot < 0 || rot > 3)
                {
                    throw new FormatException("Decoration " + d.GetInt("id") + " is out of bounds or badly turned");
                }

                decorations[i] = new DecorationDef(d.GetInt("id"), d.GetString("kind"), pos, rot);
            }

            Array.Sort(decorations, (a, b) => a.Id.CompareTo(b.Id));
            return decorations;
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
