using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    [ExcludeFromCodeCoverage]
    public class PatientOtpSendRequestModel : IRequest<PatientOtpSendResponseModel>
    {
        public string? MobileNumber { get; set; }
    }
}
