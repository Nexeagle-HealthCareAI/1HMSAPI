using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    [ExcludeFromCodeCoverage]
    public class PatientOtpVerifyRequestModel : IRequest<PatientOtpVerifyResponseModel>
    {
        public string? MobileNumber { get; set; }
        public string? Otp { get; set; }
    }
}
