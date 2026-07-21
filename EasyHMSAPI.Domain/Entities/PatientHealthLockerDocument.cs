using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    // Patient-initiated uploads for Doctor Dekho's "Health Locker" — deliberately separate from
    // PrescriptionAttachment (hospital/appointment/doctor-scoped, uploaded by staff): these are
    // owned by the patient's own OTP-verified Mobile (see PublicPatientAuth), so they exist even
    // for a patient with no PatientRegistration/appointment at any hospital yet. ApptId is optional
    // — a patient MAY tag an upload to a past appointment for their own context, never required.
    [ExcludeFromCodeCoverage]
    [Table("PatientHealthLockerDocuments")]
    public class PatientHealthLockerDocument
    {
        [Key]
        public Guid DocumentId { get; set; }
        public string Mobile { get; set; } = string.Empty;
        public Guid? ApptId { get; set; }

        public string? DocumentType { get; set; }
        public string? FileName { get; set; }
        public string? StorageUrl { get; set; }
        public string? Notes { get; set; }

        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    }
}
