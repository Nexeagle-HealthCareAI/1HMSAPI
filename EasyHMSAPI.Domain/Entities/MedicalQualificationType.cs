using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    // NMC qualification-ladder tier: 'MD'/'MS' (Broad) or 'DM'/'MCh' (SuperSpeciality).
    // Global reference data — see db/schema/migrations/create_medical_specialities_tables.sql.
    [ExcludeFromCodeCoverage]
    public class MedicalQualificationType
    {
        [Key]
        public string QualificationTypeCode { get; set; } = null!;
        public string Name { get; set; } = null!;
        public string Tier { get; set; } = null!;
        public bool IsSurgical { get; set; }
        public byte TypicalDurationYears { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public ICollection<MedicalSpeciality> Specialities { get; set; } = new List<MedicalSpeciality>();
    }
}
