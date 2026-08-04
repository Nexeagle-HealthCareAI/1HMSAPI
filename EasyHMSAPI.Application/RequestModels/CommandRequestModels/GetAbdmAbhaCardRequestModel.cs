using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    /// <summary>§11 Generate ABHA Card — requires the same live OTP-verified session as the profile
    /// update endpoints.</summary>
    [ExcludeFromCodeCoverage]
    public class GetAbdmAbhaCardRequestModel : IRequest<AbdmBinaryResponseModel>
    {
        public Guid HospitalId { get; set; }
        public string SessionTxnId { get; set; } = string.Empty;
    }
}
