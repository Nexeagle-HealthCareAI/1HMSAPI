using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class HospitalUsersListResponseModel
    {
        public Guid HospitalId { get; set; }
        public List<HospitalUsersListItem> Users { get; set; } = new();
    }

    [ExcludeFromCodeCoverage]
    public class HospitalUsersListItem
    {
        public Guid UserId { get; set; }
        public string? FullName { get; set; }
        public string MobileNumber { get; set; } = null!;
        public string? Email { get; set; }
        public string? EmployeeID { get; set; }
        public bool IsPrimary { get; set; }
        public int UsersStatusId { get; set; }
        public List<Roles>? Roles { get; set; }
        public List<string>? PermissionKeys { get; set; }
    }
}