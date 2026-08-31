using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.Handlers.CommandHandlers;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using EasyHMSAPI.UnitTests.TestUtils;
using MediatR;
using Moq;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.CommandHandlerTests
{
    [TestFixture]
    public class ClinicalOrderCommandHandlerTests
    {
        private AppDbContext _context = null!;
        private Mock<IMediator> _mediatorMock = null!;
        private ClinicalOrderCommandHandlers _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _mediatorMock = new Mock<IMediator>();
            _handler = new ClinicalOrderCommandHandlers(_context, _mediatorMock.Object);
        }

        [TearDown]
        public void TearDown()
        {
            InMemoryDbContextFactory.Destroy(_context);
            _context?.Dispose();
        }

        private Admission SeedAdmission()
        {
            var admission = new Admission
            {
                AdmissionId = Guid.NewGuid(),
                HospitalId = Guid.NewGuid(),
                PatientId = "PTID00000001",
                AdmissionNo = "ADM-1",
                AdmittedAt = DateTime.UtcNow,
                StatusCode = "ADMITTED",
                PayerType = "CASH",
            };
            _context.Admission.Add(admission);
            _context.SaveChanges();
            return admission;
        }

        [Test]
        public async Task Handle_WithSurgeryAndOrderSetContext_StampsAllThreeFields()
        {
            var admission = SeedAdmission();
            var surgeryCaseId = Guid.NewGuid();
            var orderSetId = Guid.NewGuid();

            var response = await _handler.Handle(new PlaceClinicalOrderRequestModel
            {
                HospitalId = admission.HospitalId,
                AdmissionId = admission.AdmissionId,
                OrderType = "MEDICATION",
                SurgeryCaseId = surgeryCaseId,
                SourceOrderSetId = orderSetId,
                SourceOrderSetNameSnapshot = "Standard Post-Op Protocol",
                Lines = new() { new ClinicalOrderLineInput { ItemName = "Paracetamol", Dose = "500mg" } },
            }, CancellationToken.None);

            Assert.That(response.Success, Is.True, response.Message);
            var saved = _context.ClinicalOrder.Single(o => o.OrderId == response.OrderId);
            Assert.That(saved.SurgeryCaseId, Is.EqualTo(surgeryCaseId));
            Assert.That(saved.SourceOrderSetId, Is.EqualTo(orderSetId));
            Assert.That(saved.SourceOrderSetNameSnapshot, Is.EqualTo("Standard Post-Op Protocol"));
        }

        [Test]
        public async Task Handle_ManualOrder_LeavesSurgeryAndOrderSetFieldsNull()
        {
            var admission = SeedAdmission();

            var response = await _handler.Handle(new PlaceClinicalOrderRequestModel
            {
                HospitalId = admission.HospitalId,
                AdmissionId = admission.AdmissionId,
                OrderType = "MEDICATION",
                Lines = new() { new ClinicalOrderLineInput { ItemName = "Paracetamol", Dose = "500mg" } },
            }, CancellationToken.None);

            Assert.That(response.Success, Is.True, response.Message);
            var saved = _context.ClinicalOrder.Single(o => o.OrderId == response.OrderId);
            Assert.That(saved.SurgeryCaseId, Is.Null);
            Assert.That(saved.SourceOrderSetId, Is.Null);
            Assert.That(saved.SourceOrderSetNameSnapshot, Is.Null);
        }

        [Test]
        public async Task Handle_MedicationOrder_NeverCreatesPathologyOrder()
        {
            var admission = SeedAdmission();

            var response = await _handler.Handle(new PlaceClinicalOrderRequestModel
            {
                HospitalId = admission.HospitalId,
                AdmissionId = admission.AdmissionId,
                OrderType = "MEDICATION",
                Lines = new() { new ClinicalOrderLineInput { ItemName = "Paracetamol", Dose = "500mg" } },
            }, CancellationToken.None);

            Assert.That(response.Success, Is.True, response.Message);
            Assert.That(_context.PathologyOrder.Any(), Is.False);
        }

        private PathologyTestMaster SeedPathologyTest(Guid hospitalId, Guid chargeId)
        {
            var test = new PathologyTestMaster
            {
                TestId = Guid.NewGuid(),
                HospitalId = hospitalId,
                TestCode = "CBC",
                TestName = "Complete Blood Count",
                ChargeId = chargeId,
                IsActive = true,
            };
            _context.PathologyTestMaster.Add(test);
            _context.SaveChanges();
            return test;
        }

        [Test]
        public async Task Handle_LabOrderLineWithCatalogedCharge_CreatesLinkedPathologyOrder()
        {
            var admission = SeedAdmission();
            var chargeId = Guid.NewGuid();
            var test = SeedPathologyTest(admission.HospitalId, chargeId);

            var response = await _handler.Handle(new PlaceClinicalOrderRequestModel
            {
                HospitalId = admission.HospitalId,
                AdmissionId = admission.AdmissionId,
                OrderType = "LAB",
                Lines = new() { new ClinicalOrderLineInput { ItemName = "CBC", ChargeId = chargeId } },
            }, CancellationToken.None);

            Assert.That(response.Success, Is.True, response.Message);

            var pathOrder = _context.PathologyOrder.Single(o => o.AdmissionId == admission.AdmissionId);
            Assert.That(pathOrder.SourceType, Is.EqualTo("IPD"));
            Assert.That(pathOrder.PatientId, Is.EqualTo(admission.PatientId));
            Assert.That(pathOrder.HospitalId, Is.EqualTo(admission.HospitalId));

            var pathLine = _context.PathologyOrderLine.Single(l => l.OrderId == pathOrder.OrderId);
            Assert.That(pathLine.TestId, Is.EqualTo(test.TestId));
            Assert.That(pathLine.Status, Is.EqualTo("PENDING"));

            var clinicalLine = _context.ClinicalOrderLine.Single(l => l.OrderId == response.OrderId);
            Assert.That(clinicalLine.LinkedPathologyOrderLineId, Is.EqualTo(pathLine.OrderLineId));
        }

        [Test]
        public async Task Handle_LabOrderWithBillableEncounter_TagsChargeAsLabPathSourceModule()
        {
            var admission = SeedAdmission();
            admission.EncounterId = Guid.NewGuid();
            _context.SaveChanges();
            var chargeId = Guid.NewGuid();
            SeedPathologyTest(admission.HospitalId, chargeId);

            AddChargeEventRequestModel? captured = null;
            _mediatorMock
                .Setup(m => m.Send(It.IsAny<AddChargeEventRequestModel>(), It.IsAny<CancellationToken>()))
                .Callback<object, CancellationToken>((req, _) => captured = req as AddChargeEventRequestModel)
                .ReturnsAsync(new AddChargeEventResponseModel
                {
                    Success = true,
                    Data = new AddChargesData
                    {
                        ChargeEvents = new() { new ChargeEventDetail { ChargeEventId = Guid.NewGuid() } },
                    },
                });

            var response = await _handler.Handle(new PlaceClinicalOrderRequestModel
            {
                HospitalId = admission.HospitalId,
                AdmissionId = admission.AdmissionId,
                OrderType = "LAB",
                Lines = new() { new ClinicalOrderLineInput { ItemName = "CBC", ChargeId = chargeId } },
            }, CancellationToken.None);

            Assert.That(response.Success, Is.True, response.Message);
            Assert.That(captured, Is.Not.Null);
            Assert.That(captured!.Charges!.Single().SourceModule, Is.EqualTo(BillingConstants.SourceModule.LabPath));
        }

        [Test]
        public async Task Handle_LabOrderLineWithStatUrgency_SetsIsStatOnPathologyOrder()
        {
            var admission = SeedAdmission();
            var chargeId = Guid.NewGuid();
            SeedPathologyTest(admission.HospitalId, chargeId);

            await _handler.Handle(new PlaceClinicalOrderRequestModel
            {
                HospitalId = admission.HospitalId,
                AdmissionId = admission.AdmissionId,
                OrderType = "LAB",
                Lines = new() { new ClinicalOrderLineInput { ItemName = "CBC", ChargeId = chargeId, Urgency = "STAT" } },
            }, CancellationToken.None);

            var pathOrder = _context.PathologyOrder.Single(o => o.AdmissionId == admission.AdmissionId);
            Assert.That(pathOrder.IsStat, Is.True);
        }

        [Test]
        public async Task Handle_LabOrderLineWithNoMatchingCatalogTest_DoesNotCreatePathologyOrder()
        {
            var admission = SeedAdmission();

            var response = await _handler.Handle(new PlaceClinicalOrderRequestModel
            {
                HospitalId = admission.HospitalId,
                AdmissionId = admission.AdmissionId,
                OrderType = "LAB",
                // Free-text line, no ChargeId at all -- nothing to resolve against the catalog.
                Lines = new() { new ClinicalOrderLineInput { ItemName = "Some ad-hoc lab item" } },
            }, CancellationToken.None);

            Assert.That(response.Success, Is.True, response.Message);
            Assert.That(_context.PathologyOrder.Any(), Is.False);

            var clinicalLine = _context.ClinicalOrderLine.Single(l => l.OrderId == response.OrderId);
            Assert.That(clinicalLine.LinkedPathologyOrderLineId, Is.Null);
        }

        [Test]
        public async Task Handle_LabOrderWithMixedLines_LinksOnlyTheCatalogedOne()
        {
            var admission = SeedAdmission();
            var chargeId = Guid.NewGuid();
            var test = SeedPathologyTest(admission.HospitalId, chargeId);

            var response = await _handler.Handle(new PlaceClinicalOrderRequestModel
            {
                HospitalId = admission.HospitalId,
                AdmissionId = admission.AdmissionId,
                OrderType = "LAB",
                Lines = new()
                {
                    new ClinicalOrderLineInput { ItemName = "CBC", ChargeId = chargeId },
                    new ClinicalOrderLineInput { ItemName = "Uncatalogued lab item" },
                },
            }, CancellationToken.None);

            Assert.That(response.Success, Is.True, response.Message);

            var pathOrder = _context.PathologyOrder.Single(o => o.AdmissionId == admission.AdmissionId);
            Assert.That(_context.PathologyOrderLine.Count(l => l.OrderId == pathOrder.OrderId), Is.EqualTo(1));
            Assert.That(_context.PathologyOrderLine.Single(l => l.OrderId == pathOrder.OrderId).TestId, Is.EqualTo(test.TestId));

            var clinicalLines = _context.ClinicalOrderLine.Where(l => l.OrderId == response.OrderId).ToList();
            Assert.That(clinicalLines.Count(l => l.LinkedPathologyOrderLineId.HasValue), Is.EqualTo(1));
            Assert.That(clinicalLines.Count(l => !l.LinkedPathologyOrderLineId.HasValue), Is.EqualTo(1));
        }
    }
}
