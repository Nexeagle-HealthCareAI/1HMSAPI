using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    [ExcludeFromCodeCoverage]
    public class UserSearchRequestModel : MediatR.IRequest<UserSearchResponseModel?>
    {
        public Guid? UserId { get; set; }
        /// <summary>Stamped by the controller from the verified JWT; any client-supplied value is overwritten.</summary>
        public Guid? CallerUserId { get; set; }
    }
}
