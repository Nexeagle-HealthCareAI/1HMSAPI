using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    /// <summary>§8.4 Deactivate ABHA — step 1. Requires the same live, freshly-verified session as
    /// the profile-update endpoints (SessionTxnId's cached X-Token proves this is the holder).</summary>
    [ExcludeFromCodeCoverage]
    public class RequestDeactivateAbhaOtpRequestModel : IRequest<AbdmOtpTxnResponseModel>
    {
        public Guid HospitalId { get; set; }
        public string SessionTxnId { get; set; } = string.Empty;
        public string AbhaNumber { get; set; } = string.Empty;
        // "aadhaar" (§8.4.1) | "abdm" (§8.4.2, OTP to the ABHA-linked mobile).
        public string OtpSystem { get; set; } = "abdm";
    }
}
