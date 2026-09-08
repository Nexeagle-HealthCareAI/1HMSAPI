using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    public class GetAttendanceExceptionsHandler : IRequestHandler<GetAttendanceExceptionsRequestModel, GetAttendanceExceptionsResponseModel>
    {
        private readonly AppDbContext _dbContext;

        public GetAttendanceExceptionsHandler(AppDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<GetAttendanceExceptionsResponseModel> Handle(GetAttendanceExceptionsRequestModel request, CancellationToken cancellationToken)
        {
            var start = DateOnly.FromDateTime(request.StartDate);
            var end = DateOnly.FromDateTime(request.EndDate);

            var logs = await _dbContext.HrAttendanceLog
                .Include(a => a.HrEmployee)
                    .ThenInclude(e => e.Department)
                .Where(a => a.HrEmployee.HospitalId == request.HospitalId 
                         && a.AttendanceDate >= start 
                         && a.AttendanceDate <= end
                         && (a.Status == "LATE" || a.Notes == "MISSING_IN_PUNCH" || a.Notes == "UNSCHEDULED" || (!a.PunchOut.HasValue && a.PunchIn.HasValue && a.AttendanceDate < DateOnly.FromDateTime(DateTime.UtcNow))))
                .ToListAsync(cancellationToken);

            var exceptions = new List<AttendanceExceptionDto>();

            foreach (var log in logs)
            {
                string type = "UNKNOWN";
                string desc = "";

                if (log.Status == "LATE")
                {
                    type = "LATE";
                    desc = "Punched in late for shift";
                }
                else if (log.Notes == "MISSING_IN_PUNCH")
                {
                    type = "MISSING_IN_PUNCH";
                    desc = "Punched out without punching in";
                }
                else if (log.Notes == "UNSCHEDULED")
                {
                    type = "UNSCHEDULED";
                    desc = "Punched in but not on roster";
                }
                else if (!log.PunchOut.HasValue && log.PunchIn.HasValue)
                {
                    type = "MISSING_OUT_PUNCH";
                    desc = "Shift ended without out-punch";
                }

                exceptions.Add(new AttendanceExceptionDto
                {
                    AttendanceLogId = log.HrAttendanceLogId,
                    EmployeeId = log.HrEmployeeId,
                    EmployeeName = $"{log.HrEmployee.FirstName} {log.HrEmployee.LastName}",
                    EmployeeCode = log.HrEmployee.EmployeeCode,
                    DepartmentName = log.HrEmployee.Department?.Name ?? "Unknown",
                    AttendanceDate = log.AttendanceDate.ToDateTime(new TimeOnly(0, 0)),
                    PunchIn = log.PunchIn,
                    PunchOut = log.PunchOut,
                    ExceptionType = type,
                    Description = desc
                });
            }

            return new GetAttendanceExceptionsResponseModel
            {
                Exceptions = exceptions
            };
        }
    }
}
