using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    public class InvitationValidateRequestModel : IRequest<InvitationValidateResponseModel>
    {
        public string Token { get; set; } = null!;
    }
}
