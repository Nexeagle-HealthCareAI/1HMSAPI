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
                // Self-only unless the caller holds admin_panel AND shares a hospital with the
                // target -- this endpoint returns a user's real roles/permissions/default
                // hospital, so querying someone ELSE's requires real justification, not just
                // any valid JWT. CallerUserId is controller-stamped from the verified JWT,
                // never client-supplied (see UserController.GetUserPermissions).
                //
                // admin_panel alone is NOT enough: it's granted per-hospital (every hospital's
                // own Admin/AdminDoctor role gets it at seed time, and hospital registration
                // now clones a hospital-scoped Role row rather than sharing one global row --
                // see HospitalRegisterHandler), not as a platform-wide superadmin flag. Without
                // the hospital check below, ANY hospital's admin could pull ANY OTHER
                // hospital's user's roles/permissions/default hospital -- this controller also
                // carries [SkipHospitalAccessCheck], so nothing else backstops that. Same
                // "admin role + hospital membership" pairing QuickAddUserHandler already
                // requires (CallerGuards.IsAdminAsync + its own hu.HospitalID check) for its
                // own admin_panel-gated actions.
                if (request.CallerUserId != request.UserId)
                {
                    var callerIsAdmin = request.CallerUserId != null && await _context.UserRoles
                        .Where(ur => ur.UserID == request.CallerUserId)
                        .SelectMany(ur => ur.Role.RolePermissions)
                        .AnyAsync(rp => rp.PermissionKey == "admin_panel" && rp.IsAllowed, cancellationToken);

                    var sharesHospitalWithTarget = callerIsAdmin && await _context.HospitalUsers
                        .Where(hu => hu.UserID == request.CallerUserId)
                        .AnyAsync(caller => _context.HospitalUsers.Any(target =>
                            target.UserID == request.UserId && target.HospitalID == caller.HospitalID), cancellationToken);

                    if (!sharesHospitalWithTarget)
                    {
                        return new UserPermissionsResponseModel { Forbidden = true };
                    }
                }

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
