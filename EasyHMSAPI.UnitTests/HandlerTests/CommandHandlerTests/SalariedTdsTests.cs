using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.Handlers.CommandHandlers;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.Services;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using EasyHMSAPI.UnitTests.TestUtils;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.CommandHandlerTests
{
    // Section 192 TDS used to be a fixed slab on annual CTC / 12 (no regime, standard deduction, rebate, cess or year-to-date true-up).
    // The figures below are worked by hand from the published slabs.
    [TestFixture]
    public class SalariedTdsTests
    {
        // ---------------------------------------------------------------- the pure calculator

        private static decimal Tax(string regime, decimal annualGross, decimal declared = 0m, decimal annualPt = 0m, int fy = 2025)
        {
            var rules = IncomeTaxCalculator.RulesFor(fy);
            return IncomeTaxCalculator.AnnualTax(rules, regime, IncomeTaxCalculator.TaxableIncome(rules, regime, annualGross, declared, annualPt));
        }

        [TestCase(1_275_000, 0)]        // taxable 12,00,000 -> 87A rebate wipes the tax
        [TestCase(1_300_000, 26_000)]   // taxable 12,25,000 -> marginal relief: tax limited to the 25,000 above 12L, +4% cess
        [TestCase(1_800_000, 150_800)]  // taxable 17,25,000 -> 20k+40k+60k+25k = 1,45,000 + 4% cess
        [TestCase(3_000_000, 475_800)]  // taxable 29,25,000 -> 4,57,500 + 4% cess
        [TestCase(475_000, 0)]          // below the basic exemption after the standard deduction
        public void NewRegime_FY2025_26_MatchesTheSlabs(int annualGross, int expected) =>
            Assert.That(Tax(IncomeTaxCalculator.NewRegime, annualGross), Is.EqualTo(expected));

        [Test]
        public void OldRegime_DeclaredDeductionsAndProfessionalTax_ReduceTheTaxableIncome()
        {
            // 10,00,000 - 50,000 standard - 1,50,000 declared - 2,400 PT = 7,97,600 -> 12,500 + 2,97,600 x 20% = 72,020; +4% cess = 74,900.8
            Assert.That(Tax(IncomeTaxCalculator.OldRegime, 1_000_000m, declared: 150_000m, annualPt: 2_400m), Is.EqualTo(74_901m));
        }

        [Test]
        public void OldRegime_Rebate_AtFiveLakh_ButNoMarginalRelief_JustAbove()
        {
            Assert.That(Tax(IncomeTaxCalculator.OldRegime, 550_000m), Is.EqualTo(0m), "taxable 5,00,000 is within the 87A limit");
            // taxable 5,10,000: 12,500 + 10,000 x 20% = 14,500, +4% cess = 15,080 (the old regime has no marginal relief)
            Assert.That(Tax(IncomeTaxCalculator.OldRegime, 560_000m), Is.EqualTo(15_080m));
        }

        [Test]
        public void NewRegime_IgnoresDeclaredDeductions() =>
            Assert.That(Tax(IncomeTaxCalculator.NewRegime, 1_800_000m, declared: 500_000m), Is.EqualTo(Tax(IncomeTaxCalculator.NewRegime, 1_800_000m)));

        [TestCase(2026, 3, 2025)]
        [TestCase(2026, 4, 2026)]
        [TestCase(2026, 12, 2026)]
        public void FinancialYear_RunsAprilToMarch(int year, int month, int expectedStart) =>
            Assert.That(IncomeTaxCalculator.FinancialYearStart(year, month), Is.EqualTo(expectedStart));

        [TestCase(4, 12)]
        [TestCase(8, 8)]
        [TestCase(12, 4)]
        [TestCase(1, 3)]
        [TestCase(3, 1)]
        public void MonthsRemaining_IncludesTheCurrentMonth(int month, int expected) =>
            Assert.That(IncomeTaxCalculator.MonthsRemainingInFinancialYear(month), Is.EqualTo(expected));

        [Test]
        public void AYearWithoutItsOwnRules_UsesTheLatestEarlierOne_AndSaysSo()
        {
            Assert.That(IncomeTaxCalculator.RulesFor(2025).IsFallback, Is.False);
            var later = IncomeTaxCalculator.RulesFor(2026);
            Assert.That(later.IsFallback, Is.True);
            Assert.That(later.FyStartYear, Is.EqualTo(2025));
        }

        [TestCase(null, "NEW")]
        [TestCase("", "NEW")]
        [TestCase("old", "OLD")]
        [TestCase(" OLD ", "OLD")]
        [TestCase("whatever", "NEW")]
        public void RegimeNormalisation(string? raw, string expected) => Assert.That(IncomeTaxCalculator.NormaliseRegime(raw), Is.EqualTo(expected));

        // ---------------------------------------------------------------- the payroll strategy

        private AppDbContext _context = null!;
        private HrEmployee _employee = null!;
        private HrSalaryStructure _structure = null!;
        private readonly PayrollPeriod _august = new(Month: 8, Year: 2026);

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _employee = new HrEmployee
            {
                HrEmployeeId = Guid.NewGuid(), HospitalId = Guid.NewGuid(), EmployeeCode = "EMP-1", FirstName = "A", LastName = "B", Gender = "Female",
                DateOfBirth = new DateOnly(1990, 1, 1), ContactNumber = "9999999999", EmploymentType = "FULL_TIME_SALARIED", DepartmentId = Guid.NewGuid(),
                Designation = "Doctor", DateOfJoining = new DateOnly(2020, 1, 1), PanNumber = "ABCDE1234F", PayrollTrack = "TRACK_A_SALARIED", IsActive = true, Status = "ACTIVE",
            };
            // 1,50,000 a month (18 lakh a year)
            _structure = new HrSalaryStructure
            {
                HrSalaryStructureId = Guid.NewGuid(), HrEmployeeId = _employee.HrEmployeeId, EffectiveFrom = new DateOnly(2020, 1, 1),
                MonthlyGrossCtc = 150_000m, BasicSalary = 80_000m, Hra = 40_000m, SpecialAllowance = 30_000m,
                IsPfEligible = true, IsEsiEligible = false, ProfessionalTax = 200m, IsActive = true,
            };
            _context.HrEmployee.Add(_employee);
            _context.HrSalaryStructure.Add(_structure);
            for (var day = 1; day <= 31; day++)
                _context.Set<HrAttendanceLog>().Add(new HrAttendanceLog
                {
                    HrAttendanceLogId = Guid.NewGuid(), HrEmployeeId = _employee.HrEmployeeId, AttendanceDate = new DateOnly(2026, 8, day), Status = "PRESENT", PunchSource = "BIOMETRIC",
                });
            _context.SaveChanges();
        }

        [TearDown]
        public void TearDown()
        {
            InMemoryDbContextFactory.Destroy(_context);
            _context.Dispose();
        }

        private void SeedEarlierPayslips(int fromMonth, int toMonth, int year, decimal gross, decimal tds)
        {
            for (var m = fromMonth; m <= toMonth; m++)
            {
                var run = new HrPayrollRun { HrPayrollRunId = Guid.NewGuid(), HospitalId = _employee.HospitalId, Month = m, Year = year, Status = "PAID" };
                _context.HrPayrollRun.Add(run);
                _context.HrPayslip.Add(new HrPayslip
                {
                    HrPayslipId = Guid.NewGuid(), HrPayrollRunId = run.HrPayrollRunId, HrEmployeeId = _employee.HrEmployeeId,
                    PayslipNumber = $"PAY-{year}-{m:D2}", PayrollTrack = "TRACK_A_SALARIED", GrossEarnings = gross, TdsDeducted = tds,
                });
            }
            _context.SaveChanges();
        }

        private Task<PayslipComputationResult> Compute() => new SalariedPayrollStrategy(_context).ComputeAsync(_employee, _august, CancellationToken.None);

        [Test]
        public async Task Projects_TheYear_AndSpreadsTheRemainingTaxOverTheRemainingMonths()
        {
            // Apr-Jul already paid at 1,50,000 with 11,000 TDS each. Projection = 4 x 1.5L + this month 1.5L + 7 x 1.5L = 18L.
            // New regime tax on 18L = 1,50,800. Remaining = 1,50,800 - 44,000 = 1,06,800 over the 8 months left (Aug-Mar) = 13,350.
            SeedEarlierPayslips(4, 7, 2026, gross: 150_000m, tds: 11_000m);

            var result = await Compute();

            Assert.That(result.GrossEarnings, Is.EqualTo(150_000m));
            Assert.That(result.TdsDeducted, Is.EqualTo(13_350m));
            Assert.That(result.TotalDeductions, Is.EqualTo(result.PfEmployee + result.EsiEmployee + result.ProfTax + result.TdsDeducted + result.LoanInstallment));
            Assert.That(result.NetSalary, Is.EqualTo(result.GrossEarnings - result.TotalDeductions));
        }

        [Test]
        public async Task Workings_ShowTheRegime_TheProjection_AndTheFallbackFlag()
        {
            SeedEarlierPayslips(4, 7, 2026, 150_000m, 11_000m);

            var result = await Compute();

            using var doc = JsonDocument.Parse(result.TdsWorkingsJson!);
            var w = doc.RootElement;
            Assert.That(w.GetProperty("financialYear").GetString(), Is.EqualTo("2026-27"));
            Assert.That(w.GetProperty("regime").GetString(), Is.EqualTo("NEW"));
            Assert.That(w.GetProperty("projectedAnnualGross").GetDecimal(), Is.EqualTo(1_800_000m));
            Assert.That(w.GetProperty("taxableIncome").GetDecimal(), Is.EqualTo(1_725_000m));
            Assert.That(w.GetProperty("annualTax").GetDecimal(), Is.EqualTo(150_800m));
            Assert.That(w.GetProperty("tdsDeductedEarlierThisYear").GetDecimal(), Is.EqualTo(44_000m));
            Assert.That(w.GetProperty("monthsRemaining").GetInt32(), Is.EqualTo(8));
            Assert.That(w.GetProperty("rulesFallback").GetBoolean(), Is.True, "FY 2026-27 has no rules of its own yet, so the payslip must say the 2025-26 ones were used");
        }

        [Test]
        public async Task OverPaidSoFar_NothingMoreIsDeducted_NeverNegative()
        {
            SeedEarlierPayslips(4, 7, 2026, 150_000m, 60_000m);   // already deducted 2,40,000 > the 1,50,800 annual tax

            var result = await Compute();

            Assert.That(result.TdsDeducted, Is.EqualTo(0m));
        }

        [Test]
        public async Task OldRegime_WithDeclaredDeductions_LowersTds()
        {
            SeedEarlierPayslips(4, 7, 2026, 150_000m, 0m);
            var newRegime = (await Compute()).TdsDeducted;

            _structure.TaxRegime = "OLD";
            _structure.AnnualDeclaredDeductions = 150_000m;
            _context.SaveChanges();
            var old = await Compute();

            // old regime, 18L: 18,00,000 - 50,000 - 1,50,000 - 2,400 (PT x12) = 15,97,600 -> 12,500 + 5L x 20% (to 10L)... = 1,12,500 + 5,97,600 x 30% = 1,79,280 -> 2,91,780 + 4% = 3,03,451
            using var doc = JsonDocument.Parse(old.TdsWorkingsJson!);
            Assert.That(doc.RootElement.GetProperty("regime").GetString(), Is.EqualTo("OLD"));
            Assert.That(doc.RootElement.GetProperty("taxableIncome").GetDecimal(), Is.EqualTo(1_597_600m));
            Assert.That(doc.RootElement.GetProperty("annualTax").GetDecimal(), Is.EqualTo(303_451m));
            Assert.That(old.TdsDeducted, Is.EqualTo(Math.Round(303_451m / 8m, 0, MidpointRounding.AwayFromZero)));
            Assert.That(newRegime, Is.EqualTo(Math.Round(150_800m / 8m, 0, MidpointRounding.AwayFromZero)));
        }

        [Test]
        public async Task PaysFromAnotherFinancialYear_AreNotCounted()
        {
            SeedEarlierPayslips(4, 7, 2025, 150_000m, 11_000m);     // FY 2025-26, not this one
            var result = await Compute();

            using var doc = JsonDocument.Parse(result.TdsWorkingsJson!);
            Assert.That(doc.RootElement.GetProperty("tdsDeductedEarlierThisYear").GetDecimal(), Is.EqualTo(0m));
            Assert.That(doc.RootElement.GetProperty("projectedAnnualGross").GetDecimal(), Is.EqualTo(1_200_000m));   // Aug-Mar only
        }

        [Test]
        public async Task TdsNeverTakesMoreThanTheTakeHomeAllows()
        {
            // a very high earner whose catch-up TDS would exceed gross minus other deductions
            _structure.BasicSalary = 50_000m; _structure.Hra = 0m; _structure.SpecialAllowance = 0m; _structure.MonthlyGrossCtc = 50_000m;
            _context.SaveChanges();
            SeedEarlierPayslips(4, 7, 2026, gross: 9_000_000m, tds: 0m);   // earlier months were huge, nothing deducted

            var result = await Compute();

            Assert.That(result.TdsDeducted, Is.LessThanOrEqualTo(result.GrossEarnings - result.PfEmployee - result.EsiEmployee - result.ProfTax));
            Assert.That(result.NetSalary, Is.GreaterThanOrEqualTo(0m));
        }

        // ---------------------------------------------------------------- the tax-profile handler

        private UpdateEmployeeTaxProfileHandler Handler() => new(_context);

        [Test]
        public async Task TaxProfile_SwitchToOld_StoresRegimeAndDeductions()
        {
            var r = await Handler().Handle(new UpdateEmployeeTaxProfileRequestModel
            {
                HospitalId = _employee.HospitalId, HrEmployeeId = _employee.HrEmployeeId, TaxRegime = "old", AnnualDeclaredDeductions = 120_000m,
            }, CancellationToken.None);

            Assert.That(r.Success, Is.True, r.Message);
            Assert.That(_context.HrSalaryStructure.Single().TaxRegime, Is.EqualTo("OLD"));
            Assert.That(_context.HrSalaryStructure.Single().AnnualDeclaredDeductions, Is.EqualTo(120_000m));
        }

        [Test]
        public async Task TaxProfile_BackToNew_ClearsStaleDeductions()
        {
            _structure.TaxRegime = "OLD"; _structure.AnnualDeclaredDeductions = 99_000m;
            _context.SaveChanges();

            var r = await Handler().Handle(new UpdateEmployeeTaxProfileRequestModel
            {
                HospitalId = _employee.HospitalId, HrEmployeeId = _employee.HrEmployeeId, TaxRegime = "NEW", AnnualDeclaredDeductions = 99_000m,
            }, CancellationToken.None);

            Assert.That(r.Success, Is.True);
            Assert.That(_context.HrSalaryStructure.Single().AnnualDeclaredDeductions, Is.EqualTo(0m));
        }

        [TestCase(null, 0)]
        [TestCase("MIXED", 0)]
        [TestCase("OLD", -1)]
        [TestCase("OLD", 20_000_000)]
        public async Task TaxProfile_RejectsBadInput(string? regime, int declared)
        {
            var r = await Handler().Handle(new UpdateEmployeeTaxProfileRequestModel
            {
                HospitalId = _employee.HospitalId, HrEmployeeId = _employee.HrEmployeeId, TaxRegime = regime, AnnualDeclaredDeductions = declared,
            }, CancellationToken.None);
            Assert.That(r.Success, Is.False);
            Assert.That(_context.HrSalaryStructure.Single().TaxRegime, Is.EqualTo("NEW"));
        }

        [Test]
        public async Task TaxProfile_AnEmployeeOfAnotherHospital_IsNotFound()
        {
            var r = await Handler().Handle(new UpdateEmployeeTaxProfileRequestModel
            {
                HospitalId = Guid.NewGuid(), HrEmployeeId = _employee.HrEmployeeId, TaxRegime = "OLD", AnnualDeclaredDeductions = 1,
            }, CancellationToken.None);
            Assert.That(r.Success, Is.False);
            Assert.That(r.Message, Does.Contain("not found"));
            Assert.That(_context.HrSalaryStructure.Single().TaxRegime, Is.EqualTo("NEW"));
        }
    }
}
