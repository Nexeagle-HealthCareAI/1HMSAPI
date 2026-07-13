using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class GetAdmissionReferralsResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public List<AdmissionReferralDataModel> Referrals { get; set; } = new();
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalCount { get; set; }
        // Counts per StatusCode -- ignores the request's own StatusCode filter (so switching status
        // chips never hides sibling counts) but respects every other filter (CaseType etc.). Seeded
        // with every known status at zero first, so a status with no matching referrals still shows
        // its chip instead of silently disappearing -- see IpdConstants.ReferralStatus.All.
        public List<ReferralStatusCountItem> StatusCounts { get; set; } = new();
    }

    [ExcludeFromCodeCoverage]
    public class ReferralStatusCountItem
    {
        public string StatusCode { get; set; } = null!;
        public int Count { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class AdmissionReferralDataModel
    {
        public Guid ReferralId { get; set; }
        public string? PatientId { get; set; }
        public string? PatientName { get; set; }
        public string? PatientMobile { get; set; }
        public Guid ReferringDoctorId { get; set; }
        public string? ReferringDoctorName { get; set; }
        public Guid? AppointmentId { get; set; }
        public bool SourceAppointmentCancelled { get; set; }
        public Guid? OtPlanId { get; set; }
        public string? OtPlanName { get; set; }
        public Guid? PackageTypeId { get; set; }
        public string? PackageTypeName { get; set; }
        public decimal? PackageTypePrice { get; set; }
        public string? ProcedureName { get; set; }
        public DateTime? ProbableAdmissionDate { get; set; }
        public string? CaseType { get; set; }
        public string? Notes { get; set; }
        public string? StatusCode { get; set; }
        public string? NotAdmittedReason { get; set; }
        public DateTime? FollowUpDate { get; set; }
        public string? FollowUpNotes { get; set; }
        public Guid? ConvertedAdmissionId { get; set; }
        public DateTime? AdmittedAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public int CommentCount { get; set; }
    }
}
