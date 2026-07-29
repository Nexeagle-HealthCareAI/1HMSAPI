using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    /// <summary>SessionTxnId is the TxnId from a just-completed OTP verification (create/login) —
    /// its cached X-Token proves this is the ABHA holder, not just anyone who knows the number.</summary>
    [ExcludeFromCodeCoverage]
    public class RequestUpdateMobileOtpRequestModel : IRequest<AbdmOtpTxnResponseModel>
    {
        public string SessionTxnId { get; set; } = string.Empty;
        public string NewMobile { get; set; } = string.Empty;
    }
}
