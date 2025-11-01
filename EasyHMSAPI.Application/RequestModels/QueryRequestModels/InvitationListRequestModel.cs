using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    public class InvitationListRequestModel : IRequest<InvitationListResponseModel>
    {
        public Guid HospitalId { get; set; }
        public string Scope { get; set; } = "all";
    }
}
