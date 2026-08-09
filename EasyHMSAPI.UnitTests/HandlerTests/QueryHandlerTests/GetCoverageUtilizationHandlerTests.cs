using System;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.Handlers.QueryHandlers;
using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using EasyHMSAPI.UnitTests.TestUtils;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.QueryHandlerTests
{
    [TestFixture]
    public class GetCoverageUtilizationHandlerTests
    {
        private AppDbContext _context = null!;
        private GetCoverageUtilizationHandler _handler = null!;
        private Guid _hospitalId;
        private Guid _admissionId;
        private Guid _encounterId;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _handler = new GetCoverageUtilizationHandler(_context);
            _hospitalId = Guid.NewGuid();
            _admissionId = Guid.NewGuid();
            _encounterId = Guid.NewGuid();
        }

        [TearDown]
        public void TearDown()
        {
            _context.Database.EnsureDeleted();
            _context.Dispose();
        }

        private void SeedAdmissionWithCoverage(decimal sanctionedAmount)
        {
            _context.Admission.Add(new Admission
            {
                AdmissionId = _admissionId,
                HospitalId = _hospitalId,
                PatientId = "PT001",
                AdmissionNo = "ADM-1",
                EncounterId = _encounterId,
                AdmittedAt = DateTime.UtcNow,
                StatusCode = "ADMITTED",
                PayerType = "TPA",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            });
            _context.AdmissionCoverage.Add(new AdmissionCoverage
            {
                CoverageId = Guid.NewGuid(),
                HospitalId = _hospitalId,
                AdmissionId = _admissionId,
                PayerType = "TPA",
                SanctionedAmount = sanctionedAmount,
                StatusCode = "APPROVED",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            });
        }

        private void SeedCharge(decimal netAmount, Guid? chargeId, string statusCode = "POSTED")
        {
            _context.BillingChargeEvent.Add(new BillingChargeEvent
            {
                ChargeEventId = Guid.NewGuid(),
                HospitalId = _hospitalId,
                PatientId = "PT001",
                EncounterId = _encounterId,
                ChargeId = chargeId,
                DisplayName = "Charge",
                Qty = 1,
                UnitPrice = netAmount,
                GrossAmount = netAmount,
                NetAmount = netAmount,
                StatusCode = statusCode,
                ServiceDate = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            });
        }

        [Test]
        public async Task Handle_ExcludesChargesMarkedNonPayable_FromRunningTotal()
        {
            SeedAdmissionWithCoverage(10000);
            var nonPayableCharge = new ChargeMaster
            {
                ChargeId = Guid.NewGuid(),
                HospitalId = _hospitalId,
                DisplayName = "Cosmetic item",
                DefaultRate = 0,
                IsIRDAIPayable = false,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };
            var payableCharge = new ChargeMaster
            {
                ChargeId = Guid.NewGuid(),
                HospitalId = _hospitalId,
                DisplayName = "Consult",
                DefaultRate = 0,
                IsIRDAIPayable = true,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };
            _context.ChargeMaster.AddRange(nonPayableCharge, payableCharge);
            SeedCharge(3000, nonPayableCharge.ChargeId);
            SeedCharge(2000, payableCharge.ChargeId);
            await _context.SaveChangesAsync();

            var response = await _handler.Handle(new GetCoverageUtilizationRequestModel { HospitalId = _hospitalId, AdmissionId = _admissionId }, CancellationToken.None);

            Assert.That(response.Success, Is.True);
            Assert.That(response.RunningTotal, Is.EqualTo(2000m));
        }

        [Test]
        public async Task Handle_UnclassifiedCharges_StayIncludedInRunningTotal()
        {
            SeedAdmissionWithCoverage(10000);
            // No ChargeId link at all — should stay conservatively included, not excluded.
            SeedCharge(1500, null);
            await _context.SaveChangesAsync();

            var response = await _handler.Handle(new GetCoverageUtilizationRequestModel { HospitalId = _hospitalId, AdmissionId = _admissionId }, CancellationToken.None);

            Assert.That(response.Success, Is.True);
            Assert.That(response.RunningTotal, Is.EqualTo(1500m));
        }

        [Test]
        public async Task Handle_VoidCharges_NeverCountTowardRunningTotal()
        {
            SeedAdmissionWithCoverage(10000);
            SeedCharge(5000, null, statusCode: "VOID");
            await _context.SaveChangesAsync();

            var response = await _handler.Handle(new GetCoverageUtilizationRequestModel { HospitalId = _hospitalId, AdmissionId = _admissionId }, CancellationToken.None);

            Assert.That(response.Success, Is.True);
            Assert.That(response.RunningTotal, Is.EqualTo(0m));
        }
    }
}
