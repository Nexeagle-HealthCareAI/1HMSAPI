using System.ComponentModel.DataAnnotations;

namespace EasyHMSAPI.Domain.Entities
{
    public class UserProfile
    {
        [Key]
        public Guid UserProfileID { get; set; }
        public Guid UserID { get; set; }
        public string FullName { get; set; } = null!;
        public string? Gender { get; set; }
        public string? Language { get; set; }
        public string? ProfilePictureURL { get; set; }
        public string? EmployeeID { get; set; }
        public DateTime? DateOfBirth { get; set; }
        public string? BloodGroup { get; set; }
        public string? AddressLine1 { get; set; }
        public string? AddressLine2 { get; set; }
        public string? City { get; set; }
        public string? State { get; set; }
        public string? Country { get; set; }
        public string? Pincode { get; set; }
        public string? EmergencyContactName { get; set; }
        public string? EmergencyContactNumber { get; set; }
        public int ProfileCompletionPercentage { get; set; } = 0;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public User User { get; set; } = null!;
    }
}
