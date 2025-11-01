using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    public class UserSearchRequestModel : MediatR.IRequest<UserSearchResponseModel?>
    {
        public Guid? UserId { get; set; }
    }
}
