using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    [ExcludeFromCodeCoverage]
    public class VerifyAbdmLoginOtpRequestModel : IRequest<AbdmProfileResponseModel>
    {
        public Guid HospitalId { get; set; }
        public string TxnId { get; set; } = string.Empty;
        public string Otp { get; set; } = string.Empty;
        /// <summary>Must match the loginHint used to request this OTP — "mobile" | "aadhaar" |
        /// "abha-number". Determines ABDM's expected verify scope.</summary>
        public string LoginHint { get; set; } = "mobile";
    }
}
