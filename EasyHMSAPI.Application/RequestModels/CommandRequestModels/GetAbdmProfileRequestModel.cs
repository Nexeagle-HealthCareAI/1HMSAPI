using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    /// <summary>§9 Get Profile — re-pulls the live ABDM profile (including photo) for a session
    /// that's already been OTP-verified (see RequestUpdateMobileOtpRequestModel for why HospitalId
    /// is required even though ABDM itself doesn't need it).</summary>
    [ExcludeFromCodeCoverage]
    public class GetAbdmProfileRequestModel : IRequest<AbdmProfileResponseModel>
    {
        public Guid HospitalId { get; set; }
        public string SessionTxnId { get; set; } = string.Empty;
    }
}
