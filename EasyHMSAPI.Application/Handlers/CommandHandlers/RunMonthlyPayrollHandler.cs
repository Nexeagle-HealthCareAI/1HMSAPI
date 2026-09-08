using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Application.Services;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    public class RunMonthlyPayrollHandler : IRequestHandler<RunMonthlyPayrollRequestModel, RunMonthlyPayrollResponseModel>
    {
        private readonly AppDbContext _context;

        public RunMonthlyPayrollHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<RunMonthlyPayrollResponseModel> Handle(RunMonthlyPayrollRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                // Verify no existing payroll run for this month/year
                var existingRun = await _context.HrPayrollRun
                    .AnyAsync(r => r.HospitalId == request.HospitalId && r.Month == request.Month && r.Year == request.Year, cancellationToken);
                
                if (existingRun)
                {
                    return new RunMonthlyPayrollResponseModel
                    {
                        Success = false,
                        Message = "Payroll already run for this month.",
                        Errors = new List<string> { "Duplicate Payroll Run" }
                    };
                }

                var period = new PayrollPeriod(request.Month, request.Year);
                
                var activeEmployees = await _context.HrEmployee
                    .Where(e => e.HospitalId == request.HospitalId && e.IsActive && e.Status != "INACTIVE")
                    .ToListAsync(cancellationToken);

                var run = new HrPayrollRun
                {
                    HrPayrollRunId = Guid.NewGuid(),
                    HospitalId = request.HospitalId,
                    Month = request.Month,
                    Year = request.Year,
                    Status = "DRAFT",
                    ProcessedByUserId = request.ProcessedByUserId,
                    ProcessedAt = DateTime.UtcNow
                };

                _context.HrPayrollRun.Add(run);

                decimal totalGross = 0;
                decimal totalNet = 0;
                decimal totalPf = 0;
                decimal totalEsi = 0;
                decimal totalTds = 0;

                var salariedStrategy = new SalariedPayrollStrategy(_context);
                var consultantStrategy = new ConsultantPayrollStrategy(_context);

                foreach (var employee in activeEmployees)
                {
                    IPayrollStrategy strategy = employee.PayrollTrack == "TRACK_B_CONSULTANT" 
                        ? consultantStrategy 
                        : salariedStrategy;

                    // Some active employees might not have fee config/salary structure set up yet.
                    // We catch exceptions per-employee so one bad record doesn't fail the whole hospital's payroll.
                    try
                    {
                        var result = await strategy.ComputeAsync(employee, period, cancellationToken);

                        var payslipNumber = $"PAY-{request.Year}-{request.Month:D2}-{employee.EmployeeCode}";

                        var payslip = new HrPayslip
                        {
                            HrPayslipId = Guid.NewGuid(),
                            HrPayrollRunId = run.HrPayrollRunId,
                            HrEmployeeId = employee.HrEmployeeId,
                            PayslipNumber = payslipNumber,
                            PayrollTrack = result.PayrollTrack,
                            TotalDaysInMonth = result.TotalDaysInMonth,
                            PayableDays = result.PayableDays,
                            OvertimeDays = result.OvertimeDays,
                            NightShiftCount = result.NightShiftCount,
                            
                            BasicEarned = result.BasicEarned,
                            HraEarned = result.HraEarned,
                            AllowancesEarned = result.AllowancesEarned,
                            OvertimeAmount = result.OvertimeAmount,
                            NightAllowanceAmount = result.NightAllowanceAmount,
                            IncentivesAmount = result.IncentivesAmount,
                            
                            RetainerAmount = result.RetainerAmount,
                            OpdShareAmount = result.OpdShareAmount,
                            IpdVisitAmount = result.IpdVisitAmount,
                            SurgeryShareAmount = result.SurgeryShareAmount,
                            
                            GrossEarnings = result.GrossEarnings,
                            PfEmployee = result.PfEmployee,
                            EsiEmployee = result.EsiEmployee,
                            ProfTax = result.ProfTax,
                            TdsDeducted = result.TdsDeducted,
                            LoanInstallment = result.LoanInstallment,
                            TotalDeductions = result.TotalDeductions,
                            NetSalary = result.NetSalary,
                            
                            PfEmployer = result.PfEmployer,
                            EsiEmployer = result.EsiEmployer,
                            CreatedAt = DateTime.UtcNow
                        };

                        _context.HrPayslip.Add(payslip);

                        totalGross += result.GrossEarnings;
                        totalNet += result.NetSalary;
                        totalPf += result.PfEmployee;
                        totalEsi += result.EsiEmployee;
                        totalTds += result.TdsDeducted;
                    }
                    catch (Exception ex)
                    {
                        // Log warning for missing config and skip this employee
                        Console.WriteLine($"Skipping payroll for {employee.EmployeeCode}: {ex.Message}");
                    }
                }

                run.TotalGrossDisbursement = totalGross;
                run.TotalNetDisbursement = totalNet;
                run.TotalPfDeducted = totalPf;
                run.TotalEsiDeducted = totalEsi;
                run.TotalTdsDeducted = totalTds;

                await _context.SaveChangesAsync(cancellationToken);

                return new RunMonthlyPayrollResponseModel
                {
                    Success = true,
                    Message = "Monthly payroll generated successfully",
                    HrPayrollRunId = run.HrPayrollRunId,
                    PayslipsGenerated = _context.ChangeTracker.Entries<HrPayslip>().Count(e => e.State == EntityState.Unchanged || e.State == EntityState.Added),
                    TotalNetDisbursement = totalNet
                };
            }
            catch (Exception ex)
            {
                return new RunMonthlyPayrollResponseModel
                {
                    Success = false,
                    Message = "Error occurred while generating payroll",
                    Errors = new List<string> { ex.Message }
                };
            }
        }
    }
}
