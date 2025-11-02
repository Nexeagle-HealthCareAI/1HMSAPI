using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    [ExcludeFromCodeCoverage]
    public class InvitationListRequestModel : IRequest<InvitationListResponseModel>
    {
        public Guid HospitalId { get; set; }
        public string Scope { get; set; } = "all";
    }
}
