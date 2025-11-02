using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    [ExcludeFromCodeCoverage]
    public class OtpVerifyRequestModel : IRequest<OtpVerifyResponseModel>
    {
        public string MobileNumber { get; set; } = null!;
        public string Otp { get; set; } = null!;
    }
} 