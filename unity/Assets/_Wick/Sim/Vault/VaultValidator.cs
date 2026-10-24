using System.Collections.Generic;

namespace Wick.Sim
{
    public enum VaultIssueCode : byte
    {
        WrongGridSize,
        VaultLevelOutOfRange,
        EntranceBlocked,
        ObjectOnWall,
        BrazierNotOnWall,
        BrazierNotNextToFloor,
        ObjectUnderCrate,
        ObjectOnEntrance,
        TileOccupied,
        LevelTooLow,
        TooManyBraziers,
        NoTreasure,
        TooManyTreasurePiles,
        WrongTreasureValue,
        OverBudget,
        NoHoard,
        HoardWithoutTreasure,
        TreasureUnreachable,
        HoardUnreachable,
        PatrolBlocked,
        PatrolUnreachable,
        BellowsNotOnWall,
        BellowsFacesWall,
        CrateInDoorway,
        DefenceLevelTooHigh,
        DecorationUnknown,
        DecorationPlacement,
        TooManyDecorations,
        TrophyOutsideHoard,
    }

    /// <summary>One problem with a vault. ObjectId is 0 when the issue is not about an object.</summary>
    public readonly struct VaultIssue
    {
        public readonly VaultIssueCode Code;
        public readonly int ObjectId;
        public readonly TilePos Tile;
        public readonly string Message;

        public VaultIssue(VaultIssueCode code, int objectId, TilePos tile, string message)
        {
            Code = code;
            ObjectId = objectId;
            Tile = tile;
            Message = message;
        }

        public override string ToString()
        {
            return Code + ": " + Message;
        }
    }

    /// <summary>
    /// Design rules for a player-built vault, checked by the editor (to show every problem at
    /// once) and by the server before a vault goes live. VaultJson only checks what the sim
    /// needs to run safely; anything a player builds must also pass this.
    /// </summary>
    public static class VaultValidator
    {
        public static List<VaultIssue> Validate(VaultDef vault)
        {
            var issues = new List<VaultIssue>();
            var none = new TilePos(-1, -1);

            int level = vault.VaultLevel;
            if (level < VaultRules.MinVaultLevel || level > VaultRules.MaxVaultLevel)
            {
                issues.Add(new VaultIssue(VaultIssueCode.VaultLevelOutOfRange, 0, none,
                    "Vault Level must be " + VaultRules.MinVaultLevel + " to " + VaultRules.MaxVaultLevel));
                level = level < VaultRules.MinVaultLevel ? VaultRules.MinVaultLevel : VaultRules.MaxVaultLevel;
            }

            if (!VaultRules.IsAllowedGrid(vault.Width, vault.Height, level))
            {
                issues.Add(new VaultIssue(VaultIssueCode.WrongGridSize, 0, none,
                    "Vault Level " + level + " vaults are up to " + VaultRules.GridWidth(level) + " x " +
                    VaultRules.GridHeight(level) + " tiles, in the sizes the Vault Level table lists"));
            }

            if (!vault.IsWalkable(vault.Entrance))
            {
                issues.Add(new VaultIssue(VaultIssueCode.EntranceBlocked, 0, vault.Entrance, "The entrance must be open floor"));
            }

            int cap = DefenceLevels.Cap(level);
            for (int i = 0; i < ObjectTypes.Count; i++)
            {
                var type = (ObjectType)i;
                if (vault.LevelOf(type) > cap)
                {
                    issues.Add(new VaultIssue(VaultIssueCode.DefenceLevelTooHigh, 0, none,
                        type + " is at level " + vault.LevelOf(type) + "; Vault Level " + level + " allows up to " + cap));
                }
            }

            CheckObjects(vault, level, issues);
            CheckDecorations(vault, level, issues);
            CheckTreasureAndHoard(vault, level, issues);
            CheckReachability(vault, issues);

            int points = VaultRules.BuildPoints(vault);
            int budget = VaultRules.Budget(level);
            if (points > budget)
            {
                issues.Add(new VaultIssue(VaultIssueCode.OverBudget, 0, none,
                    "Uses " + points + " build points; Vault Level " + level + " allows " + budget));
            }

            return issues;
        }

