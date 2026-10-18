using System;
using System.Collections.Generic;
using Wick.Sim;

namespace Wick.Game
{
    /// <summary>An object placed in a draft. Treasure value is not stored: it follows the level.</summary>
    public sealed class DraftObject
    {
        public int Id;
        public ObjectType Type;
        public TilePos Pos;
        public int Rot;
        public int Cycle;
        public int Offset;
        public List<TilePos> Patrol = new List<TilePos>();

        public DraftObject Clone()
        {
            var c = (DraftObject)MemberwiseClone();
            c.Patrol = new List<TilePos>(Patrol);
            return c;
        }
    }

    /// <summary>
    /// The editable vault behind the editor: tiles, entrance, hoard, objects and the clear-check.
    /// Every edit is a plain method so it can be unit-tested; ToVaultDef builds what the sim,
    /// VaultValidator and ClearCheck consume.
    /// </summary>
    public sealed class VaultDraft
    {
        public string DraftId = "";
        public string Name = "New vault";
        public int Level = 1;
        public readonly int Width = VaultRules.GridWidth;
        public readonly int Height = VaultRules.GridHeight;
        public TileType[] Tiles;
        public TilePos Entrance;
        public bool HasHoard;
        public TileRect Hoard;
        public List<DraftObject> Objects = new List<DraftObject>();
        public int NextId = 1;

        /// <summary>The run that proves this vault can be escaped (may be stale after edits).</summary>
        public ClearCheck ClearCheck;

        public string PublishedVaultId = "";
        public int PublishedVersion;

        public VaultDraft()
        {
            Tiles = new TileType[Width * Height];
            Entrance = new TilePos(Width / 2, Height - 1);
        }

        public static int DefaultCycle(ObjectType type)
        {
            switch (type)
            {
                case ObjectType.DripPipe: return 45;
                case ObjectType.SpikeFloor: return 60;
                case ObjectType.Bellows: return 75;
                default: return 0;
            }
        }

        public VaultDraft Clone()
        {
            var c = (VaultDraft)MemberwiseClone();
            c.Tiles = (TileType[])Tiles.Clone();
            c.Objects = new List<DraftObject>(Objects.Count);
            foreach (DraftObject o in Objects)
            {
                c.Objects.Add(o.Clone());
            }

            return c;
        }

        public bool InBounds(TilePos p)
        {
            return p.X >= 0 && p.Y >= 0 && p.X < Width && p.Y < Height;
        }

        public TileType TileAt(TilePos p)
        {
            return InBounds(p) ? Tiles[p.Y * Width + p.X] : TileType.Wall;
        }

        // ---- Edits (each returns whether anything changed) ------------------------------

        public bool PaintTile(TilePos p, TileType type)
        {
            if (!InBounds(p) || TileAt(p) == type)
            {
                return false;
            }

            Tiles[p.Y * Width + p.X] = type;
            return true;
        }

        public bool SetEntrance(TilePos p)
        {
            if (!InBounds(p) || p == Entrance)
            {
                return false;
            }

            Entrance = p;
            if (TileAt(p) == TileType.Wall)
            {
                PaintTile(p, TileType.Floor);
            }

            return true;
        }

        /// <summary>Adds a treasure pile on p, or removes the one already there.</summary>
        public bool ToggleTreasure(TilePos p)
        {
            if (!InBounds(p))
            {
                return false;
            }

            DraftObject existing = Objects.Find(o => o.Type == ObjectType.Treasure && o.Pos == p);
            if (existing != null)
            {
                Objects.Remove(existing);
                return true;
            }

            Place(ObjectType.Treasure, p);
            return true;
        }

        public bool SetHoard(TilePos a, TilePos b)
        {
            if (!InBounds(a) || !InBounds(b))
            {
                return false;
            }

            var rect = new TileRect(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Max(a.X, b.X), Math.Max(a.Y, b.Y));
            if (HasHoard && rect.MinX == Hoard.MinX && rect.MinY == Hoard.MinY && rect.MaxX == Hoard.MaxX && rect.MaxY == Hoard.MaxY)
            {
                return false;
            }

            Hoard = rect;
            HasHoard = true;
            return true;
        }

        public DraftObject Place(ObjectType type, TilePos p)
        {
            var o = new DraftObject { Id = NextId++, Type = type, Pos = p, Cycle = DefaultCycle(type) };
            FaceOpen(o);
            Objects.Add(o);
            return o;
        }

