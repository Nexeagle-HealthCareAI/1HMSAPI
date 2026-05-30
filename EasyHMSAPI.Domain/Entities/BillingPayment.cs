using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    // Read model for dbo.BillingPayment (payments are written by the billing/IPD service;
    // here we only read them to show paid status on the consult timeline).
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
        public decimal Amount { get; set; }
        public DateTime PaidAt { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
