using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    [ExcludeFromCodeCoverage]
    public class UserPermissionsRequestModel : IRequest<UserPermissionsResponseModel?>
    {
        public Guid? UserId { get; set; }
    }
}
