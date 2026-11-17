using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace EasyHMSAPI.Application.Services
{
    /// <summary>What ABDM tells us about the person searching for their records.</summary>
    public sealed class AbdmDiscoverRequest
    {
        public string? TransactionId { get; init; }
        /// <summary>patient.id: the ABHA address of the person searching.</summary>
        public string? AbhaAddress { get; init; }
        public string? Name { get; init; }
        public string? Gender { get; init; }
        public int? YearOfBirth { get; init; }
        /// <summary>Identifiers ABDM has itself verified (type → value), e.g. MOBILE, ABHA_NUMBER.</summary>
        public IReadOnlyList<KeyValuePair<string, string>> Verified { get; init; } = Array.Empty<KeyValuePair<string, string>>();
        /// <summary>Identifiers the patient typed (e.g. MR = our patient ID). Never proof on their own.</summary>
        public IReadOnlyList<KeyValuePair<string, string>> Unverified { get; init; } = Array.Empty<KeyValuePair<string, string>>();

        public string? VerifiedValue(string type) =>
            Verified.FirstOrDefault(v => string.Equals(v.Key, type, StringComparison.OrdinalIgnoreCase)).Value;

        public string? UnverifiedValue(string type) =>
            Unverified.FirstOrDefault(v => string.Equals(v.Key, type, StringComparison.OrdinalIgnoreCase)).Value;
    }

    /// <summary>The facts about one of our patients that discovery matches on.</summary>
    public sealed record AbdmDiscoveryCandidate(
        string PatientId, string? FullName, string? Mobile, string? Sex, int? YearOfBirth, bool YearIsExact, string? AbhaId, DateTime? RegisteredAt);

    public sealed record AbdmDiscoveryMatch(AbdmDiscoveryCandidate Patient, IReadOnlyList<string> MatchedBy);

    /// <summary>
    /// The rules for answering a discovery (ABDM M2, user-initiated linking). Pure, so every rule is
    /// tested without a database. The principle: only identifiers ABDM verified can find a patient,
    /// and a match must be to exactly one person — a wrong match would show one patient another
    /// patient's visits.
    /// </summary>
    public static class AbdmDiscoveryRules
    {
        public const int OtpMaxAttempts = 3;
        public static readonly TimeSpan OtpLifetime = TimeSpan.FromMinutes(10);

        public static AbdmDiscoverRequest Parse(string? body)
        {
            if (string.IsNullOrWhiteSpace(body))
                return new AbdmDiscoverRequest();
            try
            {
                using var doc = JsonDocument.Parse(body);
                var root = doc.RootElement;
                if (root.ValueKind != JsonValueKind.Object)
                    return new AbdmDiscoverRequest();
                var patient = Property(root, "patient");
                var hasPatient = patient.ValueKind == JsonValueKind.Object;
                return new AbdmDiscoverRequest
                {
                    TransactionId = Text(root, "transactionId"),
                    AbhaAddress = hasPatient ? Text(patient, "id") : null,
                    Name = hasPatient ? Text(patient, "name") : null,
                    Gender = hasPatient ? Text(patient, "gender") : null,
                    YearOfBirth = hasPatient ? Number(patient, "yearOfBirth") : null,
                    Verified = hasPatient ? Identifiers(patient, "verifiedIdentifiers") : Array.Empty<KeyValuePair<string, string>>(),
                    Unverified = hasPatient ? Identifiers(patient, "unverifiedIdentifiers") : Array.Empty<KeyValuePair<string, string>>()
                };
            }
            catch (JsonException)
            {
                return new AbdmDiscoverRequest();
            }
        }

        /// <summary>The last ten digits of a mobile, or null when there are fewer.</summary>
        public static string? Mobile10(string? value)
        {
            var digits = AbdmCareContextRules.Digits(value);
            return digits.Length >= 10 ? digits[^10..] : null;
        }

        /// <summary>The spellings an ABHA number may be stored under: as given, digits only, and 2-4-4-4.</summary>
        public static IReadOnlyList<string> AbhaNumberVariants(string? abhaNumber)
        {
            var digits = AbdmCareContextRules.Digits(abhaNumber);
            if (digits.Length != 14)
                return Array.Empty<string>();
            return new[] { digits, $"{digits[..2]}-{digits[2..6]}-{digits[6..10]}-{digits[10..]}" }
                .Append(abhaNumber!.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// <summary>
        /// Picks the one patient the request is about, or null.
        /// 1. ABHA (number or address ABDM verified) equal to the ABHA on our record: enough on its own.
        /// 2. Otherwise a verified mobile, and then gender, year of birth and name must all agree
        ///    where both sides know them — families share a phone.
        /// More than one patient left: the patient ID they typed (MR) may choose between them, and
        /// if it does not, nobody matches.
        /// </summary>
        public static AbdmDiscoveryMatch? Match(AbdmDiscoverRequest request, IEnumerable<AbdmDiscoveryCandidate> candidates)
        {
            var all = candidates.ToList();
            var mr = request.UnverifiedValue("MR")?.Trim();

            var abhaDigits = AbdmCareContextRules.Digits(request.VerifiedValue("ABHA_NUMBER"));
            var abhaAddress = request.AbhaAddress?.Trim();
            var byAbha = all.Where(c => !string.IsNullOrWhiteSpace(c.AbhaId) && (
                    (abhaDigits.Length == 14 && !c.AbhaId!.Contains('@') && AbdmCareContextRules.Digits(c.AbhaId) == abhaDigits)
                    || (!string.IsNullOrEmpty(abhaAddress) && string.Equals(c.AbhaId!.Trim(), abhaAddress, StringComparison.OrdinalIgnoreCase))))
                .ToList();
            var chosen = One(byAbha, mr);
            if (chosen != null)
                return new AbdmDiscoveryMatch(chosen, MatchedBy("ABHA_NUMBER", chosen, mr));
            if (byAbha.Count > 1)
                return null;

            var mobile = Mobile10(request.VerifiedValue("MOBILE"));
            if (mobile == null)
                return null;
            var gender = AbdmCareContextRules.GenderCode(request.Gender);
            var byMobile = all.Where(c => Mobile10(c.Mobile) == mobile
                    && GenderAgrees(gender, c.Sex)
                    && YearAgrees(request.YearOfBirth, c)
                    && NameAgrees(request.Name, c.FullName))
                .ToList();
            chosen = One(byMobile, mr);
            return chosen == null ? null : new AbdmDiscoveryMatch(chosen, MatchedBy("MOBILE", chosen, mr));
        }

        private static AbdmDiscoveryCandidate? One(List<AbdmDiscoveryCandidate> found, string? mr)
        {
            if (found.Count == 1)
                return found[0];
            if (found.Count > 1 && !string.IsNullOrEmpty(mr))
            {
                var byMr = found.Where(c => string.Equals(c.PatientId, mr, StringComparison.OrdinalIgnoreCase)).ToList();
                if (byMr.Count == 1)
                    return byMr[0];
            }
            return null;
        }

        private static IReadOnlyList<string> MatchedBy(string how, AbdmDiscoveryCandidate chosen, string? mr) =>
            !string.IsNullOrEmpty(mr) && string.Equals(chosen.PatientId, mr, StringComparison.OrdinalIgnoreCase)
                ? new[] { how, "MR" }
                : new[] { how };

        public static bool GenderAgrees(string? requestCode, string? ourSex)
        {
            var ours = AbdmCareContextRules.GenderCode(ourSex);
            return requestCode == null || ours == null || requestCode == ours;
        }

        /// <summary>A recorded date of birth may be a year out (ABDM often holds 1 January); an age
        /// typed at the counter may be two.</summary>
        public static bool YearAgrees(int? requestYear, AbdmDiscoveryCandidate candidate)
        {
            if (requestYear == null || candidate.YearOfBirth == null)
                return true;
            return Math.Abs(requestYear.Value - candidate.YearOfBirth.Value) <= (candidate.YearIsExact ? 1 : 2);
        }

        /// <summary>At least one name word of three letters or more in common.</summary>
        public static bool NameAgrees(string? requestName, string? ourName)
        {
            var theirs = NameWords(requestName);
            var ours = NameWords(ourName);
            if (theirs.Count == 0 || ours.Count == 0)
                return false;
            return theirs.Overlaps(ours);
        }

        private static HashSet<string> NameWords(string? name) =>
            (name ?? string.Empty)
                .Split(new[] { ' ', '.', ',', '-', '\t' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(w => w.Trim().ToLowerInvariant())
                .Where(w => w.Length >= 3)
                .ToHashSet();

        /// <summary>Year of birth as we know it: from the date of birth, else worked back from the
        /// age given at registration.</summary>
        public static (int? Year, bool Exact) YearOfBirth(DateTime? dateOfBirth, short? age, string? ageUnit, DateTime? registeredAt)
        {
            if (dateOfBirth != null)
                return (dateOfBirth.Value.Year, true);
            if (age == null || registeredAt == null)
                return (null, false);
            var unit = (ageUnit ?? "Y").Trim().ToUpperInvariant();
            var years = unit.StartsWith('M') || unit.StartsWith('D') ? 0 : age.Value;
            return (registeredAt.Value.Year - years, false);
        }

        // ---- init / confirm ------------------------------------------------------------------

        /// <summary>Every care context reference number the init request asks to link.</summary>
        public static IReadOnlyList<string> RequestedReferences(string? body)
        {
            var found = new List<string>();
            if (string.IsNullOrWhiteSpace(body))
                return found;
            try
            {
                using var doc = JsonDocument.Parse(body);
                Collect(doc.RootElement, insideCareContexts: false, found);
            }
            catch (JsonException)
            {
                // Unreadable body: nothing was asked for.
            }
            return found.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        private static void Collect(JsonElement element, bool insideCareContexts, List<string> found)
        {
            if (element.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in element.EnumerateObject())
                {
                    if (insideCareContexts
                        && string.Equals(property.Name, "referenceNumber", StringComparison.OrdinalIgnoreCase)
                        && property.Value.ValueKind == JsonValueKind.String
                        && !string.IsNullOrWhiteSpace(property.Value.GetString()))
                        found.Add(property.Value.GetString()!.Trim());
                    else
                        Collect(property.Value, insideCareContexts || string.Equals(property.Name, "careContexts", StringComparison.OrdinalIgnoreCase), found);
                }
            }
            else if (element.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in element.EnumerateArray())
                    Collect(item, insideCareContexts, found);
            }
        }

        public static string NewOtp() => RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");

        /// <summary>The OTP is stored only as this hash, salted with the link reference it belongs to.</summary>
        public static string OtpHash(string otp, string linkReference) =>
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{linkReference}:{otp.Trim()}")));

        public static bool OtpMatches(string? supplied, string linkReference, string? storedHash)
        {
            if (string.IsNullOrWhiteSpace(supplied) || string.IsNullOrEmpty(storedHash))
                return false;
            return CryptographicOperations.FixedTimeEquals(
                Encoding.ASCII.GetBytes(OtpHash(supplied, linkReference)), Encoding.ASCII.GetBytes(storedHash));
        }

        /// <summary>"******3210": enough for the patient to know which phone to look at.</summary>
        public static string MaskedMobile(string? mobile)
        {
            var ten = Mobile10(mobile);
            return ten == null ? "your registered mobile" : "******" + ten[^4..];
        }

        public static IReadOnlyList<string> SplitReferences(string? joined) =>
            (joined ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        // ---- JSON helpers --------------------------------------------------------------------

        private static JsonElement Property(JsonElement element, string name)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
                    return property.Value;
            }
            return default;
        }

        private static string? Text(JsonElement element, string name)
        {
            var value = Property(element, name);
            return value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString()) ? value.GetString()!.Trim() : null;
        }

        private static int? Number(JsonElement element, string name)
        {
            var value = Property(element, name);
            if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var n))
                return n;
            return value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out var parsed) ? parsed : null;
        }

        private static IReadOnlyList<KeyValuePair<string, string>> Identifiers(JsonElement patient, string name)
        {
            var list = new List<KeyValuePair<string, string>>();
            var array = Property(patient, name);
            if (array.ValueKind != JsonValueKind.Array)
                return list;
            foreach (var item in array.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object)
                    continue;
                var type = Text(item, "type");
                var value = Text(item, "value");
                if (type != null && value != null)
                    list.Add(new KeyValuePair<string, string>(type, value));
            }
            return list;
        }
    }
}
