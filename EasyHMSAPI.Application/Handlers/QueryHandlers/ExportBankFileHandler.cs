using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    public class ExportBankFileHandler : IRequestHandler<ExportBankFileRequestModel, ExportBankFileResponseModel>
    {
        private readonly AppDbContext _dbContext;

        public ExportBankFileHandler(AppDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<ExportBankFileResponseModel> Handle(ExportBankFileRequestModel request, CancellationToken cancellationToken)
        {
            var run = await _dbContext.HrPayrollRun
                .FirstOrDefaultAsync(r => r.HrPayrollRunId == request.HrPayrollRunId, cancellationToken);

            if (run == null)
            {
                return new ExportBankFileResponseModel { Success = false, Message = "Payroll run not found." };
            }

            var payslips = await _dbContext.HrPayslip
                .Include(p => p.HrEmployee)
                .Where(p => p.HrPayrollRunId == request.HrPayrollRunId)
                .ToListAsync(cancellationToken);

            var sb = new StringBuilder();

            if (request.BankFormat == "HDFC")
            {
                // Simple HDFC NEFT Bulk Template
                sb.AppendLine("Beneficiary Name,Beneficiary Account,IFSC,Amount,Remarks");
                foreach (var ps in payslips)
                {
                    sb.AppendLine($"{ps.HrEmployee.FirstName} {ps.HrEmployee.LastName},1234567890,HDFC0001234,{ps.NetSalary},Salary {run.Month}/{run.Year}");
                }
            }
            else if (request.BankFormat == "SBI")
            {
                // Simple SBI Corporate Template
                sb.AppendLine("Account Number,Amount,Beneficiary Name,Debit Account,Narration");
                foreach (var ps in payslips)
                {
                    sb.AppendLine($"1234567890,{ps.NetSalary},{ps.HrEmployee.FirstName} {ps.HrEmployee.LastName},333333333,Salary {run.Month}/{run.Year}");
                }
            }
            else
            {
                // Generic Template
                sb.AppendLine("EmpCode,Name,Amount,Bank_Name,Account_Number,IFSC");
                foreach (var ps in payslips)
                {
                    sb.AppendLine($"{ps.HrEmployee.EmployeeCode},{ps.HrEmployee.FirstName} {ps.HrEmployee.LastName},{ps.NetSalary},GENERIC BANK,1234567890,GEN00123");
                }
            }

            var bytes = Encoding.UTF8.GetBytes(sb.ToString());
            
            // Mark as disbursed if it's currently DRAFT
            if (run.Status == "DRAFT")
            {
                run.Status = "DISBURSED";
                await _dbContext.SaveChangesAsync(cancellationToken);
            }

            return new ExportBankFileResponseModel
            {
                Success = true,
                FileBytes = bytes,
                FileName = $"Payroll_{request.BankFormat}_{run.Month}_{run.Year}.csv",
                ContentType = "text/csv"
            };
        }
    }
}
