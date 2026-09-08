using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.Services;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using EasyHMSAPI.UnitTests.TestUtils;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.CommandHandlerTests
{
    /// <summary>
    /// Core unit tests for the Dual-Track Payroll computation engine.
    ///
    /// Track A (SalariedPayrollStrategy):
    ///   - PF deduction at 12% of Basic
    ///   - ESIC deduction at 0.75% of Gross (only if Gross ≤ ₹21,000)
    ///   - Night shift allowance = NightShiftCount × Rate
    ///   - Overtime at 1.5× hourly rate
    ///   - Net = Gross - Total Deductions
    ///
    /// Track B (ConsultantPayrollStrategy):
    ///   - TDS Section 194J at flat 10% of gross professional fees
    ///   - Admin surcharge deducted
    ///   - No PF / ESIC / PT applicable
    ///
    /// Test coverage: 20 assertions across 8 test cases.
    /// </summary>
    [TestFixture]
    public class RunMonthlyPayrollHandlerTests
    {
        private AppDbContext _context = null!;
        private HrEmployee _salariedEmployee = null!;
        private HrEmployee _consultantEmployee = null!;
        private PayrollPeriod _august2026 = null!;

        // ─── Standard CTC for test salaried employee ──────────────────────────
        private const decimal TestBasic = 17_000m;
        private const decimal TestHra = 6_800m;
        private const decimal TestSpecialAllowance = 8_000m;
        private const decimal TestMedicalAllowance = 1_500m;
        private const decimal TestMonthlyGross = 38_500m;
        private const decimal TestNightAllowanceRate = 350m;

        // ─── Standard fee config for test consultant ──────────────────────────
        private const decimal TestRetainer = 50_000m;
        private const decimal TestAdminSurcharge = 5_000m;

        [SetUp]
        public async Task SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _august2026 = new PayrollPeriod(Month: 8, Year: 2026);

            var hospitalId = Guid.NewGuid();
            var deptId = Guid.NewGuid();

            // ─── Salaried employee ────────────────────────────────────────────
            _salariedEmployee = new HrEmployee
            {
                HrEmployeeId = Guid.NewGuid(),
                HospitalId = hospitalId,
                EmployeeCode = "EMP-2023-0015",
                FirstName = "Anjali",
                LastName = "Mishra",
                Gender = "Female",
                DateOfBirth = new DateOnly(1995, 11, 5),
                ContactNumber = "+91-9833456789",
                EmploymentType = "FULL_TIME_SALARIED",
                DepartmentId = deptId,
                Designation = "Staff Nurse — ICU",
                DateOfJoining = new DateOnly(2023, 6, 1),
                PanNumber = "CDPAM7890N",
                PayrollTrack = "TRACK_A_SALARIED",
                IsActive = true,
                Status = "ACTIVE",
            };

            // ─── Salary structure for salaried employee ────────────────────────
            var salaryStructure = new HrSalaryStructure
            {
                HrSalaryStructureId = Guid.NewGuid(),
                HrEmployeeId = _salariedEmployee.HrEmployeeId,
                EffectiveFrom = new DateOnly(2023, 6, 1),
                MonthlyGrossCtc = TestMonthlyGross,
                BasicSalary = TestBasic,
                Hra = TestHra,
                DearnessAllowance = 1_700m,
                SpecialAllowance = TestSpecialAllowance,
                MedicalAllowance = TestMedicalAllowance,
                NightShiftAllowanceRate = TestNightAllowanceRate,
                IsPfEligible = true,
                IsEsiEligible = true,  // Gross is 38,500 — but for test we enable
                ProfessionalTax = 200m,
                IsActive = true,
            };

            // ─── Consultant employee ──────────────────────────────────────────
            _consultantEmployee = new HrEmployee
            {
                HrEmployeeId = Guid.NewGuid(),
                HospitalId = hospitalId,
                EmployeeCode = "EMP-2024-0001",
                FirstName = "Rajesh",
                LastName = "Sharma",
                Gender = "Male",
                DateOfBirth = new DateOnly(1975, 4, 12),
                ContactNumber = "+91-9811234567",
                EmploymentType = "VISITING_CONSULTANT",
                DepartmentId = deptId,
                Designation = "Senior Laparoscopic Surgeon",
                DateOfJoining = new DateOnly(2024, 1, 15),
                PanNumber = "ABDRS1234K",
                PayrollTrack = "TRACK_B_CONSULTANT",
                IsActive = true,
                Status = "ACTIVE",
            };

            // ─── Fee config for consultant ────────────────────────────────────
            var feeConfig = new HrConsultantFeeConfig
            {
                HrConsultantFeeConfigId = Guid.NewGuid(),
                HrEmployeeId = _consultantEmployee.HrEmployeeId,
                EffectiveFrom = new DateOnly(2024, 1, 15),
                MonthlyRetainer = TestRetainer,
                OpdSharePercent = 60m,
                IpdVisitFee = 500m,
                AdminSurcharge = TestAdminSurcharge,
                IsActive = true,
            };

            _context.Set<HrEmployee>().AddRange(_salariedEmployee, _consultantEmployee);
            _context.Set<HrSalaryStructure>().Add(salaryStructure);
            _context.Set<HrConsultantFeeConfig>().Add(feeConfig);
            await _context.SaveChangesAsync();
        }

        [TearDown]
        public void TearDown()
        {
            _context.Database.EnsureDeleted();
            _context.Dispose();
        }

        // ─── Helper: seed full-month attendance ───────────────────────────────

        private async Task SeedFullMonthAttendance(Guid employeeId, int nightShiftsCompleted = 0, decimal extraOvertimeHours = 0m)
        {
            var logs = new List<HrAttendanceLog>();
            // Seed 31 PRESENT days for August 2026
            for (int day = 1; day <= 31; day++)
            {
                logs.Add(new HrAttendanceLog
                {
                    HrAttendanceLogId = Guid.NewGuid(),
                    HrEmployeeId = employeeId,
                    AttendanceDate = new DateOnly(2026, 8, day),
                    PunchIn = new DateTime(2026, 8, day, 8, 0, 0, DateTimeKind.Utc),
                    PunchOut = new DateTime(2026, 8, day, 14, 0, 0, DateTimeKind.Utc),
                    TotalHoursWorked = 6m,
                    OvertimeHours = day == 1 ? extraOvertimeHours : 0m,
                    Status = "PRESENT",
                    PunchSource = "BIOMETRIC",
                });
            }

            // Seed completed night shift roster entries
            for (int i = 0; i < nightShiftsCompleted; i++)
            {
                var shiftId = Guid.NewGuid();
                _context.Set<HrHospitalShift>().Add(new HrHospitalShift
                {
                    HrHospitalShiftId = shiftId,
                    HospitalId = Guid.NewGuid(),
                    ShiftCode = "SFT_N",
                    ShiftName = "Night Shift",
                    StartTime = new TimeOnly(20, 0),
                    EndTime = new TimeOnly(8, 0),
                    NightAllowanceAmount = 350m,
                    IsActive = true,
                });

                _context.Set<HrDutyRoster>().Add(new HrDutyRoster
                {
                    HrDutyRosterId = Guid.NewGuid(),
                    HospitalId = Guid.NewGuid(),
                    HrEmployeeId = employeeId,
                    HrHospitalShiftId = shiftId,
                    RosterDate = new DateOnly(2026, 8, i + 1),
                    Status = "COMPLETED",
                });
            }

            _context.Set<HrAttendanceLog>().AddRange(logs);
            await _context.SaveChangesAsync();
        }

        // ═══════════════════════════════════════════════════════════════════════
        // TRACK A TESTS — SalariedPayrollStrategy
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task TrackA_PfDeductedAt12PercentOfBasicEarned()
        {
            // Arrange: full month present (31 days), basic = ₹17,000 -- deliberately above the
            // ₹15,000 EPF wage cap, so this also proves the cap is applied (SalariedPayrollStrategy's
            // documented rule: PF is capped at ₹1,800/mo once Basic > ₹15,000).
            await SeedFullMonthAttendance(_salariedEmployee.HrEmployeeId);
            var strategy = new SalariedPayrollStrategy(_context);

            // Act
            var result = await strategy.ComputeAsync(_salariedEmployee, _august2026, CancellationToken.None);

            // Assert: PF = 12% of BasicEarned, capped at a ₹15,000 wage base once configured
            // Basic exceeds it -- mirrors the cap logic in SalariedPayrollStrategy.ComputeAsync.
            decimal pfWageBase = Math.Min(result.BasicEarned, TestBasic > 15_000m ? 15_000m : result.BasicEarned);
            decimal expectedPf = Math.Round(pfWageBase * 0.12m, 2);
            Assert.That(result.PfEmployee, Is.EqualTo(expectedPf),
                $"PF should be 12% of the (possibly capped) PF wage base (₹{pfWageBase}). Expected ₹{expectedPf}, got ₹{result.PfEmployee}");
        }

        [Test]
        public async Task TrackA_EsiDeductedAt075PercentWhenEsiEligible()
        {
            // Arrange: a distinct, lower-gross employee whose Gross stays within the ESIC wage
            // ceiling (₹21,000/mo) -- the shared _salariedEmployee's gross (~₹35,000) is
            // deliberately above the ceiling, so ESI must NOT be deducted for them regardless of
            // IsEsiEligible (see SalariedPayrollStrategy.ComputeAsync's ceiling check).
            var esiEmployee = new HrEmployee
            {
                HrEmployeeId = Guid.NewGuid(),
                HospitalId = _salariedEmployee.HospitalId,
                EmployeeCode = "EMP-2023-0099",
                FirstName = "Kavita",
                LastName = "Rao",
                Gender = "Female",
                DateOfBirth = new DateOnly(1998, 3, 20),
                ContactNumber = "+91-9822233344",
                EmploymentType = "FULL_TIME_SALARIED",
                DepartmentId = _salariedEmployee.DepartmentId,
                Designation = "Ward Attendant",
                DateOfJoining = new DateOnly(2023, 6, 1),
                PanNumber = "EFGHI5678L",
                PayrollTrack = "TRACK_A_SALARIED",
                IsActive = true,
                Status = "ACTIVE",
            };
            var esiSalaryStructure = new HrSalaryStructure
            {
                HrSalaryStructureId = Guid.NewGuid(),
                HrEmployeeId = esiEmployee.HrEmployeeId,
                EffectiveFrom = new DateOnly(2023, 6, 1),
                MonthlyGrossCtc = 20_000m,
                BasicSalary = 12_000m,
                Hra = 4_000m,
                DearnessAllowance = 1_000m,
                SpecialAllowance = 2_000m,
                MedicalAllowance = 1_000m,
                IsPfEligible = true,
                IsEsiEligible = true,
                ProfessionalTax = 200m,
                IsActive = true,
            };
            _context.Set<HrEmployee>().Add(esiEmployee);
            _context.Set<HrSalaryStructure>().Add(esiSalaryStructure);
            await _context.SaveChangesAsync();
            await SeedFullMonthAttendance(esiEmployee.HrEmployeeId);
            var strategy = new SalariedPayrollStrategy(_context);

            // Act
            var result = await strategy.ComputeAsync(esiEmployee, _august2026, CancellationToken.None);

            // Assert: ESIC = 0.75% of Gross, since Gross (₹20,000) is within the ₹21,000 ceiling
            // and IsEsiEligible = true.
            Assert.That(result.GrossEarnings, Is.LessThanOrEqualTo(21_000m), "Test fixture must stay within the ESIC ceiling to exercise the eligible path.");
            decimal expectedEsi = Math.Round(result.GrossEarnings * 0.0075m, 2);
            Assert.That(result.EsiEmployee, Is.EqualTo(expectedEsi),
                $"ESIC employee should be 0.75% of Gross (₹{result.GrossEarnings}). Expected ₹{expectedEsi}, got ₹{result.EsiEmployee}");
        }

        [Test]
        public async Task TrackA_NightAllowanceAddedPerCompletedNightShift()
        {
            // Arrange: 12 completed night shifts this month
            const int nightShifts = 12;
            await SeedFullMonthAttendance(_salariedEmployee.HrEmployeeId, nightShiftsCompleted: nightShifts);
            var strategy = new SalariedPayrollStrategy(_context);

            // Act
            var result = await strategy.ComputeAsync(_salariedEmployee, _august2026, CancellationToken.None);

            // Assert: NightAllowance = 12 shifts × ₹350/shift = ₹4,200
            decimal expectedNightAllowance = nightShifts * TestNightAllowanceRate;
            Assert.That(result.NightAllowanceAmount, Is.EqualTo(expectedNightAllowance),
                $"Night allowance should be {nightShifts} × ₹{TestNightAllowanceRate} = ₹{expectedNightAllowance}");
            Assert.That(result.NightShiftCount, Is.EqualTo(nightShifts));
        }

        [Test]
        public async Task TrackA_OvertimeComputedAt1Point5xHourlyRate()
        {
            // Arrange: 4 overtime hours on day 1
            const decimal extraOtHours = 4m;
            await SeedFullMonthAttendance(_salariedEmployee.HrEmployeeId, extraOvertimeHours: extraOtHours);
            var strategy = new SalariedPayrollStrategy(_context);

            // Act
            var result = await strategy.ComputeAsync(_salariedEmployee, _august2026, CancellationToken.None);

            // Assert: OT = (Basic / (31 days × 8h)) × 1.5 × 4h
            decimal hourlyRate = TestBasic / (31m * 8m);
            decimal expectedOt = Math.Round(hourlyRate * 1.5m * extraOtHours, 2);
            Assert.That(result.OvertimeAmount, Is.EqualTo(expectedOt),
                $"Overtime should be at 1.5× hourly rate. Expected ₹{expectedOt}, got ₹{result.OvertimeAmount}");
        }

        [Test]
        public async Task TrackA_ProRatedPayForPartialMonth()
        {
            // Arrange: Only 15 days worked out of 31
            for (int day = 1; day <= 15; day++)
            {
                _context.Set<HrAttendanceLog>().Add(new HrAttendanceLog
                {
                    HrAttendanceLogId = Guid.NewGuid(),
                    HrEmployeeId = _salariedEmployee.HrEmployeeId,
                    AttendanceDate = new DateOnly(2026, 8, day),
                    Status = "PRESENT",
                    PunchSource = "BIOMETRIC",
                });
            }
            await _context.SaveChangesAsync();
            var strategy = new SalariedPayrollStrategy(_context);

            // Act
            var result = await strategy.ComputeAsync(_salariedEmployee, _august2026, CancellationToken.None);

            // Assert: Basic earned = 17000 × (15/31) = 8,225.81
            decimal expectedBasic = Math.Round(TestBasic * (15m / 31m), 2);
            Assert.That(result.BasicEarned, Is.EqualTo(expectedBasic),
                $"Basic should be pro-rated: ₹{TestBasic} × (15/31) = ₹{expectedBasic}");
            Assert.That(result.PayableDays, Is.EqualTo(15m));
        }

        [Test]
        public async Task TrackA_NetSalaryEqualsGrossMinusTotalDeductions()
        {
            // Arrange: full month
            await SeedFullMonthAttendance(_salariedEmployee.HrEmployeeId);
            var strategy = new SalariedPayrollStrategy(_context);

            // Act
            var result = await strategy.ComputeAsync(_salariedEmployee, _august2026, CancellationToken.None);

            // Assert: fundamental payroll identity
            decimal expectedNet = Math.Round(result.GrossEarnings - result.TotalDeductions, 2);
            Assert.That(result.NetSalary, Is.EqualTo(expectedNet),
                "Net Salary must equal Gross Earnings minus Total Deductions");
            Assert.That(result.TotalDeductions,
                Is.EqualTo(result.PfEmployee + result.EsiEmployee + result.ProfTax + result.TdsDeducted + result.LoanInstallment),
                "Total Deductions must equal sum of all deduction components");
        }

        [Test]
        public async Task TrackA_HalfDayCountsAs05PayableDays()
        {
            // Arrange: 20 full days + 2 half-days = 21 payable days
            for (int day = 1; day <= 20; day++)
            {
                _context.Set<HrAttendanceLog>().Add(new HrAttendanceLog
                {
                    HrAttendanceLogId = Guid.NewGuid(),
                    HrEmployeeId = _salariedEmployee.HrEmployeeId,
                    AttendanceDate = new DateOnly(2026, 8, day),
                    Status = "PRESENT",
                    PunchSource = "BIOMETRIC",
                });
            }
            // 2 half-days
            for (int day = 21; day <= 22; day++)
            {
                _context.Set<HrAttendanceLog>().Add(new HrAttendanceLog
                {
                    HrAttendanceLogId = Guid.NewGuid(),
                    HrEmployeeId = _salariedEmployee.HrEmployeeId,
                    AttendanceDate = new DateOnly(2026, 8, day),
                    Status = "HALF_DAY",
                    PunchSource = "BIOMETRIC",
                });
            }
            await _context.SaveChangesAsync();
            var strategy = new SalariedPayrollStrategy(_context);

            // Act
            var result = await strategy.ComputeAsync(_salariedEmployee, _august2026, CancellationToken.None);

            // Assert: 20 full + 2 × 0.5 = 21 payable days
            Assert.That(result.PayableDays, Is.EqualTo(21m),
                "2 half-days should each count as 0.5 payable days");
        }

        // ═══════════════════════════════════════════════════════════════════════
        // TRACK B TESTS — ConsultantPayrollStrategy
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task TrackB_TdsAt10PercentOf194J_OnRetainerOnly()
        {
            // Arrange: Consultant with only retainer (no OPD/IPD/Surgery ledger entries)
            var strategy = new ConsultantPayrollStrategy(_context);

            // Act
            var result = await strategy.ComputeAsync(_consultantEmployee, _august2026, CancellationToken.None);

            // Assert: TDS = 10% of retainer (gross fees = retainer only, no ledger entries)
            decimal expectedTds = Math.Round(TestRetainer * 0.10m, 2);
            Assert.That(result.TdsDeducted, Is.EqualTo(expectedTds),
                $"TDS 194J should be 10% of Gross. Gross = ₹{result.GrossEarnings}, Expected TDS = ₹{expectedTds}");
        }

        [Test]
        public async Task TrackB_NoPfNoEsiNoProfTaxForConsultant()
        {
            // Arrange: Visiting consultant
            var strategy = new ConsultantPayrollStrategy(_context);

            // Act
            var result = await strategy.ComputeAsync(_consultantEmployee, _august2026, CancellationToken.None);

            // Assert: PF, ESIC, and PT are all zero for consultants
            Assert.That(result.PfEmployee, Is.EqualTo(0m), "Visiting consultants are not liable for EPF");
            Assert.That(result.EsiEmployee, Is.EqualTo(0m), "Visiting consultants are not liable for ESIC");
            Assert.That(result.ProfTax, Is.EqualTo(0m), "Visiting consultants are not liable for Professional Tax");
        }

        [Test]
        public async Task TrackB_AdminSurchargeDeductedFromGross()
        {
            // Arrange
            var strategy = new ConsultantPayrollStrategy(_context);

            // Act
            var result = await strategy.ComputeAsync(_consultantEmployee, _august2026, CancellationToken.None);

            // Assert: Net = GrossFees - TDS - AdminSurcharge
            decimal expectedNet = Math.Round(result.GrossEarnings - result.TdsDeducted - TestAdminSurcharge, 2);
            Assert.That(result.NetSalary, Is.EqualTo(expectedNet),
                $"Net payable should be Gross - TDS194J - AdminSurcharge. Expected ₹{expectedNet}, got ₹{result.NetSalary}");
        }

        [Test]
        public async Task TrackB_RetainerIsAlwaysIncludedInGross()
        {
            // Arrange: Consultant with no clinical activity (only retainer due)
            var strategy = new ConsultantPayrollStrategy(_context);

            // Act
            var result = await strategy.ComputeAsync(_consultantEmployee, _august2026, CancellationToken.None);

            // Assert: Retainer must always be included regardless of clinical activity
            Assert.That(result.RetainerAmount, Is.EqualTo(TestRetainer),
                "Monthly retainer guarantee must always be in gross regardless of clinical volume");
            Assert.That(result.GrossEarnings, Is.GreaterThanOrEqualTo(TestRetainer),
                "Gross earnings must always be at least the retainer amount");
        }

        [Test]
        public async Task TrackB_WithSurgeryLedger_GrossIncludesSurgeryShare()
        {
            // Arrange: Add ConsultantIncentiveLedger entries for 2 surgeries
            var surgeryIncentives = new decimal[] { 15_000m, 12_000m };  // ₹27,000 total
            foreach (var amount in surgeryIncentives)
            {
                _context.ConsultantIncentiveLedger.Add(new ConsultantIncentiveLedger
                {
                    ConsultantIncentiveLedgerId = Guid.NewGuid(),
                    HospitalId = Guid.NewGuid(),
                    DoctorId = _consultantEmployee.HrEmployeeId,
                    PatientId = Guid.NewGuid().ToString(),
                    ChargeEventId = Guid.NewGuid(),
                    IncentiveAmount = amount,
                    StatusCode = "ACCRUED",
                    AccruedAt = new DateTime(2026, 8, 15, 10, 0, 0, DateTimeKind.Utc),
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                });
            }
            await _context.SaveChangesAsync();

            var strategy = new ConsultantPayrollStrategy(_context);

            // Act
            var result = await strategy.ComputeAsync(_consultantEmployee, _august2026, CancellationToken.None);

            // Assert: Surgery share = ₹27,000 pulled from ledger
            decimal expectedSurgeryShare = surgeryIncentives.Sum();
            Assert.That(result.SurgeryShareAmount, Is.EqualTo(expectedSurgeryShare),
                $"Surgery share should be pulled from ConsultantIncentiveLedger. Expected ₹{expectedSurgeryShare}");

            // And TDS should be 10% of the larger gross (including surgery)
            decimal expectedGross = TestRetainer + expectedSurgeryShare;
            decimal expectedTds = Math.Round(expectedGross * 0.10m, 2);
            Assert.That(result.TdsDeducted, Is.EqualTo(expectedTds),
                $"TDS 194J should be 10% of gross including surgery share. Expected ₹{expectedTds}");
        }
    }
}
