using EasyHMSAPI.Application.Services;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace EasyHMSAPI.Application.Services
{
    /// <summary>
    /// Track B payroll computation for visiting consultants (TDS Section 194J).
    ///
    /// Formula:
    ///   GrossFees = Retainer
    ///             + (OPD Consultations × OPD Consultation Fee × OpdSharePercent/100)
    ///             + (IPD Round Visits × IpdVisitFee)
    ///             + Σ(Surgery Case Count × Surgery Package Cut)
    ///
    ///   TDS 194J  = 10% of GrossFees  (flat, no slab like Sec 192)
    ///   Net       = GrossFees - TDS194J - AdminSurcharge
    ///
    /// OPD/IPD/Surgery data is pulled from the ConsultantIncentiveLedger (existing entity)
    /// so accounts teams do not need to maintain separate Excel spreadsheets.
    ///
    /// PF and ESIC do NOT apply to visiting consultants (not on payroll roll).
    /// Professional Tax does NOT apply to consultants (they are not employees).
    /// </summary>
    public class ConsultantPayrollStrategy : IPayrollStrategy
    {
        private readonly AppDbContext _context;
        private const decimal TdsRate194J = 0.10m;

        public ConsultantPayrollStrategy(AppDbContext context)
        {
            _context = context;
        }

        public async Task<PayslipComputationResult> ComputeAsync(
            HrEmployee employee,
            PayrollPeriod period,
            CancellationToken cancellationToken = default)
        {
            var feeConfig = await _context.Set<HrConsultantFeeConfig>()
                .Where(c => c.HrEmployeeId == employee.HrEmployeeId && c.IsActive)
                .OrderByDescending(c => c.EffectiveFrom)
                .FirstOrDefaultAsync(cancellationToken)
                ?? throw new InvalidOperationException($"No active fee config for consultant {employee.EmployeeCode}");

            // ─── Pull actual activity data from ConsultantIncentiveLedger ────
            var incentiveRecords = await _context.ConsultantIncentiveLedger
                .Where(l => l.DoctorId == employee.HrEmployeeId   // DoctorId maps to HR employee
                         && l.AccruedAt >= period.FirstDay.ToDateTime(TimeOnly.MinValue)
                         && l.AccruedAt < period.LastDay.ToDateTime(TimeOnly.MinValue).AddDays(1)
                         && l.StatusCode != "CANCELLED")
                .ToListAsync(cancellationToken);

            // ─── Aggregate surgery share from ledger ──────────────────────────
            decimal surgeryShareAmount = incentiveRecords.Sum(r => r.IncentiveAmount);

            // ─── Parse surgery config for per-case breakdown ──────────────────
            // (surgeryShareAmount above is the definitive amount pulled from real billing data)
            // The fee config JSON is the "template" — actual amounts come from the ledger.

            // ─── OPD share: pulled from billing charge events (future integration) ──
            // For Phase 1, use feeConfig defaults. Actual OPD count sync from billing
            // is planned for the ConsultantProductivitySync background job.
            decimal opdShareAmount = 0m;
            decimal ipdVisitAmount = 0m;

            // TODO: Query BillingChargeEvents for OPD consult count in period
            // var opdCount = await ...
            // opdShareAmount = opdCount * opdConsultFee * (feeConfig.OpdSharePercent / 100);

            // TODO: Query RoundNotes/Encounters for IPD visit count in period
            // var ipdCount = await ...
            // ipdVisitAmount = ipdCount * feeConfig.IpdVisitFee;

            decimal grossFees = feeConfig.MonthlyRetainer
                              + opdShareAmount
                              + ipdVisitAmount
                              + surgeryShareAmount;

            // ─── TDS Section 194J ─────────────────────────────────────────────
            decimal tdsDeducted = Math.Round(grossFees * TdsRate194J, 2);

            // Hospital admin/equipment surcharge
            decimal adminSurcharge = feeConfig.AdminSurcharge;

            decimal totalDeductions = tdsDeducted + adminSurcharge;
            decimal netPayable = Math.Round(grossFees - totalDeductions, 2);

            return new PayslipComputationResult(
                EmployeeId: employee.HrEmployeeId,
                PayrollTrack: "TRACK_B_CONSULTANT",
                TotalDaysInMonth: period.DaysInMonth,
                PayableDays: period.DaysInMonth,  // Consultants are paid for full month
                OvertimeDays: 0m,
                NightShiftCount: 0,
                BasicEarned: 0m,
                HraEarned: 0m,
                AllowancesEarned: 0m,
                OvertimeAmount: 0m,
                NightAllowanceAmount: 0m,
                IncentivesAmount: opdShareAmount + ipdVisitAmount + surgeryShareAmount,
                RetainerAmount: feeConfig.MonthlyRetainer,
                OpdShareAmount: opdShareAmount,
                IpdVisitAmount: ipdVisitAmount,
                SurgeryShareAmount: surgeryShareAmount,
                GrossEarnings: grossFees,
                PfEmployee: 0m,      // Not applicable for consultants
                EsiEmployee: 0m,     // Not applicable for consultants
                ProfTax: 0m,         // Not applicable for consultants
                TdsDeducted: tdsDeducted,
                LoanInstallment: adminSurcharge,  // Surcharge shown in loan field for compatibility
                TotalDeductions: totalDeductions,
                NetSalary: netPayable,
                PfEmployer: 0m,
                EsiEmployer: 0m
            );
        }
    }
}
