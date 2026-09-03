using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    /// <summary>
    /// A patient return/restock event, kept as its own parallel ledger rather than editing the
    /// original BillingInvoice/BillingChargeEvent — the original bill keeps showing exactly what
    /// was sold at sale time; the return (and its refund) is looked up separately via InvoiceId.
    /// Stock IS reversed for real (RecordInventoryMovementRequestModel, MovementType=RETURN) —
    /// only the billing side is deliberately left untouched.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class PharmacyReturn
    {
        [Key]
        public Guid ReturnId { get; set; }
        public Guid HospitalId { get; set; }
        public Guid InvoiceId { get; set; }
        public string? InvoiceNo { get; set; }
        public string? PatientId { get; set; }
        public Guid EncounterId { get; set; }

        public string ReturnNo { get; set; } = null!;
        public decimal TotalRefundAmount { get; set; }
        public string? RefundMode { get; set; }   // CASH/UPI/CARD/CREDIT_NOTE
        public string? Notes { get; set; }

        public DateTime ReturnedAt { get; set; }
        public string? ReturnedBy { get; set; }
        public Guid? ReturnedByUserId { get; set; }

        public DateTime CreatedAt { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class PharmacyReturnLine
    {
        [Key]
        public Guid ReturnLineId { get; set; }
        public Guid ReturnId { get; set; }
        public Guid ChargeEventId { get; set; }   // the original sale line this return is against
        public Guid InventoryItemId { get; set; }
        public Guid BatchId { get; set; }
        public decimal ReturnedQty { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal RefundAmount { get; set; }
    }
}
