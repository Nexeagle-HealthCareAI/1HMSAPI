using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    /// <summary>§8.4 Deactivate ABHA — step 2. ABDM requires a reason for the deactivation.</summary>
    [ExcludeFromCodeCoverage]
    public class VerifyDeactivateAbhaOtpRequestModel : IRequest<AbdmUpdateResponseModel>
    {
        public Guid HospitalId { get; set; }
        public string SessionTxnId { get; set; } = string.Empty;
        public string DeactivateTxnId { get; set; } = string.Empty;
        public string Otp { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
    }
}
