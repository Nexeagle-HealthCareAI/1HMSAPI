using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    /// <summary>
    /// Structured, per-patient allergy (table defined in easyHMSDatabase create_tables_medication_safety.sql).
    /// Read-only here: used by the prescription safety check alongside the free-text
    /// PatientRegistration.Allergies captured at booking.
    /// </summary>
    [ExcludeFromCodeCoverage]
    [Table("PatientAllergy")]
    public class PatientAllergy
    {
        [Key]
        public Guid PatientAllergyId { get; set; }
        public Guid HospitalId { get; set; }
        public string PatientId { get; set; } = null!;
        public string AllergyType { get; set; } = "DRUG";   // DRUG | FOOD | ENVIRONMENT | OTHER
        public string Allergen { get; set; } = null!;
        public string Severity { get; set; } = "MODERATE";  // MILD | MODERATE | SEVERE | ANAPHYLAXIS
        public string? Reaction { get; set; }
        public bool IsActive { get; set; } = true;
    }
}
