using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    public class UserPermissionsHandler : IRequestHandler<UserPermissionsRequestModel, UserPermissionsResponseModel?>
    {
        private readonly AppDbContext _context;

        public UserPermissionsHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<UserPermissionsResponseModel?> Handle(UserPermissionsRequestModel request, CancellationToken cancellationToken)
        {
            if (request.UserId != Guid.Empty)
            {
                var userExists = await _context.Users
                    .AnyAsync(u => u.UserID == request.UserId, cancellationToken);

                if (!userExists)
                    return null;

                var userRole = await _context.UserRoles
                    .Include(ur => ur.Role)
                        .ThenInclude(r => r.RolePermissions)
                    .FirstOrDefaultAsync(ur => ur.UserID == request.UserId, cancellationToken);

                if (userRole == null || userRole.Role == null)
                    return null;

                var response = new UserPermissionsResponseModel
                {
                    RoleId = userRole.Role.RoleID,
                    RoleName = userRole.Role.RoleName,
                    Description = userRole.Role.Description,
                    PermissionKeys = userRole.Role.RolePermissions
                        .Where(rp => rp.IsAllowed)
                        .Select(rp => rp.PermissionKey)
                        .ToList(),
                    AllRoles = null
                };

                return response;
            }
            else
            {
                var roles = await _context.Roles
                    .Select(r => new Roles
                    {
                        RoleId = r.RoleID,
                        RoleName = r.RoleName
                    })
                    .ToListAsync(cancellationToken);

                var response = new UserPermissionsResponseModel
                {
                    RoleId = null,
                    RoleName = null,
                    Description = null,
                    PermissionKeys = null,
                    AllRoles = roles
                };

                return response;
            }
        }
    }
}
