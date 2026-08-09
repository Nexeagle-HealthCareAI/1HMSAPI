using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Data.Enums;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    // Nurse-picker source for the Nursing Station roster admin UI -- narrower than
    // HospitalUsersListHandler's full response, just the fields a picker needs.
    public class GetHospitalNursesHandler : IRequestHandler<GetHospitalNursesRequestModel, GetHospitalNursesResponseModel>
    {
        private readonly AppDbContext _context;

        public GetHospitalNursesHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<GetHospitalNursesResponseModel> Handle(GetHospitalNursesRequestModel request, CancellationToken cancellationToken)
        {
            var resp = new GetHospitalNursesResponseModel();

            var hospitalUsers = await (from hu in _context.HospitalUsers.AsNoTracking()
                                        join u in _context.Users.AsNoTracking() on hu.UserID equals u.UserID
                                        where hu.HospitalID == request.HospitalId && u.UserStatusId != (int)UserStatusEnum.Revoked
                                        select new { u.UserID, u.MobileNumber }).ToListAsync(cancellationToken);

            if (hospitalUsers.Count == 0)
                return resp;

            var userIds = hospitalUsers.Select(x => x.UserID).ToList();

            var nurseUserIds = await (from ur in _context.UserRoles.AsNoTracking()
                                       join r in _context.Roles.AsNoTracking() on ur.RoleID equals r.RoleID
                                       where userIds.Contains(ur.UserID) && r.RoleName == "Nurse"
                                       select ur.UserID).Distinct().ToListAsync(cancellationToken);

            if (nurseUserIds.Count == 0)
                return resp;

            var nurseSet = nurseUserIds.ToHashSet();

            var profiles = await _context.UserProfiles.AsNoTracking()
                .Where(up => nurseUserIds.Contains(up.UserID))
                .OrderByDescending(up => up.UpdatedAt)
                .ToListAsync(cancellationToken);
            var namesByUser = profiles.GroupBy(up => up.UserID).ToDictionary(g => g.Key, g => g.First().FullName);

            resp.Nurses = hospitalUsers
                .Where(hu => nurseSet.Contains(hu.UserID))
                .Select(hu => new HospitalNurseItem
                {
                    UserId = hu.UserID,
                    FullName = namesByUser.TryGetValue(hu.UserID, out var n) ? n : null,
                    MobileNumber = hu.MobileNumber,
                })
                .OrderBy(x => x.FullName)
                .ToList();

            return resp;
        }
    }
}
