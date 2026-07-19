using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    // Many-to-many bridge: a DM/MCh super-speciality (SpecialityId) can accept more than one
    // valid feeder MD/MS (FeederSpecialityId) — e.g. DM Cardiology accepts MD Medicine,
    // Paediatrics, or Respiratory Medicine.
    [ExcludeFromCodeCoverage]
    public class MedicalSpecialityFeeder
    {
        public Guid SpecialityId { get; set; }
        public Guid FeederSpecialityId { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public MedicalSpeciality Speciality { get; set; } = null!;
        public MedicalSpeciality FeederSpeciality { get; set; } = null!;
    }
}
