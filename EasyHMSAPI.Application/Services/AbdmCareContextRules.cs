using EasyHMSAPI.Domain.Entities;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace EasyHMSAPI.Application.Services
{
    /// <summary>The fixed rules for what a care context says to ABDM.</summary>
    public static class AbdmCareContextRules
    {
        private static readonly TimeSpan Ist = TimeSpan.FromMinutes(330);

        /// <summary>Our own reference: resolves straight back to the visit or admission.</summary>
        public static string ReferenceNumber(string sourceType, Guid sourceId) => $"{sourceType}-{sourceId:N}";

        /// <summary>The title a patient reads in their ABHA app months later. It names the visit
        /// and its date, never a diagnosis or result.</summary>
        public static string Display(string sourceType, DateTime whenUtc)
        {
            var istDate = DateTime.SpecifyKind(whenUtc, DateTimeKind.Utc).Add(Ist).ToString("dd MMM yyyy", CultureInfo.InvariantCulture);
            return sourceType == AbdmCareContextSource.Ipd
                ? $"Hospital admission from {istDate}"
                : $"OPD visit on {istDate}";
        }

        /// <summary>ABDM HI types for the records this visit actually holds, in a fixed order. Empty
        /// means there is nothing to link: a care context with no records behind it is a promise
        /// the next discovery cannot keep.</summary>
        public static IReadOnlyList<string> HiTypes(bool prescription, bool vitals, bool labReport, bool dischargeSummary)
        {
            var types = new List<string>();
            if (prescription) types.Add("Prescription");
            if (labReport) types.Add("DiagnosticReport");
            if (dischargeSummary) types.Add("DischargeSummary");
            if (vitals) types.Add("WellnessRecord");
            return types;
        }

        /// <summary>ABDM's M / F / O. Null when the value is unknown — never guessed.</summary>
        public static string? GenderCode(string? value) =>
            value?.Trim().ToUpperInvariant() switch
            {
                "M" or "MALE" => "M",
                "F" or "FEMALE" => "F",
                "O" or "OTHER" or "T" or "TRANSGENDER" => "O",
                _ => null
            };

        /// <summary>Year of birth from an ABDM date string ("15-08-1990", "1990-08-15", "1990")
        /// or, failing that, our own DateOfBirth. Never estimated from an age.</summary>
        public static int? YearOfBirth(string? abdmDob, DateTime? registeredDob)
        {
            if (!string.IsNullOrWhiteSpace(abdmDob))
            {
                var digits = abdmDob.Trim();
                foreach (var part in digits.Split('-', '/', ' '))
                {
                    if (part.Length == 4 && int.TryParse(part, out var year) && year > 1900)
                        return year;
                }
            }
            return registeredDob?.Year;
        }

        public static string Digits(string? value) =>
            new((value ?? string.Empty).Where(char.IsDigit).ToArray());

        /// <summary>The 14-digit ABHA number as the number ABDM's bodies type it, or null.</summary>
        public static long? AbhaNumberValue(string? abhaNumber)
        {
            var digits = Digits(abhaNumber);
            return digits.Length == 14 && long.TryParse(digits, out var n) ? n : null;
        }

        /// <summary>The "exp" claim of a JWT, read without a library (the claims are base64url
        /// JSON). Null when the token carries none or is not a JWT.</summary>
        public static DateTime? JwtExpiry(string? token)
        {
            var parts = token?.Split('.');
            if (parts == null || parts.Length < 2)
                return null;
            try
            {
                var payload = parts[1].Replace('-', '+').Replace('_', '/');
                payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');
                using var doc = JsonDocument.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(payload)));
                return doc.RootElement.TryGetProperty("exp", out var exp) && exp.TryGetInt64(out var seconds)
                    ? DateTimeOffset.FromUnixTimeSeconds(seconds).UtcDateTime
                    : null;
            }
            catch (Exception ex) when (ex is FormatException or JsonException or ArgumentException)
            {
                return null;
            }
        }

        /// <summary>A token is used only with a minute to spare; one with no expiry is trusted
        /// until ABDM refuses it.</summary>
        public static bool IsUsable(DateTime? expiresAt, DateTime nowUtc) =>
            expiresAt == null || expiresAt.Value > nowUtc.AddMinutes(1);

        /// <summary>First string value under <paramref name="name"/> anywhere in a JSON body: the
        /// on-generate-token payload is not documented, so it is searched rather than assumed.</summary>
        public static string? FindString(string? body, string name)
        {
            if (string.IsNullOrWhiteSpace(body))
                return null;
            try
            {
                using var doc = JsonDocument.Parse(body);
                return Find(doc.RootElement, name);
            }
            catch (JsonException)
            {
                return null;
            }
        }

        private static string? Find(JsonElement element, string name)
        {
            if (element.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in element.EnumerateObject())
                {
                    if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase)
                        && property.Value.ValueKind == JsonValueKind.String
                        && !string.IsNullOrWhiteSpace(property.Value.GetString()))
                        return property.Value.GetString();
                }
                foreach (var property in element.EnumerateObject())
                {
                    var found = Find(property.Value, name);
                    if (found != null) return found;
                }
            }
            else if (element.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in element.EnumerateArray())
                {
                    var found = Find(item, name);
                    if (found != null) return found;
                }
            }
            return null;
        }
    }
}
