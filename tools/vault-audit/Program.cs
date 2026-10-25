using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Wick.Sim;

namespace Wick.VaultAudit
{
    /// <summary>
    /// Migration check for rule changes (M10.1b: pits): runs today's VaultValidator over vaults that
    /// were built under older rules and lists every one that would now be refused. Read-only: it never
    /// changes a vault. A listed live vault keeps playing as it is; its owner meets the problem the
    /// next time they publish.
    ///
    /// Usage (from the repo root):
    ///   dotnet run --project tools/vault-audit                  bundled templates, seeds, designer vaults
    ///                                                           and this PC's editor drafts
    ///   dotnet run --project tools/vault-audit -- path ...     vault documents or drafts (files or folders)
    ///   dotnet run --project tools/vault-audit -- --live development
    ///       every player's vault_live in that environment, read with the Cloud Save admin API. Needs a
    ///       service account key with Cloud Save read access in WICK_UGS_KEY_ID and WICK_UGS_SECRET.
    ///
    /// Drafts on a phone: with a development build connected,
    ///   adb exec-out run-as com.uprisestudios.wick tar c files/drafts > drafts.tar
    /// then unpack it and pass the folder.
    ///
    /// Exits 1 if any vault fails, 2 if one couldn't be read or fetched.
    /// </summary>
    public static class Program
    {
        private const string ProjectId = "71e95e2b-a983-4b74-96f4-16d2974b9c78";

        private static readonly Dictionary<string, string> Environments = new Dictionary<string, string>
        {
            ["development"] = "15ad4099-4858-4abe-a883-faff019a326f",
        };

        private static readonly string[] Bundled =
        {
            "unity/Assets/_Wick/Resources/VaultTemplates",
            "unity/Assets/_Wick/Data/SeedVaults",
            "unity/Assets/_Wick/Data/DesignerVaults",
            "unity/Assets/_Wick/Resources/DevVaults",
        };

        /// <summary>Codes added with pits: a vault failing only on others broke an older rule.</summary>
        private static readonly VaultIssueCode[] PitCodes =
        {
            VaultIssueCode.PitInDoorway, VaultIssueCode.PitBlocksRoute, VaultIssueCode.ObjectOnPit,
        };

        private static int _checked;
        private static int _failed;
        private static int _unreadable;

        public static int Main(string[] args)
        {
            int live = Array.IndexOf(args, "--live");
            if (live >= 0)
            {
                if (live + 1 >= args.Length || !Environments.TryGetValue(args[live + 1], out string environmentId))
                {
                    Console.Error.WriteLine("--live takes an environment: " + string.Join(", ", Environments.Keys));
                    return 2;
                }

                AuditLive(environmentId);
            }
            else
            {
                IEnumerable<string> roots = args.Length > 0 ? args : Bundled.Append(EditorDrafts());
                foreach (string root in roots)
                {
                    AuditPath(root);
                }
            }

            Console.WriteLine();
            Console.WriteLine(_checked + " vaults checked: " + (_checked - _failed) + " pass, " + _failed + " now refused" +
                (_unreadable > 0 ? ", " + _unreadable + " unreadable" : ""));
            return _unreadable > 0 ? 2 : _failed > 0 ? 1 : 0;
        }

        /// <summary>Where the Unity Editor keeps drafts on Windows (Application.persistentDataPath/drafts).</summary>
        private static string EditorDrafts()
        {
            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(home, "AppData", "LocalLow", "Uprise Studios", "Wax Bandits", "drafts");
        }

        // ---- Files ----------------------------------------------------------------------------

        private static void AuditPath(string path)
        {
            if (File.Exists(path))
            {
                AuditFile(path);
                return;
            }

            if (!Directory.Exists(path))
            {
                Console.WriteLine("SKIP " + path + " (not found)");
                return;
            }

            foreach (string file in Directory.GetFiles(path, "*.json").OrderBy(f => f, StringComparer.Ordinal))
            {
                AuditFile(file);
            }
        }

        private static void AuditFile(string file)
        {
            string name = Path.GetFileName(file);
            if (name == "seeds.json" || name == "designer.json")
            {
                return; // manifests, not vaults
            }

            Audit(file, File.ReadAllText(file));
        }

