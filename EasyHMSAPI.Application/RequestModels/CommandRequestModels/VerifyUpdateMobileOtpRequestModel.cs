using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    [ExcludeFromCodeCoverage]
    public class VerifyUpdateMobileOtpRequestModel : IRequest<AbdmUpdateResponseModel>
    {
        public Guid HospitalId { get; set; }
        public string AbhaNumber { get; set; } = string.Empty;
        public string SessionTxnId { get; set; } = string.Empty;
        public string UpdateTxnId { get; set; } = string.Empty;
        public string Otp { get; set; } = string.Empty;
    }
}
