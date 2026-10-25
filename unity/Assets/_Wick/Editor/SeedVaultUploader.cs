using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using Wick.Sim;

namespace Wick.Editor
{
    /// <summary>
    /// Wick > Upload Seed Vaults: checks every vault in Data/SeedVaults locally (building rules and
    /// clear-check replay), then lists each as a bot-owned live vault through the SeedVault Cloud
    /// Code function, which checks it again. Runs as a UGS service account whose key is kept in
    /// this machine's EditorPrefs, never in the project.
    /// Regenerate the vaults with: dotnet run --project tools/seedgen
    /// Designer vaults: put them in Data/DesignerVaults (Wick > Export Drafts as Designer Vaults) and run
    /// dotnet run --project tools/seedgen -- designer
    /// </summary>
    public sealed class SeedVaultUploader : EditorWindow
    {
        public const string SeedDir = "Assets/_Wick/Data/SeedVaults";
        private const string DevelopmentEnvironmentId = "15ad4099-4858-4abe-a883-faff019a326f";
        private const string KeyIdPref = "Wick.SeedUpload.KeyId";
        private const string SecretPref = "Wick.SeedUpload.Secret";
        private const string EnvPref = "Wick.SeedUpload.EnvironmentId";

        private string _keyId;
        private string _secret;
        private string _environmentId;
        private string _log = "";
        private bool _busy;
        private Vector2 _scroll;

        [MenuItem("Wick/Upload Seed Vaults")]
        public static void Open()
        {
            GetWindow<SeedVaultUploader>("Seed Vaults");
        }

        private void OnEnable()
        {
            _keyId = EditorPrefs.GetString(KeyIdPref, "");
            _secret = EditorPrefs.GetString(SecretPref, "");
            _environmentId = EditorPrefs.GetString(EnvPref, DevelopmentEnvironmentId);
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Project", CloudProjectSettings.projectId);
            _environmentId = EditorGUILayout.TextField("Environment ID", _environmentId);
            EditorGUILayout.HelpBox("Development is " + DevelopmentEnvironmentId + ". The key needs permission to run Cloud Code.",
                MessageType.None);
            _keyId = EditorGUILayout.TextField("Service account key ID", _keyId);
            _secret = EditorGUILayout.PasswordField("Secret key", _secret);

            using (new EditorGUI.DisabledScope(_busy))
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Check locally"))
                {
                    _log = "";
                    CheckAll(Log);
                }

                if (GUILayout.Button("Upload all"))
                {
                    EditorPrefs.SetString(KeyIdPref, _keyId);
                    EditorPrefs.SetString(SecretPref, _secret);
                    EditorPrefs.SetString(EnvPref, _environmentId);
                    _log = "";
                    Upload();
                }

                EditorGUILayout.EndHorizontal();
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            EditorGUILayout.TextArea(_log, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();
        }

        private void Log(string line)
        {
            _log += line + "\n";
            Repaint();
        }

        /// <summary>One seed vault: its name (also the bot's id), Crown rating and document.</summary>
        public readonly struct Seed
        {
            public readonly string Name;
            public readonly int Crowns;
            public readonly string Document;

            public Seed(string name, int crowns, string document)
            {
                Name = name;
                Crowns = crowns;
                Document = document;
            }
        }

        /// <summary>Designer vaults (designer.json, from tools/seedgen designer) first, then the generated seeds.</summary>
        public static List<Seed> LoadSeeds()
        {
            var seeds = new List<Seed>();
            foreach (string list in new[] { "designer.json", "seeds.json" })
            {
                string path = Path.Combine(SeedDir, list);
                if (!File.Exists(path))
                {
                    continue;
                }

                JsonNode manifest = JsonReader.Parse(File.ReadAllText(path));
                for (int i = 0; i < manifest.Count; i++)
                {
                    string name = manifest[i].GetString("name");
                    seeds.Add(new Seed(name, manifest[i].GetInt("crowns"), File.ReadAllText(Path.Combine(SeedDir, name + ".json"))));
                }
            }

            return seeds;
        }

        /// <returns>True if every seed passes the rules and its clear-check replays.</returns>
        public static bool CheckAll(Action<string> log)
        {
            bool ok = true;
            foreach (Seed s in LoadSeeds())
            {
                JsonNode root = JsonReader.Parse(s.Document);
                VaultDef vault = VaultJson.FromNode(root);
                List<VaultIssue> issues = VaultValidator.Validate(vault);
                ClearCheckResult check = ClearCheck.Verify(vault, VaultJson.ReadClearCheck(root));
                bool good = issues.Count == 0 && check.Ok;
                ok &= good;
                log(s.Name + ": " + (good ? "OK, " + check.Replay.Value.Stars + "* clear" : issues.Count > 0 ? issues[0].Message : check.Reason));
            }

            return ok;
        }

        private async void Upload()
        {
            _busy = true;
            try
            {
                if (!CheckAll(Log))
                {
                    Log("Fix the vaults above first. Nothing was uploaded.");
                    return;
                }

                int done = await UploadAll(CloudProjectSettings.projectId, _environmentId, _keyId, _secret, Log);
                Log("Uploaded " + done + " seed vaults.");
            }
            catch (Exception e)
            {
                Log("Upload failed: " + e.Message);
            }
            finally
            {
                _busy = false;
                Repaint();
            }
        }

        public static async Task<int> UploadAll(string projectId, string environmentId, string keyId, string secret, Action<string> log)
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            string token = await ServiceToken(http, projectId, environmentId, keyId, secret);
            int done = 0;
            foreach (Seed s in LoadSeeds())
            {
                var w = new JsonWriter();
                w.BeginObject().Name("params").BeginObject().Field("name", s.Name).Field("vault", s.Document).Field("crowns", s.Crowns)
                    .EndObject().EndObject();
                var request = new HttpRequestMessage(HttpMethod.Post,
                    "https://cloud-code.services.api.unity.com/v1/projects/" + projectId + "/modules/WickModule/SeedVault")
                {
                    Content = new StringContent(w.ToString(), Encoding.UTF8, "application/json"),
                };
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                HttpResponseMessage response = await http.SendAsync(request);
                string body = await response.Content.ReadAsStringAsync();
                bool ok = response.IsSuccessStatusCode && body.Contains("\"ok\":true");
                done += ok ? 1 : 0;
                log(s.Name + ": " + (ok ? "live (" + s.Crowns + " Crowns)" : (int)response.StatusCode + " " + body));
            }

            return done;
        }

