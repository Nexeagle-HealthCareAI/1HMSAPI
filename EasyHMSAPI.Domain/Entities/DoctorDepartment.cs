using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    [ExcludeFromCodeCoverage]
    public class DoctorDepartment
    {
        [Key]
        public Guid DoctorDepartmentID { get; set; }
        public Guid DoctorID { get; set; }
        public Guid DepartmentID { get; set; }
        public Guid? HospitalId { get; set; } // Added hospitalId
        public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
        public Doctor Doctor { get; set; } = null!;
        public Department Department { get; set; } = null!;
    }
}