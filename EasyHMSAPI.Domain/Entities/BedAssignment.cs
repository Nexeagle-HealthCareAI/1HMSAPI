using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    /// <summary>
    /// Links an admission to a bed for a span of time. Filtered unique indexes in the DB guarantee
    /// at most one ACTIVE row per bed and per admission (concurrency backstop for bed assignment).
    /// DailyRateSnapshot freezes the bed's rate at assignment time so a later master-rate change
    /// (or a ward-&gt;ICU transfer) re-rates cleanly from the segment boundary.
    /// </summary>
    [ExcludeFromCodeCoverage]
    [Table("BedAssignment")]
    public class BedAssignment
    {
        [Key]
        public Guid AssignmentId { get; set; }
        public Guid HospitalId { get; set; }
        public Guid AdmissionId { get; set; }
        public Guid BedId { get; set; }

        public DateTime AssignedAt { get; set; }
        public string? AssignedBy { get; set; }
        public DateTime? ReleasedAt { get; set; }
        public string? ReleasedBy { get; set; }

        public decimal DailyRateSnapshot { get; set; }

        public string StatusCode { get; set; } = "ACTIVE";   // ACTIVE / RELEASED
        public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string? UpdatedBy { get; set; }

        [Timestamp]
        public byte[]? RowVersion { get; set; }
    }
}
