using System.Text;
using System.Threading.Tasks;
using Wick.Sim;

namespace Wick.CloudCode
{
    /// <summary>
    /// Short vault codes for links (wick.uprisestudios.co.uk/v/CODE), pasting and install
    /// referrers: 8 characters from an alphabet without look-alikes (no 0, 1, I or O). A vault's
    /// code is derived from its id, so republishing keeps it; a clash with another vault moves on
    /// to the next salt. Codes resolve through server-only custom items code_&lt;CODE&gt;.
    /// </summary>
    public static class VaultCodes
    {
        public const string Alphabet = "23456789ABCDEFGHJKLMNPQRSTUVWXYZ";
        public const int Length = 8;
        private const string Prefix = "code_";
        private const string Key = "vault";

        public static string Derive(string vaultId, int salt)
        {
            var h = new Fnv1a64();
            foreach (byte b in Encoding.UTF8.GetBytes(vaultId + ":" + salt))
            {
                h.AddByte(b);
            }

            ulong v = h.Value;
            var code = new char[Length];
            for (int i = 0; i < Length; i++)
            {
                code[i] = Alphabet[(int)(v & 31)];
                v >>= 5;
            }

            return new string(code);
        }

        /// <summary>Upper-cases and drops spaces and dashes; null if it isn't a well-formed code.</summary>
        public static string Normalize(string text)
        {
            if (text == null)
            {
                return null;
            }

            var sb = new StringBuilder(Length);
            foreach (char c in text)
            {
                if (c == ' ' || c == '-')
                {
                    continue;
                }

                char u = char.ToUpperInvariant(c);
                if (Alphabet.IndexOf(u) < 0 || sb.Length >= Length)
                {
                    return null;
                }

                sb.Append(u);
            }

            return sb.Length == Length ? sb.ToString() : null;
        }

        /// <summary>The vault's code, claiming it on first use.</summary>
        public static async Task<string> AssignAsync(IGameStore store, string vaultId)
        {
            for (int salt = 0; salt < 10; salt++)
            {
                string code = Derive(vaultId, salt);
                string owner = await ResolveAsync(store, code);
                if (owner == vaultId)
                {
                    return code;
                }

                if (owner == null)
                {
                    await store.SetPrivateCustomAsync(Prefix + code, Key, Json.Write(vaultId));
                    return code;
                }
            }

            throw new System.InvalidOperationException("No free vault code for " + vaultId);
        }

        /// <summary>The vault id a code points to, or null.</summary>
        public static async Task<string> ResolveAsync(IGameStore store, string code)
        {
            var found = await store.GetPrivateCustomAsync(Prefix + code, new[] { Key });
            return found.TryGetValue(Key, out StoredItem item) ? Newtonsoft.Json.JsonConvert.DeserializeObject<string>(item.Json) : null;
        }
    }

    /// <summary>What a share link's web page shows about a vault. Nothing private.</summary>
    public sealed class VaultCard
    {
        public bool Ok { get; set; }
        public string Reason { get; set; }
        public string Code { get; set; }
        public int VaultLevel { get; set; }
        public int Crowns { get; set; }
        public int Raids { get; set; }
        public int Snuffs { get; set; }
        public int Traps { get; set; }
        public bool Shielded { get; set; }
    }
}
