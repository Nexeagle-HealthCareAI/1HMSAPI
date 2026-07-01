using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    /// <summary>
    /// One line of a CPOE order (e.g. one drug within a medication order). Medication-specific
    /// fields are unused for future non-medication OrderTypes. ChargeEventId is back-filled at
    /// order time when the line has a ChargeId (charge-on-event); voided, not deleted, if the
    /// line is later discontinued after billing already went through.
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

        public string? DrugName { get; set; }
        public string? SaltName { get; set; }
        public string? Dose { get; set; }
        public string? Route { get; set; }
        public string? Frequency { get; set; }
        public int? DurationDays { get; set; }
        public string? Instructions { get; set; }

        public decimal Qty { get; set; } = 1;

        public string StatusCode { get; set; } = "ACTIVE";   // ACTIVE / DISCONTINUED

        public Guid? ChargeEventId { get; set; }

        public DateTime CreatedAt { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string? UpdatedBy { get; set; }
    }
}
