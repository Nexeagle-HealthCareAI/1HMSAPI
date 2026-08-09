using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    /// <summary>§8.5 Re-activate ABHA — step 1. A deactivated account has no live session, so unlike
    /// deactivate this is a cold-start call — only the ABHA number itself is needed.</summary>
    [ExcludeFromCodeCoverage]
    public class RequestReactivateAbhaOtpRequestModel : IRequest<AbdmOtpTxnResponseModel>
    {
        public Guid HospitalId { get; set; }
        public string AbhaNumber { get; set; } = string.Empty;
    }
}
