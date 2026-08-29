using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    public class DecideHrLeaveHandler : IRequestHandler<DecideHrLeaveRequestModel, DecideHrLeaveResponseModel>
    {
        private readonly AppDbContext _context;

        public DecideHrLeaveHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<DecideHrLeaveResponseModel> Handle(DecideHrLeaveRequestModel request, CancellationToken cancellationToken)
        {
            var leave = await _context.HrLeaveRequest.FindAsync(new object[] { request.LeaveId }, cancellationToken);
            if (leave == null)
            {
                return new DecideHrLeaveResponseModel
                {
                    Success = false,
                    Message = "Leave request not found.",
                    LeaveId = request.LeaveId,
                    Status = request.Status
                };
            }

            var now = DateTime.UtcNow;
            leave.Status = request.Status;
            leave.ApprovedByUserId = request.ApprovedByUserId;
            leave.ApprovedAt = now;

            if (request.Status == "APPROVED")
            {
                // PENDING -> APPROVED deducts from the employee's annual balance ledger (see
                // HrLeaveRequest's own doc comment). Maternity/CME are tracked in their own
                // balance field but never touch CL/SL/EL, matching statutory carve-outs.
                var year = leave.StartDate.Year;
                var balance = await _context.HrLeaveBalance
                    .FirstOrDefaultAsync(b => b.HrEmployeeId == leave.HrEmployeeId && b.Year == year, cancellationToken);

                if (balance == null)
                {
                    balance = new HrLeaveBalance { HrEmployeeId = leave.HrEmployeeId, Year = year };
                    _context.HrLeaveBalance.Add(balance);
                }

                switch (leave.LeaveType)
                {
                    case "CASUAL":
                        balance.CasualLeaveBalance -= leave.TotalDays;
                        balance.CasualLeaveUsed += leave.TotalDays;
                        break;
                    case "SICK":
                        balance.SickLeaveBalance -= leave.TotalDays;
                        balance.SickLeaveUsed += leave.TotalDays;
                        break;
                    case "EARNED":
                        balance.EarnedLeaveBalance -= leave.TotalDays;
                        balance.EarnedLeaveUsed += leave.TotalDays;
                        break;
                    case "COMP_OFF":
                        balance.CompOffBalance -= leave.TotalDays;
                        break;
                    case "MATERNITY":
                        balance.MaternityLeaveBalance -= leave.TotalDays;
                        break;
                    case "CME":
                        balance.CmeLeaveBalance -= leave.TotalDays;
                        break;
                }

                balance.UpdatedAt = now;
            }
            else if (request.Status == "REJECTED")
            {
                leave.RejectionReason = request.Reason;
            }

            await _context.SaveChangesAsync(cancellationToken);

            return new DecideHrLeaveResponseModel
            {
                Success = true,
                Message = "Leave status updated successfully.",
                LeaveId = request.LeaveId,
                Status = request.Status
            };
        }
    }
}
