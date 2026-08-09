using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.Handlers.CommandHandlers;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
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
        private ClinicalOrderCommandHandlers _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _handler = new ClinicalOrderCommandHandlers(_context, new Mock<IMediator>().Object);
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
    }
}
