using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    // Upsert: InventoryItemId present => update that item in place; absent => create a new one.
    [ExcludeFromCodeCoverage]
    public class CreateInventoryItemRequestModel : IRequest<CreateInventoryItemResponseModel>
    {
        public Guid HospitalId { get; set; }
        [JsonIgnore]
        public string? LoggedInUserName { get; set; }

        public Guid? InventoryItemId { get; set; }
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

        // Drug/regulatory metadata (INV-3) — all optional, meaningless for non-drug categories.
        public string? ScheduleClass { get; set; }   // H/H1/X/NARCOTIC
        public bool IsLasa { get; set; }
        public bool IsHighAlert { get; set; }
        public string? StorageCondition { get; set; }   // ROOM/COLD_CHAIN/FROZEN/CONTROLLED
        public decimal ReorderQty { get; set; }
        public decimal? MaxStockLevel { get; set; }
        public bool IsActive { get; set; } = true;
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

        // Batch/store-aware path (INV-2), all optional — omit to keep using the legacy
        // CurrentStock-only behavior (e.g. OT's IntraOpItemUsage).
        // RECEIVE: BatchId must point to a batch the caller already created (ReceivedQty set,
        //   RemainingQty starts at 0 — this call brings RemainingQty up via the same +delta as
        //   CurrentStock/StockLevel, so nothing is ever set outside the movement mechanism).
        // ISSUE/ADJUST_OUT: supply StoreId to auto-FEFO-allocate, or BatchId to draw a specific batch.
        public Guid? BatchId { get; set; }
        public Guid? StoreId { get; set; }

        public Guid? EncounterId { get; set; }
        public string? PatientId { get; set; }
        public Guid? ChargeEventId { get; set; }
        public string? SourceModule { get; set; }
        public string? SourceRefId { get; set; }

        public string? Reason { get; set; }
        public string? Notes { get; set; }

        // India regulatory compliance (INV-8). Required on ISSUE when the item has a ScheduleClass
        // set (any of H/H1/X/NARCOTIC).
        public string? PrescriberRef { get; set; }
        // Two-person sign-off for narcotics — required on ISSUE when ScheduleClass=NARCOTIC.
        public string? WitnessBy { get; set; }
        public Guid? WitnessByUserId { get; set; }
        // Internal-only: set solely by DispenseNarcoticRequestModel's nested send, never by the
        // public API — the generic movement endpoint alone can never dispense a narcotic.
        [JsonIgnore]
        public bool IsNarcoticDispenseContext { get; set; }
    }
}
