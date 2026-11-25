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
    }
}
