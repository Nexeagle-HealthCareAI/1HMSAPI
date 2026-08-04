using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    /// <summary>§7.6 Find ABHA — step 2 (send an OTP for the chosen candidate). Verification itself
    /// reuses VerifyAbdmLoginOtpRequestModel — same endpoint, same response shape as a normal login.</summary>
    [ExcludeFromCodeCoverage]
    public class FindAbhaGenerateOtpRequestModel : IRequest<AbdmOtpTxnResponseModel>
    {
        public Guid HospitalId { get; set; }
        public string TxnId { get; set; } = string.Empty;
        public int Index { get; set; }
        // "mobile" | "aadhaar" — must match the search step's SearchBy.
        public string SearchBy { get; set; } = "mobile";
    }
}