        private static void CheckObjects(VaultDef vault, int level, List<VaultIssue> issues)
        {
            int braziers = 0;
            for (int i = 0; i < vault.ObjectCount; i++)
            {
                ObjectDef o = vault.GetObject(i);
                bool onWall = vault.TileAt(o.Pos) == TileType.Wall;

                if (VaultRules.RequiredLevel(o.Type) > level)
                {
                    issues.Add(Issue(VaultIssueCode.LevelTooLow, o,
                        o.Type + " unlocks at Vault Level " + VaultRules.RequiredLevel(o.Type)));
                }

                if (o.Type == ObjectType.Brazier)
                {
                    braziers++;
                    if (!onWall)
                    {
                        issues.Add(Issue(VaultIssueCode.BrazierNotOnWall, o, "Braziers hang on walls"));
                    }
                    else if (!HasWalkableNeighbour(vault, o.Pos))
                    {
                        issues.Add(Issue(VaultIssueCode.BrazierNotNextToFloor, o, "A brazier needs open floor beside it"));
                    }

                    CheckWallShared(vault, i, issues);
                    continue;
                }

                // A bellows is built into a wall and blows out into the room, so it can't be walked over.
                if (o.Type == ObjectType.Bellows)
                {
                    if (!onWall)
                    {
                        issues.Add(Issue(VaultIssueCode.BellowsNotOnWall, o, "Bellows are built into walls"));
                    }
                    else if (vault.TileAt(o.Pos.Step(o.Facing)) == TileType.Wall) // off-grid reads as wall
                    {
                        issues.Add(Issue(VaultIssueCode.BellowsFacesWall, o, "A bellows must blow into open floor"));
                    }

                    CheckWallShared(vault, i, issues);
                    continue;
                }

                if (onWall)
                {
                    issues.Add(Issue(VaultIssueCode.ObjectOnWall, o, o.Type + " cannot be on a wall"));
                }

                if ((o.Type == ObjectType.Crate || o.Type == ObjectType.SnufferBell) && o.Pos == vault.Entrance)
                {
                    issues.Add(Issue(VaultIssueCode.ObjectOnEntrance, o, o.Type + " cannot be on the entrance"));
                }

                if (o.Type == ObjectType.Crate && vault.TileAt(o.Pos) == TileType.Door)
                {
                    issues.Add(Issue(VaultIssueCode.CrateInDoorway, o, "Crates cannot block a doorway"));
                }

                // Earlier objects (by id) keep the tile; later ones are reported.
                for (int j = 0; j < i; j++)
                {
                    ObjectDef other = vault.GetObject(j);
                    if (other.Pos != o.Pos || IsWallMounted(other.Type))
                    {
                        continue;
                    }

                    if (o.Type == ObjectType.Crate || other.Type == ObjectType.Crate)
                    {
                        issues.Add(Issue(VaultIssueCode.ObjectUnderCrate, o, "Nothing can share a tile with a crate"));
                        break;
                    }

                    if (Category(o.Type) == Category(other.Type))
                    {
                        issues.Add(Issue(VaultIssueCode.TileOccupied, o, "Another " + Category(o.Type) + " is already here"));
                        break;
                    }
                }
            }

            if (braziers > VaultRules.BrazierLimit(level))
            {
                issues.Add(new VaultIssue(VaultIssueCode.TooManyBraziers, 0, new TilePos(-1, -1),
                    "Vault Level " + level + " allows " + VaultRules.BrazierLimit(level) + " brazier(s)"));
            }
        }

