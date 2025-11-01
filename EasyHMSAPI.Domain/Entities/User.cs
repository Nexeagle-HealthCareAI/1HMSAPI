using System.ComponentModel.DataAnnotations;

namespace EasyHMSAPI.Domain.Entities
{
    public class User
    {
        [Key]
        public Guid UserID { get; set; }
        public string MobileNumber { get; set; } = null!;
        public string? Email { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public ICollection<UserAuth> UserAuths { get; set; } = new List<UserAuth>();
        public ICollection<UserProfile> UserProfiles { get; set; } = new List<UserProfile>();
        public ICollection<HospitalUser> HospitalUsers { get; set; } = new List<HospitalUser>();
        public ICollection<Doctor> Doctors { get; set; } = new List<Doctor>();
        public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
        public ICollection<Role> CreatedRoles { get; set; } = new List<Role>();
        public ICollection<Department> CreatedDepartments { get; set; } = new List<Department>();
        public ICollection<Specialization> CreatedSpecializations { get; set; } = new List<Specialization>();
        public ICollection<Hospital> CreatedHospitals { get; set; } = new List<Hospital>();
        public ICollection<UserInvitation> SentUserInvitations { get; set; } = new List<UserInvitation>();
    }
}
