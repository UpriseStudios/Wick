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
        UnderBudget,
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
        TooManySirWhiskers,
        GateNotInDoorway,
        GateWithoutLever,
        LeverLinks,
        DecorationUnknown,
        DecorationPlacement,
        TooManyDecorations,
        TrophyOutsideHoard,
        RouteTooShort,
        PitInDoorway,
        PitBlocksRoute,
        ObjectOnPit,
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

            if (VaultRules.IceTileCount(vault) > 0 && VaultRules.RequiredLevel(ObjectType.IceTiles) > level)
            {
                issues.Add(new VaultIssue(VaultIssueCode.LevelTooLow, 0, none,
                    "Ice Tiles unlock at Vault Level " + VaultRules.RequiredLevel(ObjectType.IceTiles)));
            }

            CheckObjects(vault, level, issues);
            CheckPits(vault, issues);
            CheckGates(vault, issues);
            int cats = 0;
            for (int i = 0; i < vault.ObjectCount; i++)
            {
                if (vault.GetObject(i).Type == ObjectType.SirWhiskers && ++cats == 2)
                {
                    issues.Add(Issue(VaultIssueCode.TooManySirWhiskers, vault.GetObject(i), "Only one Sir Whiskers per vault"));
                }
            }
            CheckDecorations(vault, level, issues);
            CheckTreasureAndHoard(vault, level, issues);
            CheckReachability(vault, issues);
            int route = RouteTiles(vault);
            int minRoute = VaultRules.MinRouteTiles(vault.Width, vault.Height);
            if (route >= 0 && route < minRoute)
            {
                issues.Add(new VaultIssue(VaultIssueCode.RouteTooShort, 0, vault.Entrance,
                    "The walk from the entrance to the hoard is " + route + " tiles; a " + vault.Width + " x " + vault.Height +
                    " vault needs at least " + minRoute + ". Add walls or pits to make it wind"));
            }

            int points = VaultRules.BuildPoints(vault);
            int budget = VaultRules.Budget(level);
            if (points > budget)
            {
                issues.Add(new VaultIssue(VaultIssueCode.OverBudget, 0, none,
                    "Uses " + points + " build points; Vault Level " + level + " allows " + budget));
            }
            else if (points < VaultRules.MinBuildPoints(level))
            {
                issues.Add(new VaultIssue(VaultIssueCode.UnderBudget, 0, none,
                    "Uses " + points + " build points; Vault Level " + level + " vaults need at least " + VaultRules.MinBuildPoints(level)));
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
                else if (vault.TileAt(o.Pos) == TileType.Pit)
                {
                    issues.Add(Issue(VaultIssueCode.ObjectOnPit, o, o.Type + " cannot stand on a pit"));
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
        /// Pits stay out of doorways: never on the entrance, and never beside it or a door tile (in
        /// front of or behind it; a door's other sides are wall). Plain gaps in walls aren't doorways.
        /// </summary>
        private static void CheckPits(VaultDef vault, List<VaultIssue> issues)
        {
            for (int y = 0; y < vault.Height; y++)
            {
                for (int x = 0; x < vault.Width; x++)
                {
                    var p = new TilePos(x, y);
                    if (vault.TileAt(p) == TileType.Pit && InOrBesideDoorway(vault, p))
                    {
                        issues.Add(new VaultIssue(VaultIssueCode.PitInDoorway, 0, p,
                            "Pits can't go in a doorway or on the entrance, or right in front of or behind one"));
                    }
                }
            }
        }

        /// <summary>Whether p is a doorway (a door tile or the entrance) or next to one: where pits may not go.</summary>
        public static bool InOrBesideDoorway(VaultDef vault, TilePos p)
        {
            if (IsDoorway(vault, p))
            {
                return true;
            }

            for (int d = 0; d < 4; d++)
            {
                if (IsDoorway(vault, p.Step((Dir)d)))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsDoorway(VaultDef vault, TilePos p)
        {
            return p == vault.Entrance || vault.TileAt(p) == TileType.Door;
        }

        /// <summary>
        /// Lever Doors: a gate stands in a doorway (a door tile) and needs at least one lever; a
        /// lever works one gate (two at level 5).
        /// </summary>
        private static void CheckGates(VaultDef vault, List<VaultIssue> issues)
        {
            int maxLinks = DefenceStats.LeverLinks(vault.LevelOf(ObjectType.Gate));
            for (int i = 0; i < vault.ObjectCount; i++)
            {
                ObjectDef o = vault.GetObject(i);
                if (o.Type == ObjectType.Gate)
                {
                    if (vault.TileAt(o.Pos) != TileType.Door)
                    {
                        issues.Add(Issue(VaultIssueCode.GateNotInDoorway, o, "Gates go in doorways (paint a door first)"));
                    }

                    if (!AnyLeverOpens(vault, o.Id))
                    {
                        issues.Add(Issue(VaultIssueCode.GateWithoutLever, o, "Link a lever to this gate"));
                    }
                }
                else if (o.Type == ObjectType.Lever && (o.Links.Length == 0 || o.Links.Length > maxLinks))
                {
                    issues.Add(Issue(VaultIssueCode.LeverLinks, o, o.Links.Length == 0
                        ? "Link this lever to a gate"
                        : "A lever works " + maxLinks + " gate" + (maxLinks == 1 ? "" : "s") + " at this level"));
                }
            }
        }

        private static bool AnyLeverOpens(VaultDef vault, int gateId)
        {
            for (int i = 0; i < vault.ObjectCount; i++)
            {
                ObjectDef o = vault.GetObject(i);
                if (o.Type == ObjectType.Lever && System.Array.IndexOf(o.Links, gateId) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Tiles Wick can reach from the entrance without crossing a pit, opening gates as he goes:
        /// closed gates block, until a lever that opens them is itself reachable. Row-major.
        /// </summary>
        public static bool[] ReachableWithGates(VaultDef vault)
        {
            return ReachableWithGates(vault, true);
        }

        private static bool[] ReachableWithGates(VaultDef vault, bool pitsBlock)
        {
            var opened = new bool[vault.ObjectCount];
            while (true)
            {
                bool[] seen = Reachable(vault, vault.Entrance, opened, pitsBlock);
                bool changed = false;
                for (int i = 0; i < vault.ObjectCount; i++)
                {
                    ObjectDef o = vault.GetObject(i);
                    if (o.Type != ObjectType.Lever || !IsSet(vault, seen, o.Pos))
                    {
                        continue;
                    }

                    foreach (int id in o.Links)
                    {
                        int g = vault.IndexOfObject(id);
                        if (g >= 0 && !opened[g])
                        {
                            opened[g] = true;
                            changed = true;
                        }
                    }
                }

                if (!changed)
                {
                    return seen;
                }
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

            // Every pile needs a way there (and so back) with no pit on it. One reachable only across a
            // pit is the pit's fault (PitBlocksRoute, naming the first pit in the way), not the layout's.
            bool[] fromEntrance = ReachableWithGates(vault);
            bool[] acrossPits = null;
            for (int i = 0; i < vault.ObjectCount; i++)
            {
                ObjectDef o = vault.GetObject(i);
                if (o.Type == ObjectType.Treasure && !IsSet(vault, fromEntrance, o.Pos))
                {
                    acrossPits = acrossPits ?? ReachableWithGates(vault, false);
                    issues.Add(IsSet(vault, acrossPits, o.Pos)
                        ? new VaultIssue(VaultIssueCode.PitBlocksRoute, o.Id, FirstPitOnWay(vault, o.Pos),
                            "A pit cuts this treasure off: leave a way to it with no pits")
                        : Issue(VaultIssueCode.TreasureUnreachable, o, "Wick cannot reach this treasure"));
                }

                if (o.Type == ObjectType.NightOwl && o.Patrol.Length > 0)
                {
                    // Owls walk round pits, so a patrol may not cross one.
                    bool[] fromOwl = vault.IsWalkable(o.Pos) ? Reachable(vault, o.Pos) : null;
                    foreach (TilePos p in o.Patrol)
                    {
                        if (!vault.IsWalkable(p) || vault.TileAt(p) == TileType.Pit)
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

            if (vault.HasHoard && !Touches(vault, fromEntrance, vault.Hoard))
            {
                acrossPits = acrossPits ?? ReachableWithGates(vault, false);
                var corner = new TilePos(vault.Hoard.MinX, vault.Hoard.MinY);
                issues.Add(Touches(vault, acrossPits, vault.Hoard)
                    ? new VaultIssue(VaultIssueCode.PitBlocksRoute, 0, FirstPitOnWay(vault, corner, true),
                        "A pit cuts off the hoard room: leave a way to it with no pits")
                    : new VaultIssue(VaultIssueCode.HoardUnreachable, 0, corner, "Wick cannot reach the hoard room"));
            }
        }

        private static bool Touches(VaultDef vault, bool[] map, TileRect r)
        {
            for (int y = r.MinY; y <= r.MaxY; y++)
            {
                for (int x = r.MinX; x <= r.MaxX; x++)
                {
                    if (IsSet(vault, map, new TilePos(x, y)))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// The first pit on the shortest walk from the entrance to target (or into the hoard room),
        /// crossing pits and gates: the pit that cuts it off. Target if there is none on the walk.
        /// </summary>
        private static TilePos FirstPitOnWay(VaultDef vault, TilePos target, bool intoHoard = false)
        {
            int[] prev = Search(vault, vault.IsWalkable, p => intoHoard ? vault.Hoard.Contains(p) : p == target, out int end);
            TilePos pit = target;
            for (int at = end; prev != null; at = prev[at])
            {
                // Walking back to the entrance: the last pit seen is the first on the way.
                if (vault.TileAtIndex(at) == TileType.Pit)
                {
                    pit = new TilePos(at % vault.Width, at / vault.Width);
                }

                if (prev[at] == at)
                {
                    break;
                }
            }

            return pit;
        }

        /// <summary>
        /// The shortest walk, in tiles, from the entrance to the nearest hoard tile, or -1 with no hoard
        /// or none reachable. Only walls and pits count (pits make fine maze walls), so the winding has to
        /// come from the layout: crates (cheap in build points) and gates (which open) don't make it longer.
        /// </summary>
        public static int RouteTiles(VaultDef vault)
        {
            List<TilePos> path = RoutePath(vault);
            return path == null ? -1 : path.Count - 1;
        }

        /// <summary>The walk RouteTiles measures, entrance first, or null. The editor draws it while pits are painted.</summary>
        public static List<TilePos> RoutePath(VaultDef vault)
        {
            if (!vault.HasHoard)
            {
                return null;
            }

            int[] prev = Search(vault, p => vault.TileAt(p) != TileType.Wall && vault.TileAt(p) != TileType.Pit,
                vault.Hoard.Contains, out int end);
            if (prev == null)
            {
                return null;
            }

            var path = new List<TilePos>();
            for (int at = end; ; at = prev[at])
            {
                path.Add(new TilePos(at % vault.Width, at / vault.Width));
                if (prev[at] == at)
                {
                    path.Reverse();
                    return path;
                }
            }
        }

        /// <summary>
        /// Breadth-first from the entrance over passable tiles until a goal tile: prev[i] is the tile
        /// (row-major) one step nearer the entrance, and the entrance's own. Null if no goal is reached.
        /// </summary>
        private static int[] Search(VaultDef vault, System.Func<TilePos, bool> passable, System.Func<TilePos, bool> goal, out int end)
        {
            end = -1;
            if (!vault.InBounds(vault.Entrance))
            {
                return null;
            }

            var prev = new int[vault.Width * vault.Height];
            for (int i = 0; i < prev.Length; i++)
            {
                prev[i] = -1;
            }

            int start = vault.Entrance.Y * vault.Width + vault.Entrance.X;
            prev[start] = start;
            var queue = new Queue<TilePos>();
            queue.Enqueue(vault.Entrance);
            while (queue.Count > 0)
            {
                TilePos p = queue.Dequeue();
                if (goal(p))
                {
                    end = p.Y * vault.Width + p.X;
                    return prev;
                }

                for (int d = 0; d < 4; d++)
                {
                    TilePos n = p.Step((Dir)d);
                    if (vault.InBounds(n) && passable(n) && prev[n.Y * vault.Width + n.X] < 0)
                    {
                        prev[n.Y * vault.Width + n.X] = p.Y * vault.Width + p.X;
                        queue.Enqueue(n);
                    }
                }
            }

            return null;
        }

        /// <summary>Tiles reachable on foot from start without crossing a pit (walls, crates and pits block). Row-major.</summary>
        public static bool[] Reachable(VaultDef vault, TilePos start)
        {
            return Reachable(vault, start, null, true);
        }

        /// <summary>As Reachable, with closed gates blocking unless `opened` marks them (by object index), and pits unless told not to.</summary>
        private static bool[] Reachable(VaultDef vault, TilePos start, bool[] opened, bool pitsBlock)
        {
            bool[] closedGate = null;
            if (opened != null)
            {
                closedGate = new bool[vault.Width * vault.Height];
                for (int i = 0; i < vault.ObjectCount; i++)
                {
                    ObjectDef o = vault.GetObject(i);
                    if (o.Type == ObjectType.Gate && !opened[i] && vault.InBounds(o.Pos))
                    {
                        closedGate[o.Pos.Y * vault.Width + o.Pos.X] = true;
                    }
                }
            }

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
                    if (vault.IsWalkable(n) && !(pitsBlock && vault.TileAt(n) == TileType.Pit) && !seen[n.Y * vault.Width + n.X] &&
                        (closedGate == null || !closedGate[n.Y * vault.Width + n.X]))
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
