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
    public class GetHrLeaveBalanceHandler : IRequestHandler<GetHrLeaveBalanceRequestModel, GetHrLeaveBalanceResponseModel>
    {
        private readonly AppDbContext _context;

        public GetHrLeaveBalanceHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<GetHrLeaveBalanceResponseModel> Handle(GetHrLeaveBalanceRequestModel request, CancellationToken cancellationToken)
        {
            var year = request.Year ?? DateTime.UtcNow.Year;

            // RBAC Check for Self-Service Isolation
            var hasManageLeaves = await _context.UserRoles
                .Include(ur => ur.Role)
                .ThenInclude(r => r.RolePermissions)
                .AnyAsync(ur => ur.UserID == request.LoggedInUserId &&
                                ur.Role.RolePermissions.Any(p => p.PermissionKey == "hr.manage_leaves" && p.IsAllowed), cancellationToken);

            var query = _context.HrLeaveBalance.Include(b => b.HrEmployee).AsQueryable();

            if (!hasManageLeaves)
            {
                query = query.Where(b => b.HrEmployee.UserId == request.LoggedInUserId && b.Year == year);
            }
            else
            {
                query = query.Where(b => b.HrEmployeeId == request.EmployeeId && b.Year == year);
            }

            var balance = await query
                .Select(b => new HrLeaveBalanceDto
                {
                    HrLeaveBalanceId = b.HrLeaveBalanceId,
                    HrEmployeeId = b.HrEmployeeId,
                    Year = b.Year,
                    CasualLeaveBalance = b.CasualLeaveBalance,
                    SickLeaveBalance = b.SickLeaveBalance,
                    EarnedLeaveBalance = b.EarnedLeaveBalance,
                    CompOffBalance = b.CompOffBalance,
                    MaternityLeaveBalance = b.MaternityLeaveBalance,
                    CmeLeaveBalance = b.CmeLeaveBalance,
                    CasualLeaveUsed = b.CasualLeaveUsed,
                    SickLeaveUsed = b.SickLeaveUsed,
                    EarnedLeaveUsed = b.EarnedLeaveUsed,
                    UpdatedAt = b.UpdatedAt
                })
                .FirstOrDefaultAsync(cancellationToken);

            return new GetHrLeaveBalanceResponseModel
            {
                Success = true,
                LeaveBalance = balance
            };
        }
    }
}
