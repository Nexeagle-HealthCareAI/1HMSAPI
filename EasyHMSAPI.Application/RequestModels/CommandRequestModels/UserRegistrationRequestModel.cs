using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    [ExcludeFromCodeCoverage]
    public class UserRegistrationRequestModel : MediatR.IRequest<UserRegistrationResponseModel>
    {
        public string? MobileNumber { get; set; }
        public string? Roles { get; set; }
        public string? FullName { get; set; }
    }
} 