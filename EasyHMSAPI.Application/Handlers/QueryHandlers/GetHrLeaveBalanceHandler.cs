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

            var balance = await _context.HrLeaveBalance
                .Where(b => b.HrEmployeeId == request.EmployeeId && b.Year == year)
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
