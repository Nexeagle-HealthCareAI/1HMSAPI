using System.Globalization;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Application.Services.Models;
using EasyHMSAPI.Domain.Entities;

namespace EasyHMSAPI.Application.Services
{
    /// <summary>
    /// Registering a patient from a verified ABHA (ABDM M1: VRFY_ABHA_101, 201, 306, 308,
    /// CRT_ABHA_116 and the one-ABHA-per-patient tagging case). Free of the database, so it is easy to test.
    /// Name, date of birth and gender come from the ABHA and are never taken from the client; address,
    /// email, mobile, state, district, city and pincode stay editable.
    /// </summary>
    public static class AbhaPatientRules
    {
        public const string AlreadyLinkedMessage = "This ABHA profile is already linked to HMIS Beneficiary ID.";

        public static string Digits(string? value) => new((value ?? string.Empty).Where(char.IsDigit).ToArray());

        /// <summary>"91-1234-5678-9012" from any spelling of a 14 digit ABHA number; anything else as given.</summary>
        public static string FormatAbhaNumber(string? abhaNumber)
        {
            var d = Digits(abhaNumber);
            return d.Length == 14 ? $"{d[..2]}-{d[2..6]}-{d[6..10]}-{d[10..]}" : (abhaNumber ?? string.Empty).Trim();
        }

        /// <summary>The same ABHA: equal 14 digit numbers, or (for an address kept in AbhaId) equal addresses.</summary>
        public static bool SameAbha(string? stored, string? abhaNumber, string? abhaAddress = null)
        {
            if (string.IsNullOrWhiteSpace(stored)) return false;
            var s = stored.Trim();
            if (s.Contains('@'))
                return !string.IsNullOrWhiteSpace(abhaAddress) && string.Equals(s, abhaAddress.Trim(), StringComparison.OrdinalIgnoreCase);
            var d = Digits(s);
            return d.Length == 14 && d == Digits(abhaNumber);
        }

        /// <summary>ABDM's gender (M, F, O, T or a word) as the patient record keeps it.</summary>
        public static string? Sex(string? gender)
        {
            var g = (gender ?? string.Empty).Trim().ToLowerInvariant();
            return g switch
            {
                "" => null,
                "m" or "male" => "Male",
                "f" or "female" => "Female",
                _ => "Other"
            };
        }

