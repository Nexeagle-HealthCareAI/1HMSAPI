using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    /// <summary>
    /// FEFO ledger row — RemainingQty is decremented/incremented by whichever handler records an
    /// InventoryMovement against this batch, same "not trigger-maintained" discipline as
    /// InventoryItem.CurrentStock.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class Batch
    {
        [Key]
        public Guid BatchId { get; set; }
        public Guid HospitalId { get; set; }
        public Guid InventoryItemId { get; set; }
        public Guid StoreId { get; set; }

        public string BatchNumber { get; set; } = null!;
        public DateTime? ManufactureDate { get; set; }
        public DateTime? ExpiryDate { get; set; }

        public decimal? UnitCost { get; set; }
        public decimal ReceivedQty { get; set; }
        public decimal RemainingQty { get; set; }

        // Forward references to procurement (Vendor/GRN line) — populated once those tables exist.
        public Guid? VendorId { get; set; }
        public Guid? GrnLineId { get; set; }

        public string Status { get; set; } = null!;   // ACTIVE/EXHAUSTED/EXPIRED/QUARANTINED/RECALLED

        public DateTime CreatedAt { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string? UpdatedBy { get; set; }

        [Timestamp]
        public byte[]? RowVersion { get; set; }
    }
}
