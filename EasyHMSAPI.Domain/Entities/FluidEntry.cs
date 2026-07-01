using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    /// <summary>One intake/output entry (fluid balance charting).</summary>
    [ExcludeFromCodeCoverage]
    [Table("FluidEntry")]
    public class FluidEntry
    {
        [Key]
        public Guid FluidEntryId { get; set; }
        public Guid HospitalId { get; set; }
        public Guid AdmissionId { get; set; }
        public Guid? EncounterId { get; set; }
        public string? PatientId { get; set; }

        public string Direction { get; set; } = null!;   // IN / OUT
        public string Subtype { get; set; } = null!;      // free text: Urine/IV/Oral/Drain_A/...
        public decimal VolumeMl { get; set; }
        public string? Description { get; set; }
        public string? RouteOrSite { get; set; }
        public string? Colour { get; set; }

        public DateTime RecordedAt { get; set; }
        public string? RecordedBy { get; set; }
        public Guid? RecordedByUserId { get; set; }

        public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string? UpdatedBy { get; set; }

        [Timestamp]
        public byte[]? RowVersion { get; set; }
    }
}
