using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    /// <summary>
    /// One row per planned/performed surgery. StatusCode drives the case lifecycle
    /// (REQUESTED-&gt;SCHEDULED-&gt;PRE_OP-&gt;IN_THEATRE-&gt;POST_OP-&gt;COMPLETED, or CANCELLED);
    /// every transition is also logged to SurgeryStatusHistory.
    /// </summary>
    [ExcludeFromCodeCoverage]
    [Table("SurgeryCase")]
    public class SurgeryCase
    {
        [Key]
        public Guid SurgeryCaseId { get; set; }
        public Guid HospitalId { get; set; }
        public Guid AdmissionId { get; set; }
        public Guid? EncounterId { get; set; }
        public string? PatientId { get; set; }

        public string ProcedureName { get; set; } = null!;
        public string SurgeryType { get; set; } = null!;   // ELECTIVE/EMERGENCY
        public string Urgency { get; set; } = null!;       // ROUTINE/URGENT/EMERGENCY

        public string? RequestedBy { get; set; }
        public DateTime RequestedAt { get; set; }

        public Guid? SurgeonDoctorId { get; set; }
        public string? SurgeonName { get; set; }
        public Guid? AnaesthetistDoctorId { get; set; }
        public string? AnaesthetistName { get; set; }

        public string StatusCode { get; set; } = null!;
        public string? CancelledReason { get; set; }

        public DateTime CreatedAt { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string? UpdatedBy { get; set; }

        [Timestamp]
        public byte[]? RowVersion { get; set; }
    }
}
