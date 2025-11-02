using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    [ExcludeFromCodeCoverage]
    public class OtpSendRequestModel : IRequest<OtpSendResponseModel>
    {
        public string? MobileNumber { get; set; }
    }
} 