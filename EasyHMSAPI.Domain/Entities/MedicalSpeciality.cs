using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    // One row per NMC MD/MS/DM/MCh speciality (e.g. "Cardiology", "Neurosurgery"). Global
    // reference data — see db/data/seed/seed_medical_specialities.sql for the full catalog.
    [ExcludeFromCodeCoverage]
    public class MedicalSpeciality
    {
        [Key]
        public Guid SpecialityId { get; set; }
        public string QualificationTypeCode { get; set; } = null!;
        public string Name { get; set; } = null!;
        public string? PatientFacingName { get; set; }
        // Normalized patient-search bucket — several NMC rows can share one (e.g. DM Medical
        // Oncology + MCh Surgical Oncology + MD Radiation Oncology all -> "Oncologist (Cancer)").
        public string? PatientFacingCategory { get; set; }
        public bool SixYearDirectRouteAvailable { get; set; }
        public int SortOrder { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public MedicalQualificationType QualificationType { get; set; } = null!;
        public ICollection<MedicalSpecialityFeeder> Feeders { get; set; } = new List<MedicalSpecialityFeeder>();
        public ICollection<MedicalSpecialityFeeder> FeedsInto { get; set; } = new List<MedicalSpecialityFeeder>();
    }
}
