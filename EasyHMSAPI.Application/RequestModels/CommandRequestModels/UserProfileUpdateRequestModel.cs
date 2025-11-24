using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    [ExcludeFromCodeCoverage]
    public class UserProfileUpdateRequestModel : MediatR.IRequest<UserProfileUpdateResponseModel>
    {
        public Guid UserId { get; set; }
        public string? MobileNumber { get; set; }
        //public bool? IsActive { get; set; }
        public string? FullName { get; set; }
        public string? Gender { get; set; }
        public string? Language { get; set; }
        public string? ProfilePictureURL { get; set; }
        public string? EmployeeID { get; set; }
        public string? DateOfBirth { get; set; }
        public string? BloodGroup { get; set; }
        public string? AddressLine1 { get; set; }
        public string? AddressLine2 { get; set; }
        public string? City { get; set; }
        public string? State { get; set; }
        public string? Country { get; set; }
        public string? Pincode { get; set; }
        public string? EmergencyContactName { get; set; }
        public string? EmergencyContactNumber { get; set; }
    }
}
