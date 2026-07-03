using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    /// <summary>One operative record per SurgeryCase. Vitals during surgery reuse the existing VitalReading feature.</summary>
    [ExcludeFromCodeCoverage]
    [Table("IntraOpRecord")]
    public class IntraOpRecord
    {
        [Key]
        public Guid IntraOpRecordId { get; set; }
        public Guid HospitalId { get; set; }
        public Guid SurgeryCaseId { get; set; }

        public string? AnaesthesiaType { get; set; }   // GA/SPINAL/EPIDURAL/LOCAL/SEDATION/REGIONAL
        public DateTime? AnaesthesiaStartAt { get; set; }
        public DateTime? AnaesthesiaEndAt { get; set; }

        public DateTime? SurgeryStartAt { get; set; }   // incision
        public DateTime? SurgeryEndAt { get; set; }      // closure

        public decimal? EstimatedBloodLossMl { get; set; }
        public string? Findings { get; set; }
        public string? ProcedurePerformed { get; set; }
        public string? SurgicalTeam { get; set; }
        public string? ComplicationsNotes { get; set; }

        public string RecordedBy { get; set; } = null!;
        public DateTime RecordedAt { get; set; }

        public DateTime CreatedAt { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string? UpdatedBy { get; set; }

        [Timestamp]
        public byte[]? RowVersion { get; set; }
    }
}
