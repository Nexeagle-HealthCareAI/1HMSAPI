using System.ComponentModel.DataAnnotations;

namespace EasyHMSAPI.Domain.Entities
{
    public class DoctorDepartment
    {
        [Key]
        public Guid DoctorDepartmentID { get; set; }
        public Guid DoctorID { get; set; }
        public Guid DepartmentID { get; set; }
        public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
        public Doctor Doctor { get; set; } = null!;
        public Department Department { get; set; } = null!;
    }
} 