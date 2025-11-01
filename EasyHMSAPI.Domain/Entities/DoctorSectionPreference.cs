using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EasyHMSAPI.Domain.Entities
{
    public class DoctorSectionPreference
    {
        [Key]
        public Guid PreferenceId { get; set; }
        public Guid HospitalId { get; set; }
        public Guid DoctorId { get; set; }
        public bool Vitals { get; set; }
        public bool ChiefComplaint { get; set; }
        public bool History { get; set; }
        public bool Comorbidity { get; set; }
        public bool Examination { get; set; }
        public bool Diagnosis { get; set; }
        public bool Investigations { get; set; }
        public bool Procedures { get; set; }
        public bool Medications { get; set; }
        public bool PrivateNotes { get; set; }
        public bool CertificatesAndNotes { get; set; }
        public bool Immunizations { get; set; }
        public bool FollowUpAndReferral { get; set; }
        public bool NonPharmacologicalAdvice { get; set; }
        public bool Attachments { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public DateTime UpdatedAtUtc { get; set; }
        [Timestamp]
        public byte[]? RowVersion { get; set; }
    }
}
