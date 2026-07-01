using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class GetRestraintOrdersResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public List<RestraintOrderItem> Orders { get; set; } = new();
    }

    [ExcludeFromCodeCoverage]
    public class RestraintOrderItem
    {
        public Guid RestraintOrderId { get; set; }
        public string RestraintType { get; set; } = null!;
        public string Reason { get; set; } = null!;
        public string OrderedByDoctorName { get; set; } = null!;
        public DateTime OrderedAt { get; set; }
        public DateTime StartedAt { get; set; }
        public string? StartedBy { get; set; }
        public int MonitoringIntervalMins { get; set; }
        public bool FamilyNotified { get; set; }
        public string? FamilyNotificationNotes { get; set; }
        public DateTime? ReleasedAt { get; set; }
        public string? ReleasedBy { get; set; }
        public string? ReleaseReason { get; set; }
        public string StatusCode { get; set; } = null!;
        public string? Notes { get; set; }
    }
}
