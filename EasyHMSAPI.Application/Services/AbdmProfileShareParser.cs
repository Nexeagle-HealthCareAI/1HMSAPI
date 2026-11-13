using System.Text.Json;

namespace EasyHMSAPI.Application.Services
{
    public class ParsedProfileShare
    {
        public string? RequestId { get; set; }
        public string? HipId { get; set; }
        public string? CounterId { get; set; }
        public string? AbhaNumber { get; set; }
        public string? AbhaAddress { get; set; }
        public string? FullName { get; set; }
        public string? Gender { get; set; }
        public string? DateOfBirth { get; set; }
        public string? Mobile { get; set; }
        public string? Address { get; set; }
        public string? LinkToken { get; set; }
    }

    /// <summary>
    /// Parses ABDM's /v3/hip/patient/profile/share callback body. The public docs don't publish the
    /// exact JSON, so this searches the whole tree (case-insensitively, depth-first) for each field
    /// under several candidate names instead of assuming one shape — the same tolerance
    /// AbdmAbhaService.ReadString applies to ABHA responses. The raw body is stored alongside every
    /// share, so a wrong guess is fixed here in one place from real sandbox traffic.
    /// </summary>
    public static class AbdmProfileShareParser
    {
        public static ParsedProfileShare Parse(JsonElement root)
        {
            // Prefer the "patient" subtree for demographics so a stray top-level "name" (e.g. a
            // facility name) is never mistaken for the patient's.
            var patient = FindObject(root, "patient") ?? root;

            return new ParsedProfileShare
            {
                RequestId = FindString(root, "requestId", "request-id"),
                HipId = FindString(root, "hipId", "hip-id", "hipCode", "facilityId"),
                CounterId = FindString(root, "counterId", "counter-id", "counterCode"),
                AbhaNumber = FindString(patient, "abhaNumber", "ABHANumber", "healthIdNumber", "healthIdNo"),
                AbhaAddress = FindString(patient, "abhaAddress", "preferredAbhaAddress", "healthId", "phrAddress"),
                FullName = FindString(patient, "name", "fullName") ?? ComposeName(patient),
                Gender = FindString(patient, "gender", "sex"),
                DateOfBirth = FindString(patient, "dob", "dateOfBirth") ?? ComposeDob(patient),
                Mobile = FindString(patient, "mobile", "phoneNumber", "phone") ?? FindIdentifier(patient, "MOBILE"),
                Address = ComposeAddress(patient),
                LinkToken = FindString(root, "linkToken", "link-token", "token")
            };
        }

        private static string? ComposeName(JsonElement e)
        {
            var parts = new[] { FindString(e, "firstName"), FindString(e, "middleName"), FindString(e, "lastName") }
                .Where(s => !string.IsNullOrWhiteSpace(s));
            var joined = string.Join(' ', parts);
            return joined.Length > 0 ? joined : null;
        }

        private static string? ComposeDob(JsonElement e)
        {
            var y = FindString(e, "yearOfBirth");
            if (string.IsNullOrWhiteSpace(y)) return null;
            var m = FindString(e, "monthOfBirth");
            var d = FindString(e, "dayOfBirth");
            return string.IsNullOrWhiteSpace(m) || string.IsNullOrWhiteSpace(d)
                ? y
                : $"{d!.PadLeft(2, '0')}-{m!.PadLeft(2, '0')}-{y}";
        }

        // Address can arrive as a plain string or as { line, district, state, pincode }.
        private static string? ComposeAddress(JsonElement patient)
        {
            if (!TryFind(patient, "address", out var addr)) return null;
            if (addr.ValueKind == JsonValueKind.String) return NullIfBlank(addr.GetString());
            if (addr.ValueKind != JsonValueKind.Object) return null;

            var parts = new[]
            {
                FindString(addr, "line", "addressLine", "street"),
                FindString(addr, "district", "districtName"),
                FindString(addr, "state", "stateName"),
                FindString(addr, "pincode", "pinCode")
            }.Where(s => !string.IsNullOrWhiteSpace(s));
            var joined = string.Join(", ", parts);
            return joined.Length > 0 ? joined : null;
        }

        // Older share payloads carry phone numbers as identifiers: [{ "type": "MOBILE", "value": "..." }].
        private static string? FindIdentifier(JsonElement e, string type)
        {
            if (!TryFind(e, "identifiers", out var list) || list.ValueKind != JsonValueKind.Array) return null;
            foreach (var item in list.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object) continue;
                var t = FindString(item, "type");
                if (string.Equals(t, type, StringComparison.OrdinalIgnoreCase))
                    return FindString(item, "value");
            }
            return null;
        }

        private static string? FindString(JsonElement root, params string[] names)
        {
            foreach (var name in names)
            {
                if (TryFind(root, name, out var value))
                {
                    var s = value.ValueKind switch
                    {
                        JsonValueKind.String => value.GetString(),
                        JsonValueKind.Number => value.GetRawText(),
                        _ => null
                    };
                    if (!string.IsNullOrWhiteSpace(s)) return s.Trim();
                }
            }
            return null;
        }

        private static JsonElement? FindObject(JsonElement root, string name) =>
            TryFind(root, name, out var v) && v.ValueKind == JsonValueKind.Object ? v : null;

        // Depth-first, case-insensitive property lookup anywhere in the tree.
        private static bool TryFind(JsonElement element, string name, out JsonElement value)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    foreach (var prop in element.EnumerateObject())
                    {
                        if (string.Equals(prop.Name, name, StringComparison.OrdinalIgnoreCase))
                        {
                            value = prop.Value;
                            return true;
                        }
                    }
                    foreach (var prop in element.EnumerateObject())
                    {
                        if (TryFind(prop.Value, name, out value)) return true;
                    }
                    break;
                case JsonValueKind.Array:
                    foreach (var item in element.EnumerateArray())
                    {
                        if (TryFind(item, name, out value)) return true;
                    }
                    break;
            }
            value = default;
            return false;
        }

        private static string? NullIfBlank(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
    }
}
