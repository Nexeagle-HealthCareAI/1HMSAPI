using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    [ExcludeFromCodeCoverage]
    [Table("AppointmentVitals")]
    public class AppointmentVitals
    {
        [Key]
        public Guid VitalId { get; set; }
        [Required]
        public Guid HospitalId { get; set; }
        [Required]
        [MaxLength(20)]
        public string PatientId { get; set; } = string.Empty;
        [Required]
        public Guid ApptId { get; set; }
        [Required]
        public string VitalsJson { get; set; } = string.Empty;
        public Guid? RecordedBy { get; set; }
        [Required]
        public DateTime RecordedAt { get; set; }
    }
}
