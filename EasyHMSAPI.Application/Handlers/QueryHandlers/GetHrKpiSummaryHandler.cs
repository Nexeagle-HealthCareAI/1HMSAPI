using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    public class GetHrKpiSummaryHandler : IRequestHandler<GetHrKpiSummaryRequestModel, GetHrKpiSummaryResponseModel>
    {
        private readonly AppDbContext _context;

        public GetHrKpiSummaryHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<GetHrKpiSummaryResponseModel> Handle(GetHrKpiSummaryRequestModel request, CancellationToken cancellationToken)
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            
            // 1. Total Staff (Active)
            var activeStaffIds = await _context.HrEmployee
                .Where(e => e.HospitalId == request.HospitalId && e.IsActive && e.Status != "INACTIVE")
                .Select(e => e.HrEmployeeId)
                .ToListAsync(cancellationToken);
                
            var totalStaff = activeStaffIds.Count;

            // 2. Active On Duty Today (punched in today)
            var activeOnDutyToday = await _context.HrAttendanceLog
                .Where(a => a.HrEmployee.HospitalId == request.HospitalId 
                            && a.AttendanceDate == today 
                            && (a.Status == "PRESENT" || a.Status == "LATE" || a.Status == "HALF_DAY" || a.PunchIn != null))
                .Select(a => a.HrEmployeeId)
                .Distinct()
                .CountAsync(cancellationToken);

            // 3. Absent Today
            // Staff who are mapped to a duty roster shift today but have not punched in
            var scheduledToday = await _context.HrDutyRoster
                .Where(r => r.HrEmployee.HospitalId == request.HospitalId && r.RosterDate == today)
                .Select(r => r.HrEmployeeId)
                .Distinct()
                .ToListAsync(cancellationToken);
                
            var presentToday = await _context.HrAttendanceLog
                .Where(a => a.HrEmployee.HospitalId == request.HospitalId 
                            && a.AttendanceDate == today 
                            && (a.Status == "PRESENT" || a.Status == "LATE" || a.Status == "HALF_DAY" || a.PunchIn != null))
                .Select(a => a.HrEmployeeId)
                .Distinct()
                .ToListAsync(cancellationToken);
                
            var absentToday = scheduledToday.Except(presentToday).Count();

            // 4. Nurses on Night Shift
            var nursesOnNightShift = await _context.HrDutyRoster
                .Include(r => r.HrHospitalShift)
                .Include(r => r.HrEmployee)
                .ThenInclude(e => e.Department)
                .Where(r => r.HrEmployee.HospitalId == request.HospitalId 
                            && r.RosterDate == today 
                            && r.HrHospitalShift.ShiftCode == "SFT_N" 
                            && r.HrEmployee.Department != null 
                            && (r.HrEmployee.Department.Name == "Nursing" || r.HrEmployee.Department.Name == "NURSING"))
                .CountAsync(cancellationToken);

            // 5. On-Call Doctors
            var onCallDoctors = await _context.HrDutyRoster
                .Include(r => r.HrHospitalShift)
                .Include(r => r.HrEmployee)
                .ThenInclude(e => e.Department)
                .Where(r => r.HrEmployee.HospitalId == request.HospitalId 
                            && r.RosterDate == today 
                            && r.HrHospitalShift.ShiftCode == "SFT_CALL"
                            && r.HrEmployee.Department != null
                            && (r.HrEmployee.Department.Name == "Doctors" || r.HrEmployee.Department.Name == "DOCTORS" || r.HrEmployee.Department.Name == "Doctor" || r.HrEmployee.Department.Name == "Consultants"))
                .CountAsync(cancellationToken);

            // 6. Pending Leave Approvals
            var pendingLeaveApprovals = await _context.HrLeaveRequest
                .Where(l => l.HrEmployee.HospitalId == request.HospitalId && l.Status == "PENDING")
                .CountAsync(cancellationToken);

            // 7. Aug Payroll (Current Month Payroll)
            var currentMonth = DateTime.UtcNow.Month;
            var currentYear = DateTime.UtcNow.Year;
            
            var payrollRun = await _context.HrPayrollRun
                .Where(p => p.HospitalId == request.HospitalId && p.Month == currentMonth && p.Year == currentYear)
                .FirstOrDefaultAsync(cancellationToken);

            var payrollTotal = payrollRun?.TotalNetDisbursement ?? 0;
            var payrollStatus = payrollRun?.Status ?? "DRAFT";

            // 8. License Expiring Soon
            var thresholdDate = today.AddDays(30);
            var expiringCredentials = await _context.HrEmployeeCredential
                .Include(c => c.HrEmployee)
                .Where(c => c.HrEmployee.HospitalId == request.HospitalId 
                            && c.HrEmployee.IsActive 
                            && c.LicenseValidUntil <= thresholdDate)
                .ToListAsync(cancellationToken);

            var licenseAlerts = expiringCredentials.Select(c => {
                var daysLeft = c.LicenseValidUntil.DayNumber - today.DayNumber;
                
                string severity = daysLeft < 0 ? "CRITICAL" : (daysLeft <= 7 ? "HIGH" : "MEDIUM");
                
                return new HrLicenseAlertDto
                {
                    EmployeeId = c.HrEmployeeId,
                    EmployeeName = $"{c.HrEmployee.FirstName} {c.HrEmployee.LastName}",
                    CredentialType = c.CouncilName + " Registration",
                    ExpiryDate = c.LicenseValidUntil.ToString("yyyy-MM-dd"),
                    DaysUntilExpiry = daysLeft,
                    Severity = severity
                };
            }).OrderBy(c => c.DaysUntilExpiry).ToList();

            return new GetHrKpiSummaryResponseModel
            {
                Success = true,
                Message = "KPI summary retrieved",
                TotalStaff = totalStaff,
                ActiveOnDutyToday = activeOnDutyToday,
                AbsentToday = absentToday,
                NursesOnNightShift = nursesOnNightShift,
                OnCallDoctors = onCallDoctors,
                PendingLeaveApprovals = pendingLeaveApprovals,
                CurrentMonthPayrollTotal = payrollTotal,
                PayrollStatus = payrollStatus,
                LicenseExpiringSoon = licenseAlerts
            };
        }
    }
}
