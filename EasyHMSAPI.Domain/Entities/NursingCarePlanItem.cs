using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    /// <summary>One nursing-diagnosis entry (free text, not a coded NANDA-I taxonomy) with a goal,
    /// planned interventions, and an ACTIVE/RESOLVED/DISCONTINUED lifecycle.</summary>
    [ExcludeFromCodeCoverage]
    [Table("NursingCarePlanItem")]
    public class NursingCarePlanItem
    {
        [Key]
        public Guid CarePlanItemId { get; set; }
        public Guid HospitalId { get; set; }
        public Guid AdmissionId { get; set; }
        public Guid? EncounterId { get; set; }
        public string? PatientId { get; set; }

        public string NursingDiagnosis { get; set; } = null!;
        public string? Goal { get; set; }
        public string? PlannedInterventions { get; set; }

        public string StatusCode { get; set; } = "ACTIVE";   // ACTIVE / RESOLVED / DISCONTINUED

        public DateTime CreatedAt { get; set; }
        public string? CreatedBy { get; set; }
        public Guid? CreatedByUserId { get; set; }

        public DateTime? ResolvedAt { get; set; }
        public string? ResolvedBy { get; set; }
        public Guid? ResolvedByUserId { get; set; }
        public string? ResolutionNotes { get; set; }

        public DateTime UpdatedAt { get; set; }
        public string? UpdatedBy { get; set; }

        [Timestamp]
        public byte[]? RowVersion { get; set; }
    }
}
