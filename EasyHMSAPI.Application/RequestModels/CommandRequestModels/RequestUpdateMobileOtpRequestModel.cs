using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    /// <summary>SessionTxnId is the TxnId from a just-completed OTP verification (create/login) —
    /// its cached X-Token proves this is the ABHA holder, not just anyone who knows the number.
    /// HospitalId carries no ABDM meaning by itself, but is required so the global
    /// HospitalAccessFilter enforces hospital membership on this endpoint the same as every other
    /// ABDM request model (without it, the filter fails open and any authenticated user could call
    /// this regardless of hospital).</summary>
    [ExcludeFromCodeCoverage]
    public class RequestUpdateMobileOtpRequestModel : IRequest<AbdmOtpTxnResponseModel>
    {
        public Guid HospitalId { get; set; }
        public string SessionTxnId { get; set; } = string.Empty;
        public string NewMobile { get; set; } = string.Empty;
    }
}
