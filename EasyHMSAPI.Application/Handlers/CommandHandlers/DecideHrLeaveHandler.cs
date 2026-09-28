using EasyHMSAPI.Application.Common;
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
            var leave = await _context.HrLeaveRequest
                .Include(l => l.HrEmployee)
                .FirstOrDefaultAsync(l => l.HrLeaveRequestId == request.LeaveId, cancellationToken);

            // Looked up by ID alone, so authorize against the employee's own hospital: HospitalAccessFilter
            // never sees a hospitalId on this request, and [RequiresPermission] is not hospital-scoped.
            // A leave at someone else's hospital gets the same "not found" as a missing one.
            if (leave == null || !await CallerGuards.HasPermissionAtHospitalAsync(_context, request.ApprovedByUserId, leave.HrEmployee.HospitalId, "hr.manage_leaves", cancellationToken))
            {
                return new DecideHrLeaveResponseModel
                {
                    Success = false,
                    Message = "Leave request not found.",
                    LeaveId = request.LeaveId,
                    Status = request.Status
                };
            }

            if (request.Status != "APPROVED" && request.Status != "REJECTED")
            {
                return new DecideHrLeaveResponseModel
                {
                    Success = false,
                    Message = "Status must be APPROVED or REJECTED.",
                    LeaveId = request.LeaveId,
                    Status = leave.Status
                };
            }

            // Only a PENDING request can be decided. Without this, approving the same request twice deducted
            // the employee's balance twice, and a rejected request could be flipped to approved later.
            if (leave.Status != "PENDING")
            {
                return new DecideHrLeaveResponseModel
                {
                    Success = false,
                    Message = $"This leave request is already {leave.Status.ToLowerInvariant()}.",
                    LeaveId = request.LeaveId,
                    Status = leave.Status
                };
            }

            var now = DateTime.UtcNow;
            var attendanceDaysUpdated = 0;
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

                attendanceDaysUpdated = await MarkOnLeaveAsync(leave, request.ApprovedByUserId, now, cancellationToken);
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
                Status = request.Status,
                AttendanceDaysUpdated = attendanceDaysUpdated
            };
        }

        /// <summary>
        /// Marks every day in [StartDate, EndDate] as ON_LEAVE, skipping any day that already has an
        /// attendance row. An existing row only ever comes from an actual scan or an earlier manual
        /// entry, so leaving it alone means real activity is never silently overwritten by the leave
        /// approval -- e.g. leave approved after the fact for a day the employee actually worked.
        /// </summary>
        private async Task<int> MarkOnLeaveAsync(HrLeaveRequest leave, Guid approvedByUserId, DateTime now, CancellationToken cancellationToken)
        {
            var existingDates = await _context.HrAttendanceLog
                .Where(a => a.HrEmployeeId == leave.HrEmployeeId && a.AttendanceDate >= leave.StartDate && a.AttendanceDate <= leave.EndDate)
                .Select(a => a.AttendanceDate)
                .ToListAsync(cancellationToken);
            var existing = existingDates.ToHashSet();

            var created = 0;
            for (var date = leave.StartDate; date <= leave.EndDate; date = date.AddDays(1))
            {
                if (existing.Contains(date)) continue;

                _context.HrAttendanceLog.Add(new HrAttendanceLog
                {
                    HrEmployeeId = leave.HrEmployeeId,
                    AttendanceDate = date,
                    Status = "ON_LEAVE",
                    PunchSource = "MANUAL_OVERRIDE",
                    OvertimeHours = 0m,
                    Notes = $"Approved {leave.LeaveType} leave",
                    OverriddenByUserId = approvedByUserId,
                    OverriddenAt = now,
                    OverrideReason = $"Leave request {leave.HrLeaveRequestId} approved"
                });
                created++;
            }

            return created;
        }
    }
}
