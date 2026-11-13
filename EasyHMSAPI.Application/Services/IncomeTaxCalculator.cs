namespace EasyHMSAPI.Application.Services
{
    /// <summary>
    /// Income tax on salary (Section 192 TDS) for one financial year: slab tax, section 87A rebate (with marginal relief in the new
    /// regime), surcharge and 4% cess. Pure and deterministic so it can be unit-tested against published figures.
    ///
    /// The slab tables are DATA, kept in <see cref="Rules"/> per financial year. The Budget changes them most years: add the new
    /// year's entry (nothing else changes). A financial year with no entry uses the latest earlier one, which is flagged in
    /// <see cref="TaxYearRules.IsFallback"/> so the payslip workings can show that the rules were not year-specific.
    ///
    /// Known simplifications (documented, not hidden): senior-citizen and super-senior slabs, surcharge marginal relief,
    /// income from other sources / previous employer, and HRA / LTA exemptions (old regime) are not modelled; old-regime
    /// deductions come from the single annual declared amount per employee.
    /// </summary>
    public static class IncomeTaxCalculator
    {
        public const string NewRegime = "NEW";
        public const string OldRegime = "OLD";

        public sealed record Slab(decimal UpTo, decimal Rate);

        public sealed record TaxYearRules(
            int FyStartYear,
            Slab[] NewSlabs, decimal NewStandardDeduction, decimal NewRebateLimit, decimal NewRebateMax,
            Slab[] OldSlabs, decimal OldStandardDeduction, decimal OldRebateLimit, decimal OldRebateMax)
        {
            public bool IsFallback { get; init; }
        }

        private const decimal Max = decimal.MaxValue;

        // FY 2024-25 (AY 2025-26) and FY 2025-26 (AY 2026-27, Budget 2025). Old-regime slabs are unchanged between them.
        private static readonly Slab[] OldSlabsCommon = { new(250_000m, 0m), new(500_000m, 0.05m), new(1_000_000m, 0.20m), new(Max, 0.30m) };

        public static readonly TaxYearRules[] Rules =
        {
            new(2024,
                new Slab[] { new(300_000m, 0m), new(700_000m, 0.05m), new(1_000_000m, 0.10m), new(1_200_000m, 0.15m), new(1_500_000m, 0.20m), new(Max, 0.30m) },
                75_000m, 700_000m, 25_000m,
                OldSlabsCommon, 50_000m, 500_000m, 12_500m),
            new(2025,
                new Slab[] { new(400_000m, 0m), new(800_000m, 0.05m), new(1_200_000m, 0.10m), new(1_600_000m, 0.15m), new(2_000_000m, 0.20m), new(2_400_000m, 0.25m), new(Max, 0.30m) },
                75_000m, 1_200_000m, 60_000m,
                OldSlabsCommon, 50_000m, 500_000m, 12_500m),
        };

        /// <summary>Indian financial year: April to March. Returns the calendar year in which it starts.</summary>
        public static int FinancialYearStart(int year, int month) => month >= 4 ? year : year - 1;

        /// <summary>Months of the financial year left INCLUDING the given month (April = 12 ... March = 1).</summary>
        public static int MonthsRemainingInFinancialYear(int month) => month >= 4 ? 16 - month : 4 - month;

        public static TaxYearRules RulesFor(int fyStartYear)
        {
            var exact = Rules.FirstOrDefault(r => r.FyStartYear == fyStartYear);
            if (exact != null) return exact;
            var latestEarlier = Rules.Where(r => r.FyStartYear < fyStartYear).OrderByDescending(r => r.FyStartYear).FirstOrDefault() ?? Rules[0];
            return latestEarlier with { IsFallback = true };
        }

        public static string NormaliseRegime(string? raw) => string.Equals(raw?.Trim(), OldRegime, StringComparison.OrdinalIgnoreCase) ? OldRegime : NewRegime;

        /// <summary>Standard deduction (and, in the old regime, the other deductions) taken off the projected annual salary.</summary>
        public static decimal TaxableIncome(TaxYearRules rules, string regime, decimal annualGross, decimal declaredDeductions, decimal annualProfessionalTax)
        {
            var old = regime == OldRegime;
            var deductions = old
                ? rules.OldStandardDeduction + Math.Max(0m, declaredDeductions) + Math.Max(0m, annualProfessionalTax)
                : rules.NewStandardDeduction;
            var taxable = Math.Max(0m, annualGross - deductions);
            return Math.Floor(taxable / 10m) * 10m;   // section 288A: rounded down to a multiple of 10
        }

        /// <summary>Total annual tax on a taxable income, whole rupees (section 288B).</summary>
        public static decimal AnnualTax(TaxYearRules rules, string regime, decimal taxableIncome)
        {
            var old = regime == OldRegime;
            var slabs = old ? rules.OldSlabs : rules.NewSlabs;
            var rebateLimit = old ? rules.OldRebateLimit : rules.NewRebateLimit;
            var rebateMax = old ? rules.OldRebateMax : rules.NewRebateMax;

            var tax = SlabTax(slabs, taxableIncome);

            if (taxableIncome <= rebateLimit)
            {
                tax = Math.Max(0m, tax - rebateMax);                      // section 87A
            }
            else if (!old)
            {
                // marginal relief: just above the rebate limit the tax can never exceed the income above that limit
                tax = Math.Min(tax, taxableIncome - rebateLimit);
            }

            var surcharge = tax * SurchargeRate(old, taxableIncome);
            var cess = (tax + surcharge) * 0.04m;
            return Math.Round(tax + surcharge + cess, 0, MidpointRounding.AwayFromZero);
        }

        private static decimal SlabTax(Slab[] slabs, decimal income)
        {
            decimal tax = 0m, lower = 0m;
            foreach (var slab in slabs)
            {
                if (income <= lower) break;
                var upper = Math.Min(income, slab.UpTo);
                tax += (upper - lower) * slab.Rate;
                lower = slab.UpTo;
            }
            return tax;
        }

        // Surcharge on tax. The new regime caps it at 25%; the old regime goes to 37% above Rs 5 crore.
        private static decimal SurchargeRate(bool old, decimal income) => income switch
        {
            > 50_000_000m => old ? 0.37m : 0.25m,
            > 20_000_000m => 0.25m,
            > 10_000_000m => 0.15m,
            > 5_000_000m => 0.10m,
            _ => 0m,
        };
    }
}
