using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    /// <summary>§8.5 Re-activate ABHA — step 2. A successful verify also logs the holder in, same as
    /// a normal login (fresh X-Token cached against TxnId).</summary>
    [ExcludeFromCodeCoverage]
    public class VerifyReactivateAbhaOtpRequestModel : IRequest<AbdmProfileResponseModel>
    {
        public Guid HospitalId { get; set; }
        public string TxnId { get; set; } = string.Empty;
        public string Otp { get; set; } = string.Empty;
    }
}
