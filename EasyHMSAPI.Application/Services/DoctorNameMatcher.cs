using System.Text;

namespace EasyHMSAPI.Application.Services
{
    /// <summary>
    /// Spelling-tolerant doctor-name matching for Vita's "Dr Sharma se milna hai" asks. Patients
    /// type names in Hinglish, from memory, often misspelt ("sarma", "benerji"), so an exact SQL
    /// LIKE misses most of them. Titles (Dr, Doctor, Prof) are ignored; each query word is scored
    /// against its best-matching name word (Levenshtein similarity, with a prefix shortcut for
    /// short forms such as "bane" for "Banerjee"), and the name's score is the average.
    /// </summary>
    public static class DoctorNameMatcher
    {
        // A name counts as a match at or above this average similarity.
        public const double MatchThreshold = 0.72;

        private static readonly HashSet<string> Titles = new(StringComparer.Ordinal)
        {
            "dr", "doctor", "prof", "professor", "mr", "mrs", "ms", "miss", "sir", "madam", "ji",
        };

        public static double Score(string? query, string? fullName)
        {
            var q = Tokens(query);
            var n = Tokens(fullName);
            if (q.Count == 0 || n.Count == 0) return 0;

            double total = 0;
            foreach (var qt in q)
            {
                double best = 0;
                foreach (var nt in n)
                {
                    best = Math.Max(best, TokenSimilarity(qt, nt));
                    if (best >= 1) break;
                }
                total += best;
            }
            return Math.Round(total / q.Count, 3);
        }

        public static bool IsMatch(string? query, string? fullName) => Score(query, fullName) >= MatchThreshold;

        internal static List<string> Tokens(string? text)
        {
            var result = new List<string>();
            if (string.IsNullOrWhiteSpace(text)) return result;

            var sb = new StringBuilder();
            foreach (var ch in text.ToLowerInvariant())
            {
                if (char.IsLetter(ch)) sb.Append(ch);
                else Flush();
            }
            Flush();
            return result;

            void Flush()
            {
                if (sb.Length == 0) return;
                var token = sb.ToString();
                sb.Clear();
                if (token.Length >= 2 && !Titles.Contains(token)) result.Add(token);
            }
        }

        internal static double TokenSimilarity(string a, string b)
        {
            if (a == b) return 1;
            // "bane" -> "banerjee", "chakra" -> "chakraborty": a typed short form of a long surname.
            if (a.Length >= 3 && b.StartsWith(a, StringComparison.Ordinal)) return 0.9;
            var distance = Levenshtein(a, b);
            return 1.0 - (double)distance / Math.Max(a.Length, b.Length);
        }

        private static int Levenshtein(string a, string b)
        {
            var prev = new int[b.Length + 1];
            var curr = new int[b.Length + 1];
            for (var j = 0; j <= b.Length; j++) prev[j] = j;
            for (var i = 1; i <= a.Length; i++)
            {
                curr[0] = i;
                for (var j = 1; j <= b.Length; j++)
                {
                    var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                    curr[j] = Math.Min(Math.Min(curr[j - 1] + 1, prev[j] + 1), prev[j - 1] + cost);
                }
                (prev, curr) = (curr, prev);
            }
            return prev[b.Length];
        }
    }
}
