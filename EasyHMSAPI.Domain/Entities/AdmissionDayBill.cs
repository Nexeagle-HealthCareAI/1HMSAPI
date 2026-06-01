using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    /// <summary>
    /// A locked, numbered interim bill for one admission "billing day".
    /// Billing days are admission-anchored 24h windows: Day N runs from
    /// AdmittedAt + (N-1)*24h to AdmittedAt + N*24h.
    /// Closing a day snapshots every un-billed posted charge up to the end of
    /// that window into <see cref="AdmissionDayBillLine"/> rows, so a reprint is
    /// stable even if a charge is later voided or a late charge is added.
    /// </summary>
    [ExcludeFromCodeCoverage]
    [Table("AdmissionDayBill")]
    public class AdmissionDayBill
    {
        [Key]
        public Guid AdmissionDayBillId { get; set; }

        public Guid HospitalId { get; set; }
        public Guid AdmissionId { get; set; }
        public Guid EncounterId { get; set; }
        public string? PatientId { get; set; }

        // 1-based billing-day index within the admission.
        public int DayNumber { get; set; }

        // Admission-anchored 24h window this bill covers.
        public DateTime FromUtc { get; set; }
        public DateTime ToUtc { get; set; }

        public string InterimBillNo { get; set; } = null!;

        // Snapshot totals for this day's lines.
        public int LineCount { get; set; }
        public decimal GrossAmount { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal NetAmount { get; set; }

        // Running figures captured at close-time (for the printed interim bill).
        public decimal CumulativeNetAmount { get; set; }   // all day-bills up to and including this one
        public decimal AdvanceReceived { get; set; }       // payments received up to close-time
        public decimal BalanceDue { get; set; }            // CumulativeNetAmount - AdvanceReceived

        // CLOSED / REOPENED
        public string StatusCode { get; set; } = "CLOSED";

        public DateTime ClosedAt { get; set; }
        public string? ClosedBy { get; set; }

        public DateTime? ReopenedAt { get; set; }
        public string? ReopenedBy { get; set; }
        public string? ReopenReason { get; set; }

        public DateTime CreatedAt { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string? UpdatedBy { get; set; }

        [Timestamp]
        public byte[]? RowVersion { get; set; }
    }
}
