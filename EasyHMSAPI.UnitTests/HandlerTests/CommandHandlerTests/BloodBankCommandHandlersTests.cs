using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.Handlers.CommandHandlers;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using EasyHMSAPI.UnitTests.TestUtils;
using MediatR;
using Moq;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.CommandHandlerTests
{
    // Regression coverage for a real patient-safety gap found in a blood-bank audit: expiry was
    // captured on receipt but never re-checked at reserve/transfusion time, so an expired bag could
    // be reserved and transfused with no system pushback.
    [TestFixture]
    public class BloodBankCommandHandlersTests
    {
        private AppDbContext _context = null!;
        private Mock<IMediator> _mediatorMock = null!;
        private BloodBankCommandHandlers _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _mediatorMock = new Mock<IMediator>();
            _handler = new BloodBankCommandHandlers(_context, _mediatorMock.Object);
        }

        [TearDown]
        public void TearDown()
        {
            InMemoryDbContextFactory.Destroy(_context);
            _context?.Dispose();
        }

        private (Guid HospitalId, BloodBag Bag, Admission Admission) SeedBag(DateTime expiresAt, string status = "AVAILABLE")
        {
            var hospitalId = Guid.NewGuid();
            var bag = new BloodBag
            {
                BloodBagId = Guid.NewGuid(),
                HospitalId = hospitalId,
                BagNumber = "BAG-1",
                Component = IpdConstants.BloodComponent.All[0],
                BloodGroup = IpdConstants.BloodGroup.All[0],
                VolumeMl = 350,
                CollectedAt = expiresAt.AddDays(-30),
                ExpiresAt = expiresAt,
                Status = status,
            };
            _context.BloodBag.Add(bag);
            var admission = new Admission
            {
                AdmissionId = Guid.NewGuid(),
                HospitalId = hospitalId,
                PatientId = "PTID00000001",
                AdmissionNo = "ADM-1",
                AdmittedAt = DateTime.UtcNow,
                StatusCode = "ADMITTED",
                PayerType = "CASH",
            };
            _context.Admission.Add(admission);
            _context.SaveChanges();
            return (hospitalId, bag, admission);
        }

        [Test]
        public async Task Reserve_ExpiredBag_RejectsAndAutoDiscards()
        {
            var (hospitalId, bag, admission) = SeedBag(DateTime.UtcNow.AddDays(-1));

            var response = await _handler.Handle(new ReserveBloodBagRequestModel
            {
                HospitalId = hospitalId,
                BloodBagId = bag.BloodBagId,
                AdmissionId = admission.AdmissionId,
            }, CancellationToken.None);

            Assert.That(response.Success, Is.False);
            var saved = _context.BloodBag.Single(b => b.BloodBagId == bag.BloodBagId);
            Assert.That(saved.Status, Is.EqualTo(IpdConstants.BloodBagStatus.Discarded));
            Assert.That(saved.DiscardReason, Does.Contain("Expired"));
        }

        [Test]
        public async Task Reserve_NotYetExpiredBag_Succeeds()
        {
            var (hospitalId, bag, admission) = SeedBag(DateTime.UtcNow.AddDays(10));

            var response = await _handler.Handle(new ReserveBloodBagRequestModel
            {
                HospitalId = hospitalId,
                BloodBagId = bag.BloodBagId,
                AdmissionId = admission.AdmissionId,
            }, CancellationToken.None);

            Assert.That(response.Success, Is.True, response.Message);
            var saved = _context.BloodBag.Single(b => b.BloodBagId == bag.BloodBagId);
            Assert.That(saved.Status, Is.EqualTo(IpdConstants.BloodBagStatus.Reserved));
        }

        [Test]
        public async Task RecordTransfusion_ExpiredBag_RejectsAndAutoDiscards()
        {
            var (hospitalId, bag, admission) = SeedBag(DateTime.UtcNow.AddHours(-1), status: IpdConstants.BloodBagStatus.Reserved);

            var response = await _handler.Handle(new RecordTransfusionRequestModel
            {
                HospitalId = hospitalId,
                BloodBagId = bag.BloodBagId,
                AdmissionId = admission.AdmissionId,
                StartedAt = DateTime.UtcNow,
                VolumeGivenMl = 100,
                WitnessName = "Nurse A",
            }, CancellationToken.None);

            Assert.That(response.Success, Is.False);
            var saved = _context.BloodBag.Single(b => b.BloodBagId == bag.BloodBagId);
            Assert.That(saved.Status, Is.EqualTo(IpdConstants.BloodBagStatus.Discarded));
            _mediatorMock.Verify(m => m.Send(It.IsAny<AddChargeEventRequestModel>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Test]
        public async Task RecordTransfusion_NotYetExpiredBag_Succeeds()
        {
            var (hospitalId, bag, admission) = SeedBag(DateTime.UtcNow.AddDays(5), status: IpdConstants.BloodBagStatus.Available);

            var response = await _handler.Handle(new RecordTransfusionRequestModel
            {
                HospitalId = hospitalId,
                BloodBagId = bag.BloodBagId,
                AdmissionId = admission.AdmissionId,
                StartedAt = DateTime.UtcNow,
                VolumeGivenMl = 100,
                WitnessName = "Nurse A",
            }, CancellationToken.None);

            Assert.That(response.Success, Is.True, response.Message);
            var saved = _context.BloodBag.Single(b => b.BloodBagId == bag.BloodBagId);
            Assert.That(saved.Status, Is.EqualTo(IpdConstants.BloodBagStatus.Transfused));
        }
    }
}
