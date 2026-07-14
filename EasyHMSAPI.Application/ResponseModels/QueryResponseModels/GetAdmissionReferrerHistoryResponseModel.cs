using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class GetAdmissionReferrerHistoryResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public List<AdmissionReferrerHistoryItem> Items { get; set; } = new();
    }

    [ExcludeFromCodeCoverage]
    public class AdmissionReferrerHistoryItem
    {
        public Guid AssignmentId { get; set; }
        public string ReferralSource { get; set; } = null!;
        public Guid? ReferrerId { get; set; }
        public string? ReferrerName { get; set; }
        public string? ReferrerType { get; set; }
        public DateTime AssignedAt { get; set; }
        public string? AssignedBy { get; set; }
        public DateTime? UnassignedAt { get; set; }
        public string? UnassignedBy { get; set; }
        public string StatusCode { get; set; } = "ACTIVE";
    }
}