        /// <summary>Exchanges the service account key for a short-lived token scoped to the environment.</summary>
        private static async Task<string> ServiceToken(HttpClient http, string projectId, string environmentId, string keyId, string secret)
        {
            var request = new HttpRequestMessage(HttpMethod.Post,
                "https://services.api.unity.com/auth/v1/token-exchange?projectId=" + projectId + "&environmentId=" + environmentId);
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic",
                Convert.ToBase64String(Encoding.UTF8.GetBytes(keyId + ":" + secret)));
            HttpResponseMessage response = await http.SendAsync(request);
            string body = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException("Token exchange failed (" + (int)response.StatusCode + "): " + body);
            }

            return JsonReader.Parse(body).GetString("accessToken");
        }

        /// <summary>
        /// Batch: Unity.exe -batchmode -quit -projectPath unity -executeMethod Wick.Editor.SeedVaultUploader.UploadFromEnvironment
        /// with WICK_UGS_KEY_ID and WICK_UGS_SECRET set (and optionally WICK_UGS_ENVIRONMENT_ID).
        /// </summary>
        public static void UploadFromEnvironment()
        {
            string keyId = Environment.GetEnvironmentVariable("WICK_UGS_KEY_ID");
            string secret = Environment.GetEnvironmentVariable("WICK_UGS_SECRET");
            string env = Environment.GetEnvironmentVariable("WICK_UGS_ENVIRONMENT_ID") ?? DevelopmentEnvironmentId;
            if (string.IsNullOrEmpty(keyId) || string.IsNullOrEmpty(secret))
            {
                throw new InvalidOperationException("Set WICK_UGS_KEY_ID and WICK_UGS_SECRET");
            }

            if (!CheckAll(Debug.Log))
            {
                throw new InvalidOperationException("Seed vaults failed the local check");
            }

            int done = UploadAll(CloudProjectSettings.projectId, env, keyId, secret, Debug.Log).GetAwaiter().GetResult();
            Debug.Log("SeedVaultUploader: uploaded " + done + " seed vaults");
        }
    }
}