        /// <summary>
        /// Decorations are cosmetic, so their rules are only about looks: wall props hang on a wall
        /// beside floor, floor props stand on open floor clear of the entrance and every object, one
        /// per tile, and trophies stand in the hoard room for raiders who get that far.
        /// </summary>
        private static void CheckDecorations(VaultDef vault, int level, List<VaultIssue> issues)
        {
            if (vault.DecorationCount > VaultRules.MaxDecorations(level))
            {
                issues.Add(new VaultIssue(VaultIssueCode.TooManyDecorations, 0, new TilePos(-1, -1),
                    "Vault Level " + level + " allows " + VaultRules.MaxDecorations(level) + " decorations"));
            }

            for (int i = 0; i < vault.DecorationCount; i++)
            {
                DecorationDef d = vault.GetDecoration(i);
                if (!Decorations.IsKnown(d.Kind))
                {
                    issues.Add(new VaultIssue(VaultIssueCode.DecorationUnknown, d.Id, d.Pos, "Unknown decoration " + d.Kind));
                    continue;
                }

                string problem = DecorationProblem(vault, i);
                if (problem != null)
                {
                    issues.Add(new VaultIssue(VaultIssueCode.DecorationPlacement, d.Id, d.Pos, problem));
                }

                if (Decorations.IsTrophy(d.Kind) && !(vault.HasHoard && vault.Hoard.Contains(d.Pos)))
                {
                    issues.Add(new VaultIssue(VaultIssueCode.TrophyOutsideHoard, d.Id, d.Pos, "Trophies go in the hoard room"));
                }
            }
        }

        private static string DecorationProblem(VaultDef vault, int index)
        {
            DecorationDef d = vault.GetDecoration(index);
            for (int j = 0; j < index; j++)
            {
                if (vault.GetDecoration(j).Pos == d.Pos)
                {
                    return "Another decoration is already here";
                }
            }

            if (Decorations.IsWallMounted(d.Kind))
            {
                if (vault.TileAt(d.Pos) != TileType.Wall || !HasWalkableNeighbour(vault, d.Pos))
                {
                    return "Hang it on a wall beside open floor";
                }

                for (int i = 0; i < vault.ObjectCount; i++)
                {
                    if (vault.GetObject(i).Pos == d.Pos && IsWallMounted(vault.GetObject(i).Type))
                    {
                        return "A wall fitting is already here";
                    }
                }

                return null;
            }

            if (!vault.IsWalkable(d.Pos) || vault.TileAt(d.Pos) != TileType.Floor)
            {
                return "Stand it on open floor";
            }

            if (d.Pos == vault.Entrance)
            {
                return "Keep the entrance clear";
            }

            for (int i = 0; i < vault.ObjectCount; i++)
            {
                if (vault.GetObject(i).Pos == d.Pos)
                {
                    return "Keep it clear of traps, guardians and treasure";
                }
            }

            return null;
        }

        private static void CheckTreasureAndHoard(VaultDef vault, int level, List<VaultIssue> issues)
        {
            var none = new TilePos(-1, -1);
            int piles = 0;
            bool pileInHoard = false;
            for (int i = 0; i < vault.ObjectCount; i++)
            {
                ObjectDef o = vault.GetObject(i);
                if (o.Type != ObjectType.Treasure)
                {
                    continue;
                }

                piles++;
                pileInHoard |= vault.HasHoard && vault.Hoard.Contains(o.Pos);
                if (o.Value != VaultRules.TreasureValue(level))
                {
                    issues.Add(Issue(VaultIssueCode.WrongTreasureValue, o,
                        "Treasure piles are worth " + VaultRules.TreasureValue(level) + " at Vault Level " + level));
                }
            }

            if (piles == 0)
            {
                issues.Add(new VaultIssue(VaultIssueCode.NoTreasure, 0, none, "Place at least one treasure pile"));
            }
            else if (piles > VaultRules.MaxTreasurePiles(level))
            {
                issues.Add(new VaultIssue(VaultIssueCode.TooManyTreasurePiles, 0, none,
                    "Vault Level " + level + " allows " + VaultRules.MaxTreasurePiles(level) + " treasure piles"));
            }

            if (!vault.HasHoard)
            {
                issues.Add(new VaultIssue(VaultIssueCode.NoHoard, 0, none, "Mark the hoard room"));
            }
            else if (piles > 0 && !pileInHoard)
            {
                issues.Add(new VaultIssue(VaultIssueCode.HoardWithoutTreasure, 0, none,
                    "The hoard room needs at least one treasure pile"));
            }
        }

