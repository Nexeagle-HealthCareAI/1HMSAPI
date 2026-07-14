using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    /// <summary>
    /// A Rapid Response Team activation against an admission — who called, why, who/when
    /// responded, and the outcome. Response time (ArrivedAt - CalledAt) is a safety KPI.
    /// Open (ResolvedAt == null) activations drive a hospital-wide "open RRT" list.
    /// </summary>
    [ExcludeFromCodeCoverage]
    [Table("RapidResponseActivation")]
    public class RapidResponseActivation
    {
        [Key]
        public Guid ActivationId { get; set; }
        public Guid HospitalId { get; set; }
        public Guid AdmissionId { get; set; }
        public Guid? EncounterId { get; set; }
        public string? PatientId { get; set; }

        public string TriggerReason { get; set; } = null!;
        public int? TriggeredEwsScore { get; set; }

        public string CalledBy { get; set; } = null!;
        public DateTime CalledAt { get; set; }

        public string? RespondingTeam { get; set; }
        public DateTime? ArrivedAt { get; set; }

        public string? Outcome { get; set; }
        public string? OutcomeNotes { get; set; }
        public DateTime? ResolvedAt { get; set; }

        public DateTime CreatedAt { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string? UpdatedBy { get; set; }

        [Timestamp]
        public byte[]? RowVersion { get; set; }
    }
}
