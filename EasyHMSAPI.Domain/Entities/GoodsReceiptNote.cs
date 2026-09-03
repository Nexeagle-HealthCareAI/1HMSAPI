using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    /// <summary>An as-received record — no draft state. MatchStatus is the simple 3-way (PO/GRN/invoice) match outcome.</summary>
    [ExcludeFromCodeCoverage]
    public class GoodsReceiptNote
    {
        [Key]
        public Guid GrnId { get; set; }
        public Guid HospitalId { get; set; }

        public string GrnNumber { get; set; } = null!;
        public Guid PurchaseOrderId { get; set; }
        public Guid VendorId { get; set; }
        public Guid ReceivedStoreId { get; set; }

        public string? InvoiceNumber { get; set; }
        public DateTime? InvoiceDate { get; set; }
        public decimal? InvoiceAmount { get; set; }
        public string MatchStatus { get; set; } = null!;

        public string? ReceivedBy { get; set; }
        public Guid? ReceivedByUserId { get; set; }
        public DateTime ReceivedAt { get; set; }

        public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; }
        public string? CreatedBy { get; set; }

        [Timestamp]
        public byte[]? RowVersion { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class GoodsReceiptNoteLine
    {
        [Key]
        public Guid GrnLineId { get; set; }
        public Guid GrnId { get; set; }
        public Guid PurchaseOrderLineId { get; set; }
        public Guid InventoryItemId { get; set; }

        public string BatchNumber { get; set; } = null!;
        public DateTime? ManufactureDate { get; set; }
        public DateTime? ExpiryDate { get; set; }
        public decimal Qty { get; set; }
        public decimal Rate { get; set; }
        // Trade scheme (e.g. "10+1") — physical units received free of charge on top of Qty. Batch
        // ReceivedQty = Qty + FreeQty; the PO's own ReceivedQty tracking stays keyed to the billed
        // Qty only, since a freebie was never on the order.
        public decimal FreeQty { get; set; }
    }
}
