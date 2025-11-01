using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    public class OtpSendRequestModel : IRequest<OtpSendResponseModel>
    {
        public string? MobileNumber { get; set; }
    }
} 