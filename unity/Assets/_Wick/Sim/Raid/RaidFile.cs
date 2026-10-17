using System;

namespace Wick.Sim
{
    /// <summary>
    /// A replayable raid on disk (golden replays, replay-cli):
    /// { "simVersion", "seed", "vault": {vault JSON}, "log": base64 | "commands": [{tick,type,dir}],
    ///   "expected": { "outcome", "ticks", "hash", "stars"?, "loot"? } (optional) }.
    /// </summary>
    public sealed class RaidFile
    {
        private RaidFile(JsonNode root, int simVersion, int seed, VaultDef vault, InputLog log,
            bool hasExpected, Outcome expectedOutcome, int expectedTicks, string expectedHash,
            int? expectedStars = null, int? expectedLoot = null)
        {
            ExpectedStars = expectedStars;
            ExpectedLoot = expectedLoot;
            Root = root;
            SimVersion = simVersion;
            Seed = seed;
            Vault = vault;
            Log = log;
            HasExpected = hasExpected;
            ExpectedOutcome = expectedOutcome;
            ExpectedTicks = expectedTicks;
            ExpectedHash = expectedHash;
        }

        /// <summary>The parsed document, for callers that need extra fields.</summary>
        public JsonNode Root { get; }

        public int SimVersion { get; }
        public int Seed { get; }
        public VaultDef Vault { get; }
        public InputLog Log { get; }
        public bool HasExpected { get; }
        public Outcome ExpectedOutcome { get; }
        public int ExpectedTicks { get; }

        /// <summary>Expected final hash as 16 lowercase hex digits.</summary>
        public string ExpectedHash { get; }

        /// <summary>Expected stars and loot, when the file states them.</summary>
        public int? ExpectedStars { get; }

        public int? ExpectedLoot { get; }

        /// <summary>Whether a result matches every expectation this file states.</summary>
        public bool Matches(RaidResult r)
        {
            return HasExpected
                && r.Outcome == ExpectedOutcome
                && r.Ticks == ExpectedTicks
                && r.HashHex == ExpectedHash
                && (ExpectedStars == null || r.Stars == ExpectedStars)
                && (ExpectedLoot == null || r.Loot == ExpectedLoot);
        }

        public static RaidFile Parse(string json)
        {
            JsonNode root = JsonReader.Parse(json);
            int simVersion = root.GetInt("simVersion");
            int seed = root.GetInt("seed");
            VaultDef vault = VaultJson.FromNode(root.Get("vault"));
            InputLog log = ReadLog(root);

            JsonNode expected = root.Find("expected");
            if (expected == null || expected.Kind == JsonKind.Null)
            {
                return new RaidFile(root, simVersion, seed, vault, log, false, Outcome.Running, 0, null);
            }

            string hash = expected.GetString("hash").ToLowerInvariant();
            if (hash.Length != 16 || !IsHex(hash))
            {
                throw new FormatException("expected.hash must be 16 hex digits");
            }

            JsonNode stars = expected.Find("stars");
            JsonNode loot = expected.Find("loot");
            return new RaidFile(root, simVersion, seed, vault, log, true,
                ParseOutcome(expected.GetString("outcome")), expected.GetInt("ticks"), hash,
                stars == null ? (int?)null : stars.AsInt(),
                loot == null ? (int?)null : loot.AsInt());
        }

        private static InputLog ReadLog(JsonNode root)
        {
            JsonNode base64 = root.Find("log");
            JsonNode commands = root.Find("commands");
            if ((base64 == null) == (commands == null))
            {
                throw new FormatException("A raid file needs exactly one of \"log\" or \"commands\"");
            }

            if (base64 != null)
            {
                return InputLog.FromBase64(base64.AsString());
            }

            var list = new Command[commands.Count];
            for (int i = 0; i < commands.Count; i++)
            {
                JsonNode c = commands[i];
                CommandType type = ParseCommandType(c.GetString("type"));
                Dir dir = Command.HasDir(type) ? ParseDir(c.GetString("dir")) : Dir.N;
                list[i] = new Command(c.GetInt("tick"), type, dir);
            }

            try
            {
                return new InputLog(list);
            }
            catch (ArgumentException e)
            {
                throw new FormatException(e.Message, e);
            }
        }

        public static CommandType ParseCommandType(string s)
        {
            switch (s)
            {
                case "Dash": return CommandType.Dash;
                case "RunStart": return CommandType.RunStart;
                case "RunStop": return CommandType.RunStop;
                case "TiptoeOn": return CommandType.TiptoeOn;
                case "TiptoeOff": return CommandType.TiptoeOff;
                case "Decoy": return CommandType.Decoy;
                default: throw new FormatException("Unknown command type '" + s + "'");
            }
        }

        public static Dir ParseDir(string s)
        {
            switch (s)
            {
                case "N": return Dir.N;
                case "E": return Dir.E;
                case "S": return Dir.S;
                case "W": return Dir.W;
                default: throw new FormatException("Unknown direction '" + s + "'");
            }
        }

        public static Outcome ParseOutcome(string s)
        {
            switch (s)
            {
                case "Running": return Outcome.Running;
                case "Escaped": return Outcome.Escaped;
                case "Snuffed": return Outcome.Snuffed;
                case "TimedOut": return Outcome.TimedOut;
                default: throw new FormatException("Unknown outcome '" + s + "'");
            }
        }

        private static bool IsHex(string s)
        {
            foreach (char c in s)
            {
                if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f')))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