        private static void CheckReachability(VaultDef vault, List<VaultIssue> issues)
        {
            if (!vault.IsWalkable(vault.Entrance))
            {
                return; // already reported; nothing is reachable
            }

            bool[] fromEntrance = Reachable(vault, vault.Entrance);
            for (int i = 0; i < vault.ObjectCount; i++)
            {
                ObjectDef o = vault.GetObject(i);
                if (o.Type == ObjectType.Treasure && !IsSet(vault, fromEntrance, o.Pos))
                {
                    issues.Add(Issue(VaultIssueCode.TreasureUnreachable, o, "Wick cannot reach this treasure"));
                }

                if (o.Type == ObjectType.NightOwl && o.Patrol.Length > 0)
                {
                    bool[] fromOwl = vault.IsWalkable(o.Pos) ? Reachable(vault, o.Pos) : null;
                    foreach (TilePos p in o.Patrol)
                    {
                        if (!vault.IsWalkable(p))
                        {
                            issues.Add(new VaultIssue(VaultIssueCode.PatrolBlocked, o.Id, p, "Patrol point " + p + " is blocked"));
                        }
                        else if (fromOwl == null || !IsSet(vault, fromOwl, p))
                        {
                            issues.Add(new VaultIssue(VaultIssueCode.PatrolUnreachable, o.Id, p,
                                "The owl cannot walk to patrol point " + p));
                        }
                    }
                }
            }

            if (vault.HasHoard)
            {
                bool reachable = false;
                for (int y = vault.Hoard.MinY; y <= vault.Hoard.MaxY && !reachable; y++)
                {
                    for (int x = vault.Hoard.MinX; x <= vault.Hoard.MaxX && !reachable; x++)
                    {
                        reachable = IsSet(vault, fromEntrance, new TilePos(x, y));
                    }
                }

                if (!reachable)
                {
                    issues.Add(new VaultIssue(VaultIssueCode.HoardUnreachable, 0,
                        new TilePos(vault.Hoard.MinX, vault.Hoard.MinY), "Wick cannot reach the hoard room"));
                }
            }
        }

        /// <summary>Tiles reachable on foot from start (walls and crates block). Row-major.</summary>
        public static bool[] Reachable(VaultDef vault, TilePos start)
        {
            var seen = new bool[vault.Width * vault.Height];
            var queue = new Queue<TilePos>();
            seen[start.Y * vault.Width + start.X] = true;
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                TilePos p = queue.Dequeue();
                for (int d = 0; d < 4; d++)
                {
                    TilePos n = p.Step((Dir)d);
                    if (vault.IsWalkable(n) && !seen[n.Y * vault.Width + n.X])
                    {
                        seen[n.Y * vault.Width + n.X] = true;
                        queue.Enqueue(n);
                    }
                }
            }

            return seen;
        }

        private static bool IsSet(VaultDef vault, bool[] map, TilePos p)
        {
            return vault.InBounds(p) && map[p.Y * vault.Width + p.X];
        }

        private static bool IsWallMounted(ObjectType type)
        {
            return type == ObjectType.Brazier || type == ObjectType.Bellows;
        }

        /// <summary>One wall fitting per wall tile: earlier objects (by id) keep it.</summary>
        private static void CheckWallShared(VaultDef vault, int index, List<VaultIssue> issues)
        {
            ObjectDef o = vault.GetObject(index);
            for (int j = 0; j < index; j++)
            {
                ObjectDef other = vault.GetObject(j);
                if (other.Pos == o.Pos && IsWallMounted(other.Type))
                {
                    issues.Add(Issue(VaultIssueCode.TileOccupied, o, "Another wall fitting is already here"));
                    return;
                }
            }
        }

        private static bool HasWalkableNeighbour(VaultDef vault, TilePos p)
        {
            for (int d = 0; d < 4; d++)
            {
                if (vault.IsWalkable(p.Step((Dir)d)))
                {
                    return true;
                }
            }

            return false;
        }

        private static string Category(ObjectType type)
        {
            if (ObjectTypes.IsTrap(type)) return "trap";
            if (ObjectTypes.IsGuardian(type)) return "guardian";
            if (type == ObjectType.Treasure) return "treasure pile";
            return type.ToString();
        }

        private static VaultIssue Issue(VaultIssueCode code, ObjectDef o, string message)
        {
            return new VaultIssue(code, o.Id, o.Pos, message);
        }
    }
}
