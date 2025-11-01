using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    public class UserRegistrationRequestModel : MediatR.IRequest<UserRegistrationResponseModel>
    {
        public string? MobileNumber { get; set; }
        public string? Roles { get; set; }
        public string? FullName { get; set; }
    }
} 