using System.Text;

namespace EasyHMSAPI.Application.Services
{
    /// <summary>
    /// Small, dependency-free fuzzy-matching helpers used by patient duplicate detection.
    /// Jaro-Winkler is well suited to name spelling variants and transliteration
    /// (e.g. "Mohammed" vs "Mohammad" ≈ 0.96).
    /// </summary>
    public static class FuzzyMatch
    {
        /// <summary>Lowercase, trim, collapse whitespace and strip non-alphanumeric characters.</summary>
        public static string Normalize(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            var sb = new StringBuilder(value.Length);
            bool lastSpace = false;
            foreach (var ch in value.Trim().ToLowerInvariant())
            {
                if (char.IsLetterOrDigit(ch))
                {
                    sb.Append(ch);
                    lastSpace = false;
                }
                else if (char.IsWhiteSpace(ch))
                {
                    if (!lastSpace && sb.Length > 0) { sb.Append(' '); lastSpace = true; }
                }
                // other punctuation is dropped
            }
            return sb.ToString().TrimEnd();
        }

        /// <summary>Last 4 digits of an identifier (digits only); null when fewer than 4 digits.</summary>
        public static string? Last4Digits(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            var digits = new string(value.Where(char.IsDigit).ToArray());
            return digits.Length >= 4 ? digits[^4..] : null;
        }

        /// <summary>Jaro-Winkler similarity in [0,1] on the normalized forms of the two strings.</summary>
        public static double JaroWinkler(string? s1, string? s2)
        {
            var a = Normalize(s1);
            var b = Normalize(s2);
            if (a.Length == 0 && b.Length == 0) return 1.0;
            if (a.Length == 0 || b.Length == 0) return 0.0;
            if (a == b) return 1.0;

            double jaro = Jaro(a, b);
            // Winkler prefix boost (up to 4 leading chars, scaling factor 0.1).
            int prefix = 0;
            int max = Math.Min(4, Math.Min(a.Length, b.Length));
            while (prefix < max && a[prefix] == b[prefix]) prefix++;
            return jaro + prefix * 0.1 * (1 - jaro);
        }

        private static double Jaro(string a, string b)
        {
            int matchDistance = Math.Max(a.Length, b.Length) / 2 - 1;
            if (matchDistance < 0) matchDistance = 0;

            var aMatches = new bool[a.Length];
            var bMatches = new bool[b.Length];
            int matches = 0;

            for (int i = 0; i < a.Length; i++)
            {
                int start = Math.Max(0, i - matchDistance);
                int end = Math.Min(i + matchDistance + 1, b.Length);
                for (int j = start; j < end; j++)
                {
                    if (bMatches[j] || a[i] != b[j]) continue;
                    aMatches[i] = true;
                    bMatches[j] = true;
                    matches++;
                    break;
                }
            }
            if (matches == 0) return 0.0;

            double transpositions = 0;
            int k = 0;
            for (int i = 0; i < a.Length; i++)
            {
                if (!aMatches[i]) continue;
                while (!bMatches[k]) k++;
                if (a[i] != b[k]) transpositions++;
                k++;
            }
            transpositions /= 2;

            double m = matches;
            return (m / a.Length + m / b.Length + (m - transpositions) / m) / 3.0;
        }
    }
}
