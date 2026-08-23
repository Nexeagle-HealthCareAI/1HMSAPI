using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    [ExcludeFromCodeCoverage]
    public class UserPermissionsRequestModel : IRequest<UserPermissionsResponseModel?>
    {
        public Guid? UserId { get; set; }

        /// <summary>
        /// The caller's own identity, resolved from the verified JWT by the controller --
        /// never client-supplied. Used to enforce self-only access when UserId is set (see
        /// UserPermissionsHandler's own comment) -- same stamping pattern as
        /// AdminController.QuickAddUser's CallerUserId.
        /// </summary>
        public Guid? CallerUserId { get; set; }
    }
}
