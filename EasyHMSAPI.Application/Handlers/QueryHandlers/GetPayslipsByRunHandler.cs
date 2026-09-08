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
    public class GetPayslipsByRunHandler : IRequestHandler<GetPayslipsByRunRequestModel, GetPayslipsByRunResponseModel>
    {
        private readonly AppDbContext _dbContext;

        public GetPayslipsByRunHandler(AppDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<GetPayslipsByRunResponseModel> Handle(GetPayslipsByRunRequestModel request, CancellationToken cancellationToken)
        {
            // RBAC Check for Self-Service Isolation
            var hasManagePayroll = await _dbContext.UserRoles
                .Include(ur => ur.Role)
                .ThenInclude(r => r.RolePermissions)
                .AnyAsync(ur => ur.UserID == request.LoggedInUserId &&
                                ur.Role.RolePermissions.Any(p => p.PermissionKey == "hr.manage_payroll" && p.IsAllowed), cancellationToken);

            var query = _dbContext.HrPayslip
                .Include(p => p.HrEmployee)
                    .ThenInclude(e => e.Department)
                .Where(p => p.HrPayrollRunId == request.HrPayrollRunId);

            if (!hasManagePayroll)
            {
                query = query.Where(p => p.HrEmployee.UserId == request.LoggedInUserId);
            }

            var payslips = await query
                .Select(p => new HrPayslipDto
                {
                    HrPayslipId = p.HrPayslipId,
                    PayslipNumber = p.PayslipNumber,
                    EmployeeName = p.HrEmployee.FirstName + " " + p.HrEmployee.LastName,
                    EmployeeCode = p.HrEmployee.EmployeeCode,
                    DepartmentName = p.HrEmployee.Department != null ? p.HrEmployee.Department.Name : "Unknown",
                    Designation = p.HrEmployee.Designation,
                    PayrollTrack = p.PayrollTrack,
                    PanNumber = p.HrEmployee.PanNumber,
                    UanNumber = p.HrEmployee.UanNumber,
                    BankName = p.HrEmployee.BankName,
                    BankAccountNumber = p.HrEmployee.BankAccountNumber,

                    TotalDaysInMonth = p.TotalDaysInMonth,
                    PayableDays = p.PayableDays,
                    OvertimeDays = p.OvertimeDays,
                    NightShiftCount = p.NightShiftCount,

                    BasicEarned = p.BasicEarned,
                    HraEarned = p.HraEarned,
                    AllowancesEarned = p.AllowancesEarned,
                    OvertimeAmount = p.OvertimeAmount,
                    NightAllowanceAmount = p.NightAllowanceAmount,
                    IncentivesAmount = p.IncentivesAmount,
                    RetainerAmount = p.RetainerAmount,
                    OpdShareAmount = p.OpdShareAmount,
                    IpdVisitAmount = p.IpdVisitAmount,
                    SurgeryShareAmount = p.SurgeryShareAmount,
                    GrossEarnings = p.GrossEarnings,

                    PfEmployee = p.PfEmployee,
                    EsiEmployee = p.EsiEmployee,
                    ProfTax = p.ProfTax,
                    TdsDeducted = p.TdsDeducted,
                    LoanInstallment = p.LoanInstallment,
                    TotalDeductions = p.TotalDeductions,
                    NetSalary = p.NetSalary
                })
                .ToListAsync(cancellationToken);

            return new GetPayslipsByRunResponseModel
            {
                Payslips = payslips
            };
        }
    }
}
