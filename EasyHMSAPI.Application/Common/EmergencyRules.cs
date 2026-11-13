using EasyHMSAPI.Domain.Entities;

namespace EasyHMSAPI.Application.Common
{
    /// <summary>
    /// Shared rules of the emergency takeover (Vita vision, Phase 3): which places are listed, how they are ranked, how numbers and
    /// capabilities are cleaned, and the opt-in validation used by both the hospital form (1HMS portal) and the CMS.
    /// </summary>
    public static class EmergencyRules
    {
        /// <summary>The takeover terms a hospital accepts to be listed and alerted. Bump when the wording changes (legal review),
        /// and every hospital must accept again before it can switch the takeover on.</summary>
        public const string TermsVersion = "2026-10";

        public const int MaxPlaces = 5;          // vision: alert the top 5 nearest
        public const double NearKm = 25;          // nearest first within 25 km ...
        public const int MinPlaces = 3;           // ... then the nearest beyond that while fewer than three were found
        public const double CapabilityLeewayKm = 2; // "a capability match moves a hospital up only among hospitals within a few minutes"
        public static readonly TimeSpan RepeatWindow = TimeSpan.FromMinutes(30); // one chat session alerts once per emergency
        public static readonly TimeSpan ShareWindow = TimeSpan.FromHours(2);     // a number can be shared for this long after the alert
        private static readonly TimeSpan IstOffset = TimeSpan.FromHours(5.5);

        public static readonly string[] Capabilities = { "casualty", "icu", "trauma", "cardiac", "maternity", "paediatric", "ambulance" };

