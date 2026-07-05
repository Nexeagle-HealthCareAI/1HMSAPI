using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class GetEquipmentListResponseModel
    {
        public List<EquipmentDataModel> Equipment { get; set; } = new();
    }

    [ExcludeFromCodeCoverage]
    public class EquipmentDataModel
    {
        public Guid EquipmentId { get; set; }
        public string AssetCode { get; set; } = null!;
        public string Name { get; set; } = null!;
        public string? Model { get; set; }
        public string? SerialNumber { get; set; }
        public string? Manufacturer { get; set; }
        public string Category { get; set; } = null!;
        public string? Location { get; set; }
        public string? Department { get; set; }
        public string? AmcVendor { get; set; }
        public DateTime? InstalledAt { get; set; }
        public DateTime? WarrantyEndAt { get; set; }
        public DateTime? AmcEndAt { get; set; }
        public int? PmIntervalDays { get; set; }
        public DateTime? LastServiceAt { get; set; }
        public DateTime? NextDueAt { get; set; }
        public string Status { get; set; } = null!;
        public string? Notes { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class GetMaintenanceLogHistoryResponseModel
    {
        public List<MaintenanceLogDataModel> Logs { get; set; } = new();
    }

    [ExcludeFromCodeCoverage]
    public class MaintenanceLogDataModel
    {
        public Guid MaintenanceLogId { get; set; }
        public string ActivityType { get; set; } = null!;
        public DateTime PerformedAt { get; set; }
        public string PerformedBy { get; set; } = null!;
        public string? VendorName { get; set; }
        public decimal? Cost { get; set; }
        public string? PartsReplaced { get; set; }
        public string? Findings { get; set; }
        public string? ActionTaken { get; set; }
        public string? Outcome { get; set; }
        public DateTime? NextDueAtOverride { get; set; }
        public string? Notes { get; set; }
    }
}
