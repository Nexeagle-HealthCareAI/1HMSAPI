using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Domain.Entities;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    public class ProcessBiometricPunchHandler : IRequestHandler<ProcessBiometricPunchRequestModel, ProcessBiometricPunchResponseModel>
    {
        private readonly AppDbContext _dbContext;

        public ProcessBiometricPunchHandler(AppDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<ProcessBiometricPunchResponseModel> Handle(ProcessBiometricPunchRequestModel request, CancellationToken cancellationToken)
        {
            // 1. Lookup Employee
            var employee = await _dbContext.HrEmployee
                .FirstOrDefaultAsync(e => e.EmployeeCode == request.EmployeeCode, cancellationToken);

            if (employee == null)
            {
                return new ProcessBiometricPunchResponseModel
                {
                    Success = false,
                    Message = $"Employee with code {request.EmployeeCode} not found."
                };
            }

            var punchDate = DateOnly.FromDateTime(request.PunchTime.Date);

            // 2. Lookup existing attendance log for today
            var attendanceLog = await _dbContext.HrAttendanceLog
                .FirstOrDefaultAsync(a => a.HrEmployeeId == employee.HrEmployeeId && a.AttendanceDate == punchDate, cancellationToken);

            // 3. Lookup duty roster for today to determine if LATE and to calculate OT later
            var roster = await _dbContext.HrDutyRoster
                .Include(r => r.HrHospitalShift)
                .FirstOrDefaultAsync(r => r.HrEmployeeId == employee.HrEmployeeId && r.RosterDate == punchDate, cancellationToken);

            bool isNew = false;
            if (attendanceLog == null)
            {
                attendanceLog = new HrAttendanceLog
                {
                    HrEmployeeId = employee.HrEmployeeId,
                    AttendanceDate = punchDate,
                    PunchSource = "BIOMETRIC",
                    BiometricDeviceId = request.DeviceId,
                    Status = "PRESENT" // Default, will update
                };
                isNew = true;
            }

            // 4. Process IN/OUT punch
            if (request.PunchType.Equals("IN", StringComparison.OrdinalIgnoreCase))
            {
                attendanceLog.PunchIn = request.PunchTime;
                
                // Determine LATE status
                if (roster != null && roster.HrHospitalShift != null)
                {
                    var shiftStartTime = roster.HrHospitalShift.StartTime.ToTimeSpan();
                    var actualTime = request.PunchTime.TimeOfDay;
                    var toleranceMinutes = roster.HrHospitalShift.GracePeriodMinutes > 0 ? roster.HrHospitalShift.GracePeriodMinutes : 30;
                    
                    if (actualTime > shiftStartTime.Add(TimeSpan.FromMinutes(toleranceMinutes)))
                    {
                        attendanceLog.Status = "LATE";
                    }
                    else
                    {
                        attendanceLog.Status = "PRESENT";
                    }
                }
                else
                {
                    // If they punch in but have no roster, flag as UNSCHEDULED via Notes for Exception Dashboard
                    attendanceLog.Status = "PRESENT";
                    attendanceLog.Notes = "UNSCHEDULED";
                }
            }
            else if (request.PunchType.Equals("OUT", StringComparison.OrdinalIgnoreCase))
            {
                attendanceLog.PunchOut = request.PunchTime;

                if (attendanceLog.PunchIn.HasValue)
                {
                    // Calculate hours worked
                    var duration = request.PunchTime - attendanceLog.PunchIn.Value;
                    attendanceLog.TotalHoursWorked = (decimal)duration.TotalHours;

                    // Overtime: > 9 hours
                    if (attendanceLog.TotalHoursWorked > 9)
                    {
                        attendanceLog.OvertimeHours = attendanceLog.TotalHoursWorked.Value - 9m;
                    }
                    
                    // If they didn't punch out, it would be caught by a nightly cron job and flagged "Missing Out-Punch"
                }
                else
                {
                    // Out punch without an In punch
                    attendanceLog.Notes = "MISSING_IN_PUNCH";
                }
            }

            if (isNew)
            {
                _dbContext.HrAttendanceLog.Add(attendanceLog);
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            return new ProcessBiometricPunchResponseModel
            {
                Success = true,
                Message = "Punch recorded successfully",
                AttendanceLogId = attendanceLog.HrAttendanceLogId
            };
        }
    }
}
