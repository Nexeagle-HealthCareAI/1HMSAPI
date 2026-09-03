using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    /// <summary>
    /// Return-to-Vendor (RTV): a debit note against a supplier for near-expiry batches sent back
    /// unsold. Stock is deducted for real (MovementType=ADJUST_OUT); the note itself is a record
    /// of what was compiled/returned, printed client-side as the actual debit-note document.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class VendorReturnNote
    {
        [Key]
        public Guid VendorReturnId { get; set; }
        public Guid HospitalId { get; set; }
        public Guid VendorId { get; set; }

        public string ReturnNoteNo { get; set; } = null!;
        public decimal TotalQty { get; set; }
        public decimal TotalValue { get; set; }
        public string? Notes { get; set; }

        public DateTime GeneratedAt { get; set; }
        public string? GeneratedBy { get; set; }
        public Guid? GeneratedByUserId { get; set; }

        public DateTime CreatedAt { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class VendorReturnLine
    {
        [Key]
        public Guid VendorReturnLineId { get; set; }
        public Guid VendorReturnId { get; set; }
        public Guid InventoryItemId { get; set; }
        public Guid BatchId { get; set; }
        public string? BatchNumber { get; set; }
        public DateTime? ExpiryDate { get; set; }
        public decimal Qty { get; set; }
        public decimal UnitCost { get; set; }
        public decimal LineValue { get; set; }
    }
}
