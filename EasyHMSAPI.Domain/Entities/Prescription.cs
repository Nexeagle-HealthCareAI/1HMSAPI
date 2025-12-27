using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    [ExcludeFromCodeCoverage]
    public class Prescription
    {
        [Key]
        public Guid PrescriptionId { get; set; }
        public Guid ApptId { get; set; }
        public Guid HospitalId { get; set; }
        public Guid DoctorId { get; set; }
        public string? PatientId { get; set; }
        public string? MetaJson { get; set; }
        public string? Status { get; set; }
        public string? ChiefComplaint { get; set; }
        public string? History { get; set; }
        public string? Comorbidity { get; set; }
        public string? Examination { get; set; }
        public string? Diagnosis { get; set; }
        public string? PrivateNotes { get; set; }
        public string? CertificatesAndNotes { get; set; }
        public string? Immunizations { get; set; }
        public DateTime? FollowUpDate { get; set; }
        public string? FollowUpNotes { get; set; }
        public string? Referral { get; set; }
        public string? NonPharmacologicalAdvice { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string? UpdateBy { get; set; }
        public byte[]? RowVersion { get; set; }
    }
}
