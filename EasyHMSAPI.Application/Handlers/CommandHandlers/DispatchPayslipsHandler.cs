using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Application.Services.Interfaces;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    public class DispatchPayslipsHandler : IRequestHandler<DispatchPayslipsRequestModel, DispatchPayslipsResponseModel>
    {
        private readonly AppDbContext _dbContext;
        private readonly IWhatsAppMessagingService _whatsAppService;

        public DispatchPayslipsHandler(AppDbContext dbContext, IWhatsAppMessagingService whatsAppService)
        {
            _dbContext = dbContext;
            _whatsAppService = whatsAppService;
        }

        public async Task<DispatchPayslipsResponseModel> Handle(DispatchPayslipsRequestModel request, CancellationToken cancellationToken)
        {
            var run = await _dbContext.HrPayrollRun
                .Include(r => r.Hospital)
                .FirstOrDefaultAsync(r => r.HrPayrollRunId == request.HrPayrollRunId, cancellationToken);

            if (run == null)
            {
                return new DispatchPayslipsResponseModel { Success = false, Message = "Payroll run not found." };
            }

            var payslips = await _dbContext.HrPayslip
                .Include(p => p.HrEmployee)
                .Where(p => p.HrPayrollRunId == request.HrPayrollRunId)
                .ToListAsync(cancellationToken);

            var monthYear = $"{run.Month:D2}/{run.Year}";
            int dispatched = 0;

            foreach (var payslip in payslips)
            {
                if (!string.IsNullOrEmpty(payslip.HrEmployee.ContactNumber))
                {
                    var employeeName = $"{payslip.HrEmployee.FirstName} {payslip.HrEmployee.LastName}";
                    
                    var sent = await _whatsAppService.SendPayslipNotificationAsync(
                        payslip.HrEmployee.ContactNumber,
                        employeeName,
                        monthYear,
                        payslip.NetSalary,
                        run.Hospital.Name
                    );

                    if (sent)
                    {
                        dispatched++;
                    }
                }
            }

            // Mark the run as dispersed if not already (or add another status)
            if (run.Status != "DISBURSED")
            {
                run.Status = "DISBURSED";
                await _dbContext.SaveChangesAsync(cancellationToken);
            }

            return new DispatchPayslipsResponseModel
            {
                Success = true,
                Message = $"Dispatched WhatsApp notifications to {dispatched} employees.",
                DispatchedCount = dispatched
            };
        }
    }
}
