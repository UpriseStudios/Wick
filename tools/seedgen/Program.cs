using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Wick.Sim;

namespace Wick.SeedGen
{
    /// <summary>
    /// Generates the 20 seed vaults matchmaking falls back on (Assets/_Wick/Data/SeedVaults).
    /// Each is a room template filled with traps and guardians within its Vault Level's budget,
    /// accepted only if the validator passes and a solver finds an escape with at least 1 star,
    /// which becomes its clear-check. Deterministic: the same run gives the same files.
    ///
    /// Usage (from the repo root): dotnet run --project tools/seedgen
    /// Then in Unity: Wick > Upload Seed Vaults.
    /// </summary>
    public static class Program
    {
        private const int Count = 20;
        private const string TemplateDir = "unity/Assets/_Wick/Resources/VaultTemplates";
        private const string OutDir = "unity/Assets/_Wick/Data/SeedVaults";

        private static readonly string[] Adjectives =
        {
            "ashen", "sunken", "hollow", "gilded", "silent", "crooked", "smoky", "frozen", "rusted", "velvet",
            "wailing", "bitter", "moonlit", "dusty", "sleeping", "copper", "drowned", "thorny", "candle", "iron",
        };

        private static readonly string[] Nouns =
        {
            "crypt", "cellar", "chapel", "vault", "gallery", "cistern", "archive", "tomb", "hall", "keep",
            "abbey", "mint", "ossuary", "larder", "bastion", "reliquary", "catacomb", "treasury", "undercroft", "den",
        };

        public static int Main(string[] args)
        {
            VaultDef[] templates = Directory.GetFiles(TemplateDir, "*.json").OrderBy(f => f, StringComparer.Ordinal)
                .Select(f => VaultJson.Parse(File.ReadAllText(f))).ToArray();
            Directory.CreateDirectory(OutDir);
            var rng = new Random(2026);
            var manifest = new JsonWriter();
            manifest.BeginArray();

            for (int i = 0; i < Count; i++)
            {
                int level = i < 7 ? 1 : i < 14 ? 2 : 3;
                string name = (i + 1).ToString("00") + "-" + Adjectives[i] + "-" + Nouns[(i * 7) % Nouns.Length];
                int seed = 1000 + i;
                (VaultDef vault, InputLog log, RaidResult run, int attempts) = Build(templates[i % templates.Length], level, seed, rng, name);
                ClearCheck cc = ClearCheck.FromRun(vault, seed, log, run);
                File.WriteAllText(Path.Combine(OutDir, name + ".json"), VaultJson.WriteDocument(vault, cc));

                int crowns = i * 40;
                manifest.BeginObject().Field("name", name).Field("crowns", crowns).Field("level", level)
                    .Field("objects", vault.ObjectCount).Field("buildPoints", VaultRules.BuildPoints(vault))
                    .Field("clearStars", run.Stars).Field("clearTicks", run.Ticks).EndObject();
                Console.WriteLine(name + ": level " + level + ", " + vault.ObjectCount + " objects, " +
                    VaultRules.BuildPoints(vault) + "/" + VaultRules.Budget(level) + " pts, cleared " + run.Stars + "* in " +
                    run.Ticks + " ticks (attempt " + attempts + ")");
            }

            manifest.EndArray();
            File.WriteAllText(Path.Combine(OutDir, "seeds.json"), manifest.ToString());
            return 0;
        }

        // ---- Generation ---------------------------------------------------------------------

        private static (VaultDef, InputLog, RaidResult, int) Build(VaultDef template, int level, int seed, Random rng, string name)
        {
            for (int attempt = 1; attempt <= 500; attempt++)
            {
                // Round-trip through JSON so the clear-check is made on exactly what gets uploaded.
                VaultDef vault = VaultJson.Parse(VaultJson.Write(Fill(template, level, rng, "v_seed_" + name)));
                if (VaultValidator.Validate(vault).Count > 0)
                {
                    continue;
                }

                InputLog log = Solver.Escape(vault, seed);
                if (log == null)
                {
                    continue;
                }

                RaidResult run = RaidRunner.RunToEnd(vault, seed, log);
                if (run.Outcome == Outcome.Escaped && run.Stars >= 1 && ClearCheck.Verify(vault, ClearCheck.FromRun(vault, seed, log, run)).Ok)
                {
                    return (vault, log, run, attempt);
                }
            }

            throw new InvalidOperationException("No solvable vault for " + name);
        }

