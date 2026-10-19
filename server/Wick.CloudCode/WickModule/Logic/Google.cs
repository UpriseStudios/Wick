using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Wick.CloudCode
{
    /// <summary>Outbound HTTPS, injectable so Google calls can be tested without the network.</summary>
    public interface IHttp
    {
        /// <returns>Status code and response body.</returns>
        Task<(int Status, string Body)> PostAsync(string url, string contentType, string body, string bearerToken);

        Task<(int Status, string Body)> GetAsync(string url, string bearerToken);
    }

    public sealed class HttpClientHttp : IHttp
    {
        private static readonly HttpClient Client = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };

        public async Task<(int Status, string Body)> PostAsync(string url, string contentType, string body, string bearerToken)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(body, Encoding.UTF8, contentType),
            };
            if (bearerToken != null)
            {
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", bearerToken);
            }

            using HttpResponseMessage response = await Client.SendAsync(request);
            return ((int)response.StatusCode, await response.Content.ReadAsStringAsync());
        }

        public async Task<(int Status, string Body)> GetAsync(string url, string bearerToken)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", bearerToken);
            using HttpResponseMessage response = await Client.SendAsync(request);
            return ((int)response.StatusCode, await response.Content.ReadAsStringAsync());
        }
    }

    /// <summary>The fields Wick needs from a Google service account key file (kept in Secret Manager).</summary>
    public sealed class GoogleServiceAccount
    {
        public string ClientEmail { get; private set; }
        public string PrivateKeyPem { get; private set; }
        public string TokenUri { get; private set; }
        public string ProjectId { get; private set; }

        public static GoogleServiceAccount Parse(string json)
        {
            JObject o = JObject.Parse(json);
            return new GoogleServiceAccount
            {
                ClientEmail = (string)o["client_email"],
                PrivateKeyPem = (string)o["private_key"],
                TokenUri = (string)o["token_uri"] ?? "https://oauth2.googleapis.com/token",
                ProjectId = (string)o["project_id"],
            };
        }
    }

    /// <summary>
    /// OAuth access tokens for a Google service account (signed JWT exchanged at token_uri), cached
    /// per account and scope until shortly before they expire.
    /// </summary>
    public sealed class GoogleAuth
    {
        private static readonly Dictionary<string, (string Token, long ExpiresAtMs)> Cache = new Dictionary<string, (string, long)>();

        private readonly GoogleServiceAccount _account;
        private readonly IHttp _http;
        private readonly IClock _clock;

        public GoogleAuth(GoogleServiceAccount account, IHttp http, IClock clock)
        {
            _account = account;
            _http = http;
            _clock = clock;
        }

        public async Task<string> AccessTokenAsync(string scope)
        {
            string key = _account.ClientEmail + " " + scope;
            long now = _clock.NowMs;
            lock (Cache)
            {
                if (Cache.TryGetValue(key, out var cached) && cached.ExpiresAtMs > now + 60_000)
                {
                    return cached.Token;
                }
            }

            string form = "grant_type=" + Uri.EscapeDataString("urn:ietf:params:oauth:grant-type:jwt-bearer") +
                "&assertion=" + Uri.EscapeDataString(SignedJwt(scope, now / 1000));
            (int status, string body) = await _http.PostAsync(_account.TokenUri, "application/x-www-form-urlencoded", form, null);
            if (status != 200)
            {
                throw new InvalidOperationException("Google token exchange failed (" + status + ")");
            }

            JObject o = JObject.Parse(body);
            string token = (string)o["access_token"];
            long expiresIn = (long?)o["expires_in"] ?? 3600;
            lock (Cache)
            {
                Cache[key] = (token, now + expiresIn * 1000);
            }

            return token;
        }

        /// <summary>RS256 JWT asserting this account for the scope, valid for an hour.</summary>
        public string SignedJwt(string scope, long nowSeconds)
        {
            string header = Base64Url(Encoding.UTF8.GetBytes("{\"alg\":\"RS256\",\"typ\":\"JWT\"}"));
            var claims = new JObject
            {
                ["iss"] = _account.ClientEmail,
                ["scope"] = scope,
                ["aud"] = _account.TokenUri,
                ["iat"] = nowSeconds,
                ["exp"] = nowSeconds + 3600,
            };
            string payload = Base64Url(Encoding.UTF8.GetBytes(claims.ToString(Newtonsoft.Json.Formatting.None)));
            using RSA rsa = RSA.Create();
            rsa.ImportFromPem(_account.PrivateKeyPem);
            byte[] signature = rsa.SignData(Encoding.ASCII.GetBytes(header + "." + payload), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            return header + "." + payload + "." + Base64Url(signature);
        }

        public static string Base64Url(byte[] bytes)
        {
            return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        }
    }
}
