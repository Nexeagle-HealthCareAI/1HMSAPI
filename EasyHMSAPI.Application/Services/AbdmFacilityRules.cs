using System.Text.Json;
using System.Text.RegularExpressions;

namespace EasyHMSAPI.Application.Services
{
    /// <summary>The names ABDM's facility registry accepts when a facility is linked to our bridge.</summary>
    public static class AbdmFacilityRules
    {
        /// <summary>The name patients see when they search for the hospital in their ABHA or PHR app:
        /// letters, digits and spaces only, at most 15 characters.</summary>
        public static string HipName(string? hospitalName)
        {
            var clean = Regex.Replace(hospitalName ?? string.Empty, "[^A-Za-z0-9 ]", " ");
            clean = Regex.Replace(clean, @"\s+", " ").Trim();
            if (clean.Length > 15) clean = clean[..15].TrimEnd();
            return clean.Length == 0 ? "Hospital" : clean;
        }

        /// <summary>The facility name: letters, digits and - _ . ( ) , / with single spaces, starting
        /// with a letter or a digit.</summary>
        public static string FacilityName(string? hospitalName)
        {
            var clean = Regex.Replace(hospitalName ?? string.Empty, @"[^A-Za-z0-9\-_.(),/ ]", " ");
            clean = Regex.Replace(clean, @"\s+", " ").Trim();
            clean = Regex.Replace(clean, "^[^A-Za-z0-9]+", string.Empty);
            return clean.Length == 0 ? "Hospital" : clean;
        }

        public static bool IsValidHipName(string? name) =>
            !string.IsNullOrWhiteSpace(name) && name.Length <= 15 && Regex.IsMatch(name, "^[A-Za-z0-9 ]+$");

        /// <summary>The roles ABDM's answer to link-services says are now linked (e.g. "HIP", "HIU"),
        /// read from servicesLinked.types. Empty when nothing was linked.</summary>
        public static IReadOnlyList<string> LinkedTypes(string? answer)
        {
            var types = new List<string>();
            if (string.IsNullOrWhiteSpace(answer))
                return types;
            try
            {
                using var doc = JsonDocument.Parse(answer);
                var items = doc.RootElement.ValueKind == JsonValueKind.Array
                    ? doc.RootElement.EnumerateArray().ToList()
                    : new List<JsonElement> { doc.RootElement };
                foreach (var item in items)
                {
                    if (item.ValueKind != JsonValueKind.Object || !item.TryGetProperty("servicesLinked", out var linked) || linked.ValueKind != JsonValueKind.Object)
                        continue;
                    if (linked.TryGetProperty("active", out var active) && active.ValueKind == JsonValueKind.False)
                        continue;
                    if (linked.TryGetProperty("types", out var t) && t.ValueKind == JsonValueKind.Array)
                        foreach (var type in t.EnumerateArray())
                            if (type.ValueKind == JsonValueKind.String && !types.Contains(type.GetString()!, StringComparer.OrdinalIgnoreCase))
                                types.Add(type.GetString()!.ToUpperInvariant());
                }
            }
            catch (JsonException)
            {
            }
            return types;
        }

        /// <summary>The refusal inside ABDM's answer to link-services, or null when there is none.
        /// ABDM answers HTTP 200 even when it refuses, with an array of results such as
        /// [{"error":{"code":"2500","message":"facility id is not registered in HFR or invalid"}}].</summary>
        public static (string? Code, string Message)? LinkError(string? answer)
        {
            if (string.IsNullOrWhiteSpace(answer))
                return null;
            try
            {
                using var doc = JsonDocument.Parse(answer);
                var items = doc.RootElement.ValueKind == JsonValueKind.Array
                    ? doc.RootElement.EnumerateArray().ToList()
                    : new List<JsonElement> { doc.RootElement };
                foreach (var item in items)
                {
                    if (item.ValueKind != JsonValueKind.Object || !item.TryGetProperty("error", out var error) || error.ValueKind != JsonValueKind.Object)
                        continue;
                    var code = error.TryGetProperty("code", out var c) ? (c.ValueKind == JsonValueKind.String ? c.GetString() : c.ToString()) : null;
                    var message = error.TryGetProperty("message", out var m) && m.ValueKind == JsonValueKind.String ? m.GetString() : null;
                    return (code, string.IsNullOrWhiteSpace(message) ? "ABDM refused the link." : message!.Trim());
                }
            }
            catch (JsonException)
            {
            }
            return null;
        }
    }
}
