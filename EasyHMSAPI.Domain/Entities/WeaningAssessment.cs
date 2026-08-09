using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    /// <summary>
    /// One shift's ventilator-weaning assessment -- whether a Spontaneous Awakening Trial (SAT)
    /// and Spontaneous Breathing Trial (SBT) were performed and passed. Standard ABCDEF-bundle
    /// data points; insert-only, one row per assessment (not latest-wins -- each shift's
    /// assessment is its own record, read back as history the way SOFA scores are).
    /// </summary>
    [ExcludeFromCodeCoverage]
    [Table("WeaningAssessment")]
    public class WeaningAssessment
    {
        [Key]
        public Guid WeaningAssessmentId { get; set; }
        public Guid HospitalId { get; set; }
        public Guid AdmissionId { get; set; }
        public Guid? EncounterId { get; set; }
        public string? PatientId { get; set; }

        public bool SatPerformed { get; set; }
        public bool SatPassed { get; set; }
        public bool SbtPerformed { get; set; }
        public bool SbtPassed { get; set; }

        public string? Notes { get; set; }

        public string AssessedBy { get; set; } = null!;
        public DateTime AssessedAt { get; set; }

        public DateTime CreatedAt { get; set; }
        public string? CreatedBy { get; set; }

        [Timestamp]
        public byte[]? RowVersion { get; set; }
    }
}
