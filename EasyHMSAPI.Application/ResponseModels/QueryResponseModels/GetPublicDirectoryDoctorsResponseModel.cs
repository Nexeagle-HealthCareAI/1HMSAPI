using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class GetPublicDirectoryDoctorsResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public List<PublicDirectoryDoctorItem> Doctors { get; set; } = new();
    }

    [ExcludeFromCodeCoverage]
    public class PublicDirectoryDoctorItem
    {
        public Guid DoctorId { get; set; }
        public Guid UserId { get; set; }
        public string? FullName { get; set; }
        public string? PhotoUrl { get; set; }
        public Guid? DepartmentId { get; set; }
        public string? DepartmentName { get; set; }
        // Required by PUT doctors/profile's existing HospitalDepartmentMappingId guard — the tile
        // editor's "Save" round-trips this back unchanged when it isn't editing department.
        public Guid? HospitalDepartmentMappingId { get; set; }
        public string? LicenseNumber { get; set; }
        public string? MedicalCouncil { get; set; }
        public int? RegistrationYear { get; set; }
        public string? Qualification { get; set; }
        public int? ExperienceYears { get; set; }
        public string? Bio { get; set; }
        // OPD/IPD/emergency fees — same dbo.DoctorFees rows Configuration > Doctor Fees edits
        // (FeeType OPD_CONSULT/IPD_VISIT/EMERGENCY). IPD/Emergency are surfaced read-only here
        // purely so the tile editor's OPD-only save can round-trip them unchanged instead of
        // zeroing them out (UpsertDoctorFeeRequestModel takes all three, non-nullable).
        public decimal? OpdConsultFee { get; set; }
        public decimal? IpdVisitFee { get; set; }
        public decimal? EmergencyFee { get; set; }
        public List<string> Specializations { get; set; } = new();
        public List<string> Languages { get; set; } = new();
        public string? PublicContactEmail { get; set; }
        public string? PublicContactPhone { get; set; }
        // Computed from non-hidden DoctorReviews.
        public double? Rating { get; set; }
        public int ReviewCount { get; set; }
        // Whether this doctor currently shows on the platform-wide public directory (also requires
        // the hospital itself to be publicly listed — see Hospital.IsPubliclyListed).
        public bool IsPubliclyListed { get; set; }
        // CMS-only-controlled marketing/moderation fields — read-only here. This hospital-scoped
        // tile editor deliberately doesn't let a hospital admin change these (see
        // Doctor.IsDelistedByAdmin's doc comment); they're surfaced so an admin can at least see
        // WHY a doctor they've enabled still isn't appearing publicly, rather than a silent gap.
        public bool IsFeatured { get; set; }
        public bool IsDelistedByAdmin { get; set; }
        // Read-only here too — set only by a CMS admin from Doctor Dekho's detail view after
        // manually confirming registration against the NMC's Indian Medical Register.
        public bool IsRegistrationVerified { get; set; }
        // Unlike IsFeatured/IsDelistedByAdmin above, the discount IS hospital-editable from this
        // tile editor (PUT doctors/profile with UpdateDiscount=true) — it's a per-hospital
        // marketing decision, not platform moderation. Doctor Dekho/online-booking-only; has no
        // effect on easyHMSWeb's own in-hospital appointment or billing flows.
        public decimal? DiscountPercent { get; set; }
        public DateTime? DiscountStartAt { get; set; }
        public DateTime? DiscountEndAt { get; set; }
    }
}
