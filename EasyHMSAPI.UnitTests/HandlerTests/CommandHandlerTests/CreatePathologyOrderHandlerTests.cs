using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.Handlers.CommandHandlers;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using EasyHMSAPI.UnitTests.TestUtils;
using MediatR;
using Moq;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.CommandHandlerTests
{
    // Covers a regression found while wiring up the Test Catalog Manager's charge-linkage UI:
    // the auto-bill block here used `.Include(t => t.ChargeId)` on a scalar Guid? property, which
    // throws InvalidOperationException at runtime (Include only works on navigation properties),
    // and hardcoded `Rate = 0` with a comment claiming ChargeMaster would resolve it later -- it
    // never did, so every auto-billed lab charge would have posted at zero. Never caught before
    // because no test existed for this handler and "auto-bill on order" had no charge-linked test
    // to actually exercise the code path in practice.
    [TestFixture]
    public class CreatePathologyOrderHandlerTests
    {
        private AppDbContext _context = null!;
        private Mock<IMediator> _mediatorMock = null!;
        private CreatePathologyOrderHandler _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _mediatorMock = new Mock<IMediator>();
            _mediatorMock
                .Setup(m => m.Send(It.IsAny<AddChargeEventRequestModel>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new AddChargeEventResponseModel { Success = true });
            _handler = new CreatePathologyOrderHandler(_context, _mediatorMock.Object);
        }

        [TearDown]
        public void TearDown()
        {
            InMemoryDbContextFactory.Destroy(_context);
            _context?.Dispose();
        }

        private (Guid HospitalId, Guid TestId, Guid ChargeId) SeedAutoBillCatalog(decimal defaultRate = 250m)
        {
            var hospitalId = Guid.NewGuid();
            var chargeId = Guid.NewGuid();
            var testId = Guid.NewGuid();

            _context.BillingPolicy.Add(new BillingPolicy { HospitalId = hospitalId, LabPathTrigger = "ON_ORDER" });
            _context.ChargeMaster.Add(new ChargeMaster
            {
                ChargeId = chargeId,
                HospitalId = hospitalId,
                DisplayName = "Complete Blood Count",
                DefaultRate = defaultRate,
                IsActive = true,
            });
            _context.PathologyTestMaster.Add(new PathologyTestMaster
            {
                TestId = testId,
                HospitalId = hospitalId,
                TestCode = "HEM-CBC",
                TestName = "Complete Blood Count (CBC)",
                ChargeId = chargeId,
                IsActive = true,
            });
            _context.SaveChanges();

            return (hospitalId, testId, chargeId);
        }

        [Test]
        public async Task Handle_AutoBillOnOrderWithLinkedCharge_PostsChargeAtChargeMasterRate()
        {
            var (hospitalId, testId, chargeId) = SeedAutoBillCatalog(defaultRate: 250m);
            var encounterId = Guid.NewGuid();

            var response = await _handler.Handle(new CreatePathologyOrderRequestModel
            {
                HospitalId = hospitalId,
                PatientId = "PTID00000001",
                EncounterId = encounterId,
                TestIds = new() { testId },
                LoggedInUserName = "tester",
            }, CancellationToken.None);

            Assert.That(response.Success, Is.True, response.Message);
            _mediatorMock.Verify(m => m.Send(
                It.Is<AddChargeEventRequestModel>(r =>
                    r.PatientId == "PTID00000001" &&
                    r.EncounterId == encounterId &&
                    r.Charges.Count == 1 &&
                    r.Charges.Single().ChargeId == chargeId &&
                    r.Charges.Single().Rate == 250m &&
                    // Regression: AddChargeEventHandler writes this straight onto BillingChargeEvent
                    // with no ChargeMaster fallback -- a null DisplayName made every real (unmocked)
                    // auto-bill call fail silently, invisibly, since nothing here ever checked the
                    // mediator response. This mock can't catch that failure mode itself (it stubs
                    // AddChargeEventHandler away entirely), only that the field is populated at all.
                    !string.IsNullOrEmpty(r.Charges.Single().DisplayName)),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Test]
        public async Task Handle_AutoBillOnOrderWithNoLinkedCharge_DoesNotPostAnyCharge()
        {
            var hospitalId = Guid.NewGuid();
            var testId = Guid.NewGuid();
            _context.BillingPolicy.Add(new BillingPolicy { HospitalId = hospitalId, LabPathTrigger = "ON_ORDER" });
            _context.PathologyTestMaster.Add(new PathologyTestMaster
            {
                TestId = testId,
                HospitalId = hospitalId,
                TestCode = "HEM-ESR",
                TestName = "ESR",
                ChargeId = null,
                IsActive = true,
            });
            _context.SaveChanges();

            var response = await _handler.Handle(new CreatePathologyOrderRequestModel
            {
                HospitalId = hospitalId,
                PatientId = "PTID00000001",
                EncounterId = Guid.NewGuid(),
                TestIds = new() { testId },
                LoggedInUserName = "tester",
            }, CancellationToken.None);

            Assert.That(response.Success, Is.True, response.Message);
            _mediatorMock.Verify(m => m.Send(It.IsAny<AddChargeEventRequestModel>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Test]
        public async Task Handle_AutoBillDisabled_DoesNotPostChargeEvenWithLinkedCharge()
        {
            var (hospitalId, testId, _) = SeedAutoBillCatalog();
            var policy = _context.BillingPolicy.Single(p => p.HospitalId == hospitalId);
            policy.LabPathTrigger = "ON_REPORT_APPROVAL";
            _context.SaveChanges();

            var response = await _handler.Handle(new CreatePathologyOrderRequestModel
            {
                HospitalId = hospitalId,
                PatientId = "PTID00000001",
                EncounterId = Guid.NewGuid(),
                TestIds = new() { testId },
                LoggedInUserName = "tester",
            }, CancellationToken.None);

            Assert.That(response.Success, Is.True, response.Message);
            _mediatorMock.Verify(m => m.Send(It.IsAny<AddChargeEventRequestModel>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Test]
        public async Task Handle_IpdOrderWithNoEncounterId_SkipsBillingWithoutError()
        {
            var (hospitalId, testId, _) = SeedAutoBillCatalog();

            var response = await _handler.Handle(new CreatePathologyOrderRequestModel
            {
                HospitalId = hospitalId,
                PatientId = "PTID00000001",
                AdmissionId = Guid.NewGuid(),
                EncounterId = null,
                TestIds = new() { testId },
                LoggedInUserName = "tester",
            }, CancellationToken.None);

            Assert.That(response.Success, Is.True, response.Message);
            _mediatorMock.Verify(m => m.Send(It.IsAny<AddChargeEventRequestModel>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Test]
        public async Task Handle_IpdOrderWithResolvableAdmissionEncounter_PostsChargeAgainstAdmissionEncounter()
        {
            var (hospitalId, testId, chargeId) = SeedAutoBillCatalog(defaultRate: 300m);
            var encounterId = Guid.NewGuid();
            var admissionId = Guid.NewGuid();
            _context.Admission.Add(new Admission
            {
                AdmissionId = admissionId,
                HospitalId = hospitalId,
                PatientId = "PTID00000001",
                EncounterId = encounterId,
                AdmissionNo = "ADM-1",
                AdmittedAt = DateTime.UtcNow,
            });
            _context.SaveChanges();

            var response = await _handler.Handle(new CreatePathologyOrderRequestModel
            {
                HospitalId = hospitalId,
                PatientId = "PTID00000001",
                AdmissionId = admissionId,
                EncounterId = null,
                TestIds = new() { testId },
                LoggedInUserName = "tester",
            }, CancellationToken.None);

            Assert.That(response.Success, Is.True, response.Message);
            _mediatorMock.Verify(m => m.Send(
                It.Is<AddChargeEventRequestModel>(r =>
                    r.EncounterId == encounterId &&
                    r.Charges.Count == 1 &&
                    r.Charges.Single().ChargeId == chargeId),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Test]
        public async Task Handle_NoSourceTypeGiven_DefaultsToOpd()
        {
            var (hospitalId, testId, _) = SeedAutoBillCatalog();

            var response = await _handler.Handle(new CreatePathologyOrderRequestModel
            {
                HospitalId = hospitalId,
                PatientId = "PTID00000001",
                EncounterId = Guid.NewGuid(),
                TestIds = new() { testId },
                LoggedInUserName = "tester",
            }, CancellationToken.None);

            Assert.That(response.Success, Is.True, response.Message);
            var saved = _context.PathologyOrder.Single(o => o.OrderId == response.OrderId);
            Assert.That(saved.SourceType, Is.EqualTo("OPD"));
            Assert.That(saved.IsStat, Is.False);
        }

        [Test]
        public async Task Handle_ExplicitSourceTypeAndStat_PersistsAsGiven()
        {
            var (hospitalId, testId, _) = SeedAutoBillCatalog();

            var response = await _handler.Handle(new CreatePathologyOrderRequestModel
            {
                HospitalId = hospitalId,
                PatientId = "PTID00000001",
                EncounterId = Guid.NewGuid(),
                TestIds = new() { testId },
                LoggedInUserName = "tester",
                SourceType = "EMERGENCY",
                IsStat = true,
            }, CancellationToken.None);

            Assert.That(response.Success, Is.True, response.Message);
            var saved = _context.PathologyOrder.Single(o => o.OrderId == response.OrderId);
            Assert.That(saved.SourceType, Is.EqualTo("EMERGENCY"));
            Assert.That(saved.IsStat, Is.True);
        }
    }
}
