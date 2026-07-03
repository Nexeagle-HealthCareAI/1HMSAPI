using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    /// <summary>
    /// One line of a CPOE order (e.g. one drug within a medication order, one test within a lab
    /// order). Type-specific fields are unused/null outside their own OrderType. ChargeEventId is
    /// back-filled at order time when the line has a ChargeId (charge-on-event); voided, not
    /// deleted, if the line is later discontinued after billing already went through.
    /// </summary>
    [ExcludeFromCodeCoverage]
    [Table("ClinicalOrderLine")]
    public class ClinicalOrderLine
    {
        [Key]
        public Guid OrderLineId { get; set; }
        public Guid OrderId { get; set; }
        public Guid HospitalId { get; set; }

        public Guid? ChargeId { get; set; }
        public int DisplayOrder { get; set; }

        // Generic label for what was ordered: drug name / test name / study name / procedure
        // name / diet or nursing instruction.
        public string? ItemName { get; set; }

        // Medication-specific detail; null/unused for other OrderTypes.
        public string? SaltName { get; set; }
        public string? Dose { get; set; }
        public string? Route { get; set; }
        public string? Frequency { get; set; }
        public int? DurationDays { get; set; }
        public string? Instructions { get; set; }

        // Lab/Radiology/Procedure detail; null/unused for MEDICATION and DIET/NURSING.
        public string? Urgency { get; set; }          // ROUTINE / URGENT / STAT
        public DateTime? ScheduledAt { get; set; }    // when a Procedure/Surgery order is planned for

        // MEDICATION-only: requires a second-nurse witness co-sign at MAR administration.
        public bool IsHighAlert { get; set; }

        // Accrues one charge per IST day the line stays ACTIVE (oxygen, continuous monitoring),
        // instead of charging once at order time. Consumed by the nightly recurring-charge job.
        public bool IsDailyRecurringCharge { get; set; }

        public decimal Qty { get; set; } = 1;

        public string StatusCode { get; set; } = "ACTIVE";   // ACTIVE / DISCONTINUED

        public Guid? ChargeEventId { get; set; }

        public DateTime CreatedAt { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string? UpdatedBy { get; set; }
    }
}
