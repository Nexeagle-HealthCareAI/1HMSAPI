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
        public string? Qualification { get; set; }
        public int? ExperienceYears { get; set; }
        public string? Bio { get; set; }
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
        public decimal? DiscountPercent { get; set; }
        public DateTime? DiscountStartAt { get; set; }
        public DateTime? DiscountEndAt { get; set; }
    }
}
