using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    [ExcludeFromCodeCoverage]
    [Table("BillingChargeEvent")]
    public class BillingChargeEvent
    {
        [Key]
        public Guid ChargeEventId { get; set; }
        public Guid HospitalId { get; set; }
        public string? PatientId { get; set; }
        public Guid EncounterId { get; set; }
        public string? SourceModule { get; set; }
        // Free-text idempotency key from the source module (DB column is NVARCHAR(100), not a GUID).
        public string? SourceRefId { get; set; }

        // Links back to the originating ChargeMaster row, when one exists (manual free-text
        // charges have none). Nullable — legacy rows predating this column have no reliable
        // backfill and stay null. Needed to join ChargeMaster.IsIRDAIPayable at discharge time.
        public Guid? ChargeId { get; set; }

        public string? CategoryCode { get; set; }
        public string? DisplayName { get; set; }
        public decimal Qty { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal? GrossAmount { get; set; }
        public decimal? DiscountAmount { get; set; }
        public decimal NetAmount { get; set; }

        // Incentive for this line: seeded from ChargeMaster/BedMaster, editable per bill; null/0 = none.
        public decimal? IncentiveAmount { get; set; }

        // Best-effort treating-doctor attribution for consultant incentive accrual. No FK — same
        // soft-link convention as ClinicalOrder.OrderedByDoctorId.
        public Guid? AttributedDoctorId { get; set; }

        // GST snapshot (NULL on legacy rows)
        public string? HsnSacCode { get; set; }
        public decimal? GstRate { get; set; }
        public decimal? TaxableAmount { get; set; }
        public decimal CgstAmount { get; set; }
        public decimal SgstAmount { get; set; }
        public decimal IgstAmount { get; set; }
        public decimal TaxAmount { get; set; }
        public bool IsTaxInclusive { get; set; }
        public bool IsInterState { get; set; }

        // Client-generated key from the offline sync engine's outbox replay (Idempotency-Key
        // header) — lets a retried "add-event" call be detected and short-circuited instead of
        // re-posting the same batch of charges. Null for calls made without the header.
        public string? IdempotencyKey { get; set; }

        public string? StatusCode { get; set; }
        public DateTime ServiceDate { get; set; }
        public bool IsBackdated { get; set; }
        public string? BackdateReason { get; set; }
        public DateTime? PostedAt { get; set; }
        public string? PostedBy { get; set; }
        public DateTime? VoidedAt { get; set; }
        public string? VoidedBy { get; set; }
        public string? VoidReason { get; set; }
        public string? MetaJson { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string? UpdatedBy { get; set; }
    }
}
