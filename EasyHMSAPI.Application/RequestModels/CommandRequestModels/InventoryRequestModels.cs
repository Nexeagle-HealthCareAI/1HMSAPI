using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    [ExcludeFromCodeCoverage]
    public class CreateInventoryItemRequestModel : IRequest<CreateInventoryItemResponseModel>
    {
        public Guid HospitalId { get; set; }
        [JsonIgnore]
        public string? LoggedInUserName { get; set; }

        public string ItemCode { get; set; } = null!;
        public string ItemName { get; set; } = null!;
        public string? GenericName { get; set; }
        public string? Manufacturer { get; set; }
        public string Category { get; set; } = null!;
        public string? Unit { get; set; }
        public decimal? DefaultRate { get; set; }
        public string? HsnSacCode { get; set; }
        public decimal? GstSlabPercent { get; set; }
        public bool IsTaxable { get; set; }
        public Guid? ChargeId { get; set; }
        public decimal MinStockLevel { get; set; }
        public string? StoreLocation { get; set; }
    }

    // Unified movement handler — one request shape for RECEIVE/ISSUE/RETURN/ADJUST_IN/ADJUST_OUT
    // (mirrors RecordMedicationAdministrationRequestModel's single-handler-many-actions shape).
    // Also callable via nested _mediator.Send() from another handler's own transaction (e.g.
    // IntraOpItemUsage) — the same pattern AddChargeEventRequestModel already supports for CPOE.
    [ExcludeFromCodeCoverage]
    public class RecordInventoryMovementRequestModel : IRequest<RecordInventoryMovementResponseModel>
    {
        public Guid HospitalId { get; set; }
        [JsonIgnore]
        public string? LoggedInUserName { get; set; }
        [JsonIgnore]
        public Guid? LoggedInUserId { get; set; }

        public Guid InventoryItemId { get; set; }
        public string MovementType { get; set; } = null!;
        public decimal Qty { get; set; }
        public decimal? UnitCost { get; set; }
        public string? BatchNumber { get; set; }
        public DateTime? ExpiryDate { get; set; }

        public Guid? EncounterId { get; set; }
        public string? PatientId { get; set; }
        public Guid? ChargeEventId { get; set; }
        public string? SourceModule { get; set; }
        public string? SourceRefId { get; set; }

        public string? Reason { get; set; }
        public string? Notes { get; set; }
    }
}
