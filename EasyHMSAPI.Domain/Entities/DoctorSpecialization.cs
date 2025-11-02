using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    [ExcludeFromCodeCoverage]
    public class DoctorSpecialization
    {
        [Key]
        public Guid DoctorSpecializationID { get; set; }
        public Guid DoctorID { get; set; }
        public Guid SpecializationID { get; set; }
        public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
        public Doctor Doctor { get; set; } = null!;
        public Specialization Specialization { get; set; } = null!;
    }
} 