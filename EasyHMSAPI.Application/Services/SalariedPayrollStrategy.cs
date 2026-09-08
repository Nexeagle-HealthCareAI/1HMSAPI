using EasyHMSAPI.Application.Services;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Services
{
    /// <summary>
    /// Track A payroll computation for full-time salaried employees.
    ///
    /// Statutory deductions (Indian labour law):
    ///   PF Employee   = 12% of Basic Salary (capped at ₹1,800/mo if Basic > ₹15,000)
    ///   ESIC Employee = 0.75% of Gross (only if Gross ≤ ₹21,000/mo)
    ///   Prof Tax      = State-configured slab (e.g. ₹200/mo for Bihar/Maharashtra)
    ///   TDS           = Section 192 (income tax slab — simplified as % for now)
    ///
    /// Employer contributions (informational, not deducted from employee):
    ///   PF Employer   = 12% of Basic (8.33% → EPS, 3.67% → EPF)
    ///   ESIC Employer = 3.25% of Gross
    ///
    /// Earnings:
    ///   Basic         = (BasicSalary / TotalDays) × PayableDays
    ///   HRA           = (Hra / TotalDays) × PayableDays
    ///   Allowances    = (DA + SpecialAllowance + MedicalAllowance) × (PayableDays / TotalDays)
    ///   NightAllowance= NightShiftCount × NightShiftAllowanceRate
    ///   Overtime      = OvertimeHours × (BasicSalary / (TotalDays × 8h)) × 1.5
    /// </summary>
    public class SalariedPayrollStrategy : IPayrollStrategy
    {
        private readonly AppDbContext _context;

        // EPF threshold: PF is capped at ₹15,000 basic for new joiners
        private const decimal PfBasicCap = 15_000m;
        // ESIC gross ceiling: employees with Gross > ₹21,000 are not ESIC eligible
        private const decimal EsiGrossCeiling = 21_000m;
        // PF rate: 12% employee contribution
        private const decimal PfRate = 0.12m;
        // ESIC employee rate: 0.75%
        private const decimal EsiEmployeeRate = 0.0075m;
        // ESIC employer rate: 3.25%
        private const decimal EsiEmployerRate = 0.0325m;
        // Overtime multiplier: 1.5× hourly rate
        private const decimal OtMultiplier = 1.5m;
        // Standard working hours per day
        private const decimal StandardHoursPerDay = 8m;

        public SalariedPayrollStrategy(AppDbContext context)
        {
            _context = context;
        }

        public async Task<PayslipComputationResult> ComputeAsync(
            HrEmployee employee,
            PayrollPeriod period,
            CancellationToken cancellationToken = default)
        {
            var salary = await _context.Set<HrSalaryStructure>()
                .Where(s => s.HrEmployeeId == employee.HrEmployeeId && s.IsActive)
                .OrderByDescending(s => s.EffectiveFrom)
                .FirstOrDefaultAsync(cancellationToken)
                ?? throw new InvalidOperationException($"No active salary structure for employee {employee.EmployeeCode}");

            // ─── Attendance data for the period ───────────────────────────────
            var attendance = await _context.Set<HrAttendanceLog>()
                .Where(a => a.HrEmployeeId == employee.HrEmployeeId
                         && a.AttendanceDate >= period.FirstDay
                         && a.AttendanceDate <= period.LastDay
                         && (a.Status == "PRESENT" || a.Status == "LATE" || a.Status == "HALF_DAY"))
                .ToListAsync(cancellationToken);

            // ─── Roster: count night shifts in period ─────────────────────────
            var nightShiftCount = await _context.Set<HrDutyRoster>()
                .Include(r => r.HrHospitalShift)
                .CountAsync(r => r.HrEmployeeId == employee.HrEmployeeId
                              && r.RosterDate >= period.FirstDay
                              && r.RosterDate <= period.LastDay
                              && r.HrHospitalShift.ShiftCode == "SFT_N"
                              && r.Status == "COMPLETED", cancellationToken);

            // ─── Compute payable days ─────────────────────────────────────────
            decimal payableDays = 0m;
            decimal totalOvertimeHours = 0m;

            foreach (var log in attendance)
            {
                payableDays += log.Status == "HALF_DAY" ? 0.5m : 1.0m;
                totalOvertimeHours += log.OvertimeHours;
            }

            // ─── Prorate earnings ─────────────────────────────────────────────
            int totalDays = period.DaysInMonth;
            decimal prorateRatio = payableDays / totalDays;

            decimal basicEarned = Math.Round(salary.BasicSalary * prorateRatio, 2);
            decimal hraEarned = Math.Round(salary.Hra * prorateRatio, 2);
            decimal allowancesEarned = Math.Round(
                (salary.DearnessAllowance + salary.SpecialAllowance + salary.MedicalAllowance + salary.UniformAllowance) * prorateRatio, 2);

            // Night shift allowance: flat rate × completed night shifts (no proration)
            decimal nightAllowance = Math.Round(nightShiftCount * salary.NightShiftAllowanceRate, 2);

            // Overtime: hourly rate = BasicSalary / (totalDays × 8h), then × 1.5 × OT hours
            decimal hourlyRate = salary.BasicSalary / (totalDays * StandardHoursPerDay);
            decimal overtimeAmount = Math.Round(hourlyRate * OtMultiplier * totalOvertimeHours, 2);

            decimal grossEarnings = basicEarned + hraEarned + allowancesEarned + nightAllowance + overtimeAmount;

            // ─── Deductions ───────────────────────────────────────────────────
            decimal pfEmployee = 0m;
            decimal pfEmployer = 0m;

            if (salary.IsPfEligible)
            {
                // PF is computed on basic earned (capped at ₹15,000 if configured so)
                decimal pfBase = Math.Min(basicEarned, salary.BasicSalary > PfBasicCap ? PfBasicCap : basicEarned);
                pfEmployee = Math.Round(pfBase * PfRate, 2);
                pfEmployer = Math.Round(pfBase * PfRate, 2);
            }

            decimal esiEmployee = 0m;
            decimal esiEmployer = 0m;

            if (salary.IsEsiEligible && grossEarnings <= EsiGrossCeiling)
            {
                esiEmployee = Math.Round(grossEarnings * EsiEmployeeRate, 2);
                esiEmployer = Math.Round(grossEarnings * EsiEmployerRate, 2);
            }

            decimal profTax = payableDays >= (totalDays * 0.5m) ? salary.ProfessionalTax : 0m;

            // TDS Section 192: simplified computation
            // A full implementation would use the annual CTC projection and IT slab table.
            // Here we use 0% for annual income ≤ ₹5L, 5% for ₹5L-₹7.5L, etc.
            decimal annualGross = salary.MonthlyGrossCtc * 12;
            decimal tdsDeducted = annualGross switch
            {
                <= 500_000m => 0m,
                <= 750_000m => Math.Round((annualGross - 500_000m) * 0.05m / 12, 2),
                <= 1_000_000m => Math.Round(((annualGross - 750_000m) * 0.10m + 12_500m) / 12, 2),
                <= 1_250_000m => Math.Round(((annualGross - 1_000_000m) * 0.15m + 37_500m) / 12, 2),
                <= 1_500_000m => Math.Round(((annualGross - 1_250_000m) * 0.20m + 75_000m) / 12, 2),
                _ => Math.Round(((annualGross - 1_500_000m) * 0.30m + 125_000m) / 12, 2),
            };

            // TODO: Add loan installment deduction when HrLoanLedger is implemented
            decimal loanInstallment = 0m;

            decimal totalDeductions = pfEmployee + esiEmployee + profTax + tdsDeducted + loanInstallment;
            decimal netSalary = Math.Round(grossEarnings - totalDeductions, 2);

            return new PayslipComputationResult(
                EmployeeId: employee.HrEmployeeId,
                PayrollTrack: "TRACK_A_SALARIED",
                TotalDaysInMonth: totalDays,
                PayableDays: payableDays,
                OvertimeDays: Math.Round(totalOvertimeHours / StandardHoursPerDay, 1),
                NightShiftCount: nightShiftCount,
                BasicEarned: basicEarned,
                HraEarned: hraEarned,
                AllowancesEarned: allowancesEarned,
                OvertimeAmount: overtimeAmount,
                NightAllowanceAmount: nightAllowance,
                IncentivesAmount: 0m,
                RetainerAmount: 0m,
                OpdShareAmount: 0m,
                IpdVisitAmount: 0m,
                SurgeryShareAmount: 0m,
                GrossEarnings: grossEarnings,
                PfEmployee: pfEmployee,
                EsiEmployee: esiEmployee,
                ProfTax: profTax,
                TdsDeducted: tdsDeducted,
                LoanInstallment: loanInstallment,
                TotalDeductions: totalDeductions,
                NetSalary: netSalary,
                PfEmployer: pfEmployer,
                EsiEmployer: esiEmployer
            );
        }
    }
}
