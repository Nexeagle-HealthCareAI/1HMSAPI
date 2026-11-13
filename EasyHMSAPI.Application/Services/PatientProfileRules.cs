using System.Net.Mail;

namespace EasyHMSAPI.Application.Services
{
    /// <summary>What a patient may save on their own account profile, and how it is cleaned.</summary>
    public static class PatientProfileRules
    {
        public const int MaxName = 200, MaxEmail = 200, MaxGuardianName = 200, MaxRelation = 50, MaxAge = 120;
        public static readonly string[] Sexes = { "Male", "Female", "Other" };
        public static readonly string[] AgeUnits = { "Y", "M", "D" };
        public static readonly string[] BloodGroups = { "A+", "A-", "B+", "B-", "AB+", "AB-", "O+", "O-" };
        public const int MaxPartOfName = 100, MaxFamilyMembers = 10, MaxAllergies = 500;

        /// <summary>"o+", "O +" and "O POSITIVE" are all O+; empty is none; anything else is not a blood group we store ("?").</summary>
        public static string? NormalizeBloodGroup(string? v)
        {
            var t = Clean(v)?.ToUpperInvariant().Replace(" ", "");
            if (t == null) return null;
            t = t.Replace("POSITIVE", "+").Replace("NEGATIVE", "-");
            return BloodGroups.Contains(t) ? t : "?";
        }

        /// <summary>First and last name, from either the two parts or (older callers) one full name, whose first word is the first name.</summary>
        public static (string? First, string? Last) SplitName(string? first, string? last, string? fullName)
        {
            var f = Clean(first);
            var l = Clean(last);
            if (f != null) return (f, l);
            var full = Clean(fullName);
            if (full == null) return (null, l);
            var i = full.IndexOf(' ');
            return i < 0 ? (full, l) : (full[..i], l ?? Clean(full[(i + 1)..]));
        }

        public static string JoinName(string first, string? last) => string.IsNullOrEmpty(last) ? first : $"{first} {last}";

        public const int MaxDoctorName = 200, MaxLocation = 300;

        /// <summary>The first problem with the visit a record is tied to, or null. A system appointment and a typed-in doctor are one or the other.</summary>
        public static string? ValidateVisit(Guid? apptId, string? doctor, string? clinic, string? location, DateTime? visitDate)
        {
            var hasOwn = Clean(doctor) != null || Clean(clinic) != null || Clean(location) != null;
            if (apptId.HasValue && hasOwn) return "Choose a visit from your appointments, or add a doctor yourself, not both.";
            if (hasOwn && Clean(doctor) == null) return "Enter the doctor's name.";
            if ((Clean(doctor)?.Length ?? 0) > MaxDoctorName || (Clean(clinic)?.Length ?? 0) > MaxDoctorName) return $"The doctor or clinic name is too long (max {MaxDoctorName} characters).";
            if ((Clean(location)?.Length ?? 0) > MaxLocation) return $"The location is too long (max {MaxLocation} characters).";
            if (visitDate.HasValue && visitDate.Value.Date > DateTime.UtcNow.Date.AddDays(1)) return "The visit date cannot be in the future.";
            return null;
        }

        /// <summary>The first problem with a family member's details, or null.</summary>
        public static string? ValidateMember(string? first, string? last, string? relation, int? age, string? ageUnit, string? sex, string? bloodGroup, string? allergies)
        {
            var f = Clean(first);
            if (f == null) return "Enter the first name.";
            if (f.Length > MaxPartOfName || (Clean(last)?.Length ?? 0) > MaxPartOfName) return $"A name is too long (max {MaxPartOfName} characters).";
            var rel = Clean(relation);
            if (rel == null) return "Choose how this person is related to you.";
            if (rel.Length > MaxRelation) return $"The relation is too long (max {MaxRelation} characters).";
            if (age.HasValue && (age < 0 || age > MaxAge)) return $"Age must be between 0 and {MaxAge}.";
            var unit = Clean(ageUnit)?.ToUpperInvariant();
            if (unit != null && !AgeUnits.Contains(unit)) return "Age unit must be years, months or days.";
            if (NormalizeSex(sex) == "?") return "Choose Male, Female or Other.";
            if (NormalizeBloodGroup(bloodGroup) == "?") return "Choose a valid blood group.";
            if ((Clean(allergies)?.Length ?? 0) > MaxAllergies) return $"Allergies are too long (max {MaxAllergies} characters).";
            return null;
        }

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

        /// <summary>Own details: the two name parts (or one full name for older callers) plus blood group.</summary>
        public static string? ValidateProfile(string? first, string? last, string? fullName, int? age, string? ageUnit, string? sex, string? email, string? bloodGroup, string? guardianName, string? guardianRelation)
        {
            var (f, l) = SplitName(first, last, fullName);
            if (f == null) return "Enter your first name.";
            if (f.Length > MaxPartOfName || (l?.Length ?? 0) > MaxPartOfName) return $"A name is too long (max {MaxPartOfName} characters).";
            if (NormalizeBloodGroup(bloodGroup) == "?") return "Choose a valid blood group.";
            return Validate(JoinName(f, l), age, ageUnit, sex, email, guardianName, guardianRelation);
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
