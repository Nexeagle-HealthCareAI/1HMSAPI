using System.Text.Json;
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
    ///   TDS           = Section 192: projected annual income -> slab tax (regime, 87A rebate, surcharge, 4% cess) less TDS already
    ///                   deducted this financial year, spread over the months left (see IncomeTaxCalculator)
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
                         && (a.Status == "PRESENT" || a.Status == "LATE" || a.Status == "HALF_DAY" || a.Status == "ON_LEAVE"))
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
            // Worked and approved-leave days (PRESENT / LATE / HALF_DAY / ON_LEAVE -- CASUAL, SICK and
            // EARNED leave are all paid) plus weekly offs and declared holidays. Whether offs/holidays
            // are payable is a per-hospital policy (HrPayrollSettings); the default is payable.
            PayrollCalendarPolicy calendarPolicy;
            try
            {
                calendarPolicy = await PayrollCalendarPolicy.LoadAsync(_context, employee.HospitalId, period, cancellationToken);
            }
            catch (Exception)
            {
                // Settings tables not deployed yet: fall back to the documented default policy.
                calendarPolicy = PayrollCalendarPolicy.Default;
            }

            decimal payableDays = PayableDayCalculator.Compute(
                period, employee.DateOfJoining, attendance.Select(a => (a.AttendanceDate, a.Status)), calendarPolicy);
            decimal totalOvertimeHours = attendance.Sum(a => a.OvertimeHours);

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

            // TDS Section 192 — projection-based. Never takes more than the pay left after PF / ESIC / professional tax.
            var (tdsDeducted, tdsWorkingsJson) = await ComputeTdsAsync(
                employee, salary, period, grossEarnings, pfEmployee + esiEmployee + profTax, profTax, cancellationToken);

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
                EsiEmployer: esiEmployer,
                TdsWorkingsJson: tdsWorkingsJson
            );
        }

        /// <summary>
        /// Projects the financial year's salary: what was already paid (earlier payslips this FY), this month's actual gross, and the
        /// recurring fixed pay for each remaining month (so one month of leave-without-pay or overtime does not distort the rest of the
        /// year). Annual tax on that projection, minus TDS already deducted this FY, is spread over the months left, which also trues up
        /// automatically when pay changes part-way through the year.
        /// </summary>
        private async Task<(decimal Tds, string WorkingsJson)> ComputeTdsAsync(
            HrEmployee employee, HrSalaryStructure salary, PayrollPeriod period,
            decimal grossThisMonth, decimal otherDeductionsThisMonth, decimal profTaxThisMonth, CancellationToken cancellationToken)
        {
            var fyStart = IncomeTaxCalculator.FinancialYearStart(period.Year, period.Month);
            var rules = IncomeTaxCalculator.RulesFor(fyStart);
            var regime = IncomeTaxCalculator.NormaliseRegime(salary.TaxRegime);
            var monthsRemaining = IncomeTaxCalculator.MonthsRemainingInFinancialYear(period.Month);

            // Earlier payslips of this financial year (months before this one), whichever run produced them.
            var fyStartKey = fyStart * 12 + 3;                       // April of fyStart, as year*12 + (month-1)
            var thisKey = period.Year * 12 + (period.Month - 1);
            var earlier = await _context.Set<HrPayslip>()
                .Where(p => p.HrEmployeeId == employee.HrEmployeeId
                         && p.HrPayrollRun.Year * 12 + (p.HrPayrollRun.Month - 1) >= fyStartKey
                         && p.HrPayrollRun.Year * 12 + (p.HrPayrollRun.Month - 1) < thisKey)
                .Select(p => new { p.GrossEarnings, p.TdsDeducted })
                .ToListAsync(cancellationToken);
            decimal ytdGross = earlier.Sum(p => p.GrossEarnings);
            decimal ytdTds = earlier.Sum(p => p.TdsDeducted);

            decimal fixedMonthly = salary.BasicSalary + salary.Hra + salary.DearnessAllowance + salary.SpecialAllowance
                                 + salary.MedicalAllowance + salary.UniformAllowance;
            decimal projectedAnnualGross = ytdGross + grossThisMonth + (monthsRemaining - 1) * fixedMonthly;

            // Old regime only: professional tax paid is a deduction (annualised from the structure's monthly figure).
            decimal annualProfTax = (salary.ProfessionalTax > 0 ? salary.ProfessionalTax : profTaxThisMonth) * 12;
            decimal taxable = IncomeTaxCalculator.TaxableIncome(rules, regime, projectedAnnualGross, salary.AnnualDeclaredDeductions, annualProfTax);
            decimal annualTax = IncomeTaxCalculator.AnnualTax(rules, regime, taxable);

            decimal remaining = Math.Max(0m, annualTax - ytdTds);
            decimal monthly = Math.Round(remaining / monthsRemaining, 0, MidpointRounding.AwayFromZero);
            decimal cap = Math.Max(0m, grossThisMonth - otherDeductionsThisMonth);
            decimal tds = Math.Min(monthly, cap);

            var workings = JsonSerializer.Serialize(new
            {
                financialYear = $"{fyStart}-{(fyStart + 1) % 100:D2}",
                regime,
                rulesFallback = rules.IsFallback,
                projectedAnnualGross = Math.Round(projectedAnnualGross, 2),
                taxableIncome = taxable,
                annualTax,
                tdsDeductedEarlierThisYear = ytdTds,
                monthsRemaining,
                cappedByTakeHome = tds < monthly,
            });
            return (tds, workings);
        }
    }
}
