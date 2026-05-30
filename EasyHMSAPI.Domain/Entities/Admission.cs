using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    [ExcludeFromCodeCoverage]
    [Table("Admission")]
    public class Admission
    {
        [Key]
        public Guid AdmissionId { get; set; }

        public Guid HospitalId { get; set; }
        public string PatientId { get; set; } = null!;
        public Guid EncounterId { get; set; }
        public Guid? PrimaryDoctorId { get; set; }

        public string AdmissionNo { get; set; } = null!;

        public DateTime AdmittedAt { get; set; }
        public string? AdmittedBy { get; set; }

        public DateTime? ExpectedDischargeAt { get; set; }

        public DateTime? DischargedAt { get; set; }
        public string? DischargedBy { get; set; }
        public string? DischargeNotes { get; set; }

        public string StatusCode { get; set; } = "ADMITTED";   // ADMITTED / DISCHARGED / CANCELLED

        public string? AdmissionReason { get; set; }
        public string? Diagnosis { get; set; }

        public DateTime? CancelledAt { get; set; }
        public string? CancelledBy { get; set; }
        public string? CancelReason { get; set; }

        public DateTime CreatedAt { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string? UpdatedBy { get; set; }

        [Timestamp]
        public byte[]? RowVersion { get; set; }
    }
}