        /// <summary>A vault document, or an editor draft that wraps one under "vault".</summary>
        private static void Audit(string label, string json)
        {
            VaultDef vault;
            try
            {
                JsonNode root = JsonReader.Parse(json);
                vault = VaultJson.FromNode(root.Find("vault") ?? root);
            }
            catch (Exception e) when (e is FormatException || e is ArgumentException)
            {
                _unreadable++;
                Console.WriteLine("READ " + label + ": " + e.Message);
                return;
            }

            _checked++;
            List<VaultIssue> issues = VaultValidator.Validate(vault);
            if (issues.Count == 0)
            {
                Console.WriteLine("OK   " + label);
                return;
            }

            _failed++;
            bool pits = issues.Any(i => PitCodes.Contains(i.Code));
            Console.WriteLine("FAIL " + label + " (Vault Level " + vault.VaultLevel + (pits ? ", pit rules" : ", older rules") + ")");
            foreach (VaultIssue issue in issues)
            {
                Console.WriteLine("       " + issue.Code + (issue.Tile.X >= 0 ? " at " + issue.Tile : "") + ": " + issue.Message);
            }
        }

        // ---- Live vaults ----------------------------------------------------------------------

        private static void AuditLive(string environmentId)
        {
            string keyId = Environment.GetEnvironmentVariable("WICK_UGS_KEY_ID");
            string secret = Environment.GetEnvironmentVariable("WICK_UGS_SECRET");
            if (string.IsNullOrEmpty(keyId) || string.IsNullOrEmpty(secret))
            {
                Console.Error.WriteLine("Set WICK_UGS_KEY_ID and WICK_UGS_SECRET to a service account key that can read Cloud Save.");
                _unreadable++;
                return;
            }

            using var http = new HttpClient { BaseAddress = new Uri("https://services.api.unity.com"), Timeout = TimeSpan.FromSeconds(30) };
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic",
                Convert.ToBase64String(Encoding.UTF8.GetBytes(keyId + ":" + secret)));
            string data = "/cloud-save/v1/data/projects/" + ProjectId + "/environments/" + environmentId;

            int players = 0;
            for (string page = data + "/players?limit=100"; page != null;)
            {
                using JsonDocument list = Get(http, page);
                if (list == null)
                {
                    return;
                }

                foreach (JsonElement row in list.RootElement.GetProperty("results").EnumerateArray())
                {
                    players++;
                    AuditPlayer(http, data, row.GetProperty("id").GetString());
                }

                page = list.RootElement.TryGetProperty("links", out JsonElement links) && links.TryGetProperty("next", out JsonElement next) &&
                    next.ValueKind == JsonValueKind.String ? next.GetString() : null;
            }

            Console.WriteLine(players + " players with Cloud Save data");
        }

        private static void AuditPlayer(HttpClient http, string data, string playerId)
        {
            using JsonDocument items = Get(http, data + "/players/" + playerId + "/protected/items?keys=vault_live");
            if (items == null)
            {
                return;
            }

            foreach (JsonElement item in items.RootElement.GetProperty("results").EnumerateArray())
            {
                JsonElement value = item.GetProperty("value");
                if (value.ValueKind == JsonValueKind.String)
                {
                    value = JsonDocument.Parse(value.GetString()).RootElement; // stored as a JSON string
                }

                string vaultId = value.TryGetProperty("vaultId", out JsonElement id) ? id.GetString() : "?";
                if (!value.TryGetProperty("document", out JsonElement document) || document.ValueKind != JsonValueKind.String)
                {
                    _unreadable++;
                    Console.WriteLine("READ player " + playerId + ": vault_live has no document");
                    continue;
                }

                Audit(vaultId + " (player " + playerId + ")", document.GetString());
            }
        }

        private static JsonDocument Get(HttpClient http, string path)
        {
            HttpResponseMessage response = http.GetAsync(path).GetAwaiter().GetResult();
            string body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            if (!response.IsSuccessStatusCode)
            {
                _unreadable++;
                Console.WriteLine("HTTP " + (int)response.StatusCode + " for " + path + ": " + body);
                return null;
            }

            return JsonDocument.Parse(body);
        }
    }
}
