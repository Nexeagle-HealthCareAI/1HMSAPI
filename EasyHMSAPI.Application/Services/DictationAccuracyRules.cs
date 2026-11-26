using System.Text.RegularExpressions;

namespace EasyHMSAPI.Application.Services
{
    /// <summary>
    /// Rules that make a dictation more accurate and easier to check, with no AI involved:
    ///  - the vocabulary hint handed to the transcriber so it expects this patient's medicines;
    ///  - correcting a medicine name that was heard slightly wrong against the names on record;
    ///  - picking out what the doctor should look at before accepting: doses, and anything unclear.
    /// Pure functions, so they are tested directly.
    /// </summary>
    public static class DictationAccuracyRules
    {
        /// <summary>Whisper reads only the tail of its prompt (about 224 tokens), so the hint stays short.</summary>
        public const int MaxHintLength = 700;
        public const int MaxHintMedicines = 40;

        private const string CommonTerms = "tablet, capsule, injection, syrup, mg, mcg, ml, IV, IM, SC, OD, BD, TDS, QID, HS, SOS, PRN, stat, before food, after food";

        public const string NameKnown = "KNOWN";
        public const string NameCorrected = "CORRECTED";
        public const string NameUnverified = "UNVERIFIED";

        public sealed record NameCheck(string Name, string Status, string? HeardAs);
        public sealed record Correction(string Heard, string Corrected);
        public sealed record TranscriptCheck(string Text, List<Correction> Corrections);

        /// <summary>
        /// A short line of context for the transcriber: the medicines this patient is on, then common
        /// prescription terms. Medicine names only — never the patient's name or any identifier.
        /// </summary>
        public static string BuildVocabularyHint(IEnumerable<string?> medicineNames)
        {
            var names = DistinctNames(medicineNames).Take(MaxHintMedicines).ToList();
            var hint = "Hospital discharge dictation by a doctor.";
            foreach (var name in names)
            {
                var next = hint + (hint.EndsWith('.') ? " Medicines: " : ", ") + name;
                if (next.Length + CommonTerms.Length + 12 > MaxHintLength) break;
                hint = next;
            }
            if (!hint.EndsWith('.')) hint += ".";
            return hint + " Terms: " + CommonTerms + ".";
        }

        /// <summary>
        /// Checks a medicine name the dictation produced against the names on record for this stay.
        /// An exact or near-exact match is returned in the recorded spelling; a close miss
        /// ("Ticagrelol") is corrected and says what was heard; anything else is left as heard and
        /// marked unverified, so the doctor knows it was not found rather than trusting it silently.
        /// </summary>
        public static NameCheck CheckMedicineName(string? heard, IEnumerable<string?> knownNames)
        {
            var spoken = (heard ?? string.Empty).Trim();
            if (spoken.Length == 0) return new NameCheck(spoken, NameUnverified, null);
            var known = DistinctNames(knownNames).ToList();

            // Same drug word, or one is the other's opening five letters or more ("pantop" for
            // pantoprazole). A three-letter fragment is not enough to call it the same medicine.
            var key = DischargeAiRules.MedicineKey(spoken);
            var exact = known.FirstOrDefault(name =>
            {
                var k = DischargeAiRules.MedicineKey(name);
                if (k.Length == 0 || key.Length == 0) return false;
                if (k == key) return true;
                return Math.Min(k.Length, key.Length) >= 5 && (k.StartsWith(key, StringComparison.Ordinal) || key.StartsWith(k, StringComparison.Ordinal));
            });
            if (exact != null) return new NameCheck(exact, NameKnown, null);

            var best = ClosestKey(key, known);
            return best != null ? new NameCheck(best, NameCorrected, spoken) : new NameCheck(spoken, NameUnverified, null);
        }

