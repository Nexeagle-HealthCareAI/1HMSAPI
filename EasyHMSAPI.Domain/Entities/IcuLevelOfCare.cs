using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    /// <summary>Insert-only — re-assess by inserting a new row, latest wins (mirrors NursingAssessment/PreOpAssessment).</summary>
    [ExcludeFromCodeCoverage]
    [Table("IcuLevelOfCare")]
    public class IcuLevelOfCare
    {
        [Key]
        public Guid IcuLevelOfCareId { get; set; }
        public Guid HospitalId { get; set; }
        public Guid AdmissionId { get; set; }
        public Guid? EncounterId { get; set; }
        public string? PatientId { get; set; }

        public string Level { get; set; } = null!;   // LEVEL_1/LEVEL_2/LEVEL_3
        public string? Reason { get; set; }
        public string? Notes { get; set; }

        public string AssessedBy { get; set; } = null!;
        public DateTime AssessedAt { get; set; }
    }
}
