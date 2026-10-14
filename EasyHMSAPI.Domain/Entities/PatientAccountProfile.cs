using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    // The details a patient keeps on their own Doctor Dekho account and edits themselves. Separate from PatientRegistration on purpose:
    // those rows belong to the hospital that registered the patient, so a patient editing here never changes a hospital's record.
    // Keyed by the OTP-verified mobile, like the Health Locker.
    [ExcludeFromCodeCoverage]
    [Table("PatientAccountProfiles")]
    public class PatientAccountProfile
    {
        [Key]
        public string Mobile { get; set; } = null!;
        public string FullName { get; set; } = null!;
        public short? Age { get; set; }
        // Y | M | D
        public string? AgeUnit { get; set; }
        // Male | Female | Other
        public string? Sex { get; set; }
        public string? Email { get; set; }
        public string? GuardianName { get; set; }
        public string? GuardianRelation { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
