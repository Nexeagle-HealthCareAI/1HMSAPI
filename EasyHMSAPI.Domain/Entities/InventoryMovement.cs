using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    /// <summary>Insert-only stock movement audit trail — no RowVersion, rows are never updated.</summary>
    [ExcludeFromCodeCoverage]
    [Table("InventoryMovement")]
    public class InventoryMovement
    {
        [Key]
        public Guid InventoryMovementId { get; set; }
        public Guid HospitalId { get; set; }
        public Guid InventoryItemId { get; set; }

        public string MovementType { get; set; } = null!;   // RECEIVE/ISSUE/RETURN/ADJUST_IN/ADJUST_OUT/TRANSFER

        public decimal Qty { get; set; }
        public decimal? UnitCost { get; set; }
        public string? BatchNumber { get; set; }
        public DateTime? ExpiryDate { get; set; }

        // Batch/store-aware path (INV-2). Nullable — legacy callers (OT's IntraOpItemUsage, the
        // original /inventory/items/movement endpoint) pass none of these and are unaffected.
        public Guid? BatchId { get; set; }
        public Guid? FromStoreId { get; set; }
        public Guid? ToStoreId { get; set; }

        public Guid? EncounterId { get; set; }
        public string? PatientId { get; set; }
        public Guid? ChargeEventId { get; set; }
        public string? SourceModule { get; set; }
        public string? SourceRefId { get; set; }

        public string? Reason { get; set; }
        public string? Notes { get; set; }

        public DateTime MovedAt { get; set; }
        public string? MovedBy { get; set; }
        public Guid? MovedByUserId { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}
