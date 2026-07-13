using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class GetAdmissionDoctorHistoryResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public List<AdmissionDoctorHistoryItem> Items { get; set; } = new();
    }

    [ExcludeFromCodeCoverage]
    public class AdmissionDoctorHistoryItem
    {
        public Guid AssignmentId { get; set; }
        public Guid DoctorId { get; set; }
        public string? DoctorName { get; set; }
        public DateTime AssignedAt { get; set; }
        public string? AssignedBy { get; set; }
        public DateTime? UnassignedAt { get; set; }
        public string? UnassignedBy { get; set; }
        public string StatusCode { get; set; } = "ACTIVE";
    }
}
