using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    [ExcludeFromCodeCoverage]
    public class UserLoginRequestModel : MediatR.IRequest<UserLoginResponseModel>
    {
        public bool IsLoginWithOtp { get; set; }
        public string? EmailOrPhone { get; set; }
        public string? Password { get; set; }
        public string? Otp { get; set; }
    }
} 