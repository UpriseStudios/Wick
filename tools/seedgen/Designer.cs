using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Wick.Bots;
using Wick.Sim;

namespace Wick.SeedGen
{
    /// <summary>
    /// Designer seed vaults (M10, B5): checks and prepares hand-made vaults for the seed pool.
    ///
    /// Put vaults in unity/Assets/_Wick/Data/DesignerVaults as vault documents (with or without a
    /// clear-check) or as editor drafts (Wick > Export Drafts as Designer Vaults copies yours there).
    /// Then: dotnet run --project tools/seedgen -- designer
    ///
    /// Each vault must pass VaultValidator. Its clear-check must replay; if it has none (or a stale
    /// one), the balance bot tries to escape it as base Wick and its run becomes the clear-check, or
    /// the vault is reported as needing a test run in the editor. Good vaults are written to
    /// Data/SeedVaults as d-*.json with a designer.json manifest (Crowns from their Vault Level),
    /// which Wick > Upload Seed Vaults lists alongside the generated seeds.
    /// </summary>
    public static class Designer
    {
        private const string InDir = "unity/Assets/_Wick/Data/DesignerVaults";
        private const string OutDir = "unity/Assets/_Wick/Data/SeedVaults";

        /// <summary>40 designer vaults across the 10 Vault Levels: 4 a level.</summary>
        public const int TargetPerLevel = 4;

        public static int Run()
        {
            if (!Directory.Exists(InDir))
            {
                Directory.CreateDirectory(InDir);
            }

            string[] files = Directory.GetFiles(InDir, "*.json").OrderBy(f => f, StringComparer.Ordinal).ToArray();
            var manifest = new JsonWriter();
            manifest.BeginArray();
            var perLevel = new int[VaultRules.MaxVaultLevel + 1];
            int failed = 0;
            foreach (string file in files)
            {
                string name = "d-" + Slug(Path.GetFileNameWithoutExtension(file));
                string problem = Prepare(File.ReadAllText(file), out VaultDef vault, out ClearCheck cc, out string how);
                if (problem != null)
                {
                    failed++;
                    Console.WriteLine("FAIL " + Path.GetFileName(file) + ": " + problem);
                    continue;
                }

                int level = vault.VaultLevel;
                int crowns = (level - 1) * 150 + perLevel[level] * 30;
                perLevel[level]++;
                File.WriteAllText(Path.Combine(OutDir, name + ".json"), VaultJson.WriteDocument(vault, cc));
                manifest.BeginObject().Field("name", name).Field("crowns", crowns).Field("level", level)
                    .Field("objects", vault.ObjectCount).Field("buildPoints", VaultRules.BuildPoints(vault))
                    .Field("clearStars", cc.Stars).EndObject();
                Console.WriteLine("OK   " + name + ": Vault Level " + level + ", " + VaultRules.BuildPoints(vault) + "/" +
                    VaultRules.Budget(level) + " pts, " + cc.Stars + "* clear-check (" + how + "), " + crowns + " Crowns");
            }

            manifest.EndArray();
            File.WriteAllText(Path.Combine(OutDir, "designer.json"), manifest.ToString());

            Console.WriteLine();
            Console.WriteLine("Coverage (target " + TargetPerLevel + " a level, " + TargetPerLevel * VaultRules.MaxVaultLevel + " in all):");
            for (int level = 1; level <= VaultRules.MaxVaultLevel; level++)
            {
                Console.WriteLine("  Vault Level " + level + ": " + perLevel[level] + (perLevel[level] < TargetPerLevel ? "  (" +
                    (TargetPerLevel - perLevel[level]) + " to go)" : ""));
            }

            Console.WriteLine(files.Length - failed + " ready, " + failed + " need attention");
            return failed > 0 ? 1 : 0;
        }

        /// <summary>Checks one vault and makes sure it has a clear-check. Returns null when it is ready.</summary>
        public static string Prepare(string json, out VaultDef vault, out ClearCheck clearCheck, out string how)
        {
            vault = null;
            clearCheck = null;
            how = null;
            try
            {
                JsonNode root = JsonReader.Parse(json);
                JsonNode vaultNode = root.Find("vault") ?? root; // an editor draft wraps the document
                vault = VaultJson.FromNode(vaultNode);
                clearCheck = VaultJson.ReadClearCheck(vaultNode);
            }
            catch (FormatException e)
            {
                return "can't be read: " + e.Message;
            }

            List<VaultIssue> issues = VaultValidator.Validate(vault);
            if (issues.Count > 0)
            {
                return "breaks the building rules: " + string.Join("; ", issues.Select(i => i.Message));
            }

            if (clearCheck != null && clearCheck.Params.IsBaseWick && ClearCheck.Verify(vault, clearCheck).Ok)
            {
                how = "yours";
                return null;
            }

            // No usable run: let the bot try (base Wick, a standard raid), with a few seeds.
            for (int seed = 1; seed <= 5; seed++)
            {
                RaidResult r = new Bot().Play(vault, seed, RaidParams.Default, out InputLog log);
                if (r.Outcome == Outcome.Escaped && r.Stars >= 1)
                {
                    clearCheck = ClearCheck.FromRun(vault, seed, log, r, RaidParams.Default);
                    how = "found by the bot";
                    return null;
                }
            }

            return "has no clear-check and the bot couldn't escape it: test it in the editor and export it again";
        }

        private static string Slug(string name)
        {
            var chars = name.ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray();
            return string.Join("-", new string(chars).Split(new[] { '-' }, StringSplitOptions.RemoveEmptyEntries));
        }
    }
}
