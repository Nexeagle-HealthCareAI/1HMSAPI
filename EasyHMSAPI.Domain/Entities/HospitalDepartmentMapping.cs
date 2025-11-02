using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    [ExcludeFromCodeCoverage]
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