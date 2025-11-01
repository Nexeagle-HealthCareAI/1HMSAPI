namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    public class UserSearchResponseModel
    {
        public Guid UserId { get; set; }
        public string MobileNumber { get; set; } = null!;
        public string? Email { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public UserAuthInfo? UserAuth { get; set; }
        public UserProfileInfo? UserProfile { get; set; }
        
    }

    public class UserAuthInfo
    {
        public Guid UserAuthId { get; set; }
        public string? LoginMethod { get; set; }
        public int FailedLoginAttempts { get; set; }
        public bool IsLocked { get; set; }
        public string? LastLoginIP { get; set; }
        public DateTime? LastLoginTime { get; set; }
        public DateTime? PasswordSetAt { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class UserProfileInfo
    {
        public Guid UserProfileId { get; set; }
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
        public int ProfileCompletionPercentage { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}