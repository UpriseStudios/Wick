using System.Text;

namespace Wick.Services
{
    /// <summary>
    /// Vault share links: https://wick.uprisestudios.co.uk/v/CODE, where CODE is the vault's
    /// 8-character code (no 0, 1, I or O). Also finds a code in anything a player might bring
    /// back: a link, a pasted message, a typed code or the Play install referrer.
    /// </summary>
    public static class VaultLink
    {
        public const string Domain = "wick.uprisestudios.co.uk";
        public const string Alphabet = "23456789ABCDEFGHJKLMNPQRSTUVWXYZ";
        public const int CodeLength = 8;

        /// <summary>What the web page copies for the iOS first-launch paste prompt.</summary>
        public const string ClipboardPrefix = "Wax Bandits vault code: ";

        public static string Build(string code)
        {
            return "https://" + Domain + "/v/" + code;
        }

        public static string Store(string code)
        {
            return "https://play.google.com/store/apps/details?id=com.uprisestudios.wick&referrer=vault%3D" + code;
        }

        /// <summary>A code from a link (".../v/CODE"), a referrer ("vault=CODE"), or text holding one; else null.</summary>
        public static string FindCode(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return null;
            }

            string[] markers = { "/v/", "vault=", "vault%3D", "vault%3d", ClipboardPrefix };
            foreach (string marker in markers)
            {
                int at = text.IndexOf(marker, System.StringComparison.OrdinalIgnoreCase);
                if (at >= 0)
                {
                    string code = Normalize(Token(text, at + marker.Length));
                    if (code != null)
                    {
                        return code;
                    }
                }
            }

            return Normalize(text.Trim()); // just the code, typed or pasted
        }

        /// <summary>Upper-case, without spaces or dashes; null unless it's a well-formed code.</summary>
        public static string Normalize(string text)
        {
            if (text == null)
            {
                return null;
            }

            var sb = new StringBuilder(CodeLength);
            foreach (char c in text)
            {
                if (c == ' ' || c == '-')
                {
                    continue;
                }

                char u = char.ToUpperInvariant(c);
                if (Alphabet.IndexOf(u) < 0 || sb.Length >= CodeLength)
                {
                    return null;
                }

                sb.Append(u);
            }

            return sb.Length == CodeLength ? sb.ToString() : null;
        }

        private static string Token(string text, int start)
        {
            int end = start;
            while (end < text.Length && (char.IsLetterOrDigit(text[end]) || text[end] == '-'))
            {
                end++;
            }

            return text.Substring(start, end - start);
        }
    }
}