        public static string? CleanText(string? value, int max)
        {
            var flat = string.Join(' ', (value ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
            if (flat.Length == 0) return null;
            return flat.Length <= max ? flat : flat[..max];
        }

        /// <summary>A number a patient can call: digits only, +91 / leading 0 kept as dialled. Short codes (102, 108) are allowed for
        /// ambulance services a CMS admin adds. Null when it is not a plausible number.</summary>
        public static string? CleanPhone(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            var digits = new string(value.Where(char.IsDigit).ToArray());
            if (digits.Length == 12 && digits.StartsWith("91")) digits = digits[2..];
            return digits.Length is >= 3 and <= 12 ? digits : null;
        }

        /// <summary>A 10-digit Indian mobile (WhatsApp alerts, a patient's number). Null otherwise.</summary>
        public static string? CleanMobile(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            var digits = new string(value.Where(char.IsDigit).ToArray());
            if (digits.Length == 12 && digits.StartsWith("91")) digits = digits[2..];
            if (digits.Length == 11 && digits.StartsWith("0")) digits = digits[1..];
            return digits.Length == 10 && digits[0] >= '6' ? digits : null;
        }

        public static string? CleanCapabilities(IEnumerable<string>? values)
        {
            var list = (values ?? Array.Empty<string>())
                .Select(v => (v ?? string.Empty).Trim().ToLowerInvariant())
                .Select(v => v == "pediatric" ? "paediatric" : v)
                .Where(v => Capabilities.Contains(v)).Distinct().ToList();
            return list.Count == 0 ? null : string.Join(',', list);
        }

        public static string[] CapabilityList(string? stored)
            => string.IsNullOrWhiteSpace(stored) ? Array.Empty<string>() : stored.Split(',', StringSplitOptions.RemoveEmptyEntries);

        public static bool IsOpenAt(HospitalEmergencySetting s, DateTime utcNow)
        {
            if (s.Is24x7 || s.OpenTime == null || s.CloseTime == null) return true;
            var t = (utcNow + IstOffset).TimeOfDay;
            var open = s.OpenTime.Value;
            var close = s.CloseTime.Value;
            return open <= close ? t >= open && t < close : t >= open || t < close;   // overnight hours, e.g. 20:00 to 08:00
        }

        public static double DistanceKm(double lat1, double lng1, double lat2, double lng2)
        {
            const double r = 6371.0;
            double Rad(double d) => d * Math.PI / 180.0;
            var dLat = Rad(lat2 - lat1);
            var dLng = Rad(lng2 - lng1);
            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) + Math.Cos(Rad(lat1)) * Math.Cos(Rad(lat2)) * Math.Sin(dLng / 2) * Math.Sin(dLng / 2);
            return r * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        }

        public static string IstClock(DateTime utc) => (utc + IstOffset).ToString("HH:mm");

        /// <summary>
        /// Nearest first, open now only: everything within 25 km; when fewer than three are that close, the nearest beyond 25 km
        /// until there are three. A place with the capability asked for (cardiac, maternity, ...) moves ahead of others that are at
        /// most 2 km nearer. At most five.
        /// </summary>
        public static List<(HospitalEmergencySetting Place, double Km)> Rank(IEnumerable<HospitalEmergencySetting> listed, double lat, double lng,
            string? capability, DateTime utcNow, int take = MaxPlaces)
        {
            take = Math.Clamp(take, 1, MaxPlaces);
            var cap = (capability ?? string.Empty).Trim().ToLowerInvariant();
            var open = listed.Where(p => IsOpenAt(p, utcNow))
                .Select(p => (Place: p, Km: DistanceKm(lat, lng, (double)p.Latitude, (double)p.Longitude)))
                .OrderBy(x => x.Km).ToList();
            var near = open.Where(x => x.Km <= NearKm).ToList();
            var chosen = near.Count >= MinPlaces ? near : near.Concat(open.Where(x => x.Km > NearKm).Take(MinPlaces - near.Count)).ToList();
            double Score((HospitalEmergencySetting Place, double Km) x)
                => x.Km - (cap.Length > 0 && CapabilityList(x.Place.Capabilities).Contains(cap) ? CapabilityLeewayKm : 0);
            return chosen.OrderBy(Score).ThenBy(x => x.Km).Take(take).ToList();
        }

        /// <summary>The fields a hospital or the CMS fills in, checked the same way for both. Returns the problem, or null.</summary>
        public static string? Validate(EmergencyPlaceInput i)
        {
            if (CleanPhone(i.EmergencyPhone) == null) return "Enter the 24x7 emergency phone number.";
            if (!string.IsNullOrWhiteSpace(i.AlternatePhone) && CleanPhone(i.AlternatePhone) == null) return "The alternate number does not look right.";
            if (!string.IsNullOrWhiteSpace(i.AlertWhatsApp) && CleanMobile(i.AlertWhatsApp) == null) return "The WhatsApp number for alerts must be a 10-digit mobile.";
            if (!string.IsNullOrWhiteSpace(i.DutyContactPhone) && CleanPhone(i.DutyContactPhone) == null) return "The duty contact's number does not look right.";
            if (i.Latitude is null || i.Longitude is null || i.Latitude < -90 || i.Latitude > 90 || i.Longitude < -180 || i.Longitude > 180
                || (i.Latitude == 0 && i.Longitude == 0)) return "Set the map pin (latitude and longitude).";
            if (!i.Is24x7)
            {
                if (ParseTime(i.OpenTime) == null || ParseTime(i.CloseTime) == null) return "Enter the emergency hours (open and close, HH:mm) or choose 24x7.";
                if (ParseTime(i.OpenTime) == ParseTime(i.CloseTime)) return "Open and close times cannot be the same; choose 24x7 instead.";
            }
            if (i.Kind is not null && i.Kind != "HOSPITAL" && i.Kind != "AMBULANCE") return "Kind must be HOSPITAL or AMBULANCE.";
            return null;
        }

        public static TimeSpan? ParseTime(string? hhmm)
            => TimeSpan.TryParseExact((hhmm ?? string.Empty).Trim(), new[] { @"hh\:mm", @"h\:mm", @"hh\:mm\:ss" }, null, out var t) && t < TimeSpan.FromDays(1) ? t : null;

        /// <summary>Copies the input onto the row (after Validate). True when a field a patient relies on changed (numbers, pin, hours),
        /// which sends a hospital's verified listing back to the CMS for checking.</summary>
        public static bool Apply(HospitalEmergencySetting row, EmergencyPlaceInput i)
        {
            var before = (row.EmergencyPhone, row.AlternatePhone, row.AlertWhatsApp, row.Latitude, row.Longitude, row.Is24x7, row.OpenTime, row.CloseTime);
            row.Kind = i.Kind ?? row.Kind ?? "HOSPITAL";
            row.EmergencyPhone = CleanPhone(i.EmergencyPhone)!;
            row.AlternatePhone = CleanPhone(i.AlternatePhone);
            row.AlertWhatsApp = CleanMobile(i.AlertWhatsApp);
            row.Latitude = Math.Round(i.Latitude!.Value, 6);
            row.Longitude = Math.Round(i.Longitude!.Value, 6);
            row.Landmark = CleanText(i.Landmark, 200);
            row.Address = CleanText(i.Address, 400);
            row.City = CleanText(i.City, 100);
            row.Is24x7 = i.Is24x7;
            row.OpenTime = i.Is24x7 ? null : ParseTime(i.OpenTime);
            row.CloseTime = i.Is24x7 ? null : ParseTime(i.CloseTime);
            row.Capabilities = CleanCapabilities(i.Capabilities);
            row.DutyContactName = CleanText(i.DutyContactName, 100);
            row.DutyContactPhone = CleanPhone(i.DutyContactPhone);
            var after = (row.EmergencyPhone, row.AlternatePhone, row.AlertWhatsApp, row.Latitude, row.Longitude, row.Is24x7, row.OpenTime, row.CloseTime);
            return before != after;
        }
    }

    /// <summary>The opt-in fields (vision: "What a hospital fills in to opt in"), shared by the hospital form and the CMS.</summary>
    public class EmergencyPlaceInput
    {
        public string? Kind { get; set; }
        public string? EmergencyPhone { get; set; }
        public string? AlternatePhone { get; set; }
        public string? AlertWhatsApp { get; set; }
        public decimal? Latitude { get; set; }
        public decimal? Longitude { get; set; }
        public string? Landmark { get; set; }
        public string? Address { get; set; }
        public string? City { get; set; }
        public bool Is24x7 { get; set; } = true;
        public string? OpenTime { get; set; }
        public string? CloseTime { get; set; }
        public List<string>? Capabilities { get; set; }
        public string? DutyContactName { get; set; }
        public string? DutyContactPhone { get; set; }
    }
}
