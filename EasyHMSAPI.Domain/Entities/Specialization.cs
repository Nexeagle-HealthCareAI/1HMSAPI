using System.ComponentModel.DataAnnotations;

namespace EasyHMSAPI.Domain.Entities
{
    public class Specialization
    {
        [Key]
        public Guid SpecializationID { get; set; }
        public Guid DepartmentID { get; set; }
        public Guid? HospitalID { get; set; }
        public string Name { get; set; } = null!;
        public string? Description { get; set; }
        public bool IsActive { get; set; } = true;
        public Guid? CreatedByUserID { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public Department Department { get; set; } = null!;
        public Hospital? Hospital { get; set; }
        public User? CreatedByUser { get; set; }
        public ICollection<DoctorSpecialization> DoctorSpecializations { get; set; } = new List<DoctorSpecialization>();
    }
}