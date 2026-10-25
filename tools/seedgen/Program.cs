using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Wick.Bots;
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
    /// Designer vaults: dotnet run --project tools/seedgen -- designer (see Designer.cs)
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
            if (args.Length > 0 && args[0] == "designer")
            {
                return Designer.Run();
            }

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
                VaultDef vault = VaultJson.Parse(VaultJson.Write(VaultGen.Fill(template, level, rng, "v_seed_" + name)));
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
    }
}
