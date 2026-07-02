using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    /// <summary>
    /// One discharge summary per admission (UNIQUE HospitalId+AdmissionId). Signing locks further
    /// edits — a medico-legal document handed to the patient / submitted to a TPA, unlike round
    /// notes' addendum model.
    /// </summary>
    [ExcludeFromCodeCoverage]
    [Table("DischargeSummary")]
    public class DischargeSummary
    {
        [Key]
        public Guid DischargeSummaryId { get; set; }
        public Guid HospitalId { get; set; }
        public Guid AdmissionId { get; set; }
        public Guid? EncounterId { get; set; }
        public string? PatientId { get; set; }

        public string? AdmittingDiagnosis { get; set; }
        public string? FinalDiagnosis { get; set; }
        public string? ChiefComplaint { get; set; }
        public string? HistoryOfPresentIllness { get; set; }
        public string? CourseInHospital { get; set; }
        public string? ProceduresPerformed { get; set; }

        public string? ConditionAtDischarge { get; set; }   // STABLE/IMPROVED/RECOVERED/REFERRED/LAMA/EXPIRED

        public string? DischargeMedications { get; set; }
        public string? FollowUpInstructions { get; set; }
        public DateTime? FollowUpDate { get; set; }
        public string? DietInstructions { get; set; }
        public string? ActivityRestrictions { get; set; }
        public string? AdditionalNotes { get; set; }

        public bool IsSigned { get; set; }
        public DateTime? SignedAt { get; set; }
        public string? SignedBy { get; set; }
        public Guid? SignedByDoctorId { get; set; }
        public string? SignedByDoctorName { get; set; }

        public DateTime CreatedAt { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string? UpdatedBy { get; set; }

        [Timestamp]
        public byte[]? RowVersion { get; set; }
    }
}