        /// <summary>
        /// Fixes medicine names in the transcript that were heard slightly wrong. Deliberately strict —
        /// only a long word, one or two letters away from a medicine on record, starting with the same
        /// letter — so ordinary words are never rewritten. Every change is reported.
        /// </summary>
        public static TranscriptCheck CorrectTranscript(string? transcript, IEnumerable<string?> knownNames)
        {
            var text = transcript ?? string.Empty;
            var keys = DistinctNames(knownNames).Select(DischargeAiRules.MedicineKey).Where(k => k.Length >= 6).Distinct().ToList();
            var corrections = new List<Correction>();
            if (keys.Count == 0 || text.Length == 0) return new TranscriptCheck(text, corrections);

            var fixedText = Word.Replace(text, m =>
            {
                var word = m.Value;
                var lower = word.ToLowerInvariant();
                if (lower.Length < 6 || keys.Contains(lower)) return word;
                var match = keys
                    .Where(k => k[0] == lower[0] && Math.Abs(k.Length - lower.Length) <= 2)
                    .Select(k => (Key: k, Distance: Distance(lower, k)))
                    .Where(x => x.Distance is > 0 and <= 2)
                    .OrderBy(x => x.Distance)
                    .FirstOrDefault();
                if (match.Key == null) return word;
                var replacement = char.IsUpper(word[0]) ? char.ToUpperInvariant(match.Key[0]) + match.Key[1..] : match.Key;
                if (!corrections.Any(c => string.Equals(c.Heard, word, StringComparison.OrdinalIgnoreCase)))
                    corrections.Add(new Correction(word, replacement));
                return replacement;
            });
            return new TranscriptCheck(fixedText, corrections);
        }

        /// <summary>Every dose-like figure in the text ("40 mg", "0.5 ml", "10 units"), in order, without repeats.</summary>
        public static List<string> DosesToCheck(string? text)
        {
            var list = new List<string>();
            foreach (Match m in Dose.Matches(text ?? string.Empty))
            {
                var value = Regex.Replace(m.Value.Trim(), @"\s+", " ");
                if (!list.Contains(value, StringComparer.OrdinalIgnoreCase)) list.Add(value);
                if (list.Count == 20) break;
            }
            return list;
        }

        /// <summary>True when the transcriber's own figures say a stretch of audio was hard to hear.</summary>
        public static bool IsUnclearSegment(double? avgLogProb, double? noSpeechProb, double? compressionRatio) =>
            (avgLogProb.HasValue && avgLogProb.Value < -0.9) ||
            (noSpeechProb.HasValue && noSpeechProb.Value > 0.6) ||
            (compressionRatio.HasValue && compressionRatio.Value > 2.4);

        private static IEnumerable<string> DistinctNames(IEnumerable<string?> names) =>
            (names ?? Enumerable.Empty<string?>())
                .Select(n => (n ?? string.Empty).Trim())
                .Where(n => n.Length > 0)
                .GroupBy(n => n.ToLowerInvariant())
                .Select(g => g.First());

        private static string? ClosestKey(string key, List<string> known)
        {
            if (key.Length < 5) return null;
            return known
                .Select(name => (Name: name, Key: DischargeAiRules.MedicineKey(name)))
                .Where(x => x.Key.Length >= 5 && x.Key[0] == key[0])
                .Select(x => (x.Name, Distance: Distance(key, x.Key), Longest: Math.Max(key.Length, x.Key.Length)))
                .Where(x => x.Distance <= 2 && (double)x.Distance / x.Longest <= 0.25)
                .OrderBy(x => x.Distance)
                .Select(x => x.Name)
                .FirstOrDefault();
        }

        /// <summary>Edit distance (insert, delete, substitute).</summary>
        public static int Distance(string a, string b)
        {
            if (a.Length == 0) return b.Length;
            if (b.Length == 0) return a.Length;
            var prev = new int[b.Length + 1];
            var cur = new int[b.Length + 1];
            for (var j = 0; j <= b.Length; j++) prev[j] = j;
            for (var i = 1; i <= a.Length; i++)
            {
                cur[0] = i;
                for (var j = 1; j <= b.Length; j++)
                    cur[j] = Math.Min(Math.Min(cur[j - 1] + 1, prev[j] + 1), prev[j - 1] + (a[i - 1] == b[j - 1] ? 0 : 1));
                (prev, cur) = (cur, prev);
            }
            return prev[b.Length];
        }

        private static readonly Regex Word = new(@"[A-Za-z]{6,}", RegexOptions.Compiled);
        private static readonly Regex Dose = new(@"\b\d+(?:[.,]\d+)?\s?(?:mg|mcg|microgram(?:s)?|g|gm|ml|l|iu|units?|puffs?|drops?|%)(?![A-Za-z])", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    }
}
