using EasyHMSAPI.Domain.Entities;

namespace EasyHMSAPI.Application.Services
{
    /// <summary>
    /// Open/Closed Principle: The payroll engine is open for extension (new payroll tracks)
    /// but closed for modification — each track implements this interface independently.
    ///
    /// Current implementations:
    ///   - SalariedPayrollStrategy  (Track A: full-time staff, TDS Sec 192)
    ///   - ConsultantPayrollStrategy (Track B: visiting consultants, TDS Sec 194J)
    /// </summary>
    public interface IPayrollStrategy
    {
        /// <summary>
        /// Computes the payslip for one employee in a given calendar month.
        /// Pulls attendance, roster, overtime, night shifts, and incentive ledger
        /// from the DB within the method — no pre-loaded state is required.
        /// </summary>
        Task<PayslipComputationResult> ComputeAsync(
            HrEmployee employee,
            PayrollPeriod period,
            CancellationToken cancellationToken = default);
    }

    /// <summary>Immutable value object representing the payroll period.</summary>
    public sealed record PayrollPeriod(int Month, int Year)
    {
        public int DaysInMonth => DateTime.DaysInMonth(Year, Month);
        public DateOnly FirstDay => new(Year, Month, 1);
        public DateOnly LastDay => new(Year, Month, DaysInMonth);
    }

    /// <summary>
    /// Result of the payroll computation for a single employee.
    /// All monetary values are in INR, rounded to 2 decimal places.
    /// </summary>
    public sealed record PayslipComputationResult(
        // Identity
        Guid EmployeeId,
        string PayrollTrack,

        // Period
        int TotalDaysInMonth,
        decimal PayableDays,
        decimal OvertimeDays,
        int NightShiftCount,

        // Track A: Earnings breakdown
        decimal BasicEarned,
        decimal HraEarned,
        decimal AllowancesEarned,
        decimal OvertimeAmount,
        decimal NightAllowanceAmount,
        decimal IncentivesAmount,

        // Track B: Fee breakdown
        decimal RetainerAmount,
        decimal OpdShareAmount,
        decimal IpdVisitAmount,
        decimal SurgeryShareAmount,

        // Gross & Deductions
        decimal GrossEarnings,
        decimal PfEmployee,
        decimal EsiEmployee,
        decimal ProfTax,
        decimal TdsDeducted,
        decimal LoanInstallment,
        decimal TotalDeductions,
        decimal NetSalary,

        // Employer contributions
        decimal PfEmployer,
        decimal EsiEmployer
    );
}
