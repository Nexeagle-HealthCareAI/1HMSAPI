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
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.CommandHandlerTests
{
    [TestFixture]
    public class RapidResponseCommandHandlerTests
    {
        private AppDbContext _context = null!;
        private RapidResponseCommandHandlers _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _handler = new RapidResponseCommandHandlers(_context);
        }

        [TearDown]
        public void TearDown()
        {
            InMemoryDbContextFactory.Destroy(_context);
            _context.Dispose();
        }

        private (Guid hospitalId, Admission admission) SeedBasics()
        {
            var hospitalId = Guid.NewGuid();
            _context.Hospitals.Add(new Hospital
            {
                HospitalID = hospitalId, Name = "Hosp", Email = "e@m.com", Type = "General", RegistrationNumber = "REG001",
                Contact = "1234567890", Location = "Test Location", City = "Test City", State = "Test State",
                Country = "Test Country", Pincode = "123456", CreatedByUserID = Guid.NewGuid(),
            });
            var admission = new Admission
            {
                AdmissionId = Guid.NewGuid(), HospitalId = hospitalId, PatientId = "PTID00000001",
                AdmissionNo = "ADM-1", AdmittedAt = DateTime.UtcNow, StatusCode = "ADMITTED",
            };
            _context.Admission.Add(admission);
            _context.SaveChanges();
            return (hospitalId, admission);
        }

        [Test]
        public async Task Handle_FullLifecycle_ActivateArriveResolve()
        {
            var (hospitalId, admission) = SeedBasics();

            var activateResponse = await _handler.Handle(new ActivateRapidResponseRequestModel
            {
                HospitalId = hospitalId, AdmissionId = admission.AdmissionId,
                TriggerReason = IpdConstants.RrtTriggerReason.HighEws, TriggeredEwsScore = 8, LoggedInUserName = "Nurse Joy",
            }, CancellationToken.None);

            Assert.That(activateResponse.Success, Is.True, activateResponse.Message);
            Assert.That(activateResponse.ActivationId, Is.Not.Null);

            var activationId = activateResponse.ActivationId!.Value;
            var saved = _context.RapidResponseActivation.Single(a => a.ActivationId == activationId);
            Assert.That(saved.ResolvedAt, Is.Null);
            Assert.That(saved.ArrivedAt, Is.Null);

            var arriveResponse = await _handler.Handle(new MarkRapidResponseArrivedRequestModel { HospitalId = hospitalId, ActivationId = activationId }, CancellationToken.None);
            Assert.That(arriveResponse.Success, Is.True, arriveResponse.Message);

            var afterArrive = _context.RapidResponseActivation.Single(a => a.ActivationId == activationId);
            Assert.That(afterArrive.ArrivedAt, Is.Not.Null);

            var resolveResponse = await _handler.Handle(new ResolveRapidResponseRequestModel
            {
                HospitalId = hospitalId, ActivationId = activationId,
                Outcome = IpdConstants.RrtOutcome.StabilizedOnWard, LoggedInUserName = "Dr. House",
            }, CancellationToken.None);
            Assert.That(resolveResponse.Success, Is.True, resolveResponse.Message);

            var resolved = _context.RapidResponseActivation.Single(a => a.ActivationId == activationId);
            Assert.That(resolved.ResolvedAt, Is.Not.Null);
            Assert.That(resolved.Outcome, Is.EqualTo(IpdConstants.RrtOutcome.StabilizedOnWard));
        }

        [Test]
        public async Task Handle_ActivateInvalidTriggerReason_ReturnsFailure()
        {
            var (hospitalId, admission) = SeedBasics();

            var response = await _handler.Handle(new ActivateRapidResponseRequestModel
            {
                HospitalId = hospitalId, AdmissionId = admission.AdmissionId, TriggerReason = "NOT_REAL",
            }, CancellationToken.None);

            Assert.That(response.Success, Is.False);
        }

        [Test]
        public async Task Handle_ResolveAlreadyResolvedActivation_ReturnsFailure()
        {
            var (hospitalId, admission) = SeedBasics();
            var activate = await _handler.Handle(new ActivateRapidResponseRequestModel
            {
                HospitalId = hospitalId, AdmissionId = admission.AdmissionId, TriggerReason = IpdConstants.RrtTriggerReason.NurseConcern,
            }, CancellationToken.None);
            var activationId = activate.ActivationId!.Value;

            await _handler.Handle(new ResolveRapidResponseRequestModel
            {
                HospitalId = hospitalId, ActivationId = activationId, Outcome = IpdConstants.RrtOutcome.Other,
            }, CancellationToken.None);

            var secondResolve = await _handler.Handle(new ResolveRapidResponseRequestModel
            {
                HospitalId = hospitalId, ActivationId = activationId, Outcome = IpdConstants.RrtOutcome.Other,
            }, CancellationToken.None);

            Assert.That(secondResolve.Success, Is.False);
            Assert.That(secondResolve.Message, Does.Contain("already resolved"));
        }

        [Test]
        public async Task Handle_UnknownAdmission_ReturnsFailure()
        {
            var (hospitalId, _) = SeedBasics();

            var response = await _handler.Handle(new ActivateRapidResponseRequestModel
            {
                HospitalId = hospitalId, AdmissionId = Guid.NewGuid(), TriggerReason = IpdConstants.RrtTriggerReason.Other,
            }, CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Does.Contain("not found"));
        }
    }
}
