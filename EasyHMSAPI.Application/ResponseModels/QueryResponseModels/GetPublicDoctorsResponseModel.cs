using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class GetPublicDoctorsResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public List<PublicDoctorInfo> Doctors { get; set; } = new();
        // Flat Page/PageSize/TotalCount convention — matches GetChargeMastersResponseModel,
        // BedMasterResponseModels, etc. (this API's own established pagination shape).
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalCount { get; set; }
    }

    // Public-safe field set only — no LicenseNumber, MedicalCouncil, RegistrationYear, UserId,
    // mobile/email, or anything queue/schedule-internal.
    [ExcludeFromCodeCoverage]
    public class PublicDoctorInfo
    {
        public Guid DoctorId { get; set; }
        public string? FullName { get; set; }
        public string? PhotoUrl { get; set; }
        public string? Qualification { get; set; }
        public int? ExperienceYears { get; set; }
        public string? Bio { get; set; }
        public string? DepartmentName { get; set; }
        // Patient-facing NMC speciality info (dbo.MedicalSpecialities), when the doctor has one
        // linked — a cleaner, authoritative alternative to fuzzy-matching DepartmentName for
        // Doctor Dekho's specialty categorization. Null when unset; consumers fall back to
        // DepartmentName as before.
        public string? PrimaryMedicalSpecialityPatientFacingName { get; set; }
        public string? PrimaryMedicalSpecialityCategory { get; set; }
        public List<string> Specializations { get; set; } = new();
        public List<string> Languages { get; set; } = new();
        // Computed from non-hidden DoctorReviews — null/0 when the doctor has no reviews yet, so
        // the frontend's existing "hide the badge when falsy" rendering is unaffected.
        public double? Rating { get; set; }
        public int ReviewCount { get; set; }
        // OPD_CONSULT DoctorFees.Amount at this doctor's (canonical) hospital — null when no
        // active fee is configured, so the frontend falls back to "Accepting patients".
        public decimal? Fee { get; set; }
        // CMS-controlled marketing fields — DiscountPercent/DiscountedFee are only populated
        // (non-null) when the doctor's scheduled discount window is currently active; the
        // frontend's existing "null means nothing to show" convention (see Fee) applies here too.
        public decimal? DiscountPercent { get; set; }
        public decimal? DiscountedFee { get; set; }
        public bool IsFeatured { get; set; }
        // Drives the public "Verified profile" badge — set by a CMS admin only after manually
        // confirming this doctor's registration against the NMC's Indian Medical Register.
        public bool IsRegistrationVerified { get; set; }
        // Same TimeOff > Override > Template precedence as the single-doctor
        // GetPublicDoctorAvailabilityHandler (see DoctorAvailabilityResolver), resolved for
        // today's date and batched per page so the directory grid never needs a per-card call.
        public bool IsAvailableToday { get; set; }
        // Manual "online now" toggle (Doctor.IsOnlineNow) — a doctor/staff-set signal, separate
        // from IsAvailableToday's schedule-derived status. Drives a distinct "Online now" badge.
        public bool IsOnlineNow { get; set; }

        // Which (publicly-listed) hospital this doctor belongs to — needed now that the
        // directory spans every opted-in hospital, not just one scoped by an API key.
        public Guid HospitalId { get; set; }
        public string? HospitalName { get; set; }
        // Full street-level address as entered on the hospital's own profile (Hospital.Location)
        // — previously only City/State reached the public API, so doctor cards could never show
        // more than "City, State". Address/Pincode fill that gap.
        public string? Address { get; set; }
        public string? City { get; set; }
        public string? State { get; set; }
        public string? Pincode { get; set; }
        // GPS pin for a "get directions" link — inherited from the hospital, since a doctor
        // doesn't have their own address (see Hospital.Latitude/Longitude).
        public decimal? Latitude { get; set; }
        public decimal? Longitude { get; set; }
    }
}
