using System.Net.Mail;

namespace EasyHMSAPI.Application.Services
{
    /// <summary>What a patient may save on their own account profile, and how it is cleaned.</summary>
    public static class PatientProfileRules
    {
        public const int MaxName = 200, MaxEmail = 200, MaxGuardianName = 200, MaxRelation = 50, MaxAge = 120;
        public static readonly string[] Sexes = { "Male", "Female", "Other" };
        public static readonly string[] AgeUnits = { "Y", "M", "D" };

        public static string? Clean(string? v)
        {
            var t = v == null ? null : string.Join(' ', v.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
            return string.IsNullOrEmpty(t) ? null : t;
        }

        /// <summary>"male", "M" and "MALE" are all Male; anything else is not a sex we store.</summary>
        public static string? NormalizeSex(string? sex) => Clean(sex)?.ToUpperInvariant() switch
        {
            null => null,
            "M" or "MALE" => "Male",
            "F" or "FEMALE" => "Female",
            "O" or "OTHER" => "Other",
            _ => "?",
        };

        public static bool IsValidEmail(string email)
        {
            if (email.Length > MaxEmail || email.Contains(' ')) return false;
            try { return new MailAddress(email).Address == email && email.Contains('.', StringComparison.Ordinal); }
            catch (FormatException) { return false; }
        }

        /// <summary>The first problem with what was sent, or null.</summary>
        public static string? Validate(string? fullName, int? age, string? ageUnit, string? sex, string? email, string? guardianName, string? guardianRelation)
        {
            var name = Clean(fullName);
            if (name == null) return "Enter your full name.";
            if (name.Length < 2) return "Your name is too short.";
            if (name.Length > MaxName) return $"Your name is too long (max {MaxName} characters).";
            if (age.HasValue && (age < 0 || age > MaxAge)) return $"Age must be between 0 and {MaxAge}.";
            var unit = Clean(ageUnit)?.ToUpperInvariant();
            if (unit != null && !AgeUnits.Contains(unit)) return "Age unit must be years, months or days.";
            if (NormalizeSex(sex) == "?") return "Choose Male, Female or Other.";
            var mail = Clean(email);
            if (mail != null && !IsValidEmail(mail)) return "Enter a valid email address.";
            if ((Clean(guardianName)?.Length ?? 0) > MaxGuardianName) return $"The guardian's name is too long (max {MaxGuardianName} characters).";
            if ((Clean(guardianRelation)?.Length ?? 0) > MaxRelation) return $"The relation is too long (max {MaxRelation} characters).";
            return null;
        }
    }
}
