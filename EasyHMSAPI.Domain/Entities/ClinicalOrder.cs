using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    /// <summary>
    /// CPOE order header. One generic schema covers every order type (see
    /// IpdConstants.ClinicalOrderType) — Phase 3 only exercises MEDICATION.
    /// </summary>
    [ExcludeFromCodeCoverage]
    [Table("ClinicalOrder")]
    public class ClinicalOrder
    {
        [Key]
        public Guid OrderId { get; set; }
        public Guid HospitalId { get; set; }
        public Guid AdmissionId { get; set; }
        // Null when the admission has IPD billing disabled — order lines then can't be charged.
        public Guid? EncounterId { get; set; }
        public string PatientId { get; set; } = null!;

        public string OrderType { get; set; } = "MEDICATION";
        public string StatusCode { get; set; } = "ACTIVE";   // ACTIVE / DISCONTINUED / COMPLETED

        public DateTime OrderedAt { get; set; }
        public string? OrderedBy { get; set; }
        public Guid? OrderedByDoctorId { get; set; }

        public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string? UpdatedBy { get; set; }

        [Timestamp]
        public byte[]? RowVersion { get; set; }
    }
}
