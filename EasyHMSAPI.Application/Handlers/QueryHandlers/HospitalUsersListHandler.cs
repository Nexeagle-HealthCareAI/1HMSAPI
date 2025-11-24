using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Data.Enums;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    public class HospitalUsersListHandler : IRequestHandler<HospitalUsersListRequestModel, HospitalUsersListResponseModel>
    {
        private readonly AppDbContext _context;
        public HospitalUsersListHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<HospitalUsersListResponseModel> Handle(HospitalUsersListRequestModel request, CancellationToken cancellationToken)
        {
            var resp = new HospitalUsersListResponseModel { HospitalId = request.HospitalId };

            var query = from hu in _context.HospitalUsers.AsNoTracking()
                        join u in _context.Users.AsNoTracking() on hu.UserID equals u.UserID
                        where hu.HospitalID == request.HospitalId && u.UserStatusId != (int)UserStatusEnum.Revoked
                        join up in _context.UserProfiles.AsNoTracking()
                            .Where(up => up.UserStatusId != (int)UserStatusEnum.Revoked) on u.UserID equals up.UserID into upg
                        from up in upg.OrderByDescending(x => x.UpdatedAt).Take(1).DefaultIfEmpty()
                        select new HospitalUsersListItem
                        {
                            UserId = u.UserID,
                            FullName = up != null ? up.FullName : null,
                            MobileNumber = u.MobileNumber,
                            Email = u.Email,
                            EmployeeID = hu.EmployeeID,
                            IsPrimary = hu.IsPrimary,
                            UsersStatusId = u.UserStatusId,
                            Roles = new List<Roles>(),
                            PermissionKeys = new List<string>()
                        };

            resp.Users = await query.OrderByDescending(x => x.IsPrimary).ThenBy(x => x.FullName).ToListAsync(cancellationToken);

            if (resp.Users.Count == 0)
            {
                return resp;
            }

            var userIds = resp.Users.Select(x => x.UserId).ToList();
            var userRoles = await (from ur in _context.UserRoles.AsNoTracking()
                                   join r in _context.Roles.AsNoTracking() on ur.RoleID equals r.RoleID
                                   where userIds.Contains(ur.UserID) && (r.HospitalID == request.HospitalId || r.IsSystemDefined)
                                   select new
                                   {
                                       ur.UserID,
                                       r.RoleID,
                                       r.RoleName
                                   }).ToListAsync(cancellationToken);

            var rolesByUser = userRoles
                .GroupBy(x => x.UserID)
                .ToDictionary(g => g.Key, g => g.Select(x => new Roles { RoleId = x.RoleID, RoleName = x.RoleName }).ToList());

            var roleIds = userRoles.Select(x => x.RoleID).Distinct().ToList();

            var rolePerms = await (from rp in _context.RolePermissions.AsNoTracking()
                                   where roleIds.Contains(rp.RoleID)
                                   select new
                                   {
                                       rp.RoleID,
                                       rp.PermissionKey
                                   }).ToListAsync(cancellationToken);

            var permKeysByRole = rolePerms
                .GroupBy(x => x.RoleID)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(p => p.PermissionKey).ToList()
                );

            foreach (var u in resp.Users)
            {
                if (rolesByUser.TryGetValue(u.UserId, out var rlist))
                {
                    u.Roles = rlist;

                    var keys = new HashSet<string>();
                    foreach (var rr in rlist)
                    {
                        if (permKeysByRole.TryGetValue(rr.RoleId, out var k))
                        {
                            foreach (var item in k) keys.Add(item);
                        }
                    }
                    u.PermissionKeys = keys.Count > 0 ? keys.ToList() : new List<string>();
                }

            }
            return resp;
        }
    }
}
