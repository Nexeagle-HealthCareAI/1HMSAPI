using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    /// <summary>Status: DRAFT/APPROVED/SENT/PARTIALLY_RECEIVED/RECEIVED/CANCELLED.</summary>
    [ExcludeFromCodeCoverage]
    public class PurchaseOrder
    {
        [Key]
        public Guid PurchaseOrderId { get; set; }
        public Guid HospitalId { get; set; }

        public string PoNumber { get; set; } = null!;
        public Guid VendorId { get; set; }
        public Guid? IndentId { get; set; }

        public string Status { get; set; } = null!;

        public string? OrderedBy { get; set; }
        public Guid? OrderedByUserId { get; set; }
        public DateTime OrderedAt { get; set; }

        public string? ApprovedBy { get; set; }
        public Guid? ApprovedByUserId { get; set; }
        public DateTime? ApprovedAt { get; set; }

        public DateTime? ExpectedDeliveryDate { get; set; }
        public string? CancelledReason { get; set; }
        public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string? UpdatedBy { get; set; }

        [Timestamp]
        public byte[]? RowVersion { get; set; }
    }

    /// <summary>ReceivedQty is not trigger-maintained — CreateGoodsReceiptNote updates it in the same transaction as the movement post.</summary>
    [ExcludeFromCodeCoverage]
    public class PurchaseOrderLine
    {
        [Key]
        public Guid PurchaseOrderLineId { get; set; }
        public Guid PurchaseOrderId { get; set; }
        public Guid InventoryItemId { get; set; }
        public decimal Qty { get; set; }
        public decimal Rate { get; set; }
        public decimal ReceivedQty { get; set; }

        [Timestamp]
        public byte[]? RowVersion { get; set; }
    }
}
