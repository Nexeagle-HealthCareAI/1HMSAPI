using EasyHMSAPI.Domain.Entities;

namespace EasyHMSAPI.Application.Common
{
    /// <summary>
    /// When is a doctor's professional profile "confirmed", i.e. ready to start working? Registration creates the admin-doctor's Doctor row with the
    /// placeholder licence number "PENDING" (hospital registration cannot ask for it), and quick-add creates doctors with real details. The doctor
    /// confirms their profile by supplying the three regulatory identifiers: a real licence number, the state medical council, and the year of
    /// registration. Until then they cannot go online / appear as available, and the Doctor Board asks them to complete it.
    /// </summary>
    public static class DoctorProfileRules
    {
        public const string PlaceholderLicence = "PENDING";

        public static bool IsPlaceholderLicence(string? licenceNumber)
            => string.IsNullOrWhiteSpace(licenceNumber) || string.Equals(licenceNumber.Trim(), PlaceholderLicence, StringComparison.OrdinalIgnoreCase);

        /// <summary>What is still missing for the profile to count as confirmed (empty when it is).</summary>
        public static List<string> MissingItems(string? licenceNumber, string? medicalCouncil, int? registrationYear)
        {
            var missing = new List<string>();
            if (IsPlaceholderLicence(licenceNumber)) missing.Add("Medical licence / registration number");
            if (string.IsNullOrWhiteSpace(medicalCouncil)) missing.Add("State medical council");
            if (!registrationYear.HasValue || registrationYear < 1950 || registrationYear > DateTime.UtcNow.Year) missing.Add("Year of registration");
            return missing;
        }

        public static List<string> MissingItems(Doctor d) => MissingItems(d.LicenseNumber, d.MedicalCouncil, d.RegistrationYear);

        public static bool IsConfirmed(Doctor d) => MissingItems(d).Count == 0;

        public const string NotConfirmedMessage =
            "Confirm your professional profile (licence number, state medical council and year of registration) before you go online.";
    }
}
