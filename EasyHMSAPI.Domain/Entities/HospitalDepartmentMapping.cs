using System.ComponentModel.DataAnnotations;

namespace EasyHMSAPI.Domain.Entities
{
    public class HospitalDepartmentMapping
    {
        [Key]
        public Guid MappingID { get; set; }
        public Guid HospitalID { get; set; }
        public Guid DepartmentID { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime MappedAt { get; set; } = DateTime.UtcNow;
        public Hospital Hospital { get; set; } = null!;
        public Department Department { get; set; } = null!;
    }
}