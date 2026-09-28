using EasyHMSAPI.Application.Common;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    /// <summary>
    /// HR's manual correction of one employee-day: fixes a bad punch, records a walk-in that never
    /// scanned, or overrides what the nightly close job guessed. Always stamps who did it and why
    /// (OverriddenBy/At/Reason) — the one write path on this table that a person, not a device or a
    /// job, is responsible for.
    /// </summary>
    public class SetAttendanceOverrideHandler : IRequestHandler<SetAttendanceOverrideRequestModel, SetAttendanceOverrideResponseModel>
    {
        private static readonly HashSet<string> AllowedStatuses = new(StringComparer.OrdinalIgnoreCase)
        {
            "PRESENT", "LATE", "HALF_DAY", "ABSENT", "ON_LEAVE"
        };

        private readonly AppDbContext _context;

        public SetAttendanceOverrideHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<SetAttendanceOverrideResponseModel> Handle(SetAttendanceOverrideRequestModel request, CancellationToken cancellationToken)
        {
            var status = request.Status?.Trim().ToUpperInvariant();
            if (string.IsNullOrEmpty(status) || !AllowedStatuses.Contains(status))
                return Fail("Status must be one of PRESENT, LATE, HALF_DAY, ABSENT or ON_LEAVE.");
            if (string.IsNullOrWhiteSpace(request.Reason))
                return Fail("A reason is required for a manual attendance correction.");
            if (request.PunchIn.HasValue && request.PunchOut.HasValue && request.PunchOut.Value <= request.PunchIn.Value)
                return Fail("Punch-out must be after punch-in.");

            var employee = await _context.HrEmployee.FirstOrDefaultAsync(e => e.HrEmployeeId == request.HrEmployeeId, cancellationToken);
            // Looked up by ID alone, so authorize against the employee's own hospital: another
            // hospital's employee gets the same "not found" as one that doesn't exist.
            if (employee == null || !await CallerGuards.HasPermissionAtHospitalAsync(_context, request.LoggedInUserId, employee.HospitalId, "hr.manage_employees", cancellationToken))
                return Fail("Employee not found.");

            var log = await _context.HrAttendanceLog.FirstOrDefaultAsync(
                a => a.HrEmployeeId == request.HrEmployeeId && a.AttendanceDate == request.AttendanceDate, cancellationToken);
            var isNew = log == null;
            if (isNew)
            {
                log = new HrAttendanceLog { HrEmployeeId = request.HrEmployeeId, AttendanceDate = request.AttendanceDate, OvertimeHours = 0m };
                _context.HrAttendanceLog.Add(log);
            }

            log!.Status = status;
            log.PunchSource = "MANUAL_OVERRIDE";
            // Only touch a punch time the caller actually sent -- leaving both null on an existing
            // record keeps its current times rather than blanking them out.
            if (request.PunchIn.HasValue) log.PunchIn = request.PunchIn;
            if (request.PunchOut.HasValue) log.PunchOut = request.PunchOut;
            log.TotalHoursWorked = log.PunchIn.HasValue && log.PunchOut.HasValue
                ? Math.Round((decimal)(log.PunchOut.Value - log.PunchIn.Value).TotalHours, 2)
                : log.TotalHoursWorked;
            if (request.Notes != null)
                log.Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();

            log.OverriddenByUserId = request.LoggedInUserId;
            log.OverriddenAt = DateTime.UtcNow;
            log.OverrideReason = request.Reason.Trim();

            await _context.SaveChangesAsync(cancellationToken);

            return new SetAttendanceOverrideResponseModel
            {
                Success = true,
                Message = isNew ? "Attendance record created." : "Attendance record updated.",
                AttendanceLogId = log.HrAttendanceLogId
            };
        }

        private static SetAttendanceOverrideResponseModel Fail(string message) => new() { Success = false, Message = message };
    }
}
