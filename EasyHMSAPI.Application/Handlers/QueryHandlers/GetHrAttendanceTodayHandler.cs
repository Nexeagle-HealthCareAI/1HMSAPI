using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    public class GetHrAttendanceTodayHandler : IRequestHandler<GetHrAttendanceTodayRequestModel, GetHrAttendanceTodayResponseModel>
    {
        private readonly AppDbContext _context;

        public GetHrAttendanceTodayHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<GetHrAttendanceTodayResponseModel> Handle(GetHrAttendanceTodayRequestModel request, CancellationToken cancellationToken)
        {
            var logs = await _context.HrAttendanceLog
                .Include(a => a.HrEmployee)
                .Where(a => a.HrEmployee.HospitalId == request.HospitalId && a.AttendanceDate == request.Date)
                .Select(a => new HrAttendanceLogDto
                {
                    HrAttendanceLogId = a.HrAttendanceLogId,
                    HrEmployeeId = a.HrEmployeeId,
                    EmployeeName = a.HrEmployee.FirstName + " " + a.HrEmployee.LastName,
                    EmployeeCode = a.HrEmployee.EmployeeCode,
                    AttendanceDate = a.AttendanceDate,
                    PunchIn = a.PunchIn,
                    PunchOut = a.PunchOut,
                    TotalHoursWorked = a.TotalHoursWorked,
                    OvertimeHours = a.OvertimeHours,
                    PunchSource = a.PunchSource,
                    Status = a.Status,
                    Notes = a.Notes
                })
                .ToListAsync(cancellationToken);

            return new GetHrAttendanceTodayResponseModel
            {
                Success = true,
                AttendanceLogs = logs
            };
        }
    }
}
