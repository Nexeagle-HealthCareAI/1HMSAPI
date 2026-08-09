using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    [ExcludeFromCodeCoverage]
    public class RequestAbdmLoginOtpRequestModel : IRequest<AbdmOtpTxnResponseModel>
    {
        public Guid HospitalId { get; set; }
        public string LoginId { get; set; } = string.Empty;
        /// <summary>"mobile" | "aadhaar" | "abha-number".</summary>
        public string LoginHint { get; set; } = "mobile";
        /// <summary>"abdm" (ABHA-linked mobile OTP) | "aadhaar" (UIDAI Aadhaar OTP).</summary>
        public string OtpSystem { get; set; } = "abdm";
    }
}
