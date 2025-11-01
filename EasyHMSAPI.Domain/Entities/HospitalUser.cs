using System.ComponentModel.DataAnnotations;

namespace EasyHMSAPI.Domain.Entities
{
    public class HospitalUser
    {
        [Key]
        public Guid HospitalUserID { get; set; }
        public Guid HospitalID { get; set; }
        public Guid UserID { get; set; }
        public bool IsPrimary { get; set; } = false;
        public string? EmployeeID { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public Hospital Hospital { get; set; } = null!;
        public User User { get; set; } = null!;
    }
}