        private static VaultDef Fill(VaultDef t, int level, Random rng, string vaultId)
        {
            var tiles = new TileType[t.Width * t.Height];
            for (int i = 0; i < tiles.Length; i++)
            {
                tiles[i] = t.TileAtIndex(i);
            }

            var objects = new List<ObjectDef>();
            int id = 1;
            var used = new HashSet<TilePos>();

            // Treasure: the template's pile plus extra piles in the hoard.
            List<TilePos> hoard = Floor(t).Where(p => t.Hoard.Contains(p)).ToList();
            int piles = 1 + rng.Next(VaultRules.MaxTreasurePiles(level));
            foreach (TilePos p in Shuffle(hoard, rng).Take(piles))
            {
                objects.Add(Obj(id++, ObjectType.Treasure, p, 0, 0, VaultRules.TreasureValue(level)));
                used.Add(p);
            }

            if (rng.Next(2) == 0)
            {
                TilePos? wall = WallBesideFloor(t, rng);
                if (wall.HasValue)
                {
                    objects.Add(Obj(id++, ObjectType.Brazier, wall.Value, 0, 0, 0));
                }
            }

            int budget = VaultRules.Budget(level);
            int spent = 0;
            List<TilePos> floor = Floor(t).Where(p => p != t.Entrance && !used.Contains(p)).ToList();
            var kinds = new List<ObjectType> { ObjectType.DripPipe, ObjectType.SpikeFloor, ObjectType.Bellows, ObjectType.Crate };
            if (level >= 2)
            {
                kinds.Add(ObjectType.SnufferBell);
                kinds.Add(ObjectType.NightOwl);
            }

            if (level >= 3)
            {
                kinds.Add(ObjectType.WaxMoths);
            }

            int guardians = 0;
            for (int tries = 0; tries < 40 && floor.Count > 0; tries++)
            {
                ObjectType type = kinds[rng.Next(kinds.Count)];
                int cost = VaultRules.Cost(type);
                if (spent + cost > budget || (ObjectTypes.IsGuardian(type) && guardians >= 1))
                {
                    continue;
                }

                if (type == ObjectType.Bellows)
                {
                    TilePos? wall = WallBesideFloor(t, rng);
                    if (!wall.HasValue || objects.Any(o => o.Pos == wall.Value))
                    {
                        continue;
                    }

                    int gust = 60 + rng.Next(4) * 15;
                    objects.Add(new ObjectDef(id++, type, wall.Value, OpenSide(t, wall.Value), gust, rng.Next(gust), 0, new TilePos[0]));
                    spent += cost;
                    continue;
                }

                TilePos at = floor[rng.Next(floor.Count)];
                floor.Remove(at);
                int cycle = type == ObjectType.DripPipe ? 45 + rng.Next(3) * 15 : type == ObjectType.SpikeFloor ? 60 + rng.Next(3) * 15 : 0;
                TilePos[] patrol = type == ObjectType.NightOwl ? Patrol(t, at) : new TilePos[0];
                objects.Add(new ObjectDef(id++, type, at, rng.Next(4), cycle, cycle > 0 ? rng.Next(cycle) : 0, 0, patrol));
                spent += cost;
                guardians += ObjectTypes.IsGuardian(type) ? 1 : 0;
            }

            return new VaultDef(1, vaultId, 1, level, t.Width, t.Height, tiles, t.Entrance, objects.ToArray(), t.HasHoard, t.Hoard, t.Theme);
        }

        private static ObjectDef Obj(int id, ObjectType type, TilePos p, int rot, int cycle, int value)
        {
            return new ObjectDef(id, type, p, rot, cycle, 0, value, new TilePos[0]);
        }

        private static IEnumerable<TilePos> Floor(VaultDef v)
        {
            for (int y = 0; y < v.Height; y++)
            {
                for (int x = 0; x < v.Width; x++)
                {
                    var p = new TilePos(x, y);
                    if (v.TileAt(p) == TileType.Floor)
                    {
                        yield return p;
                    }
                }
            }
        }

        private static TilePos? WallBesideFloor(VaultDef v, Random rng)
        {
            var walls = new List<TilePos>();
            for (int y = 0; y < v.Height; y++)
            {
                for (int x = 0; x < v.Width; x++)
                {
                    var p = new TilePos(x, y);
                    if (v.TileAt(p) == TileType.Wall && OpenSide(v, p) >= 0)
                    {
                        walls.Add(p);
                    }
                }
            }

            return walls.Count == 0 ? (TilePos?)null : walls[rng.Next(walls.Count)];
        }

        private static int OpenSide(VaultDef v, TilePos p)
        {
            for (int d = 0; d < 4; d++)
            {
                if (v.TileAt(p.Step((Dir)d)) == TileType.Floor)
                {
                    return d;
                }
            }

            return -1;
        }

        /// <summary>A short back-and-forth along the row, on open floor.</summary>
        private static TilePos[] Patrol(VaultDef v, TilePos from)
        {
            for (int dx = 3; dx >= 1; dx--)
            {
                foreach (int sign in new[] { 1, -1 })
                {
                    var to = new TilePos(from.X + sign * dx, from.Y);
                    if (v.TileAt(to) == TileType.Floor)
                    {
                        return new[] { from, to };
                    }
                }
            }

            return new TilePos[0];
        }

