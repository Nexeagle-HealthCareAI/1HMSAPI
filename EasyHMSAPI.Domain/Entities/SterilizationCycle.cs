using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    /// <summary>Insert-only. On completion with a PASS/FAIL biological indicator result, linked InstrumentSet rows flip to STERILE/QUARANTINED.</summary>
    [ExcludeFromCodeCoverage]
    [Table("SterilizationCycle")]
    public class SterilizationCycle
    {
        [Key]
        public Guid SterilizationCycleId { get; set; }
        public Guid HospitalId { get; set; }

        public string CycleNumber { get; set; } = null!;
        public string? AutoclaveLabel { get; set; }
        public string CycleType { get; set; } = null!;   // STEAM/ETO/PLASMA

        public DateTime StartedAt { get; set; }
        public DateTime? EndedAt { get; set; }

        public string BiologicalIndicatorResult { get; set; } = null!;   // PASS/FAIL/PENDING
        public string? ChemicalIndicatorResult { get; set; }              // PASS/FAIL

        public string OperatorName { get; set; } = null!;
        public Guid? OperatorByUserId { get; set; }

        public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; }
        public string? CreatedBy { get; set; }

        [Timestamp]
        public byte[]? RowVersion { get; set; }
    }
}
