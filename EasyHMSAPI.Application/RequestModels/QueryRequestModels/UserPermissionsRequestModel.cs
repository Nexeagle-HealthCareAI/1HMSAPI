using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    public class UserPermissionsRequestModel : IRequest<UserPermissionsResponseModel?>
    {
        public Guid? UserId { get; set; }
    }
}