        private static List<T> Shuffle<T>(List<T> items, Random rng)
        {
            var list = new List<T>(items);
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }

            return list;
        }
    }

    /// <summary>
    /// Finds an escape: the shortest path to the nearest hoard pile and back, walked one dash at a
    /// time, waiting before each step for the first moment it can be taken without losing a flicker.
    /// </summary>
    public static class Solver
    {
        private const int MaxWait = 150;

        public static InputLog Escape(VaultDef vault, int seed)
        {
            List<TilePos> treasure = new List<TilePos>();
            for (int i = 0; i < vault.ObjectCount; i++)
            {
                ObjectDef o = vault.GetObject(i);
                if (o.Type == ObjectType.Treasure && vault.Hoard.Contains(o.Pos))
                {
                    treasure.Add(o.Pos);
                }
            }

            List<TilePos> there = treasure.Select(p => Path(vault, vault.Entrance, p)).Where(p => p != null)
                .OrderBy(p => p.Count).FirstOrDefault();
            if (there == null)
            {
                return null;
            }

            List<TilePos> back = Path(vault, there[there.Count - 1], vault.Entrance);
            var route = new List<TilePos>(there);
            route.AddRange(back.Skip(1));

            var commands = new List<Command>();
            int tick = 0;
            for (int i = 1; i < route.Count; i++)
            {
                Dir dir = DirBetween(route[i - 1], route[i]);
                bool stepped = false;
                for (int wait = 0; wait <= MaxWait && !stepped; wait++)
                {
                    var trial = new List<Command>(commands) { Command.Dash(tick + wait, dir) };
                    int arrived = TryStep(vault, seed, trial, route[i], i == route.Count - 1);
                    if (arrived >= 0)
                    {
                        commands = trial;
                        tick = arrived;
                        stepped = true;
                    }
                }

                if (!stepped || tick >= SimTuning.RaidTicks)
                {
                    return null;
                }
            }

            return new InputLog(commands.ToArray());
        }

        /// <summary>The tick Wick stands on target with every flicker he had, or -1.</summary>
        private static int TryStep(VaultDef vault, int seed, List<Command> commands, TilePos target, bool last)
        {
            var sim = new RaidSim(vault, seed, new InputLog(commands.ToArray()));
            int lastTick = commands[commands.Count - 1].Tick;
            int flickersBefore = -1;
            while (!sim.IsOver)
            {
                if (sim.State.Tick == lastTick)
                {
                    flickersBefore = sim.State.Wick.Flickers;
                }

                sim.Step();
                if (flickersBefore >= 0 && sim.State.Wick.Flickers < flickersBefore)
                {
                    return -1;
                }

                WickState w = sim.State.Wick;
                if (sim.State.Tick > lastTick && w.Tile == target && !w.Moving)
                {
                    // Must also survive a moment of standing there.
                    for (int i = 0; i < 6 && !sim.IsOver; i++)
                    {
                        sim.Step();
                        if (sim.State.Wick.Flickers < flickersBefore)
                        {
                            return -1;
                        }
                    }

                    return sim.State.Tick - 6;
                }

                if (sim.State.Tick > lastTick + 60)
                {
                    return -1;
                }
            }

            return last && sim.State.Outcome == Outcome.Escaped ? sim.State.Tick : -1;
        }

        private static List<TilePos> Path(VaultDef v, TilePos from, TilePos to)
        {
            var prev = new Dictionary<TilePos, TilePos>();
            var queue = new Queue<TilePos>();
            queue.Enqueue(from);
            prev[from] = from;
            while (queue.Count > 0)
            {
                TilePos p = queue.Dequeue();
                if (p == to)
                {
                    var path = new List<TilePos> { p };
                    while (p != from)
                    {
                        p = prev[p];
                        path.Add(p);
                    }

                    path.Reverse();
                    return path;
                }

                for (int d = 0; d < 4; d++)
                {
                    TilePos n = p.Step((Dir)d);
                    if (v.IsWalkable(n) && !prev.ContainsKey(n) && !IsBell(v, n))
                    {
                        prev[n] = p;
                        queue.Enqueue(n);
                    }
                }
            }

            return null;
        }

        private static bool IsBell(VaultDef v, TilePos p)
        {
            for (int i = 0; i < v.ObjectCount; i++)
            {
                if (v.GetObject(i).Type == ObjectType.SnufferBell && v.GetObject(i).Pos == p)
                {
                    return true;
                }
            }

            return false;
        }

        private static Dir DirBetween(TilePos a, TilePos b)
        {
            if (b.Y < a.Y) return Dir.N;
            if (b.X > a.X) return Dir.E;
            if (b.Y > a.Y) return Dir.S;
            return Dir.W;
        }
    }
}
