using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    /// <summary>Insert-only — re-assess by inserting a new row, latest wins (mirrors NursingAssessment).</summary>
    [ExcludeFromCodeCoverage]
    [Table("PreOpAssessment")]
    public class PreOpAssessment
    {
        [Key]
        public Guid PreOpAssessmentId { get; set; }
        public Guid HospitalId { get; set; }
        public Guid SurgeryCaseId { get; set; }

        public string? AsaGrade { get; set; }   // I..VI
        public bool NpoConfirmed { get; set; }
        public bool AllergiesReviewed { get; set; }
        public bool InvestigationsReviewed { get; set; }
        public bool ConsentConfirmed { get; set; }

        public string? Notes { get; set; }

        public string AssessedBy { get; set; } = null!;
        public DateTime AssessedAt { get; set; }
    }
}
