using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    /// <summary>
    /// Occupational health vaccination record.
    /// Tracks mandatory hospital staff vaccines: Hepatitis B (3 doses + antibody titer),
    /// Tetanus Toxoid, Typhoid, Influenza.
    /// </summary>
    [ExcludeFromCodeCoverage]
    [Table("HrVaccinationRecords")]
    public class HrVaccinationRecord
    {
        [Key]
        public Guid HrVaccinationRecordId { get; set; } = Guid.NewGuid();

        [Required]
        public Guid HrEmployeeId { get; set; }

        /// <summary>"Hepatitis B", "Tetanus Toxoid", "Typhoid", "Influenza"</summary>
        [Required]
        [MaxLength(100)]
        public string VaccineName { get; set; } = null!;

        /// <summary>Dose number (e.g. 1, 2, 3 for Hepatitis B).</summary>
        public int DoseNumber { get; set; } = 1;

        public DateOnly AdministeredOn { get; set; }

        /// <summary>Next due date for boosters or follow-up doses.</summary>
        public DateOnly? NextDueDate { get; set; }

        [MaxLength(100)]
        public string? BatchNumber { get; set; }

        [MaxLength(100)]
        public string? AdministeredBy { get; set; }

        /// <summary>For Hepatitis B: antibody titer test result (IU/L).</summary>
        [MaxLength(50)]
        public string? AntibodyTiterResult { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public HrEmployee HrEmployee { get; set; } = null!;
    }
}
