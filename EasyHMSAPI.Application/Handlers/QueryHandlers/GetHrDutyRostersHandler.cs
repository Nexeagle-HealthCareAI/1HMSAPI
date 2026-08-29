using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    public class GetHrDutyRostersHandler : IRequestHandler<GetHrDutyRostersRequestModel, GetHrDutyRostersResponseModel>
    {
        private readonly AppDbContext _context;

        public GetHrDutyRostersHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<GetHrDutyRostersResponseModel> Handle(GetHrDutyRostersRequestModel request, CancellationToken cancellationToken)
        {
            var startDate = DateOnly.FromDateTime(request.StartDate);
            var endDate = DateOnly.FromDateTime(request.EndDate);

            // RBAC Check for Self-Service Isolation
            var hasManageRoster = await _context.UserRoles
                .Include(ur => ur.Role)
                .ThenInclude(r => r.RolePermissions)
                .AnyAsync(ur => ur.UserID == request.LoggedInUserId &&
                                ur.Role.RolePermissions.Any(p => p.PermissionKey == "hr.manage_roster" && p.IsAllowed), cancellationToken);

            var query = _context.HrDutyRoster
                .Include(r => r.HrEmployee)
                    .ThenInclude(e => e.Department)
                .Include(r => r.HrHospitalShift)
                .AsQueryable();

            if (!hasManageRoster)
            {
                query = query.Where(r => r.HrEmployee.UserId == request.LoggedInUserId);
            }

            var rosters = await query
                .Where(r => r.HospitalId == request.HospitalId && r.RosterDate >= startDate && r.RosterDate <= endDate)
                .Select(r => new HrDutyRosterDto
                {
                    HrDutyRosterId = r.HrDutyRosterId,
                    HospitalId = r.HospitalId,
                    EmployeeId = r.HrEmployeeId,
                    EmployeeName = r.HrEmployee.FirstName + " " + r.HrEmployee.LastName,
                    EmployeeCode = r.HrEmployee.EmployeeCode,
                    DepartmentName = r.HrEmployee.Department != null ? r.HrEmployee.Department.Name : "N/A",
                    ShiftId = r.HrHospitalShiftId,
                    ShiftCode = r.HrHospitalShift.ShiftCode,
                    ShiftName = r.HrHospitalShift.ShiftName,
                    RosterDate = r.RosterDate,
                    IsOnCall = r.IsOnCall,
                    WardId = r.WardId,
                    Status = r.Status,
                    RestPeriodViolation = r.RestPeriodViolation,
                    ViolationMessage = r.ViolationMessage,
                    SwappedWithRosterId = r.SwappedWithRosterId,
                    CreatedAt = r.CreatedAt
                })
                .ToListAsync(cancellationToken);

            return new GetHrDutyRostersResponseModel
            {
                Success = true,
                Rosters = rosters
            };
        }
    }
}
