using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    // Maps dbo.BillingPayment (read on the consult timeline; written by the payment handler).
    [ExcludeFromCodeCoverage]
    [Table("BillingPayment")]
    public class BillingPayment
    {
        [Key]
        public Guid PaymentId { get; set; }
        public Guid HospitalId { get; set; }
        public string? PatientId { get; set; }
        public Guid EncounterId { get; set; }
        public string? ReceiptNo { get; set; }
        public string? PaymentType { get; set; }   // PAYMENT / ADVANCE / REFUND
        public string? PaymentMode { get; set; }
        public string? PaymentDescription { get; set; }
        public string? TransactionId { get; set; }
        public decimal Amount { get; set; }
        public DateTime PaidAt { get; set; }
        public Guid? ReferencePaymentId { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string? UpdatedBy { get; set; }
    }
}
