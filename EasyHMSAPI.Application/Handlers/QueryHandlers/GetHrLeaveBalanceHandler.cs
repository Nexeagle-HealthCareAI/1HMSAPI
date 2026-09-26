using EasyHMSAPI.Application.Common;
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

            // RBAC Check for Self-Service Isolation. The employee is looked up by ID alone, so "manager" has to
            // mean manager at THAT EMPLOYEE'S hospital, not hr.manage_leaves on any role at any hospital.
            var employeeHospitalId = await _context.HrEmployee
                .AsNoTracking()
                .Where(e => e.HrEmployeeId == request.EmployeeId)
                .Select(e => (Guid?)e.HospitalId)
                .FirstOrDefaultAsync(cancellationToken);
            var hasManageLeaves = employeeHospitalId.HasValue
                && await CallerGuards.HasPermissionAtHospitalAsync(_context, request.LoggedInUserId, employeeHospitalId.Value, "hr.manage_leaves", cancellationToken);

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
