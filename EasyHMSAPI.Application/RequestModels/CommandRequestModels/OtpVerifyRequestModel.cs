using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    public class OtpVerifyRequestModel : IRequest<OtpVerifyResponseModel>
    {
        public string MobileNumber { get; set; } = null!;
        public string Otp { get; set; } = null!;
    }
} 