        public bool Move(int id, TilePos p)
        {
            DraftObject o = Find(id);
            if (o == null || !InBounds(p) || o.Pos == p)
            {
                return false;
            }

            o.Pos = p;
            FaceOpen(o);
            return true;
        }

        /// <summary>
        /// Turns an object a quarter clockwise (its facing). A bellows turns to its next side
        /// that opens onto floor, so it always blows into the room.
        /// </summary>
        public bool Rotate(int id)
        {
            DraftObject o = Find(id);
            if (o == null)
            {
                return false;
            }

            for (int turn = 1; turn <= 4; turn++)
            {
                int rot = (o.Rot + turn) % 4;
                if (o.Type != ObjectType.Bellows || OpenSide(o.Pos, rot))
                {
                    bool changed = rot != o.Rot;
                    o.Rot = rot;
                    return changed;
                }
            }

            return false;
        }

        /// <summary>A bellows keeps its facing if that side is open floor, else takes the first open side.</summary>
        private void FaceOpen(DraftObject o)
        {
            if (o.Type != ObjectType.Bellows || OpenSide(o.Pos, o.Rot))
            {
                return;
            }

            for (int rot = 0; rot < 4; rot++)
            {
                if (OpenSide(o.Pos, rot))
                {
                    o.Rot = rot;
                    return;
                }
            }
        }

        private bool OpenSide(TilePos p, int rot)
        {
            return TileAt(p.Step((Dir)rot)) != TileType.Wall; // off-grid reads as wall
        }

        public bool Delete(int id)
        {
            return Objects.RemoveAll(o => o.Id == id) > 0;
        }

        public bool AddWaypoint(int owlId, TilePos p)
        {
            DraftObject o = Find(owlId);
            if (o == null || o.Type != ObjectType.NightOwl || !InBounds(p))
            {
                return false;
            }

            if (o.Patrol.Count > 0 && o.Patrol[o.Patrol.Count - 1] == p)
            {
                o.Patrol.RemoveAt(o.Patrol.Count - 1); // tapping the last point again removes it
                return true;
            }

            o.Patrol.Add(p);
            return true;
        }

        public bool ClearPatrol(int owlId)
        {
            DraftObject o = Find(owlId);
            if (o == null || o.Patrol.Count == 0)
            {
                return false;
            }

            o.Patrol.Clear();
            return true;
        }

        public bool SetLevel(int level)
        {
            level = Math.Max(VaultRules.MinVaultLevel, Math.Min(VaultRules.MaxVaultLevel, level));
            if (level == Level)
            {
                return false;
            }

            Level = level;
            return true;
        }

        public DraftObject Find(int id)
        {
            return Objects.Find(o => o.Id == id);
        }

        /// <summary>The object a tap on p means: guardians first, then traps, crates, treasure, braziers.</summary>
        public DraftObject ObjectAt(TilePos p)
        {
            DraftObject best = null;
            foreach (DraftObject o in Objects)
            {
                if (o.Pos == p && (best == null || Priority(o.Type) < Priority(best.Type)))
                {
                    best = o;
                }
            }

            return best;
        }

        public int BuildPoints
        {
            get
            {
                int total = 0;
                foreach (DraftObject o in Objects)
                {
                    total += VaultRules.Cost(o.Type);
                }

                return total;
            }
        }

        public int Count(ObjectType type)
        {
            return Objects.FindAll(o => o.Type == type).Count;
        }

        // ---- Placement checks (the editor refuses a drop instead of placing it red) ---------

        /// <summary>Why this item can't come out of the shop at all, or null if it can.</summary>
        public string ShopBlock(ObjectType type)
        {
            int level = VaultRules.RequiredLevel(type);
            if (level > Level)
            {
                return "Unlocks at Vault Level " + level;
            }

            if (type == ObjectType.Brazier && Count(type) >= VaultRules.BrazierLimit(Level))
            {
                return "No more braziers at this Vault Level";
            }

            if (type == ObjectType.Treasure && Count(type) >= VaultRules.MaxTreasurePiles(Level))
            {
                return "No more treasure piles at this Vault Level";
            }

            if (BuildPoints + VaultRules.Cost(type) > VaultRules.Budget(Level))
            {
                return "Not enough build points";
            }

            return null;
        }

        /// <summary>Why placing this item on p would be refused, or null if it can go there.</summary>
        public string PlaceProblem(ObjectType type, TilePos p)
        {
            string block = ShopBlock(type);
            if (block != null)
            {
                return block;
            }

            VaultDraft trial = Clone();
            trial.Place(type, p);
            return NewProblem(this, trial);
        }

