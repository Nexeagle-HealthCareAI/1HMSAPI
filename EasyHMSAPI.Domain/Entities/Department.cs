using System.ComponentModel.DataAnnotations;

namespace EasyHMSAPI.Domain.Entities
{
    public class Department
    {
        [Key]
        public Guid DepartmentID { get; set; }
        public Guid? HospitalID { get; set; }
        public string Name { get; set; } = null!;
        public string? Description { get; set; }
        public bool IsActive { get; set; } = true;
        public Guid? CreatedByUserID { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public Hospital? Hospital { get; set; }
        public User? CreatedByUser { get; set; }
        public ICollection<HospitalDepartmentMapping> HospitalDepartmentMappings { get; set; } = new List<HospitalDepartmentMapping>();
        public ICollection<DoctorDepartment> DoctorDepartments { get; set; } = new List<DoctorDepartment>();
        public ICollection<Specialization> Specializations { get; set; } = new List<Specialization>();
    }
}