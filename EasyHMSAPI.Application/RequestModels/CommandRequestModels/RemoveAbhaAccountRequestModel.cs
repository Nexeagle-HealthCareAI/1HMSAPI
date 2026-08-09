using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    /// <summary>Removes the local hospital record of an ABHA account — does NOT deactivate or delete
    /// the ABHA number itself on ABDM's side (see Deactivate ABHA for that).</summary>
    [ExcludeFromCodeCoverage]
    public class RemoveAbhaAccountRequestModel : IRequest<RemoveAbhaAccountResponseModel>
    {
        public Guid HospitalId { get; set; }
        public Guid AbhaAccountId { get; set; }
    }
}
