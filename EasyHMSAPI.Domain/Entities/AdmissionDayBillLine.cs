using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    /// <summary>
    /// Frozen snapshot of a single charge line captured when an admission day was closed.
    /// Values are copied from the BillingChargeEvent at close-time so the interim bill
    /// never changes if the underlying charge is later edited or voided. ChargeEventId is
    /// kept so we can tell which charges have already been billed into a closed day.
    /// </summary>
    [ExcludeFromCodeCoverage]
    [Table("AdmissionDayBillLine")]
    public class AdmissionDayBillLine
    {
        [Key]
        public Guid AdmissionDayBillLineId { get; set; }

        public Guid AdmissionDayBillId { get; set; }
        public Guid HospitalId { get; set; }
        public Guid ChargeEventId { get; set; }

        public string? CategoryCode { get; set; }
        public string? DisplayName { get; set; }
        public DateTime ServiceDate { get; set; }

        public decimal Qty { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal GrossAmount { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal NetAmount { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}
