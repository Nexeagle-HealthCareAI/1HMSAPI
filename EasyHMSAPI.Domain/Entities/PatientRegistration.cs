using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    [ExcludeFromCodeCoverage]
    [Table("PatientRegistrations")]
    public class PatientRegistration
    {
        [Key]
        public Guid RegistrationId { get; set; }
        public Guid HospitalId { get; set; }
        public string? PatientId { get; set; }
        public DateTime? RegisteredAt { get; set; }
        public Guid? RegisteredBy { get; set; }
        public string? FullName { get; set; }
        public string? Mobile { get; set; }
        public short? AgeYears { get; set; }
        public string? Sex { get; set; }
        public string? AddressLine { get; set; }
        public string? City { get; set; }
        public string? State { get; set; }
        public string? Country { get; set; }
        public string? Pincode { get; set; }
        public string? InsuranceId { get; set; }

        // Additional demographics captured at booking (all optional).
        public string? BloodGroup { get; set; }
        public string? Allergies { get; set; }
        public string? Block { get; set; }
        public string? AlternateMobile { get; set; }
        public string? Email { get; set; }
        public string? EmergencyContactName { get; set; }
        public string? EmergencyContactRelation { get; set; }
        public string? EmergencyContactPhone { get; set; }

        // Admission-module demographics, government IDs & granular address (all optional).
        public DateTime? DateOfBirth { get; set; }
        public string? Religion { get; set; }
        public string? Nationality { get; set; }
        public string? AadhaarNumber { get; set; }
        public string? PanNumber { get; set; }
        public string? AbhaId { get; set; }
        public string? FlatHouse { get; set; }
        public string? Street { get; set; }
        public string? District { get; set; }

        // Duplicate-merge audit: when set, this record was merged into the canonical UHID and is
        // hidden from pickers; kept so old printed UHIDs still resolve. [[admission-module]]
        public string? MergedIntoPatientId { get; set; }
        public DateTime? MergedAt { get; set; }
        public string? MergedBy { get; set; }
    }
}
