using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class GetAdmissionReferralsResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public List<AdmissionReferralDataModel> Referrals { get; set; } = new();
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
        public Guid? OtPlanId { get; set; }
        public string? OtPlanName { get; set; }
        public string? ProcedureName { get; set; }
        public DateTime? ProbableAdmissionDate { get; set; }
        public string? CaseType { get; set; }
        public string? Notes { get; set; }
        public string? StatusCode { get; set; }
        public string? NotAdmittedReason { get; set; }
        public DateTime? FollowUpDate { get; set; }
        public string? FollowUpNotes { get; set; }
        public Guid? ConvertedAdmissionId { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
