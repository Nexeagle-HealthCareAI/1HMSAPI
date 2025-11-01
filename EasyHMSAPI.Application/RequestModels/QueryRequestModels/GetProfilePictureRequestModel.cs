using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    public class GetProfilePictureRequestModel : IRequest<GetProfilePictureResponseModel>
    {
        public Guid UserId { get; set; }
    }
}
