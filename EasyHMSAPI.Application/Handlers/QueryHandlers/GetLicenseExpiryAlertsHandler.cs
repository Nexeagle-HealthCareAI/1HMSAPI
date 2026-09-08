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
    public class GetLicenseExpiryAlertsHandler : IRequestHandler<GetLicenseExpiryAlertsRequestModel, GetLicenseExpiryAlertsResponseModel>
    {
        private readonly AppDbContext _context;

        public GetLicenseExpiryAlertsHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<GetLicenseExpiryAlertsResponseModel> Handle(GetLicenseExpiryAlertsRequestModel request, CancellationToken cancellationToken)
        {
            var alerts = new List<LicenseAlertDto>();
            var today = DateOnly.FromDateTime(DateTime.UtcNow);

            var credentials = await _context.HrEmployeeCredential
                .Include(c => c.HrEmployee)
                .Where(c => c.HrEmployee.HospitalId == request.HospitalId && c.HrEmployee.IsActive && c.HrEmployee.Status != "INACTIVE")
                .ToListAsync(cancellationToken);

            foreach (var cred in credentials)
            {
                var employeeName = $"{cred.HrEmployee.FirstName} {cred.HrEmployee.LastName}".Trim();

                // Check main license
                int daysMain = cred.LicenseValidUntil.DayNumber - today.DayNumber;
                if (daysMain <= 60)
                {
                    alerts.Add(new LicenseAlertDto
                    {
                        HrEmployeeId = cred.HrEmployeeId,
                        EmployeeName = employeeName,
                        Designation = cred.HrEmployee.Designation,
                        DocumentName = $"{cred.CouncilName} Registration",
                        ExpiryDate = cred.LicenseValidUntil.ToString("MMM dd, yyyy"),
                        DaysLeft = daysMain,
                        Severity = DetermineSeverity(daysMain)
                    });
                }

                // Check BLS if exists
                if (cred.BlsExpiryDate.HasValue)
                {
                    int daysBls = cred.BlsExpiryDate.Value.DayNumber - today.DayNumber;
                    if (daysBls <= 60)
                    {
                        alerts.Add(new LicenseAlertDto
                        {
                            HrEmployeeId = cred.HrEmployeeId,
                            EmployeeName = employeeName,
                            Designation = cred.HrEmployee.Designation,
                            DocumentName = "BLS Certification",
                            ExpiryDate = cred.BlsExpiryDate.Value.ToString("MMM dd, yyyy"),
                            DaysLeft = daysBls,
                            Severity = DetermineSeverity(daysBls)
                        });
                    }
                }
                
                // Check ACLS if exists
                if (cred.AclsExpiryDate.HasValue)
                {
                    int daysAcls = cred.AclsExpiryDate.Value.DayNumber - today.DayNumber;
                    if (daysAcls <= 60)
                    {
                        alerts.Add(new LicenseAlertDto
                        {
                            HrEmployeeId = cred.HrEmployeeId,
                            EmployeeName = employeeName,
                            Designation = cred.HrEmployee.Designation,
                            DocumentName = "ACLS Certification",
                            ExpiryDate = cred.AclsExpiryDate.Value.ToString("MMM dd, yyyy"),
                            DaysLeft = daysAcls,
                            Severity = DetermineSeverity(daysAcls)
                        });
                    }
                }
            }

            return new GetLicenseExpiryAlertsResponseModel
            {
                Success = true,
                Message = "Alerts retrieved",
                Alerts = alerts.OrderBy(a => a.DaysLeft).ToList()
            };
        }

        private string DetermineSeverity(int daysLeft)
        {
            if (daysLeft <= 7) return "CRITICAL";
            if (daysLeft <= 30) return "HIGH";
            return "MEDIUM";
        }
    }
}
