using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Data.Enums;
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
                    .AnyAsync(u => u.UserID == request.UserId && u.UserStatusId != (int)UserStatusEnum.Revoked, cancellationToken);

                if (!userExists)
                    return null;

                var userRolesList = await _context.UserRoles
                    .Include(ur => ur.Role)
                    .ThenInclude(r => r.RolePermissions)
                    .Where(ur => ur.UserID == request.UserId)
                    .ToListAsync(cancellationToken);

                if (!userRolesList.Any())
                    return null;

                // Fetch default hospitalId for the user (IsPrimary = true, else first hospital)
                var hospitalUser = await _context.HospitalUsers
                    .Where(hu => hu.UserID == request.UserId)
                    .OrderByDescending(hu => hu.IsPrimary) // IsPrimary first
                    .ThenBy(hu => hu.CreatedAt)
                    .FirstOrDefaultAsync(cancellationToken);

                var response = new UserPermissionsResponseModel
                {
                    RoleId = userRolesList.First().Role.RoleID,
                    RoleName = string.Join(",", userRolesList.Select(ur => ur.Role.RoleName)),
                    Description = string.Join(" | ", userRolesList.Select(ur => ur.Role.Description)),
                    PermissionKeys = userRolesList.SelectMany(ur => ur.Role.RolePermissions)
                        .Where(rp => rp.IsAllowed)
                        .Select(rp => rp.PermissionKey)
                        .Distinct()
                        .ToList(),
                    AllRoles = null,
                    HospitalId = hospitalUser?.HospitalID // Set default hospitalId if found
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
                    AllRoles = roles,
                    HospitalId = null
                };

                return response;
            }
        }
    }
}