        /// <summary>Why moving an object to p would be refused, or null if it can go there.</summary>
        public string MoveProblem(int id, TilePos p)
        {
            VaultDraft trial = Clone();
            if (!trial.Move(id, p))
            {
                return InBounds(p) ? null : "Off the vault";
            }

            return NewProblem(this, trial);
        }

        /// <summary>The first problem the change from before to after adds, or null.</summary>
        public static string NewProblem(VaultDraft before, VaultDraft after)
        {
            List<VaultIssue> had = VaultValidator.Validate(before.ToVaultDef());
            foreach (VaultIssue issue in VaultValidator.Validate(after.ToVaultDef()))
            {
                if (!had.Exists(h => h.Code == issue.Code && h.ObjectId == issue.ObjectId))
                {
                    return issue.Message;
                }
            }

            return null;
        }

        // ---- Conversion -----------------------------------------------------------------

        public VaultDef ToVaultDef()
        {
            var objects = new ObjectDef[Objects.Count];
            for (int i = 0; i < Objects.Count; i++)
            {
                DraftObject o = Objects[i];
                int value = o.Type == ObjectType.Treasure ? VaultRules.TreasureValue(Level) : 0;
                objects[i] = new ObjectDef(o.Id, o.Type, o.Pos, o.Rot, o.Cycle, o.Offset, value, o.Patrol.ToArray());
            }

            Array.Sort(objects, (a, b) => a.Id.CompareTo(b.Id));
            return new VaultDef(1, PublishedVaultId, PublishedVersion, Level, Width, Height, (TileType[])Tiles.Clone(),
                Entrance, objects, HasHoard, Hoard);
        }

        public static VaultDraft FromVault(VaultDef vault, string name)
        {
            var d = new VaultDraft
            {
                Name = name,
                Level = vault.VaultLevel,
                Entrance = vault.Entrance,
                HasHoard = vault.HasHoard,
                Hoard = vault.Hoard,
            };
            for (int i = 0; i < d.Tiles.Length; i++)
            {
                d.Tiles[i] = vault.TileAtIndex(i);
            }

            for (int i = 0; i < vault.ObjectCount; i++)
            {
                ObjectDef o = vault.GetObject(i);
                d.Objects.Add(new DraftObject
                {
                    Id = o.Id, Type = o.Type, Pos = o.Pos, Rot = o.Rot, Cycle = o.Cycle, Offset = o.Offset,
                    Patrol = new List<TilePos>(o.Patrol),
                });
                d.NextId = Math.Max(d.NextId, o.Id + 1);
            }

            return d;
        }

        // ---- Storage --------------------------------------------------------------------

        /// <summary>{"draft": {id, name, published}, "vault": full vault document incl. clear-check}.</summary>
        public string ToJson()
        {
            var w = new JsonWriter();
            w.BeginObject();
            w.Name("draft").BeginObject()
                .Field("draftId", DraftId)
                .Field("name", Name)
                .Field("publishedVaultId", PublishedVaultId)
                .Field("publishedVersion", PublishedVersion)
                .EndObject();
            w.Name("vault").BeginObject();
            VaultJson.WriteFields(w, ToVaultDef());
            if (ClearCheck != null)
            {
                w.Name("clearCheck");
                ClearCheck.Write(w);
            }

            w.EndObject();
            w.EndObject();
            return w.ToString();
        }

        public static VaultDraft FromJson(string json)
        {
            JsonNode root = JsonReader.Parse(json);
            JsonNode meta = root.Get("draft");
            JsonNode vaultNode = root.Get("vault");
            VaultDraft d = FromVault(VaultJson.FromNode(vaultNode), meta.GetString("name", "Vault"));
            d.DraftId = meta.GetString("draftId", "");
            d.PublishedVaultId = meta.GetString("publishedVaultId", "");
            d.PublishedVersion = meta.GetInt("publishedVersion", 0);
            d.ClearCheck = VaultJson.ReadClearCheck(vaultNode);
            return d;
        }

        private static int Priority(ObjectType type)
        {
            if (ObjectTypes.IsGuardian(type)) return 0;
            if (ObjectTypes.IsTrap(type)) return 1;
            if (type == ObjectType.Crate) return 2;
            if (type == ObjectType.Treasure) return 3;
            return 4;
        }
    }
}