        /// <summary>ABDM sends dd-MM-yyyy, yyyy-MM-dd, or the year alone.</summary>
        public static (DateTime? Date, bool YearOnly) BirthDate(string? dob)
        {
            var v = (dob ?? string.Empty).Trim();
            if (v.Length == 0) return (null, false);
            if (v.Length == 4 && int.TryParse(v, out var year) && year > 1850)
                return (new DateTime(year, 1, 1), true);
            foreach (var f in new[] { "dd-MM-yyyy", "d-M-yyyy", "yyyy-MM-dd", "dd/MM/yyyy", "yyyy-MM-ddTHH:mm:ss" })
                if (DateTime.TryParseExact(v, f, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
                    return (d.Date, false);
            return (null, false);
        }

        /// <summary>Age in whole years (months or days for an infant), as the patient record keeps it.</summary>
        public static (short Age, string Unit)? Age(string? dob, DateTime today)
        {
            var (date, _) = BirthDate(dob);
            if (date == null || date > today) return null;
            var years = today.Year - date.Value.Year - (today.DayOfYear < date.Value.DayOfYear ? 1 : 0);
            if (years >= 1) return ((short)years, "Y");
            var months = (today.Year - date.Value.Year) * 12 + today.Month - date.Value.Month - (today.Day < date.Value.Day ? 1 : 0);
            if (months >= 1) return ((short)months, "M");
            return ((short)Math.Max(0, (today - date.Value).Days), "D");
        }

        /// <summary>Whether a typed mobile and the ABHA's mobile are the same number (last ten digits).</summary>
        public static bool SameMobile(string? a, string? b)
        {
            static string Ten(string d) => d.Length > 10 ? d[^10..] : d;
            var x = Ten(Digits(a));
            return x.Length == 10 && x == Ten(Digits(b));
        }

        public static VerifiedAbhaProfile ToVerified(AbdmProfileResult r) =>
            new(r.AbhaNumber, r.AbhaAddress, r.FullName, r.Gender, r.DateOfBirth, r.Mobile, r.Email, r.Address, r.State, r.District, r.City, r.Pincode);

        public static VerifiedAbhaProfile ToVerified(AbdmEnrollResult r) =>
            new(r.AbhaNumber, r.AbhaAddress, r.FullName, r.Gender, r.DateOfBirth, r.Mobile, r.Email, r.Address, r.State, r.District, r.City, r.Pincode);

        public static AbdmEnrollResponseModel EnrolResponse(AbdmEnrollResult r, bool mobileVerified, string? linkToken) => new()
        {
            Success = true,
            TxnId = r.TxnId,
            AbhaNumber = r.AbhaNumber,
            AbhaAddress = r.AbhaAddress,
            FullName = r.FullName,
            Gender = r.Gender,
            DateOfBirth = r.DateOfBirth,
            Mobile = r.Mobile,
            MobileVerified = mobileVerified,
            IsNew = r.IsNew,
            Email = r.Email,
            Address = r.Address,
            State = r.State,
            District = r.District,
            City = r.City,
            Pincode = r.Pincode,
            ProfilePhoto = r.ProfilePhoto,
            LinkToken = linkToken
        };

        public static AbhaPatientSummary Summary(PatientRegistration p, IEnumerable<string>? matchedOn = null) => new()
        {
            PatientId = p.PatientId ?? string.Empty,
            FullName = p.FullName,
            Mobile = p.Mobile,
            Email = p.Email,
            Sex = p.Sex,
            Age = p.Age,
            AgeUnit = p.AgeUnit,
            DateOfBirth = p.DateOfBirth?.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture),
            AddressLine = p.AddressLine,
            City = p.City,
            District = p.District,
            State = p.State,
            Pincode = p.Pincode,
            AbhaId = p.AbhaId,
            RegisteredAt = p.RegisteredAt,
            MatchedOn = matchedOn?.ToList() ?? new List<string>()
        };

        /// <summary>
        /// Beneficiary matching before registering (CRT_ABHA_116): a patient of this hospital who may be
        /// this ABHA holder. Same mobile with a close name; or a close name with the same sex and age
        /// within two years (or the same birth date). A patient already holding a different ABHA is still
        /// shown, so staff see it, but cannot be linked (one ABHA per patient).
        /// </summary>
        public static List<string>? MatchReasons(PatientRegistration p, VerifiedAbhaProfile abha, DateTime today)
        {
            var sim = FuzzyMatch.JaroWinkler(abha.FullName, p.FullName);
            var mobile = SameMobile(p.Mobile, abha.Mobile);
            var sex = Sex(abha.Gender);
            var sameSex = sex != null && string.Equals(sex, p.Sex, StringComparison.OrdinalIgnoreCase);
            var (dob, yearOnly) = BirthDate(abha.DateOfBirth);
            var sameDob = dob != null && !yearOnly && p.DateOfBirth?.Date == dob.Value.Date;
            var age = Age(abha.DateOfBirth, today);
            var closeAge = age is { Unit: "Y" } a && p.Age != null && (p.AgeUnit ?? "Y") == "Y" && Math.Abs(p.Age.Value - a.Age) <= 2;

            var reasons = new List<string>();
            if (mobile && sim >= 0.80) reasons.Add("MOBILE");
            if (sim >= 0.85 && sameSex && (sameDob || closeAge))
            {
                reasons.Add("NAME");
                reasons.Add(sameDob ? "DOB" : "AGE");
                reasons.Add("SEX");
            }
            else if (reasons.Count > 0 && sim >= 0.80) reasons.Add("NAME");
            return reasons.Count == 0 ? null : reasons.Distinct().ToList();
        }

        /// <summary>
        /// Puts a verified ABHA on a patient record. Name, birth date, age and sex are the ABHA's;
        /// the editable fields are the ones staff confirmed on the form, and an empty one never wipes
        /// what the record holds.
        /// </summary>
        public static void Apply(PatientRegistration p, VerifiedAbhaProfile abha, EditableFields form, DateTime today)
        {
            static string? Keep(string? now, string? before) => string.IsNullOrWhiteSpace(now) ? before : now.Trim();
            p.AbhaId = FormatAbhaNumber(abha.AbhaNumber);
            p.FullName = Keep(abha.FullName, p.FullName);
            p.Sex = Sex(abha.Gender) ?? p.Sex;
            var (dob, yearOnly) = BirthDate(abha.DateOfBirth);
            if (dob != null && !yearOnly) p.DateOfBirth = dob;
            if (Age(abha.DateOfBirth, today) is { } age) { p.Age = age.Age; p.AgeUnit = age.Unit; }
            p.Mobile = Keep(Digits(form.Mobile).Length >= 10 ? Digits(form.Mobile)[^10..] : null, p.Mobile);
            p.Email = Keep(form.Email, p.Email);
            p.AddressLine = Keep(form.AddressLine, p.AddressLine);
            p.City = Keep(form.City, p.City);
            p.District = Keep(form.District, p.District);
            p.State = Keep(form.State, p.State);
            p.Pincode = Keep(form.Pincode, p.Pincode);
            if (string.IsNullOrWhiteSpace(p.Country)) p.Country = "India";
        }

        public sealed record EditableFields(string? Mobile, string? Email, string? AddressLine, string? City, string? District, string? State, string? Pincode);
    }
}
