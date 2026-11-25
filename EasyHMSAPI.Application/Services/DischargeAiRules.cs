using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace EasyHMSAPI.Application.Services
{
    /// <summary>
    /// Pure rules for the AI-assisted discharge summary: which sections AI may write, the prompts,
    /// how the model's JSON answer is read back, and how patient identifiers are removed before any
    /// text leaves the server. No I/O here, so all of it is unit-tested.
    ///
    /// Safety contract (enforced here and in the handlers):
    ///  • AI only ever writes narrative sections. It never writes diagnoses, medicines, doses or
    ///    dates. Medicine changes heard in a dictation come back only as suggestions the doctor taps.
    ///  • Nothing AI returns is saved or signed by the server; the doctor accepts each section.
    ///  • The patient's name, relatives' names, IDs, phone numbers, Aadhaar/ABHA and email are
    ///    replaced before the text is sent.
    /// </summary>
    public static class DischargeAiRules
    {
        public static class SectionSource
        {
            public const string Records = "RECORDS";
            public const string Ai = "AI";
            public const string Doctor = "DOCTOR";
            public static readonly string[] All = { Records, Ai, Doctor };
        }

        /// <summary>Narrative sections AI may draft, with the heading used in prompts.</summary>
        public static readonly IReadOnlyDictionary<string, string> DraftableSections = new Dictionary<string, string>
        {
            ["chiefComplaint"] = "Chief complaint",
            ["historyOfPresentIllness"] = "History of present illness",
            ["courseInHospital"] = "Course in hospital",
            ["keyInvestigations"] = "Key investigations",
            ["proceduresPerformed"] = "Procedures performed",
            ["followUpInstructions"] = "Follow-up instructions",
            ["dietInstructions"] = "Diet advice",
            ["activityRestrictions"] = "Activity advice",
            ["warningSigns"] = "Come back at once if (warning signs)",
        };

        /// <summary>Patient-advice sections that are also printed in the hospital's second language.</summary>
        public static readonly string[] PatientAdviceSections = { "followUpInstructions", "dietInstructions", "activityRestrictions", "warningSigns" };

        /// <summary>Languages a hospital can add under the English text, by code.</summary>
        public static readonly IReadOnlyDictionary<string, string> PatientLanguages = new Dictionary<string, string>
        {
            ["hi"] = "Hindi",
        };

        public static readonly string[] Styles = { "SHORTER", "DETAIL", "PATIENT" };

        public const int MaxSectionLength = 4000;
        public const int MaxSourceLength = 24000;
        public const long MaxAudioBytes = 25L * 1024 * 1024; // Groq direct-upload limit (free tier)

        /// <summary>Requested section keys filtered to the ones AI may draft; empty/null means all of them.</summary>
        public static List<string> ResolveSections(IEnumerable<string>? requested)
        {
            var list = requested?.Where(k => !string.IsNullOrWhiteSpace(k)).Select(k => k.Trim()).ToList();
            if (list == null || list.Count == 0) return DraftableSections.Keys.ToList();
            return DraftableSections.Keys.Where(k => list.Contains(k, StringComparer.OrdinalIgnoreCase)).ToList();
        }

        public static string? NormalizeLanguage(string? code)
        {
            if (string.IsNullOrWhiteSpace(code)) return null;
            var c = code.Trim().ToLowerInvariant();
            return PatientLanguages.ContainsKey(c) ? c : null;
        }

        public static string? NormalizeStyle(string? style)
        {
            if (string.IsNullOrWhiteSpace(style)) return null;
            var s = style.Trim().ToUpperInvariant();
            return Styles.Contains(s) ? s : null;
        }

        // ── De-identification ─────────────────────────────────────────────────────────────────

        private static readonly Regex AadhaarLike = new(@"\b\d{4}[\s-]?\d{4}[\s-]?\d{4}\b", RegexOptions.Compiled);
        private static readonly Regex LongDigits = new(@"(?<!\d)(?:\+?91[\s-]?)?\d{10,}(?!\d)", RegexOptions.Compiled);
        private static readonly Regex IndianMobile = new(@"(?<!\d)(?:\+?91[\s-]?)?[6-9]\d{4}[\s-]?\d{5}(?!\d)", RegexOptions.Compiled);
        private static readonly Regex Email = new(@"[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}", RegexOptions.Compiled);
        private static readonly Regex AbhaAddress = new(@"\b[\w.]+@(sbx|abdm)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>
        /// Replaces identifiers before text is sent to the AI provider. <paramref name="names"/> are
        /// people's names (patient, relatives) — the whole name and each part of 3+ letters becomes
        /// "the patient" / "the relative". <paramref name="identifiers"/> are exact strings such as
        /// UHID, admission number, phone numbers, Aadhaar and ABHA. Then any remaining 10+ digit
        /// number, Aadhaar-shaped number, email or ABHA address is masked.
        /// </summary>
        public static string Redact(string? text, IEnumerable<string?>? patientNames, IEnumerable<string?>? relativeNames = null, IEnumerable<string?>? identifiers = null)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            var result = text;

            foreach (var id in (identifiers ?? Array.Empty<string?>()).Where(s => !string.IsNullOrWhiteSpace(s) && s!.Trim().Length >= 4)
                         .Select(s => s!.Trim()).Distinct().OrderByDescending(s => s.Length))
                result = Regex.Replace(result, Regex.Escape(id), "[id]", RegexOptions.IgnoreCase);

            result = ReplaceNames(result, patientNames, "the patient");
            result = ReplaceNames(result, relativeNames, "the relative");

            result = AbhaAddress.Replace(result, "[id]");
            result = Email.Replace(result, "[email]");
            result = AadhaarLike.Replace(result, "[number]");
            result = IndianMobile.Replace(result, "[number]");
            result = LongDigits.Replace(result, "[number]");
            return result;
        }

        private static string ReplaceNames(string text, IEnumerable<string?>? names, string replacement)
        {
            var parts = new List<string>();
            foreach (var n in (names ?? Array.Empty<string?>()).Where(n => !string.IsNullOrWhiteSpace(n)))
            {
                var full = n!.Trim();
                parts.Add(full);
                parts.AddRange(full.Split(new[] { ' ', '.', ',' }, StringSplitOptions.RemoveEmptyEntries)
                    .Where(p => p.Length >= 3 && !CommonWords.Contains(p, StringComparer.OrdinalIgnoreCase)));
            }
            foreach (var p in parts.Distinct(StringComparer.OrdinalIgnoreCase).OrderByDescending(p => p.Length))
                text = Regex.Replace(text, @"\b" + Regex.Escape(p) + @"\b", replacement, RegexOptions.IgnoreCase);
            return text;
        }

        // Name parts that are also ordinary words or titles — never blanked out of clinical text.
        private static readonly string[] CommonWords = { "Kumar", "Kumari", "Devi", "Mrs", "Miss", "Shri", "Smt", "Baby", "Master", "Unknown", "Male", "Female", "Patient" };

        // ── Prompts ───────────────────────────────────────────────────────────────────────────

        private const string Rules =
            "Rules: use ONLY facts present in the input. Never invent findings, results, diagnoses, medicines, doses or dates. " +
            "If the input has nothing for a section, return an empty string for it. Write plain text, no markdown, no headings, " +
            "no bullet symbols other than '- ' at the start of a line. Doctor-facing sections: concise, professional, third person, " +
            "past tense. Patient-advice sections (follow-up, diet, activity, warning signs): short simple sentences a patient's family " +
            "can follow, second person. The person is called 'the patient'; never guess a name.";

        public static string BuildDraftSystemPrompt(IEnumerable<string> sections)
        {
            var keys = sections.ToList();
            var sb = new StringBuilder();
            sb.Append("You are a clinical documentation assistant helping a doctor at an Indian hospital write a discharge summary. ");
            sb.Append("From the hospital stay records provided, draft these sections. Respond with a JSON object with exactly these keys (all strings): ");
            sb.Append(string.Join(", ", keys.Select(k => $"\"{k}\" ({DraftableSections[k]})")));
            sb.Append(". ").Append(Rules);
            return sb.ToString();
        }

        public static string BuildDictationSystemPrompt()
        {
            var sb = new StringBuilder();
            sb.Append("You are a clinical scribe. A doctor dictated notes for a patient's discharge summary, in English, Hindi or a mix. ");
            sb.Append("Sort what was said into these keys of a JSON object (all strings, in English): ");
            sb.Append(string.Join(", ", DraftableSections.Select(s => $"\"{s.Key}\" ({s.Value})")));
            sb.Append(". Also return \"medicationChanges\": an array of objects {\"medicineName\", \"action\", \"newDose\", \"note\"} where action is ");
            sb.Append("CONTINUE, CHANGE, STOP or ADD, only for medicines the doctor actually mentioned; and \"followUpAfterDays\": a whole number ");
            sb.Append("if the doctor said when to follow up, otherwise null. Keep medicine names and doses exactly as spoken. ");
            sb.Append("The current medicine list is given for reference only; do not list a medicine the doctor did not mention. ");
            sb.Append(Rules);
            return sb.ToString();
        }

        public static string BuildRewriteSystemPrompt(string sectionKey, string style)
        {
            var heading = DraftableSections.TryGetValue(sectionKey, out var h) ? h : sectionKey;
            var how = style switch
            {
                "SHORTER" => "Make it shorter: keep every clinical fact, remove repetition, about half the length.",
                "DETAIL" => "Make it clearer and more complete using only the facts already in the text, in well-formed sentences. Do not add new facts.",
                _ => "Rewrite it in plain, simple words a patient's family can understand, in short sentences, second person.",
            };
            return $"You edit the \"{heading}\" section of a hospital discharge summary. {how} Use only facts in the text. " +
                   "Plain text only, no markdown. Respond with the rewritten text only.";
        }

        // ── Reading the answer back ───────────────────────────────────────────────────────────

        /// <summary>Reads the model's JSON object into section texts, keeping only allowed keys, cleaning
        /// markdown and capping length. Returns an empty dictionary for anything unreadable.</summary>
        public static Dictionary<string, string> ParseSections(string? json, IEnumerable<string> allowedKeys)
        {
            var result = new Dictionary<string, string>();
            var root = TryParseObject(json);
            if (root == null) return result;
            var allowed = allowedKeys.ToList();
            foreach (var prop in root.Value.EnumerateObject())
            {
                var key = allowed.FirstOrDefault(k => string.Equals(k, prop.Name, StringComparison.OrdinalIgnoreCase));
                if (key == null) continue;
                var text = prop.Value.ValueKind switch
                {
                    JsonValueKind.String => prop.Value.GetString(),
                    JsonValueKind.Array => string.Join("\n", prop.Value.EnumerateArray()
                        .Where(e => e.ValueKind == JsonValueKind.String).Select(e => "- " + e.GetString()!.Trim().TrimStart('-', '•', ' '))),
                    _ => null,
                };
                var clean = CleanText(text);
                if (!string.IsNullOrEmpty(clean)) result[key] = clean;
            }
            return result;
        }

        public sealed record MedicationChange(string MedicineName, string Action, string? NewDose, string? Note);

        public static List<MedicationChange> ParseMedicationChanges(string? json)
        {
            var list = new List<MedicationChange>();
            var root = TryParseObject(json);
            if (root == null || !root.Value.TryGetProperty("medicationChanges", out var arr) || arr.ValueKind != JsonValueKind.Array)
                return list;
            foreach (var item in arr.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object) continue;
                var name = Str(item, "medicineName");
                var action = Str(item, "action")?.ToUpperInvariant();
                if (string.IsNullOrWhiteSpace(name) || action is not ("CONTINUE" or "CHANGE" or "STOP" or "ADD")) continue;
                list.Add(new MedicationChange(name.Trim(), action, NullIfBlank(Str(item, "newDose")), NullIfBlank(Str(item, "note"))));
                if (list.Count >= 30) break;
            }
            return list;
        }

        private static readonly Regex DoseFormPrefix = new(@"^(tab|tablet|cap|capsule|inj|injection|syp|syrup|susp|drops?|oint|cream|gel|neb|inh|sachet)\.?\s+", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>Core drug word of a medicine name: "Tab. Metoprolol 25 mg" → "metoprolol".</summary>
        public static string MedicineKey(string? name)
        {
            if (string.IsNullOrWhiteSpace(name)) return string.Empty;
            var t = DoseFormPrefix.Replace(name.Trim(), string.Empty);
            var first = t.Split(new[] { ' ', '-', '(', ',', '/' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty;
            return first.ToLowerInvariant();
        }

        /// <summary>The medicine on the current list that a spoken name refers to, matched on the core drug word.</summary>
        public static string? MatchMedicine(string spokenName, IEnumerable<string?> currentMedicines)
        {
            var key = MedicineKey(spokenName);
            if (key.Length < 3) return null;
            return currentMedicines.FirstOrDefault(m =>
            {
                var k = MedicineKey(m);
                return k.Length >= 3 && (k == key || k.StartsWith(key, StringComparison.Ordinal) || key.StartsWith(k, StringComparison.Ordinal));
            });
        }

        public static int? ParseFollowUpDays(string? json)
        {
            var root = TryParseObject(json);
            if (root == null || !root.Value.TryGetProperty("followUpAfterDays", out var v)) return null;
            int days;
            if (v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out days)) { }
            else if (v.ValueKind == JsonValueKind.String && int.TryParse(v.GetString(), out days)) { }
            else return null;
            return days is >= 1 and <= 365 ? days : null;
        }

        public static string CleanText(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;
            var t = text.Replace("\r\n", "\n").Trim();
            t = Regex.Replace(t, @"^```[a-z]*\s*|\s*```$", string.Empty);
            t = Regex.Replace(t, @"\*\*(.+?)\*\*", "$1");
            t = Regex.Replace(t, @"(?m)^\s{0,3}#{1,6}\s*", string.Empty);
            t = Regex.Replace(t, @"(?m)^\s*[•*]\s+", "- ");
            t = Regex.Replace(t, @"\n{3,}", "\n\n").Trim();
            return t.Length > MaxSectionLength ? t[..MaxSectionLength].TrimEnd() : t;
        }

        /// <summary>Keeps a stored JSON column well-formed: returns compact JSON for an object, else null.</summary>
        public static string? NormalizeJsonObject(string? json)
        {
            var root = TryParseObject(json);
            return root == null ? null : JsonSerializer.Serialize(root.Value);
        }

        /// <summary>Section-source map with unknown keys and values dropped.</summary>
        public static string? NormalizeSectionSources(string? json)
        {
            var root = TryParseObject(json);
            if (root == null) return null;
            var map = new Dictionary<string, string>();
            foreach (var p in root.Value.EnumerateObject())
            {
                if (p.Value.ValueKind != JsonValueKind.String) continue;
                var v = p.Value.GetString()!.ToUpperInvariant();
                if (SectionSource.All.Contains(v) && p.Name.Length <= 60) map[p.Name] = v;
            }
            return map.Count == 0 ? null : JsonSerializer.Serialize(map);
        }

        private static JsonElement? TryParseObject(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            var t = json.Trim();
            // Models occasionally wrap JSON in prose or a code fence; take the outermost object.
            var start = t.IndexOf('{');
            var end = t.LastIndexOf('}');
            if (start < 0 || end <= start) return null;
            try
            {
                using var doc = JsonDocument.Parse(t[start..(end + 1)]);
                return doc.RootElement.ValueKind == JsonValueKind.Object ? doc.RootElement.Clone() : null;
            }
            catch (JsonException)
            {
                return null;
            }
        }

        private static string? Str(JsonElement obj, string name)
        {
            foreach (var p in obj.EnumerateObject())
                if (string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase))
                    return p.Value.ValueKind == JsonValueKind.String ? p.Value.GetString() : p.Value.ValueKind == JsonValueKind.Number ? p.Value.ToString() : null;
            return null;
        }

        private static string? NullIfBlank(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
    }
}
