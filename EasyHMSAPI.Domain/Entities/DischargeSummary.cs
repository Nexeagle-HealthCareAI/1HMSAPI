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
        public string? FinalDiagnosisIcd10Code { get; set; }
        public string? FinalDiagnosisIcd10Name { get; set; }
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

        // Stable object-key prefix for re-signing a fresh presigned URL on every view (never a raw
        // URL — those expire). AccessToken is the long random opaque string a QR/WhatsApp link
        // encodes — lets the patient view the PDF without logging in, without being guessable.
        public string? PdfBlobKey { get; set; }
        public string? AccessToken { get; set; }
        public DateTime? PdfUploadedAt { get; set; }

        public DateTime CreatedAt { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string? UpdatedBy { get; set; }

        [Timestamp]
        public byte[]? RowVersion { get; set; }
    }
}
