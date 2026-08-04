using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    [ExcludeFromCodeCoverage]
    public class VerifyAadhaarOtpRequestModel : IRequest<AbdmEnrollResponseModel>
    {
        public Guid HospitalId { get; set; }
        public string TxnId { get; set; } = string.Empty;
        public string Otp { get; set; } = string.Empty;
        // Primary mobile number — mandatory on ABDM's side even if it matches the Aadhaar-linked one.
        public string Mobile { get; set; } = string.Empty;
    }
}
