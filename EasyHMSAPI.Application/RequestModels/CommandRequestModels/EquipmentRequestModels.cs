using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    // Upsert: EquipmentId present => update that asset in place; absent => create a new one.
    [ExcludeFromCodeCoverage]
    public class UpsertEquipmentRequestModel : IRequest<UpsertEquipmentResponseModel>
    {
        public Guid HospitalId { get; set; }
        [JsonIgnore]
        public string? LoggedInUserName { get; set; }

        public Guid? EquipmentId { get; set; }
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
        public string? Status { get; set; }
        public string? Notes { get; set; }
    }

    // Inserts a maintenance log row and, from it, recomputes NextDueAt/LastServiceAt/Status on the
    // parent Equipment — the single place those denormalized fields ever change.
    [ExcludeFromCodeCoverage]
    public class RecordMaintenanceLogRequestModel : IRequest<RecordMaintenanceLogResponseModel>
    {
        public Guid HospitalId { get; set; }
        [JsonIgnore]
        public string? LoggedInUserName { get; set; }
        [JsonIgnore]
        public Guid? LoggedInUserId { get; set; }

        public Guid EquipmentId { get; set; }
        public string ActivityType { get; set; } = null!;
        public DateTime? PerformedAt { get; set; }
        public string? VendorName { get; set; }
        public decimal? Cost { get; set; }
        public string? PartsReplaced { get; set; }
        public string? Findings { get; set; }
        public string? ActionTaken { get; set; }
        public string? Outcome { get; set; }
        public DateTime? NextDueAtOverride { get; set; }
        public string? Notes { get; set; }
        public string? Attachments { get; set; }
    }
}
